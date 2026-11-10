import { createHash } from 'node:crypto';
import {safeProviderResponse} from './response.mjs';

const UPSTREAM = 'https://ws.audioscrobbler.com/2.0/';
const HEX = /^[a-f0-9]{32}$/i;
// Request tokens and session credentials are opaque, not MD5 signatures. Bounded URL-safe alphabet.
const SESSION_KEY = /^[A-Za-z0-9_-]{32}$/;
const REQUEST_TOKEN = /^[A-Za-z0-9_-]{32}$/;
const reply = (status, value) => new Response(JSON.stringify(value), {
  status, headers: { 'Content-Type': 'application/json', 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff' }
});
const text = (v, max) => typeof v === 'string' && v.trim().length > 0 && v.length <= max && !/[\x00-\x1f\x7f]/.test(v);

export function validatedParams(input, nowSeconds) {
  if (!input || typeof input !== 'object' || Array.isArray(input)) throw new Error('invalid');
  const schemas = {
    'auth.getToken': [], 'auth.getSession': ['token'],
    'track.love': ['sk','artist','track'], 'track.unlove': ['sk','artist','track'],
    'track.scrobble': ['sk','artist','track','timestamp','album','duration'],
    'track.updateNowPlaying': ['sk','artist','track','album','duration']
  };
  const fields = schemas[input.method];
  if (!fields || Object.keys(input).some(k => k !== 'method' && !fields.includes(k))) throw new Error('invalid');
  const result = {method: input.method};
  if (input.method === 'auth.getSession') {
    if (typeof input.token!=='string' || input.token.length!==32 || !REQUEST_TOKEN.test(input.token)) throw new Error('invalid');
    result.token = input.token;
  }
  if (input.method.startsWith('track.')) {
    if (typeof input.sk!=='string' || input.sk.length!==32 || !SESSION_KEY.test(input.sk) || !text(input.artist,512) || !text(input.track,512)) throw new Error('invalid');
    Object.assign(result,{sk: input.sk,artist: input.artist,track: input.track});
    if (input.album !== undefined) { if (!text(input.album,512)) throw new Error('invalid'); result.album=input.album; }
    if (input.duration !== undefined) {
      if (!Number.isSafeInteger(input.duration) || input.duration<1 || input.duration>86400) throw new Error('invalid');
      result.duration=String(input.duration);
    }
    if (input.method==='track.scrobble') {
      if (!Number.isSafeInteger(input.timestamp) || input.timestamp<0 || input.timestamp>nowSeconds+60) throw new Error('invalid');
      result.timestamp=String(input.timestamp);
    }
  }
  return result;
}
export function signature(params, secret) {
  return createHash('md5').update(Object.keys(params).sort().map(k=>k+params[k]).join('')+secret,'utf8').digest('hex');
}
async function readLimited(stream, limit) {
  if (!stream) throw new Error('empty');
  const reader=stream.getReader(); const chunks=[]; let size=0;
  try { for (;;) { const {done,value}=await reader.read(); if(done) break; size+=value.length;
    if(size>limit) throw new Error('large'); chunks.push(value); }
    const bytes=new Uint8Array(size);let pos=0;for(const chunk of chunks){bytes.set(chunk,pos);pos+=chunk.length;}
    return new TextDecoder('utf-8',{fatal:true}).decode(bytes);
  } finally { await reader.cancel().catch(()=>{}); }
}
/** Preparation only. Not deployed. No console calls, arbitrary upstream, password flow or automatic retries. */
export function handler({fetcher=fetch,gate,clock=()=>Date.now()}={}) {
  return async (request,env) => {
    if(new URL(request.url).pathname!=='/v1/lastfm' || request.method!=='POST') return reply(404,{broker_error:'not_found'});
    if(!/^application\/json(?:;|$)/i.test(request.headers.get('Content-Type')??'')) return reply(415,{broker_error:'json_required'});
    if(!HEX.test(env.LASTFM_API_KEY??'') || !HEX.test(env.LASTFM_API_SECRET??'') || !gate) return reply(503,{broker_error:'not_configured'});
    let params;
    try { params=validatedParams(JSON.parse(await readLimited(request.body,4096)),Math.floor(clock()/1000)); }
    catch { return reply(400,{broker_error:'invalid_request'}); }
    // Gate must enforce both abuse limits and a strict atomic daily forwarding allowance. Never fail open.
    try { if(!await gate(request,params)) return reply(429,{broker_error:'limited'}); }
    catch { return reply(503,{broker_error:'limiter_unavailable'}); }
    const args={...params,api_key:env.LASTFM_API_KEY};
    const body=new URLSearchParams({...args,api_sig:signature(args,env.LASTFM_API_SECRET),format:'json'});
    try {
      const response=await fetcher(UPSTREAM,{method:'POST',body,redirect:'manual',signal:AbortSignal.timeout(15000)});
      if(response.status>=300 && response.status<400) throw new Error("redirect");
      const payload=JSON.parse(await readLimited(response.body,65536));
      if(!payload || typeof payload!=='object' || Array.isArray(payload)) throw new Error('invalid');
      // Preserve provider semantics; a transport/parse failure is never proof a scrobble was rejected.
      const safe=safeProviderResponse(params.method,payload);
      if(params.method==='auth.getToken' && safe.token)
        safe.authorizationUrl='https://www.last.fm/api/auth/?api_key='+env.LASTFM_API_KEY+'&token='+safe.token;
      return reply(response.status,safe);
    } catch { return reply(502,{broker_error:'upstream_outcome_unknown'}); }
  };
}
