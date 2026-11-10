const {Miniflare,convertV4MiniflareOptions,Response}=require(process.env.MINIFLARE_MODULE || 'miniflare');
const assert=require('node:assert/strict');
(async()=>{
 const path=require('node:path');
 const bundle=path.resolve(process.env.WORKER_BUNDLE || '/tmp/scrobblix-worker-dryrun/worker.js');process.chdir(path.dirname(bundle));
 let calls=0;
 const mf=new Miniflare(convertV4MiniflareOptions({modules:true,scriptPath:bundle,compatibilityDate:'2026-11-07',bindings:{LASTFM_API_KEY:'a'.repeat(32),LASTFM_API_SECRET:'b'.repeat(32),DAILY_FORWARD_LIMIT:'3'},durableObjects:{FORWARD_BUDGET:{className:'ScrobblixBudget',useSQLite:true}},ratelimits:{PER_IP:{namespace_id:'133001',simple:{limit:30,period:60}}},outboundService:async request=>{calls++;assert.equal(new URL(request.url).hostname,'ws.audioscrobbler.com');return Response.json({token:'c'.repeat(32)});}}));
 try {
  await mf.ready;
  const request=()=>mf.dispatchFetch('https://broker.invalid/v1/lastfm',{method:'POST',headers:{'Content-Type':'application/json','CF-Connecting-IP':'192.0.2.10'},body:JSON.stringify({method:'auth.getToken'})});
  const responses=await Promise.all(Array.from({length:12},request));
  const statuses=responses.map(r=>r.status);
  assert.equal(statuses.filter(x=>x===200).length,3);assert.equal(statuses.filter(x=>x===429).length,9);assert.equal(calls,3);
  console.log('Actual local workerd:12 concurrent requests,3 forwarded,9 capped,3 exact fake-upstream calls. No real provider traffic.');
 }finally{await mf.dispose();}
})().catch(e=>{console.error(e);process.exitCode=1;});
