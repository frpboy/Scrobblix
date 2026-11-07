const HEX=/^[a-f0-9]{32}$/i;
const REQUEST_TOKEN=/^[A-Za-z0-9]{32}$/;
const plain=v=>v && typeof v==='object' && !Array.isArray(v);
const text=(v,max)=>typeof v==='string' && v.length<=max && !/[\x00-\x1f\x7f]/.test(v);
const invalid=()=>{throw new Error('invalid provider shape');};
const numeric=(v,max=2147483647)=>{
 if((typeof v!=='string' || !/^\d+$/.test(v)) && !Number.isSafeInteger(v))return invalid();
 const n=Number(v);if(!Number.isSafeInteger(n) || n<0 || n>max)return invalid();return v;
};
function item(value,scrobble){
 if(!plain(value))return invalid();const result={};
 for(const key of ['artist','track','album','albumArtist']) if(value[key]!==undefined) {
  const v=value[key];
  if(typeof v==='string') {if(!text(v,1024))return invalid();result[key]=v;}
  else {if(!plain(v) || !text(v['#text'],1024))return invalid();result[key]={'#text':v['#text']};
   if(v.corrected!==undefined)result[key].corrected=numeric(v.corrected,1);}
 }
 const ignored=value.ignoredMessage;
 if(!plain(ignored))return invalid();
 const code=ignored.code??ignored['@attr']?.code;
 result.ignoredMessage={code:numeric(code,5)};
 if(text(ignored['#text'],1024))result.ignoredMessage['#text']=ignored['#text'];
 if(plain(ignored['@attr']))result.ignoredMessage['@attr']={code:numeric(code,5)};
 if(scrobble && value.timestamp!==undefined)result.timestamp=numeric(value.timestamp);
 return result;
}
/** Explicit result fields only. Never reflect arbitrary provider JSON or secret-bearing echoes. */
export function safeProviderResponse(method,payload) {
 if(!plain(payload))return invalid();
 if(payload.error!==undefined) {
  const error=numeric(payload.error);if(Number(error)===0)return invalid();
  return {error:Number(error),message:'Last.fm returned an API error'};
 }
 if(method==='auth.getToken') {
  if(typeof payload.token!=='string' || !REQUEST_TOKEN.test(payload.token))return invalid();return {token:payload.token};
 }
 if(method==='auth.getSession') {
  const s=payload.session;if(!plain(s) || !text(s.name,255) || !s.name.trim() || !HEX.test(s.key??''))return invalid();
  const session={name:s.name,key:s.key};if(s.subscriber!==undefined)session.subscriber=numeric(s.subscriber,1);
  return {session};
 }
 if(method==='track.love' || method==='track.unlove') {
  // Last.fm JSON success is empty; a nonempty unrecognized envelope is not success evidence.
  if(Object.keys(payload).length!==0)return invalid();return {};
 }
 if(method==='track.updateNowPlaying')return {nowplaying:item(payload.nowplaying,false)};
 if(method==='track.scrobble') {
  const s=payload.scrobbles;if(!plain(s) || !plain(s['@attr']))return invalid();
  const attrs={accepted:numeric(s['@attr'].accepted,1),ignored:numeric(s['@attr'].ignored,1)};
  if(Number(attrs.accepted)+Number(attrs.ignored)!==1)return invalid();
  if(Array.isArray(s.scrobble) && s.scrobble.length!==1)return invalid();
  const value=item(Array.isArray(s.scrobble)?s.scrobble[0]:s.scrobble,true);
  return {scrobbles:{'@attr':attrs,scrobble:value}};
 }
 return invalid();
}
