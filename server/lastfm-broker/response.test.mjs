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

test('opaque request tokens and session keys retain distinct response fields',()=>{
 assert.deepEqual(safe('auth.getToken',{token:'Zg_-'.repeat(8)}),{token:'Zg_-'.repeat(8)});
 assert.deepEqual(safe('auth.getSession',{session:{name:'Synthetic',key:'Zg_-'.repeat(8)}}),{session:{name:'Synthetic',key:'Zg_-'.repeat(8)}});
});

test('session credentials preserve bounded opaque URL-safe bytes, never coerce or trim',()=>{
 for(const key of ['a'.repeat(32),'Zg_-'.repeat(8),'_'.repeat(32),'-'.repeat(32)])
  assert.equal(safe('auth.getSession',{session:{name:'Synthetic',key,subscriber:0}}).session.key,key);
 for(const key of [null,{},['a'.repeat(32)],'a'.repeat(31),'a'.repeat(33),'a'.repeat(32)+'\n','+'.repeat(32),'/'.repeat(32),'é'.repeat(32),' '.repeat(32)])
  assert.throws(()=>safe('auth.getSession',{session:{name:'Synthetic',key}}));
});

test('optional empty album fields may omit JSON text without hiding acceptance or ignored status',()=>{
 for(const accepted of [true,false]) for(const array of [true,false]) {
  const row={artist:{corrected:'0','#text':'Synthetic Artist'},track:{corrected:'0','#text':'Synthetic Track'},album:{corrected:'0',debug:'strip'},albumArtist:{corrected:'0'},ignoredMessage:{code:accepted?'0':'1','#text':''},timestamp:'1794078600'};
  const payload={scrobbles:{'@attr':{accepted:accepted?1:0,ignored:accepted?0:1},scrobble:array?[row]:row}};
  const result=safe('track.scrobble',payload);
  assert.deepEqual(result.scrobbles.scrobble.album,{'#text':'',corrected:'0'});
  assert.deepEqual(result.scrobbles.scrobble.albumArtist,{'#text':'',corrected:'0'});
  assert.equal(result.scrobbles.scrobble.ignoredMessage.code,accepted?'0':'1');
  assert.equal(result.scrobbles['@attr'].accepted,accepted?1:0);
  assert.deepEqual(safe('track.updateNowPlaying',{nowplaying:row}).nowplaying.album,{'#text':'',corrected:'0'});
 }
});
test('empty optional metadata compatibility does not coerce, invent required text, or bypass result validation',()=>{
 const row={artist:{corrected:'0','#text':'Synthetic Artist'},track:{corrected:'0','#text':'Synthetic Track'},album:{corrected:'0'},ignoredMessage:{code:'0'}};
 for(const album of [null,[],{'#text':null},{'#text':123},{'#text':[]},{'#text':'x'.repeat(1025)},{'#text':'x\n'},{corrected:2},{corrected:{}}])
  assert.throws(()=>safe('track.updateNowPlaying',{nowplaying:{...row,album}}));
 for(const key of ['artist','track'])assert.throws(()=>safe('track.updateNowPlaying',{nowplaying:{...row,[key]:{corrected:'0'}}}));
 for(const ignoredMessage of [null,{},[],{code:6},{code:null}])assert.throws(()=>safe('track.updateNowPlaying',{nowplaying:{...row,ignoredMessage}}));
 for(const attrs of [{accepted:0,ignored:0},{accepted:1,ignored:1},{}])assert.throws(()=>safe('track.scrobble',{scrobbles:{'@attr':attrs,scrobble:row}}));
});
