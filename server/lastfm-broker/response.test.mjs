import test from 'node:test';
import assert from 'node:assert/strict';
import {safeProviderResponse as safe} from './response.mjs';
test('auth returns only intended token/session fields, strips secret echoes',()=>{
 assert.deepEqual(safe('auth.getToken',{token:'a'.repeat(32),password:'synthetic',debug:{secret:'synthetic'}}),{token:'a'.repeat(32)});
 assert.deepEqual(safe('auth.getSession',{session:{name:'Synthetic',key:'b'.repeat(32),subscriber:'0',secret:'synthetic'},trace:'synthetic'}),{session:{name:'Synthetic',key:'b'.repeat(32),subscriber:'0'}});
 for(const value of [{},null,[],{token:'bad'}])assert.throws(()=>safe('auth.getToken',value));
});
test('API error preserves code without untrusted arbitrary message or extra fields',()=>{
 assert.deepEqual(safe('auth.getSession',{error:14,message:'synthetic secret echo',api_secret:'synthetic'}),{error:14,message:'Last.fm returned an API error'});
 for(const error of [0,-1,'not-code',{},null])assert.throws(()=>safe('auth.getToken',{error}));
});
test('single scrobble and NowPlaying preserve acceptance/ignored semantics only',()=>{
 const row={track:{'#text':'Synthetic',corrected:'0',debug:'strip'},artist:'Synthetic',ignoredMessage:{code:'0','#text':''},timestamp:'1780000000',password:'strip'};
 const result=safe('track.scrobble',{scrobbles:{'@attr':{accepted:'1',ignored:'0',extra:'strip'},scrobble:row},extra:'strip'});
 assert.equal(result.scrobbles.scrobble.ignoredMessage.code,'0');assert.equal(result.scrobbles['@attr'].accepted,'1');
 assert.equal(result.scrobbles.scrobble.password,undefined);assert.equal(result.scrobbles.scrobble.track.debug,undefined);
 assert.deepEqual(safe('track.updateNowPlaying',{nowplaying:{ignoredMessage:{code:'1'}}}),{nowplaying:{ignoredMessage:{code:'1'}}});
 for(const p of [{},{scrobbles:{}},{scrobbles:{'@attr':{accepted:1,ignored:1},scrobble:row}},{scrobbles:{'@attr':{accepted:1,ignored:0},scrobble:[row,row]}}])assert.throws(()=>safe('track.scrobble',p));
});
test('love success must be exact empty object, never fabricated from unknown result',()=>{
 assert.deepEqual(safe('track.love',{}),{});assert.deepEqual(safe('track.unlove',{}),{});
 assert.throws(()=>safe('track.love',{unexpected:'value'}));assert.throws(()=>safe('unknown',{}));
});
