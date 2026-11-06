import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';

const host = process.argv[2];
assert.ok(host, 'Pass the built native-host DLL path.');
const envelopes = [
  {protocolVersion:2147483648,messageId:'large',type:'ping.v1'},
  {protocolVersion:-2147483649,messageId:'negative',type:'ping.v1'},
  {protocolVersion:1.5,messageId:'fractional',type:'ping.v1'},
  {protocolVersion:'1',messageId:'string',type:'ping.v1'},
  {protocolVersion:1,messageId:'alive',type:'ping.v1'},
];
const framed = envelopes.map(value => {
  const payload = Buffer.from(JSON.stringify(value));
  const length = Buffer.alloc(4);length.writeInt32LE(payload.length);
  return Buffer.concat([length,payload]);
});
// No browser identity is supplied. A valid envelope must fail closed as untrusted;
// malformed versions must return invalid_envelope without terminating the host.
const result = spawnSync('dotnet',[host],{input:Buffer.concat(framed),timeout:15000});
assert.ifError(result.error);
assert.equal(result.status,0,`Host must survive invalid frames: ${result.stderr?.toString()}`);
const replies=[];
for(let offset=0;offset<result.stdout.length;) {
  assert.ok(offset+4<=result.stdout.length,'Complete length prefix');
  const length=result.stdout.readInt32LE(offset);offset+=4;
  assert.ok(length>0 && offset+length<=result.stdout.length,'Complete bounded response');
  replies.push(JSON.parse(result.stdout.subarray(offset,offset+length)));offset+=length;
}
assert.equal(replies.length,envelopes.length,'Each frame must receive a response');
for(const reply of replies.slice(0,-1))assert.equal(reply.code,'extension.invalid_envelope');
assert.equal(replies.at(-1).code,'extension.untrusted_sender');
console.log('Native-host framing assertions passed: oversized/fractional/wrong-type versions fail closed and host survives.');
