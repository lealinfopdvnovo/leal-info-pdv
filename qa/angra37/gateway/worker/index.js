import {Accounts} from './access.js';
// Validation and project prompt reused unchanged from ANGRA gateway domain.mjs.
export function mm(value,unit){if(typeof value!=='string'||value.length>80||!/^\d+(\.\d+)?$/.test(value))throw Error('Medida inválida');const factor={mm:1n,cm:10n,m:1000n}[unit];if(!factor)throw Error('Unidade inválida');let [a,b='']=value.split('.');let number=BigInt(a+b)*factor;if(number<=0n)throw Error('Medida deve ser positiva');let s=number.toString().padStart(b.length+1,'0');return b.length?(s.slice(0,-b.length)+'.'+s.slice(-b.length)).replace(/\.?0+$/,''):s;}
export function validate(p){if(!p||p.schemaVersion!==1||!/^[0-9a-f-]{36}$/i.test(p.id)||!Number.isInteger(p.revision)||p.revision<0)throw Error('Projeto inválido');for(const [k,max] of [['title',160],['type',100],['model',200],['configuration',4000]])if(typeof p[k]!=='string'||p[k].length>max)throw Error('Campo inválido');if(!p.type.trim()||!p.title.trim()||!Array.isArray(p.measures)||p.measures.length<1||p.measures.length>24)throw Error('Serviço e medidas obrigatórios');const labels=new Set();for(const x of p.measures){if(typeof x.label!=='string'||!x.label.trim()||x.label.length>100||labels.has(x.label.trim().toLowerCase())||mm(x.value,x.unit)!==x.mm)throw Error('Medida inconsistente');labels.add(x.label.trim().toLowerCase());}return {schemaVersion:1,id:p.id,revision:p.revision,title:p.title,type:p.type,model:p.model,configuration:p.configuration,measures:structuredClone(p.measures)};}
export function prompt(p){validate(p);return `Produza uma SIMULAÇÃO VISUAL do serviço de vidraçaria selecionado na foto real enviada. Tipo: ${p.type}. Modelo/configuração: ${p.model||'não especificado'}. Detalhes: ${p.configuration||'não especificados'}. Medidas informadas pelo profissional: ${p.measures.map(x=>`${x.label}: ${x.value} ${x.unit} (canônico ${x.mm} mm)`).join('; ')}. Preserve ambiente, perspectiva e elementos fora da área do serviço. Não assuma que é janela; respeite o tipo selecionado. Não invente medidas, quantidade de folhas, ferragens ou especificação estrutural ausentes. Não acrescente texto de cotas na imagem. A imagem é ilustrativa; as medidas numéricas permanecem separadas e não são inferidas da foto.`;}

