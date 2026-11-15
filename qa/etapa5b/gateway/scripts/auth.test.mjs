import test from 'node:test';import assert from 'node:assert/strict';import {webcrypto,generateKeyPairSync,sign} from 'node:crypto';import gateway,{verifyDevice} from '../worker/index.js';
globalThis.crypto??=webcrypto;
class Bucket{m=new Map();async get(k){let o=this.m.get(k);return o?{etag:o.etag,json:async()=>JSON.parse(o.s),text:async()=>o.s}:null;}async list(){return {objects:[]};}async delete(){}async put(k,s,opt={}){let old=this.m.get(k);if(opt.onlyIf?.etagMatches&&old?.etag!==opt.onlyIf.etagMatches)return null;if(opt.onlyIf?.etagDoesNotMatch==='*'&&old)return null;let o={s,etag:crypto.randomUUID()};this.m.set(k,o);return o;}}
const hash=async x=>Buffer.from(await crypto.subtle.digest('SHA-256',x)).toString('hex');
test('ECDSA Android DER: approved only, raw body binding, replay rejected',async()=>{const bucket=new Bucket(),keys=generateKeyPairSync('ec',{namedCurve:'prime256v1'}),der=keys.publicKey.export({type:'spki',format:'der'}),id=await hash(der),body=Buffer.from('{"name":"Dona Maria","photo":"a/b"}'),stamp=String(Date.now()),nonce=crypto.randomUUID();
const message='POST\n/v1/simulations\n'+stamp+'\n'+nonce+'\n'+await hash(body);const signature=sign('sha256',Buffer.from(message),keys.privateKey).toString('base64');const r=()=>new Request('https://test/v1/simulations',{method:'POST',headers:{'X-Angra-Device':id,'X-Angra-Time':stamp,'X-Angra-Nonce':nonce,'X-Angra-Signature':signature}});
await bucket.put('devices/'+id,JSON.stringify({publicKey:der.toString('base64'),approved:false}));assert.equal(await verifyDevice(r(),body,{BUCKET:bucket}),false);
await bucket.put('devices/'+id,JSON.stringify({publicKey:der.toString('base64'),approved:true}));assert.equal(await verifyDevice(r(),Buffer.from('{}'),{BUCKET:bucket}),false);assert.equal(await verifyDevice(r(),body,{BUCKET:bucket}),id);assert.equal(await verifyDevice(r(),body,{BUCKET:bucket}),false);
});
test('unsigned generation never reaches provider; PDV disabled; administrator not public',async()=>{const env={BUCKET:new Bucket(),OWNER_EMAIL:'owner@example.test',ADMIN_TOKEN:'test-only-admin'};let r=await gateway.fetch(new Request('https://test/v1/simulations',{method:'POST'}),env);assert.equal(r.status,401);r=await gateway.fetch(new Request('https://test/v1/projects',{method:'POST'}),env);assert.equal(r.status,409);r=await gateway.fetch(new Request('https://test/admin/approve',{method:'POST'}),env);assert.equal(r.status,401);});
test('legacy four-attempt counter cannot block the same approved device; failed upstream calls do not create a daily quota',async()=>{
 const bucket=new Bucket(),keys=generateKeyPairSync('ec',{namedCurve:'prime256v1'}),der=keys.publicKey.export({type:'spki',format:'der'}),id=await hash(der);
 await bucket.put('devices/'+id,JSON.stringify({publicKey:der.toString('base64'),approved:true}));
 const legacy='usage/'+id+'/'+new Date().toISOString().slice(0,10);await bucket.put(legacy,'4');
 const body=Buffer.from(JSON.stringify({project:{schemaVersion:1,id:crypto.randomUUID(),revision:1,title:'Dona Maria',type:'Box de banheiro',model:'',configuration:'',measures:[{label:'Largura',value:'2',unit:'m',mm:'2000'},{label:'Altura',value:'2',unit:'m',mm:'2000'}]},photoBase64:Buffer.from([255,216,...Array(30).fill(0)]).toString('base64')}));
 // Transport fault injection only: no generated image or end-to-end success is simulated.
 const originalFetch=globalThis.fetch;let calls=0;
 globalThis.fetch=async(url)=>{assert.equal(String(url),'https://timbrooks-instruct-pix2pix.hf.space/gradio_api/upload');calls++;return new Response('',{status:503});};
 try{
  for(let n=0;n<6;n++){
   const stamp=String(Date.now()),nonce=crypto.randomUUID(),message='POST\n/v1/simulations\n'+stamp+'\n'+nonce+'\n'+await hash(body);
   const sig=sign('sha256',Buffer.from(message),keys.privateKey).toString('base64');
   const response=await gateway.fetch(new Request('https://test/v1/simulations',{method:'POST',headers:{'Content-Type':'application/json','X-Angra-Device':id,'X-Angra-Time':stamp,'X-Angra-Nonce':nonce,'X-Angra-Signature':sig},body}),{BUCKET:bucket});
   assert.equal(response.status,503);assert.doesNotMatch((await response.json()).error,/neste aparelho|Tente em outro dia/);
  }
  assert.equal(calls,6);assert.equal(await (await bucket.get(legacy)).text(),'4');assert.equal((await (await bucket.get('devices/'+id)).json()).approved,true);
 }finally{globalThis.fetch=originalFetch;}
});
test('autorização real do proprietário: conta correta, troca de conta, CSRF e aprovação sem contornar autenticação',async()=>{
 const bucket=new Bucket(),id='a'.repeat(64),env={BUCKET:bucket,OWNER_EMAIL:'  Owner@Example.Test  '};
 await bucket.put('sync/grants/'+id,JSON.stringify({role:'pdv',approved:false,pendingAt:Date.now(),bindingOnly:true,name:'PDV QA'}));
 const path='https://test/sync/authorize?device='+id;
 let r=await gateway.fetch(new Request(path),env);assert.equal(r.status,302);assert.match(r.headers.get('location'),/signin-with-chatgpt/);
 const wrong={'oai-authenticated-user-id':'wrong-id','oai-authenticated-user-email':'wrong@example.test'};
 r=await gateway.fetch(new Request(path,{headers:wrong}),env);assert.equal(r.status,403);assert.match(await r.text(),/TROCAR CONTA E AUTORIZAR/);assert.equal(JSON.parse((await bucket.get('sync/grants/'+id)).s||JSON.stringify(await(await bucket.get('sync/grants/'+id)).json())).approved,false);
 const owner={'oai-authenticated-user-id':'owner-site-id','oai-authenticated-user-email':'owner@example.test'};
 r=await gateway.fetch(new Request(path,{headers:owner}),env);assert.equal(r.status,200);const body=await r.text(),csrf=body.match(/name="csrf" value="([a-f0-9]{64})"/)[1];assert.match(body,/AUTORIZAR ESTE PDV/);
 r=await gateway.fetch(new Request(path,{method:'POST',headers:{...owner,Origin:'https://test'},body:new URLSearchParams({csrf})}),env);assert.equal(r.status,403);
 r=await gateway.fetch(new Request(path,{method:'POST',headers:{...owner,Origin:'https://test',Cookie:'angra_sync_csrf='+csrf},body:new URLSearchParams({csrf})}),env);assert.equal(r.status,200);const grant=await(await bucket.get('sync/grants/'+id)).json();assert.equal(grant.approved,true);assert.equal(grant.tenant,id);assert.equal(grant.bindingOnly,true);
});
