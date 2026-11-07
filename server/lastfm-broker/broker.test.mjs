import test from 'node:test';
import assert from 'node:assert/strict';
import {handler,validatedParams,signature} from './broker.mjs';
const env={LASTFM_API_KEY:'a'.repeat(32),LASTFM_API_SECRET:'b'.repeat(32)};
const request=(body)=>new Request('https://broker.invalid/v1/lastfm',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
test('strict method and fields, no password or generic signing/proxy',()=>{
 for(const payload of [{method:'user.getInfo'},{method:'auth.getToken',api_key:'x'},{method:'auth.getToken',url:'https://invalid/'},{method:'auth.getMobileSession',password:'test'}])assert.throws(()=>validatedParams(payload,100));
 assert.deepEqual(validatedParams({method:'auth.getToken'},100),{method:'auth.getToken'});
});
test('track bounds and timestamp fence',()=>{
 const p={method:'track.scrobble',sk:'c'.repeat(32),artist:'Artist',track:'Title',timestamp:99};
 assert.equal(validatedParams(p,100).timestamp,'99');
 for(const changes of [{timestamp:161},{artist:'x'.repeat(513)},{sk:'invalid'},{duration:-1},{artist:'x\n'}])assert.throws(()=>validatedParams({...p,...changes},100));
});
test('signature canonical order',()=>{assert.equal(signature({b:'2',a:'1'},'x'),signature({a:'1',b:'2'},'x'));});
test('only fixed HTTPS provider receives credentials, no retry',async()=>{
 let calls=0;const h=handler({gate:async()=>true,fetcher:async(url,options)=>{calls++;assert.equal(url,'https://ws.audioscrobbler.com/2.0/');assert.equal(options.redirect,'manual');assert.equal(options.method,'POST');assert.equal(options.body.get('api_key'),env.LASTFM_API_KEY);assert.equal(options.body.get('api_sig').length,32);return Response.json({token:'c'.repeat(32)});}});
 assert.equal((await h(request({method:'auth.getToken'}),env)).status,200);assert.equal(calls,1);
});
test('bad/oversize requests never forwarded',async()=>{
 let calls=0;const h=handler({gate:async()=>true,fetcher:async()=>{calls++;return Response.json({});}});
 assert.equal((await h(request({method:'auth.getToken',extra:'x'.repeat(5000)}),env)).status,400);assert.equal(calls,0);
});
test('limit, missing configuration and limiter failure fail closed',async()=>{
 const r=request({method:'auth.getToken'});
 assert.equal((await handler()(r.clone(),env)).status,503);
 assert.equal((await handler({gate:async()=>false})(r.clone(),env)).status,429);
 assert.equal((await handler({gate:async()=>{throw Error('test')}})(r.clone(),env)).status,503);
});
test('network ambiguity explicit with no retry',async()=>{
 let calls=0;const h=handler({gate:async()=>true,fetcher:async()=>{calls++;throw Error('network')}});
 const r=await h(request({method:'auth.getToken'}),env);assert.equal(r.status,502);assert.deepEqual(await r.json(),{error:'upstream_outcome_unknown'});assert.equal(calls,1);
});
test('oversize upstream response not reflected',async()=>{
 const h=handler({gate:async()=>true,fetcher:async()=>new Response('x'.repeat(65537))});assert.equal((await h(request({method:'auth.getToken'}),env)).status,502);
});

test('provider redirects never followed or exposed as accepted',async()=>{
 let calls=0;const h=handler({gate:async()=>true,fetcher:async()=>{calls++;return new Response(null,{status:302,headers:{Location:'https://invalid.example/'}});}});
 assert.equal((await h(request({method:'auth.getToken'}),env)).status,502);assert.equal(calls,1);
});

test('love and unlove preserve Windows features without generic signing surface',async()=>{
 for(const method of ['track.love','track.unlove']) {
  const payload={method,sk:'c'.repeat(32),artist:'Synthetic Artist',track:'Synthetic Title'};
  assert.deepEqual(validatedParams(payload,100),payload);
  for(const extra of [{album:'unexpected'},{timestamp:99},{api_sig:'bad'},{url:'https://invalid/'},{password:'not allowed'}])assert.throws(()=>validatedParams({...payload,...extra},100));
  let calls=0;
  const h=handler({gate:async()=>true,fetcher:async(url,options)=>{
   calls++;assert.equal(url,'https://ws.audioscrobbler.com/2.0/');
   assert.equal(options.method,'POST');assert.equal(options.body.get('method'),method);
   assert.equal(options.body.get('artist'),payload.artist);assert.equal(options.body.get('track'),payload.track);
   assert.equal(options.body.get('sk'),payload.sk);assert.equal(options.body.get('api_sig').length,32);
   return Response.json({});
  }});
  assert.equal((await h(request(payload),env)).status,200);assert.equal(calls,1);
 }
});
test('invalid paths verbs content types and body encodings never consume forwarding allowance',async()=>{
 let gates=0,calls=0;const h=handler({gate:async()=>{gates++;return true;},fetcher:async()=>{calls++;return Response.json({});}});
 const cases=[
  new Request('https://broker.invalid/not-an-endpoint',{method:'POST',body:'{}'}),
  new Request('https://broker.invalid/v1/lastfm'),
  new Request('https://broker.invalid/v1/lastfm',{method:'POST',headers:{'Content-Type':'text/plain'},body:'{}'}),
  new Request('https://broker.invalid/v1/lastfm',{method:'POST',headers:{'Content-Type':'application/json'},body:'{'}),
  request({method:'auth.getSession',token:'invalid'}),request({method:'track.love',sk:'c'.repeat(32),artist:'a',track:'b',api_key:'x'})
 ];
 for(const r of cases)assert.ok((await h(r,env)).status>=400);
 assert.equal(gates,0);assert.equal(calls,0);
});
test('upstream cookies locations and arbitrary headers are never relayed',async()=>{
 const h=handler({gate:async()=>true,fetcher:async()=>new Response(JSON.stringify({token:'c'.repeat(32)}),{headers:{'Set-Cookie':'synthetic=private','Location':'https://invalid/','X-Private':'synthetic'}})});
 const response=await h(request({method:'auth.getToken'}),env);
 for(const key of ['Set-Cookie','Location','X-Private'])assert.equal(response.headers.get(key),null);
 assert.equal(response.headers.get('Cache-Control'),'no-store');
});

test('authorization URL is generated from fixed Last.fm origin and bound token, never upstream URL',async()=>{
 const token='c'.repeat(32);
 const h=handler({gate:async()=>true,fetcher:async()=>Response.json({token,authorizationUrl:'https://invalid.example/',secret:'not reflected'})});
 const response=await h(request({method:'auth.getToken'}),env);
 assert.deepEqual(await response.json(),{token,authorizationUrl:'https://www.last.fm/api/auth/?api_key='+env.LASTFM_API_KEY+'&token='+token});
});