const ORIGIN='https://timbrooks-instruct-pix2pix.hf.space';
const API=ORIGIN+'/gradio_api';
let active=false;
const answer=(status,data)=>new Response(JSON.stringify(data),{status,headers:{'Content-Type':'application/json; charset=utf-8','Cache-Control':'no-store'}});
function decodePhoto(s){
 if(typeof s!=='string'||s.length>8000000||!/^[A-Za-z0-9+/]*={0,2}$/.test(s))throw Error('Foto inválida');
 const raw=atob(s),bytes=Uint8Array.from(raw,c=>c.charCodeAt(0));
 if(bytes.length<16||bytes[0]!==255||bytes[1]!==216)throw Error('Foto JPEG inválida');return bytes;
}
async function limitedJson(request,withRaw=false){
 if(!request.headers.get('content-type')?.includes('application/json'))throw Error('Use JSON');
 const reader=request.body?.getReader();if(!reader)throw Error('Solicitação vazia');
 const chunks=[];let size=0;
 while(true){const {done,value}=await reader.read();if(done)break;size+=value.length;if(size>10000000){await reader.cancel();throw Error('Foto excede limite');}chunks.push(value);}
 const bytes=new Uint8Array(size);let off=0;for(const c of chunks){bytes.set(c,off);off+=c.length;}const body=JSON.parse(new TextDecoder().decode(bytes));return withRaw?{body,raw:bytes}:body;
}
export async function generateFree(project,photo,signal){
 const form=new FormData();form.append('files',new Blob([photo],{type:'image/jpeg'}),'local.jpg');
 const up=await fetch(API+'/upload',{method:'POST',body:form,signal});if(!up.ok)throw Error('UPLOAD_UNAVAILABLE');
 const paths=await up.json();if(!Array.isArray(paths)||typeof paths[0]!=='string'||!paths[0].startsWith('/'))throw Error('UPLOAD_INVALID');
 // Instruction edits the supplied photograph; canonical numeric measurements remain separate.
 const type=project.type.trim().toLocaleLowerCase('pt-BR');
 const kind=({'box de banheiro':'shower enclosure','espelho':'mirror','janela':'window','porta':'glass door','vidro fixo':'fixed glass panel','divisória':'glass partition','fechamento':'glass enclosure','guarda-corpo':'glass railing'})[type]||project.type;
 const dimensions=project.measures.map(m=>`${m.label}: ${m.value} ${m.unit}`).join('; ');
 const instruction=(type==='box de banheiro'
 ?'Enclose the existing shower area inside an upright transparent glass shower cubicle with vertical glass walls and a vertical glass door consistent with the selected configuration, standing on the tiled floor. '
 :'Install the selected glasswork service in the supplied photograph. Project type: '+kind+'. ')
 +'Requested dimensions: '+dimensions+'. Model: '+(project.model||'not specified')+'. Configuration: '+(project.configuration||'not specified')+'. Keep the existing room and camera view. Preserve elements outside the installation area. No glass enclosure lying on the floor. No text or technical dimension labels. Commercial visual simulation only; do not infer fabrication dimensions.';

 const input={path:paths[0],orig_name:'local.jpg',mime_type:'image/jpeg',meta:{_type:'gradio.FileData'}};
 const posted=await fetch(API+'/call/generate',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({data:[input,instruction,40,'Fix Seed',46582,'Fix CFG',10,1.2]}),signal});
 if(!posted.ok)throw Error('PROVIDER_UNAVAILABLE');const job=await posted.json();
 if(typeof job.event_id!=='string'||!/^[a-zA-Z0-9_-]{1,100}$/.test(job.event_id))throw Error('JOB_INVALID');
 const stream=await fetch(API+'/call/generate/'+job.event_id,{signal});if(!stream.ok||!stream.body)throw Error('QUEUE_UNAVAILABLE');
 const reader=stream.body.getReader(),decoder=new TextDecoder();let pending='',event='',output;
 try{
  while(!output){
   const {done,value}=await reader.read();if(done)break;pending+=decoder.decode(value,{stream:true});
   if(pending.length>1000000)throw Error('EVENT_LIMIT');let i;
   while((i=pending.indexOf('\n'))>=0){
    const line=pending.slice(0,i).trim();pending=pending.slice(i+1);
    if(line.startsWith('event:'))event=line.slice(6).trim();
    if(line.startsWith('data:')&&event==='error')throw Error('PROVIDER_GENERATION_ERROR');
    if(line.startsWith('data:')&&event==='complete')output=JSON.parse(line.slice(5));
   }
  }
 }finally{await reader.cancel().catch(()=>{});}
 const file=output?.[3];if(!file||typeof file.url!=='string')throw Error('RESULT_MISSING');
 const url=new URL(file.url);if(url.origin!==ORIGIN||!url.pathname.startsWith('/gradio_api/file='))throw Error('RESULT_INVALID');
 const image=await fetch(url,{signal});if(!image.ok)throw Error('RESULT_UNAVAILABLE');
 const bytes=new Uint8Array(await image.arrayBuffer());if(bytes.length<32||bytes.length>12000000)throw Error('RESULT_SIZE');
 const jpeg=bytes[0]===255&&bytes[1]===216,png=bytes[0]===137&&bytes[1]===80,webp=String.fromCharCode(...bytes.slice(0,4))==='RIFF'&&String.fromCharCode(...bytes.slice(8,12))==='WEBP';
 if(!jpeg&&!png&&!webp)throw Error('RESULT_FORMAT');
 let binary='';for(let i=0;i<bytes.length;i+=32768)binary+=String.fromCharCode(...bytes.subarray(i,i+32768));
 return {simulationId:crypto.randomUUID(),imageBase64:btoa(binary),providerRequestId:job.event_id,imageMimeType:webp?'image/webp':png?'image/png':'image/jpeg',simulationNotice:'Simulação comercial. Medidas de fabricação não são inferidas da imagem.'};
}
const utf8=s=>new TextEncoder().encode(s);
const bytes64=s=>Uint8Array.from(atob(s),c=>c.charCodeAt(0));
const hex=bytes=>Array.from(new Uint8Array(bytes),b=>b.toString(16).padStart(2,'0')).join('');
const digest=async bytes=>hex(await crypto.subtle.digest('SHA-256',bytes));
const validId=s=>typeof s==='string'&&/^[a-f0-9]{64}$/.test(s);
const html=s=>new Response('<!doctype html><html lang="pt-BR"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>ANGRA VIDROS</title><body style="font:18px system-ui;background:#081224;color:#e2edf9;padding:32px;max-width:600px;margin:auto"><h1>ANGRA VIDROS</h1>'+s+'</body></html>',{headers:{'Content-Type':'text/html; charset=utf-8','Cache-Control':'no-store','Content-Security-Policy':"default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; frame-ancestors 'none'",'X-Content-Type-Options':'nosniff'}});
async function deviceRecord(bucket,id){if(!validId(id))return null;const obj=await bucket.get('devices/'+id);return obj?{value:await obj.json(),etag:obj.etag}:null;}
async function approve(bucket,id){const rec=await deviceRecord(bucket,id);if(!rec||Date.now()-rec.value.created>86400000)return false;rec.value.approved=true;return !!await bucket.put('devices/'+id,JSON.stringify(rec.value),{onlyIf:{etagMatches:rec.etag}});}
async function counter(bucket,key,max){
 for(let attempt=0;attempt<4;attempt++){
  const old=await bucket.get(key),count=old?Number(await old.text()):0;if(count>=max)return false;
  const saved=await bucket.put(key,String(count+1),{onlyIf:old?{etagMatches:old.etag}:{etagDoesNotMatch:'*'}});if(saved)return true;
 }return false;
}
function rawSignature(der){
 // Android SHA256withECDSA produces ASN.1 DER; WebCrypto expects IEEE-P1363.
 if(der[0]!==48||der.length<8||der.length>72||der[1]!==der.length-2)throw Error('SIGNATURE');let i=2;
 const out=new Uint8Array(64);for(let n=0;n<2;n++){if(der[i++]!==2)throw Error('SIGNATURE');let size=der[i++];if(size<1||size>33||i+size>der.length)throw Error('SIGNATURE');let part=der.subarray(i,i+size);i+=size;if(part.length===33){if(part[0]!==0)throw Error('SIGNATURE');part=part.subarray(1);}out.set(part,n*32+32-part.length);}if(i!==der.length)throw Error('SIGNATURE');return out;
}
export async function verifyDevice(request,body,env,allowUnapproved=false){
 const id=request.headers.get('X-Angra-Device'),stamp=request.headers.get('X-Angra-Time'),nonce=request.headers.get('X-Angra-Nonce'),sig=request.headers.get('X-Angra-Signature');
 if(!validId(id)||!/^\d{13}$/.test(stamp||'')||Math.abs(Date.now()-Number(stamp))>300000||!/^[0-9a-f-]{36}$/.test(nonce||'')||!sig||sig.length>120)return false;
 const rec=await deviceRecord(env.BUCKET,id);if(!rec||(!allowUnapproved&&!rec.value.approved))return false;
 try{
  const key=await crypto.subtle.importKey('spki',bytes64(rec.value.publicKey),{name:'ECDSA',namedCurve:'P-256'},false,['verify']);
  const message=request.method+'\n'+new URL(request.url).pathname+'\n'+stamp+'\n'+nonce+'\n'+await digest(body);
  if(!await crypto.subtle.verify({name:'ECDSA',hash:'SHA-256'},key,rawSignature(bytes64(sig)),utf8(message)))return false;
  const oldNonces=await env.BUCKET.list({prefix:'nonces/'+id+'/',limit:100});const expired=oldNonces.objects.filter(o=>Date.now()-new Date(o.uploaded).getTime()>86400000).map(o=>o.key);if(expired.length)await env.BUCKET.delete(expired);
  const nk='nonces/'+id+'/'+nonce;
  if(!await env.BUCKET.put(nk,String(Date.now()),{onlyIf:{etagDoesNotMatch:'*'}}))return false;
  return id;
 }catch{return false;}
}
export async function authorizationRoutes(request,env){
 const u=new URL(request.url),path=u.pathname;
 if(!env.BUCKET||!env.OWNER_EMAIL||!env.ADMIN_TOKEN)return answer(503,{error:'Acesso seguro temporariamente indisponível'});
 if(path==='/device/register'&&request.method==='POST'){
  let j;try{j=await limitedJson(request);if(typeof j.publicKey!=='string'||j.publicKey.length>180)throw Error();const key=bytes64(j.publicKey);await crypto.subtle.importKey('spki',key,{name:'ECDSA',namedCurve:'P-256'},false,['verify']);const id=await digest(key),rec=await deviceRecord(env.BUCKET,id);
   if(!rec){if(!await counter(env.BUCKET,'registration/'+new Date().toISOString().slice(0,10),20))return answer(429,{error:'Limite de autorizações temporariamente atingido'});await env.BUCKET.put('devices/'+id,JSON.stringify({publicKey:j.publicKey,approved:false,created:Date.now()}),{onlyIf:{etagDoesNotMatch:'*'}});}
   return answer(200,{deviceId:id,approved:!!rec?.value.approved,authorizationUrl:u.origin+'/pair?device='+id});
  }catch{return answer(400,{error:'Identidade do aparelho inválida'});}
 }
 if(path==='/pair'){
  const id=u.searchParams.get('device');if(!validId(id))return answer(400,{error:'Aparelho inválido'});
  const owner=request.headers.get('oai-authenticated-user-id');if(!owner)return Response.redirect(u.origin+'/signin-with-chatgpt?return_to='+encodeURIComponent('/pair?device='+id),302);
  if((request.headers.get('oai-authenticated-user-email')||'').toLowerCase()!==env.OWNER_EMAIL.toLowerCase())return html('<p>Somente o proprietário deste serviço pode autorizar o aparelho.</p>');
  const rec=await deviceRecord(env.BUCKET,id);if(!rec)return html('<p>Solicitação não encontrada. Volte ao aplicativo e tente novamente.</p>');
  if(rec.value.approved)return html('<p>Celular autorizado. Volte ao ANGRA VIDROS e toque em CRIAR IMAGEM COM AS MEDIDAS.</p>');
  if(request.method==='POST'){
   const form=await request.formData(),cookie=request.headers.get('Cookie')||'',csrf=cookie.match(/(?:^|; )angra_csrf=([a-f0-9]{64})/);if(request.headers.get('Origin')!==u.origin||!csrf||form.get('csrf')!==csrf[1])return answer(403,{error:'Autorização inválida'});
   if(!await approve(env.BUCKET,id))return answer(409,{error:'Solicitação expirada. Tente novamente no app'});
   return html('<p>Celular autorizado. Volte ao ANGRA VIDROS e toque em CRIAR IMAGEM COM AS MEDIDAS.</p>');
  }
  const csrf=hex(crypto.getRandomValues(new Uint8Array(32))),response=html('<p>Autorizar este celular a criar simulações comerciais gratuitas?</p><p>Identificação: '+id.slice(0,12)+'</p><form method="post"><input type="hidden" name="csrf" value="'+csrf+'"><button style="padding:16px;font-size:18px">AUTORIZAR ESTE CELULAR</button></form>');response.headers.set('Set-Cookie','angra_csrf='+csrf+'; Secure; HttpOnly; SameSite=Strict; Max-Age=600; Path=/pair');return response;
 }
 if(path==='/ci/authorize'&&request.method==='POST'){
  try{
   const body=await limitedJson(request),id=body.deviceId;if(!validId(id))throw Error();
   const jwt=(request.headers.get('Authorization')||'').replace(/^Bearer /,'');if(jwt.length>16000)throw Error();const parts=jwt.split('.');if(parts.length!==3)throw Error();
   const decode=s=>bytes64(s.replace(/-/g,'+').replace(/_/g,'/').padEnd(Math.ceil(s.length/4)*4,'='));
   const header=JSON.parse(new TextDecoder().decode(decode(parts[0]))),claims=JSON.parse(new TextDecoder().decode(decode(parts[1])));
   const now=Date.now()/1000;
   if(header.alg!=='RS256'||claims.iss!=='https://token.actions.githubusercontent.com'||claims.aud!=='angra-vidros-ci:'+id||claims.repository_id!=='1358575974'||claims.repository_owner_id!=='325432049'||claims.ref!=='refs/heads/feature/app-vidraceiro-android-foundation'||claims.workflow_ref!=='lealinfopdvnovo/leal-info-pdv/.github/workflows/app_vidraceiro_android.yml@refs/heads/feature/app-vidraceiro-android-foundation'||claims.exp<now||claims.nbf>now+30)throw Error();
   const jwks=await fetch('https://token.actions.githubusercontent.com/.well-known/jwks',{signal:AbortSignal.timeout(10000)}).then(r=>r.json());const jwk=jwks.keys?.find(k=>k.kid===header.kid&&k.kty==='RSA');if(!jwk)throw Error();
   const key=await crypto.subtle.importKey('jwk',jwk,{name:'RSASSA-PKCS1-v1_5',hash:'SHA-256'},false,['verify']);
   if(!await crypto.subtle.verify('RSASSA-PKCS1-v1_5',key,decode(parts[2]),utf8(parts[0]+'.'+parts[1])))throw Error();
   if(!await counter(env.BUCKET,'ci-authorization/'+new Date().toISOString().slice(0,10),6))return answer(429,{error:'Limite de testes atingido'});
   if(!await approve(env.BUCKET,id))throw Error();return answer(200,{approved:true});
  }catch{return answer(401,{error:'Identidade de teste não autorizada'});}
 }
 if(path.startsWith('/admin/')){
  const expected='Bearer '+env.ADMIN_TOKEN,provided=request.headers.get('Authorization')||'';if(await digest(utf8(expected))!==await digest(utf8(provided)))return answer(401,{error:'Não autorizado'});
  if(path==='/admin/approve'&&request.method==='POST'){const j=await limitedJson(request);const ok=await approve(env.BUCKET,j.deviceId);return answer(ok?200:400,{approved:ok});}
  return answer(404,{error:'Rota inexistente'});
 }
 return null;
}

