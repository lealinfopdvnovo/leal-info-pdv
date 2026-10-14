import {retentionHours,pruneJobs} from './retention.mjs';
import http from 'node:http';import {mkdir,writeFile,readFile,rename,access} from 'node:fs/promises';import {join,resolve} from 'node:path';import {randomUUID,timingSafeEqual} from 'node:crypto';import {validate,prompt,hash} from './domain.mjs';
const generationOnly=process.env.GENERATION_ONLY==='true';
const token=process.env.GATEWAY_TOKEN,client=process.env.CLIENT_CODE,inbox=process.env.PDV_INBOX,key=process.env.OPENAI_API_KEY;
if(!token||token.length<32||(!generationOnly&&(!client||!/^[a-zA-Z0-9_-]{1,32}$/.test(client)||!inbox)))throw Error('Configure GATEWAY_TOKEN forte, CLIENT_CODE e PDV_INBOX absoluto.');
if(!generationOnly&&!inbox.startsWith('/')&&!/^[A-Za-z]:[\\/]/.test(inbox))throw Error('PDV_INBOX deve ser absoluto');
const root=resolve(process.env.DATA_DIR||'vidraceiro-data');let generating=false;
if(!generationOnly){await mkdir(join(root,'jobs'),{recursive:true});await mkdir(inbox,{recursive:true});const retention=retentionHours(process.env.RETENTION_HOURS??24);await pruneJobs(join(root,'jobs'),retention);setInterval(()=>pruneJobs(join(root,'jobs'),retention).catch(()=>console.error('Falha na limpeza dos jobs privados; verificar armazenamento.')),300000).unref();}
const attempts=new Map(),hourlyLimit=Number(process.env.HOURLY_REQUEST_LIMIT||30);
if(!Number.isInteger(hourlyLimit)||hourlyLimit<1||hourlyLimit>1000)throw Error('HOURLY_REQUEST_LIMIT inválido');
async function jsonFile(path,value){const temp=path+'.'+randomUUID()+'.tmp';await writeFile(temp,JSON.stringify(value),{mode:0o600,flag:'wx'});await rename(temp,path);}
const server=http.createServer(async(req,res)=>{
 const answer=(code,data)=>{res.writeHead(code,{'Content-Type':'application/json','Cache-Control':'no-store'});res.end(JSON.stringify(data));};
 try{const incoming=req.headers.authorization||'',wanted='Bearer '+token;if(incoming.length!==wanted.length||!timingSafeEqual(Buffer.from(incoming),Buffer.from(wanted)))return answer(401,{error:'Não autorizado'});
 if(req.method!=='POST')return answer(405,{error:'Método não permitido'});let size=0,chunks=[];for await(const c of req){size+=c.length;if(size>16000000)return answer(413,{error:'Pacote grande demais'});chunks.push(c);}const body=JSON.parse(Buffer.concat(chunks).toString('utf8'));
 if(req.url==='/v1/simulations'){
  if(!key)return answer(503,{error:'Serviço de imagens temporariamente indisponível'});if(generating)return answer(429,{error:'Outra geração está em andamento'});
  const project=validate(body.project);if(typeof body.photoBase64!=='string'||body.photoBase64.length>14000000)return answer(400,{error:'Foto inválida'});const photo=Buffer.from(body.photoBase64,'base64');if(photo.length<16||photo[0]!==255||photo[1]!==216)return answer(400,{error:'Use a foto JPEG normalizada pelo app'});
  const window=Math.floor(Date.now()/3600000);for(const w of attempts.keys())if(w!==window)attempts.delete(w);if((attempts.get(window)||0)>=hourlyLimit)return answer(429,{error:'Limite temporário de geração atingido'});attempts.set(window,(attempts.get(window)||0)+1);
  generating=true;try{const form=new FormData();form.set('model',process.env.IMAGE_MODEL||'gpt-image-1');form.set('prompt',prompt(project));form.set('image',new Blob([photo],{type:'image/jpeg'}),'local.jpg');form.set('size','auto');form.set('n','1');
   const response=await fetch('https://api.openai.com/v1/images/edits',{method:'POST',headers:{Authorization:'Bearer '+key},body:form,signal:AbortSignal.timeout(150000)});if(!response.ok)return answer(502,{error:'Provedor não concluiu a imagem'});const data=await response.json(),base64=data.data?.[0]?.b64_json;if(!base64)return answer(502,{error:'Resposta sem imagem'});const image=Buffer.from(base64,'base64');if(image.length>12000000)return answer(502,{error:'Imagem excede limite'});const id=randomUUID();if(generationOnly)return answer(200,{simulationId:id,imageBase64:base64,providerRequestId:response.headers.get('x-request-id')});const folder=join(root,'jobs',id);await mkdir(folder);await writeFile(join(folder,'original.jpg'),photo,{mode:0o600});await writeFile(join(folder,'simulation.png'),image,{mode:0o600});await jsonFile(join(folder,'project.json'),{schemaVersion:1,simulationId:id,clientCode:client,project,createdUtc:new Date().toISOString(),originalSha256:hash(photo),simulationSha256:hash(image),financialEffect:'NONE'});return answer(200,{simulationId:id,imageBase64:base64});
  }finally{generating=false;}
 }
 if(req.url==='/v1/projects'){
  if(generationOnly)return answer(409,{error:'Envio ao PDV não está ativado nesta etapa'});
  if(typeof body.simulationId!=='string'||!/^[0-9a-f-]{36}$/i.test(body.simulationId))return answer(400,{error:'Referência inválida'});const folder=join(root,'jobs',body.simulationId);const packet=JSON.parse(await readFile(join(folder,'project.json'),'utf8'));if(packet.clientCode!==client) return answer(403,{error:'Cliente diferente'});const target=join(inbox,client,body.simulationId);try{await access(join(target,'ready.json'));return answer(200,{receiptId:body.simulationId,status:'AWAITING_PDV_IMPORT'});}catch{}
  await mkdir(target,{recursive:true});const original=await readFile(join(folder,'original.jpg')),image=await readFile(join(folder,'simulation.png'));if(hash(original)!==packet.originalSha256||hash(image)!==packet.simulationSha256)throw Error('Hash divergente');await writeFile(join(target,'original.jpg'),original,{mode:0o600});await writeFile(join(target,'simulation.png'),image,{mode:0o600});await jsonFile(join(target,'project.json'),packet);await jsonFile(join(target,'ready.json'),{receiptId:body.simulationId,status:'AWAITING_PDV_IMPORT',receivedUtc:new Date().toISOString()});return answer(200,{receiptId:body.simulationId,status:'AWAITING_PDV_IMPORT'});
 }
 answer(404,{error:'Rota inexistente'});
 }catch{answer(400,{error:'Não foi possível processar a solicitação'});}
});server.requestTimeout=170000;server.headersTimeout=20000;server.listen(Number(process.env.PORT||8787),process.env.HOST||(generationOnly?'0.0.0.0':'127.0.0.1'),()=>console.log('Gateway iniciado; geração protegida e sem conteúdo sensível nos logs.'));
process.on('SIGTERM',()=>{server.close(()=>process.exit(0));setTimeout(()=>process.exit(0),9000).unref();});
