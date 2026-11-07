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
 const r=await h(request({method:'auth.getToken'}),env);assert.equal(r.status,502);assert.deepEqual(await r.json(),{broker_error:'upstream_outcome_unknown'});assert.equal(calls,1);
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

test('broker rejection marker is separate from provider API errors and only pre-forward cases allow retry',async()=>{
 let calls=0;const fetcher=async()=>{calls++;return Response.json({token:'c'.repeat(32)})};
 for(const [gate,status,marker] of [[async()=>false,429,'limited'],[async()=>{throw Error('synthetic')},503,'limiter_unavailable']]) {
  const result=await handler({gate,fetcher})(request({method:'auth.getToken'}),env);
  assert.equal(result.status,status);assert.deepEqual(await result.json(),{broker_error:marker});
 }
 const invalid=await handler({gate:async()=>true,fetcher})(request({method:'unknown'}),env);
 assert.equal(invalid.status,400);assert.deepEqual(await invalid.json(),{broker_error:'invalid_request'});assert.equal(calls,0);
});

test('opaque nonhex request tokens accepted, unsafe tokens rejected; no diagnostics remain',async()=>{
 const token='Zg_-'.repeat(8);
 const h=handler({gate:async()=>true,fetcher:async()=>Response.json({token})});
 const r=await h(request({method:'auth.getToken'}),env);
 assert.deepEqual(await r.json(),{token,authorizationUrl:'https://www.last.fm/api/auth/?api_key='+env.LASTFM_API_KEY+'&token='+token});
 assert.equal(validatedParams({method:'auth.getSession',token},0).token,token);
 for(const bad of ['x'.repeat(31),'x'.repeat(33),'x'.repeat(31)+'&','x'.repeat(31)+' ', 'x'.repeat(31)+'é','x'.repeat(31)+'\n']){
  assert.throws(()=>validatedParams({method:'auth.getSession',token:bad},0));
  const reject=handler({gate:async()=>true,fetcher:async()=>Response.json({token:bad})});
  assert.deepEqual(await (await reject(request({method:'auth.getToken'}),env)).json(),{broker_error:'upstream_outcome_unknown'});
 }
 assert.equal(validatedParams({method:'track.love',sk:token,artist:'a',track:'b'},0).sk,token);
});

// Full synthetic envelopes: no real session exchange or listening request.
test('URL-safe request token round trip stays bounded; failures have no diagnostics',async()=>{
 for(const token of ['Zg_-'.repeat(8),'_'.repeat(32),'-'.repeat(32)]) {
  let calls=0;
  const h=handler({gate:async()=>true,fetcher:async(url,options)=>{
   calls++;assert.equal(options.body.get('token'),token);
   assert.match(options.body.get('api_sig'),/^[a-f0-9]{32}$/);
   return Response.json({session:{name:'Synthetic',key:'c'.repeat(32),subscriber:0,debug:'private'},debug:'private'});
  }});
  const r=await h(request({method:'auth.getSession',token}),env);
  assert.equal(r.status,200);assert.deepEqual(await r.json(),{session:{name:'Synthetic',key:'c'.repeat(32),subscriber:0}});assert.equal(calls,1);
 }
 for(const payload of [null,[],{}, {error:0},{error:{private:'echo'}},{token:5},{token:'x'},{token:'+'.repeat(32)},{token:'é'.repeat(32)},{token:' '.repeat(32)}]) {
  const h=handler({gate:async()=>true,fetcher:async()=>Response.json(payload)});
  assert.deepEqual(await (await h(request({method:'auth.getToken'}),env)).json(),{broker_error:'upstream_outcome_unknown'});
 }
 for(const body of ['bad-json','x'.repeat(65537)]) {
  const h=handler({gate:async()=>true,fetcher:async()=>new Response(body)});
  assert.deepEqual(await (await h(request({method:'auth.getToken'}),env)).json(),{broker_error:'upstream_outcome_unknown'});
 }
});

test('request token type and exact length precede regex coercion and final-newline semantics',async()=>{
 for(const token of ['x'.repeat(32)+'\n','x'.repeat(32)+'\r','x'.repeat(32)+'\r\n',['x'.repeat(32)],{token:'x'.repeat(32)},null,32]) {
  assert.throws(()=>validatedParams({method:'auth.getSession',token},0));
  let calls=0;const h=handler({gate:async()=>true,fetcher:async()=>{calls++;return Response.json({token});}});
  const rejected=await h(request({method:'auth.getSession',token}),env);
  assert.equal(rejected.status,400);assert.equal(calls,0);
  const malformed=await h(request({method:'auth.getToken'}),env);
  assert.equal(malformed.status,502);assert.deepEqual(await malformed.json(),{broker_error:'upstream_outcome_unknown'});
 }
});

test('opaque session credentials survive track forwarding byte-exact and remain signed',async()=>{
 for(const sk of ['a'.repeat(32),'Zg_-'.repeat(8),'_'.repeat(32),'-'.repeat(32)]) {
  let calls=0;
  const h=handler({gate:async()=>true,fetcher:async(url,options)=>{
   calls++;assert.equal(options.body.get('sk'),sk);assert.match(options.body.get('api_sig'),/^[a-f0-9]{32}$/);
   return Response.json({});
  }});
  const r=await h(request({method:'track.love',sk,artist:'Synthetic',track:'Synthetic'}),env);
  assert.equal(r.status,200);assert.equal(calls,1);
 }
 for(const sk of [null,{},['a'.repeat(32)],'a'.repeat(31),'a'.repeat(33),'a'.repeat(32)+'\n','+'.repeat(32),'/'.repeat(32),'é'.repeat(32),' '.repeat(32)])
  assert.throws(()=>validatedParams({method:'track.love',sk,artist:'Synthetic',track:'Synthetic'},0));
});