const gateway={async fetch(request,env){
 const path=new URL(request.url).pathname;
 if(path==='/access/owner')return Accounts.ownerPage(request,env);
 if(path.startsWith('/ci/')||path.startsWith('/admin/')||path==='/sync/ci-authorize'||path==='/sync/ci-grant')return answer(404,{error:'Rota inexistente neste gateway QA37'});
 if(path.startsWith('/sync-file/'))return syncFile(request,env);
 if(path.startsWith('/sync/'))return syncRoutes(request,env);
 if(path==='/'&&request.method==='GET')return answer(200,{service:'ANGRA VIDROS',purpose:'ANGRA VIDROS QA37 — integração independente',environment:'QA37',client:'005',financialEffect:'NONE'});
 if(path==='/v1/projects')return answer(409,{error:'Integração com PDV não está ativada nesta etapa'});
 if(path.startsWith('/device/')||path==='/pair'||path.startsWith('/admin/')||path==='/ci/authorize')return authorizationRoutes(request,env);
 if(path!=='/v1/simulations')return answer(404,{error:'Rota inexistente'});
 if(request.method!=='POST')return answer(405,{error:'Método não permitido'});
 if(!env.BUCKET)return answer(503,{error:'Acesso temporariamente indisponível'});
 if(!request.headers.get('X-Angra-Signature'))return answer(401,{error:'Autorize este celular antes de criar a simulação'});
 let body,project,photo,raw;
 try{const parsed=await limitedJson(request,true);body=parsed.body;raw=parsed.raw;project=validate(body.project);photo=decodePhoto(body.photoBase64);}catch{return answer(400,{error:'Projeto ou foto inválidos'});}
 const id=await verifyDevice(request,raw,env);if(!id)return answer(401,{error:'Autorize este celular antes de criar a simulação'});
 const ar=await readObject(env.BUCKET,grantKey(id)),grant=ar?.data;if(!usable(grant)||grant.role!=='app')throw new Accounts.Error(403,'Conecte o app ao PDV.');const actor=await Accounts.authenticated(env,grant,id,request.headers.get('X-Angra-Session'));if(!Accounts.permission(actor,'images'))throw new Accounts.Error(403,'Criação de imagem não autorizada.');
 if(active)return answer(429,{error:'Outra geração está em andamento. Aguarde.'});
 // No invented device/day quota: only the upstream provider controls its free GPU allowance.
 // Keep approved-device signatures, replay rejection and single-generation protection.
 // Legacy usage/* counters are deliberately ignored; device identity is never reset.
 active=true;try{return answer(200,await generateFree(project,photo,AbortSignal.timeout(150000)));}
 catch{return answer(503,{error:'O motor gratuito não concluiu a simulação. A cota compartilhada pode estar indisponível. Nenhuma cobrança foi iniciada.'});}
 finally{active=false;}
}};

// ANGRA structured synchronization. Reuses this Worker's R2 and P-256 proof.
const SYNC_ENTITIES=['clients','suppliers','materials','projects'];
const UUID=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const safeHtml=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
async function readObject(bucket,key){const r=await bucket.get(key);return r?{data:await r.json(),etag:r.etag}:null;}
const grantKey=id=>'sync/grants/'+id;
function usable(g){return g&&g.approved&&(!g.expires||g.expires>Date.now());}
function sorted(value){if(Array.isArray(value))return value.map(sorted);if(value&&typeof value==='object')return Object.fromEntries(Object.keys(value).sort().map(k=>[k,sorted(value[k])]));return value;}
function checkedPayload(entity,p){
 if(!p||typeof p!=='object'||Array.isArray(p)||JSON.stringify(p).length>2000000)throw Error('Dados excedem limite ou formato inválido');
 if(entity==='clients'||entity==='suppliers'){for(const k of ['name','document','phone','email','address','cep','street','number','complement','district','city','uf'])if(p[k]!==undefined&&(typeof p[k]!=='string'||p[k].length>(k==='address'?4000:1000)))throw Error('Cadastro inválido');if(!p.name?.trim())throw Error('Nome obrigatório');}
 if(entity==='materials'&&(!p.name||typeof p.name!=='string'))throw Error('Produto inválido');
 if(entity==='projects'){
  if(!p.title?.trim()||!p.type?.trim()||!Array.isArray(p.measures)||!p.measures.length||p.measures.length>24)throw Error('Projeto sem serviço ou medidas');
  const names=new Set();for(const m of p.measures){if(typeof m.label!=='string'||!m.label.trim()||names.has(m.label.toLowerCase())||typeof m.value!=='string'||typeof m.mm!=='string')throw Error('Dimensão inválida');names.add(m.label.toLowerCase());if(mm(m.value.replace(',','.'),m.unit)!==mm(m.mm,'mm'))throw Error('Dimensão física inconsistente');}
  if(p.clientLocalId&&!UUID.test(p.clientLocalId))throw Error('Vínculo de cliente inválido');
  if(p.materialLocalIds&&(!Array.isArray(p.materialLocalIds)||p.materialLocalIds.some(id=>!UUID.test(id))))throw Error('Vínculo de material inválido');
 }
 return p;
}
async function ciGrant(request,env,body){
 // GitHub identity creates ONLY an expiring isolated test namespace, never production membership.
 try{
  const id=body.deviceId;if(!validId(id)||!['pdv','app'].includes(body.role))throw Error();
  const parts=(request.headers.get('Authorization')||'').replace(/^Bearer /,'').split('.');if(parts.length!==3)throw Error();
  const dec=s=>bytes64(s.replace(/-/g,'+').replace(/_/g,'/').padEnd(Math.ceil(s.length/4)*4,'='));
  const head=JSON.parse(new TextDecoder().decode(dec(parts[0]))),c=JSON.parse(new TextDecoder().decode(dec(parts[1]))),now=Date.now()/1000;
  if(head.alg!=='RS256'||c.iss!=='https://token.actions.githubusercontent.com'||c.aud!=='angra-sync-ci:'+id||c.repository_id!=='1358575974'||c.repository_owner_id!=='325432049'||!((c.ref==='refs/heads/feature/angra-pdv-sync'&&c.workflow_ref==='lealinfopdvnovo/leal-info-pdv/.github/workflows/angra_sync_test.yml@refs/heads/feature/angra-pdv-sync')||(c.event_name==='pull_request'&&c.head_ref==='feature/angra-pdv-sync'&&c.base_ref==='feature/app-vidraceiro-android-foundation'&&/^refs\/pull\/[0-9]+\/merge$/.test(c.ref)&&c.workflow_ref==='lealinfopdvnovo/leal-info-pdv/.github/workflows/angra_sync_test.yml@'+c.ref))||c.exp<now||c.nbf>now+30||!/^\d+$/.test(c.run_id))throw Error();
  const jwks=await fetch('https://token.actions.githubusercontent.com/.well-known/jwks',{signal:AbortSignal.timeout(10000)}).then(r=>r.json()),jwk=jwks.keys?.find(k=>k.kid===head.kid&&k.kty==='RSA');if(!jwk)throw Error();
  const key=await crypto.subtle.importKey('jwk',jwk,{name:'RSASSA-PKCS1-v1_5',hash:'SHA-256'},false,['verify']);if(!await crypto.subtle.verify('RSASSA-PKCS1-v1_5',key,dec(parts[2]),utf8(parts[0]+'.'+parts[1])))throw Error();
  const previousGrant=(await readObject(env.BUCKET,grantKey(id)))?.data;if(previousGrant&&!/^test-[0-9]+(?:-other)?$/.test(previousGrant.tenant||''))throw Error();
  let device=await deviceRecord(env.BUCKET,id);if(!device){if(typeof body.publicKey!=='string'||body.publicKey.length>180)throw Error();const pub=bytes64(body.publicKey);await crypto.subtle.importKey('spki',pub,{name:'ECDSA',namedCurve:'P-256'},false,['verify']);if(await digest(pub)!==id)throw Error();await env.BUCKET.put('devices/'+id,JSON.stringify({publicKey:body.publicKey,approved:false,created:Date.now(),ciOnly:true}),{onlyIf:{etagDoesNotMatch:'*'}});}else if(device.value.approved)throw Error();
  const tenant='test-'+c.run_id+(body.otherTenant?'-other':'');
  const value={approved:true,role:body.role,tenant,name:'TESTE AUTOMATIZADO',bindingOnly:body.bindingOnly===true,expires:Date.now()+7200000};await env.BUCKET.put(grantKey(id),JSON.stringify(value));return answer(200,value);
 }catch{return answer(401,{error:'Identidade de teste não autorizada'});}
}
async function syncRoutes(request,env){
 const u=new URL(request.url),path=u.pathname;if(!path.startsWith('/sync/'))return null;
 if(!env.BUCKET)return answer(503,{error:'Armazenamento indisponível'});
 if(path==='/sync/authorize'){
  const id=u.searchParams.get('device');if(!validId(id))return answer(400,{error:'Identidade inválida'});
  if(!request.headers.get('oai-authenticated-user-id'))return Response.redirect(u.origin+'/signin-with-chatgpt?return_to='+encodeURIComponent('/sync/authorize?device='+id),302);
  const signedEmail=(request.headers.get('oai-authenticated-user-email')||'').trim();
  if(!env.OWNER_EMAIL)return answer(503,{error:'Configuração do proprietário indisponível'});
  if(signedEmail.toLowerCase()!==env.OWNER_EMAIL.trim().toLowerCase()){
   const back='/sync/authorize?device='+id;
   const page=html('<h2>Entre com a conta do proprietário</h2><p>A conta conectada'+(signedEmail?': <strong>'+safeHtml(signedEmail)+'</strong>': ' não informou e-mail')+' não é a conta autorizada para este PDV.</p><p>Nenhum vínculo ou dado foi alterado.</p><p><a style="display:inline-block;padding:16px;background:#1fc8e8;color:#081224;border-radius:10px;text-decoration:none" target="_top" href="/signout-with-chatgpt?return_to='+encodeURIComponent(back)+'">TROCAR CONTA E AUTORIZAR</a></p><p>Depois de entrar com a conta proprietária, confirme AUTORIZAR ESTE PDV e volte ao programa para gerar o código.</p>');return new Response(page.body,{status:403,headers:page.headers});
  }
  const pending=await readObject(env.BUCKET,grantKey(id));if(!pending||pending.data.role!=='pdv'||Date.now()-pending.data.pendingAt>86400000)return html('<p>Solicitação expirada. Inicie novamente a conexão no PDV.</p>');
  if(pending.data.approved)return html('<p>PDV autorizado. Volte ao computador. A sincronização funciona automaticamente.</p>');
  if(request.method==='POST'){
   const form=await request.formData(),csrf=(request.headers.get('Cookie')||'').match(/(?:^|; )angra_sync_csrf=([a-f0-9]{64})/);if(request.headers.get('Origin')!==u.origin||!csrf||form.get('csrf')!==csrf[1])return answer(403,{error:'Confirmação inválida'});
   pending.data.approved=true;pending.data.tenant=id;if(!await env.BUCKET.put(grantKey(id),JSON.stringify(pending.data),{onlyIf:{etagMatches:pending.etag}}))return answer(409,{error:'Solicitação alterada. Atualize a página para confirmar novamente.'});return html('<p>PDV autorizado. Volte ao computador e use o código de conexão no app.</p>');
  }
  const csrf=hex(crypto.getRandomValues(new Uint8Array(32))),r=html('<h2>Conectar PDV</h2><p>'+safeHtml(pending.data.name)+'</p><p>Identificação: '+id.slice(0,16)+'</p><p>Autorize apenas o computador ANGRA VIDROS correto. Cada PDV tem base isolada.</p><form method="post"><input type="hidden" name="csrf" value="'+csrf+'"><button style="padding:16px">AUTORIZAR ESTE PDV</button></form>');r.headers.set('Set-Cookie','angra_sync_csrf='+csrf+'; Secure; HttpOnly; SameSite=Strict; Max-Age=600; Path=/sync/authorize');return r;
 }
 if(request.method!=='POST')return answer(405,{error:'Use POST'});
 let body,raw;try{({body,raw}=await limitedJson(request,true));}catch{return answer(400,{error:'Solicitação inválida'});}
 if(path==='/sync/ci-grant')return ciGrant(request,env,body);
 const id=await verifyDevice(request,raw,env,true);if(!id)return answer(401,{error:'Assinatura ou identidade inválida'});
 const grantRecord=await readObject(env.BUCKET,grantKey(id));let g=grantRecord?.data;
 if(path==='/sync/request-pdv'){
  if(g?.role==='app')return answer(409,{error:'Aparelho já vinculado como celular'});
  if(!g){g={approved:false,role:'pdv',name:String(body.name||'PDV').slice(0,100),pendingAt:Date.now(),bindingOnly:body.bindingOnly===true};await env.BUCKET.put(grantKey(id),JSON.stringify(g),{onlyIf:{etagDoesNotMatch:'*'}});}
  if(body.bindingOnly===true&&g.bindingOnly!==true&&!g.workshopOnly){const saved=await readObject(env.BUCKET,grantKey(id));if(!saved)return answer(409,{error:'Atualize o vínculo'});saved.data.bindingOnly=true;if(!await env.BUCKET.put(grantKey(id),JSON.stringify(saved.data),{onlyIf:{etagMatches:saved.etag}}))return answer(409,{error:'Vínculo alterado. Tente novamente.'});g=saved.data;}
  return answer(200,{approved:usable(g),tenant:usable(g)?g.tenant:null,authorizationUrl:u.origin+'/sync/authorize?device='+id});
 }
 // A signed handset can leave its own grant without contacting the PDV or affecting siblings.
 if(path==='/sync/unlink'){
  if(g?.role!=='app')return answer(403,{error:'Somente o aplicativo pode desfazer seu próprio vínculo'});
  const current=await readObject(env.BUCKET,grantKey(id));if(!current||current.data.role!=='app')return answer(409,{error:'Vínculo alterado. Tente novamente.'});
  if(current.data.approved!==true)return answer(200,{connected:false,revoked:true});
  current.data.approved=false;current.data.unlinkedAt=Date.now();
  if(!await env.BUCKET.put(grantKey(id),JSON.stringify(current.data),{onlyIf:{etagMatches:current.etag}}))return answer(409,{error:'Vínculo alterado. Tente novamente.'});
  return answer(200,{connected:false,revoked:true});
 }
 if(path==='/sync/pair'){
  if(typeof body.code!=='string'||!(/^(?:[0-9]{6}|[A-F0-9]{24})$/).test(body.code))return answer(400,{error:'Código inválido'});
  if(!await counter(env.BUCKET,'sync/pair-attempt/'+id+'/'+new Date().toISOString().slice(0,10),20))return answer(429,{error:'Muitas tentativas de conexão'});
  if(body.code.length===6&&!await counter(env.BUCKET,'sync/short-attempt/'+await digest(utf8(request.headers.get('CF-Connecting-IP')||'shared'))+'/'+Math.floor(Date.now()/600000),40))return answer(429,{error:'Muitas tentativas. Aguarde para tentar novamente.'});
  const key='sync/tickets/'+await digest(utf8(body.code)),ticket=await readObject(env.BUCKET,key);if(!ticket||(g?.unlinkedAt&&ticket.data.expires-600000<=g.unlinkedAt)||ticket.data.expires<Date.now()||(ticket.data.claimedBy&&ticket.data.claimedBy!==id))return answer(403,{error:'Código expirado ou já utilizado'});
  if(usable(g)&&g.tenant!==ticket.data.tenant)return answer(409,{error:'Este aparelho já pertence a outro PDV. Seus dados foram preservados'});
  const pg=(await readObject(env.BUCKET,grantKey(ticket.data.pdv)))?.data;if(!usable(pg)||pg.role!=='pdv'||(pg.ticketEpoch||'')!==(ticket.data.ticketEpoch||''))return answer(403,{error:'PDV não autorizado'});
  ticket.data.claimedBy=id;if(!await env.BUCKET.put(key,JSON.stringify(ticket.data),{onlyIf:{etagMatches:ticket.etag}}))return answer(409,{error:'Código em uso; tente novamente'});
  if(g?.role==='pdv')return answer(409,{error:'Um PDV não pode ser vinculado como celular'});
  const imageDevice=await deviceRecord(env.BUCKET,id);if(imageDevice&&!imageDevice.value.approved){imageDevice.value.approved=true;if(!await env.BUCKET.put('devices/'+id,JSON.stringify(imageDevice.value),{onlyIf:{etagMatches:imageDevice.etag}}))return answer(409,{error:'Registro alterado. Tente novamente.'});}
  g={approved:true,role:'app',tenant:ticket.data.tenant,name:pg.name,pdv:ticket.data.pdv,linkEpoch:pg.linkEpoch||'',bindingOnly:pg.bindingOnly===true,...(pg.expires?{expires:pg.expires}:{})};if(!await env.BUCKET.put(grantKey(id),JSON.stringify(g),{onlyIf:grantRecord?{etagMatches:grantRecord.etag}:{etagDoesNotMatch:'*'}}))return answer(409,{error:'Vínculo alterado. Gere um novo código.'});return answer(200,{tenant:g.tenant,name:g.name,connected:true,bindingOnly:g.bindingOnly===true});
 }
 if(g?.role==='app'&&g.pdv){const parent=(await readObject(env.BUCKET,grantKey(g.pdv)))?.data;if(!usable(parent)||(parent.linkEpoch||'')!==(g.linkEpoch||''))return answer(403,{error:'DESCONECTADO — vínculo revogado no PDV. Gere um novo código para reconectar.'});g.bindingOnly=parent.bindingOnly===true;g.workshopOnly=parent.workshopOnly===true;}
 if(!usable(g))return answer(403,{error:'Conecte este aparelho ao PDV antes de sincronizar'});
 const accessReply=await Accounts.api(request,env,g,id,path,body);if(accessReply)return accessReply;
 const actor=g.role==='app'&&path!=='/sync/status'?await Accounts.authenticated(env,g,id,request.headers.get('X-Angra-Session')):null;
 if(actor&&path==='/sync/exchange'&&!Accounts.permission(actor,'send')&&!Accounts.permission(actor,'history'))throw new Accounts.Error(403,'Sincronização não autorizada.');
 if(path==='/sync/enable-workshop'){
  if(g.role!=='pdv')return answer(403,{error:'Somente o PDV pode ativar a integração da vidraçaria'});
  if(g.bindingOnly||!g.workshopOnly){const record=await readObject(env.BUCKET,grantKey(id));if(!record||!usable(record.data))return answer(403,{error:'PDV não autorizado'});record.data.bindingOnly=false;record.data.workshopOnly=true;if(!await env.BUCKET.put(grantKey(id),JSON.stringify(record.data),{onlyIf:{etagMatches:record.etag}}))return answer(409,{error:'Vínculo mudou. Tente novamente.'});g=record.data;}
  return answer(200,{tenant:g.tenant,enabled:true,bindingOnly:false});
 }
 if(path==='/sync/status'){
  const includeLinks=body.includeLinkedDevices!==false;let linked=includeLinks?0:null;if(g.role==='pdv'){if(includeLinks){let cursor;do{const page=await env.BUCKET.list({prefix:'sync/grants/',limit:1000,cursor});for(let offset=0;offset<page.objects.length;offset+=32){const children=await Promise.all(page.objects.slice(offset,offset+32).map(item=>readObject(env.BUCKET,item.key)));for(const child of children.map(x=>x?.data))if(usable(child)&&child.role==='app'&&child.pdv===id&&(child.linkEpoch||'')===(g.linkEpoch||''))linked++;}cursor=page.truncated?page.cursor:undefined;}while(cursor);}await env.BUCKET.put('sync/seen/'+g.tenant,JSON.stringify({at:Date.now()}));}
  return answer(200,{tenant:g.tenant,role:g.role,name:g.name,connected:g.role==='app'?true:includeLinks?linked>0:null,linkedDevices:linked,linkStatusIncluded:includeLinks,bindingOnly:g.bindingOnly===true,workshopOnly:g.workshopOnly===true,pdvLastSeen:(await readObject(env.BUCKET,'sync/seen/'+g.tenant))?.data.at||null});
 }
 if(path==='/sync/revoke'){
  if(g.role!=='pdv')return answer(403,{error:'Somente o PDV pode revogar o vínculo'});
  const current=await readObject(env.BUCKET,grantKey(id));if(!current)return answer(409,{error:'Vínculo alterado. Atualize o status.'});
  current.data.linkEpoch=crypto.randomUUID();current.data.ticketEpoch=crypto.randomUUID();
  if(!await env.BUCKET.put(grantKey(id),JSON.stringify(current.data),{onlyIf:{etagMatches:current.etag}}))return answer(409,{error:'Vínculo alterado. Tente novamente.'});
  return answer(200,{connected:false,revoked:true});
 }
 if(path==='/sync/ticket'){
  if(g.role!=='pdv')return answer(403,{error:'Somente o PDV pode vincular celular'});
  const current=await readObject(env.BUCKET,grantKey(id));if(!current)return answer(409,{error:'PDV indisponível'});
  const epoch=crypto.randomUUID(),expires=Date.now()+600000;let code,key;
  for(let attempt=0;attempt<12;attempt++){
   if(body.shortCode===true){let n;do{n=crypto.getRandomValues(new Uint32Array(1))[0];}while(n>=4294000000);code=String(n%1000000).padStart(6,'0');}else code=hex(crypto.getRandomValues(new Uint8Array(12))).toUpperCase();
   key='sync/tickets/'+await digest(utf8(code));const previous=await readObject(env.BUCKET,key);if(previous&&previous.data.expires>=Date.now()){code=null;continue;}
   if(await env.BUCKET.put(key,JSON.stringify({tenant:g.tenant,pdv:id,expires,ticketEpoch:epoch}),{onlyIf:previous?{etagMatches:previous.etag}:{etagDoesNotMatch:'*'}}))break;code=null;
  }
  if(!code)return answer(409,{error:'Gere novamente o código de conexão'});
  current.data.ticketEpoch=epoch;
  if(!await env.BUCKET.put(grantKey(id),JSON.stringify(current.data),{onlyIf:{etagMatches:current.etag}}))return answer(409,{error:'Outro código está sendo gerado. Tente novamente.'});
  return answer(200,{code,expires,tenant:g.tenant});
 }
 if(g.bindingOnly===true&&path==='/sync/exchange')return answer(200,{tenant:g.tenant,results:[],records:[],cursor:null,bindingOnly:true,pdvLastSeen:(await readObject(env.BUCKET,'sync/seen/'+g.tenant))?.data.at||null});
 if(g.bindingOnly===true&&(path.startsWith('/sync/photo-')))return answer(403,{error:'Esta etapa permite somente vinculação; dados preservados'});
 if(path==='/sync/exchange'){
  if(raw.length>4000000||!Array.isArray(body.changes)||body.changes.length>20)return answer(400,{error:'Lote inválido'});
  const results=[];for(const change of body.changes){
   try{
    const {entity,id:recordId,operationId,baseRevision}=change;let payload=change.payload;
    if((g.workshopOnly&&entity==='suppliers')||!SYNC_ENTITIES.includes(entity)||!UUID.test(recordId)||!UUID.test(operationId)||!Number.isSafeInteger(baseRevision)||baseRevision<0)throw Error('Identificador ou revisão inválida');
    if(entity==='materials'&&g.role!=='pdv'){results.push({entity,id:recordId,error:'Produtos e estoque são controlados pelo PDV'});continue;}
    const key='sync/data/'+g.tenant+'/'+entity+'/'+recordId,current=await readObject(env.BUCKET,key);if(actor)await Accounts.writing(env,g,id,actor,entity,current?.data,payload);let authorship=current?.data.authorship||current?.data.payload?.authorship||null;if(entity==='projects'){authorship=authorship||(actor?Accounts.author(actor):null);if(authorship){payload=structuredClone(payload);payload.authorship=authorship;}}checkedPayload(entity,payload);const hash=await digest(utf8(JSON.stringify(sorted(payload))));
    if(current?.data.operationId===operationId&&current.data.hash!==hash)throw Error('Idempotência: operação reutilizada com dados diferentes');
    if(current&&((current.data.hash===hash&&!(g.role==='pdv'&&(current.data.receivedByPdv!==true||(change.officialId&&String(change.officialId)!==String(current.data.officialId)))))||current.data.operationId===operationId)){results.push({entity,id:recordId,ok:true,record:current.data});continue;}
    if((current?.data.revision||0)!==baseRevision){results.push({entity,id:recordId,conflict:true,record:current?.data||null});continue;}
    const record={entity,id:recordId,revision:baseRevision+1,operationId,hash,payload,authorship,lastSentBy:actor?Accounts.author(actor):current?.data.lastSentBy||null,updatedAt:Date.now(),writer:id,receivedByPdv:g.role==='pdv',officialId:g.role==='pdv'?change.officialId||current?.data.officialId||null:current?.data.officialId||null};
    const saved=await env.BUCKET.put(key,JSON.stringify(record),{onlyIf:current?{etagMatches:current.etag}:{etagDoesNotMatch:'*'}});
    results.push(saved?{entity,id:recordId,ok:true,record}:{entity,id:recordId,conflict:true,record:(await readObject(env.BUCKET,key))?.data||null});
   }catch(e){results.push({entity:change?.entity,id:change?.id,error:e.message||'Registro inválido'});}
  }
  if(g.role==='pdv')await env.BUCKET.put('sync/seen/'+g.tenant,JSON.stringify({at:Date.now()}));
  const page=await env.BUCKET.list({prefix:'sync/data/'+g.tenant+'/',limit:5,...(typeof body.cursor==='string'&&body.cursor!==''?{cursor:body.cursor}:{})}),records=[];
  const snapshot=await Promise.all(page.objects.filter(item=>item.key.startsWith('sync/data/'+g.tenant+'/')).map(item=>readObject(env.BUCKET,item.key)));for(const rec of snapshot)if(rec&&(!g.workshopOnly||rec.data.entity!=='suppliers')&&(!actor||(rec.data.entity==='projects'?Accounts.canView(actor,rec.data):Accounts.permission(actor,rec.data.entity==='clients'?'customers':rec.data.entity))))records.push(rec.data);
  return answer(200,{tenant:g.tenant,results,records,cursor:page.truncated?page.cursor:null,pdvLastSeen:(await readObject(env.BUCKET,'sync/seen/'+g.tenant))?.data.at||null});
 }
 if(path==='/sync/photo-put'||path==='/sync/photo-get'){
  // Separate authenticated binary transfer uses a signed JSON manifest and a short-lived capability.
  const {projectId,sha256,size}=body;if(!UUID.test(projectId)||!validId(sha256))return answer(400,{error:'Anexo inválido'});
  const record=await readObject(env.BUCKET,'sync/data/'+g.tenant+'/projects/'+projectId);if(!record)return answer(409,{error:'Sincronize o projeto antes do anexo'});
  if(actor&&(!Accounts.canView(actor,record.data)||!Accounts.permission(actor,path==='/sync/photo-put'?'send':Accounts.permission(actor,'history')?'history':'send')))throw new Accounts.Error(403,'Anexo não autorizado.');
  const manifest=record.data.payload.photoAttachment;if(manifest?.sha256!==sha256)return answer(409,{error:'Anexo não corresponde à revisão atual do projeto'});
  if(path==='/sync/photo-put'&&(!Number.isInteger(size)||size<16||size>8000000||size!==manifest.sizeBytes))return answer(400,{error:'Tamanho inválido'});
  const capability=hex(crypto.getRandomValues(new Uint8Array(32))),expires=Date.now()+120000;
  await env.BUCKET.put('sync/caps/'+await digest(utf8(capability)),JSON.stringify({tenant:g.tenant,device:id,projectId,sha256,size,...actor?{userSessionHash:actor.sessionHash,authorUid:record.data.authorship?.uid||''}:{},mode:path.endsWith('put')?'put':'get',expires}));return answer(200,{url:u.origin+'/sync-file/'+capability,expires});
 }
 return answer(404,{error:'Rota de integração inexistente'});
}
async function syncFile(request,env){
 const cap=new URL(request.url).pathname.split('/').pop();if(!/^[a-f0-9]{64}$/.test(cap||''))return answer(403,{error:'Anexo não autorizado'});
 const rec=await readObject(env.BUCKET,'sync/caps/'+await digest(utf8(cap)));if(!rec||rec.data.expires<Date.now())return answer(403,{error:'Autorização de anexo expirada'});
 const g=rec.data,key='sync/photos/'+g.tenant+'/'+g.projectId+'/'+g.sha256;
 const actor=g.device?(await readObject(env.BUCKET,grantKey(g.device)))?.data:null;if(actor?.role==='app'&&!await Accounts.capAllowed(env,g))return answer(403,{error:'Acesso do usuário revogado'});if(!usable(actor)||actor.tenant!==g.tenant)return answer(403,{error:'Vínculo do anexo revogado'});if(actor.pdv){const parent=(await readObject(env.BUCKET,grantKey(actor.pdv)))?.data;if(!usable(parent)||(parent.linkEpoch||'')!==(actor.linkEpoch||''))return answer(403,{error:'Vínculo do anexo revogado'});}
 if(g.mode==='get'&&request.method==='GET'){const file=await env.BUCKET.get(key);return file?new Response(file.body,{headers:{'Content-Type':'image/jpeg','Cache-Control':'private, no-store','X-Content-Type-Options':'nosniff'}}):answer(404,{error:'Anexo ainda pendente'});}
 if(g.mode!=='put'||request.method!=='PUT'||request.headers.get('content-type')!=='image/jpeg')return answer(405,{error:'Envio de anexo inválido'});
 const reader=request.body?.getReader();if(!reader)return answer(400,{error:'Foto vazia'});let n=0,chunks=[];while(true){const r=await reader.read();if(r.done)break;n+=r.value.length;if(n>g.size){await reader.cancel();return answer(413,{error:'Foto excede tamanho autorizado'});}chunks.push(r.value);}
 const bytes=new Uint8Array(n);let offset=0;for(const part of chunks){bytes.set(part,offset);offset+=part.length;}
 if(n!==g.size||bytes[0]!==255||bytes[1]!==216||await digest(bytes)!==g.sha256)return answer(400,{error:'Foto não confere com o manifesto'});
 await env.BUCKET.put(key,bytes,{httpMetadata:{contentType:'image/jpeg'}});return answer(200,{stored:true,sha256:g.sha256});
}

export default {async fetch(request,env){try{return await gateway.fetch(request,env);}catch(e){if(e instanceof Accounts.Error)return answer(e.status,{error:e.message,accessDenied:true});return answer(500,{error:'Serviço temporariamente indisponível. Dados preservados.'});}}};
