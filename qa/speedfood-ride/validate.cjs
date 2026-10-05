const fs=require('node:fs'),crypto=require('node:crypto'),assert=require('node:assert/strict');
(async()=>{
const {publisher,driver,driverUid}=JSON.parse(fs.readFileSync(process.env.SPEEDFOOD_TEST_TOKEN_FILE,'utf8'));
const base='https://novo-91da7436-default-rtdb.firebaseio.com';
const code='Q'+crypto.randomBytes(12).toString('hex').slice(0,11).toUpperCase(), tracking=crypto.randomBytes(32).toString('hex');
async function request(path,token,method='GET',body){const response=await fetch(base+'/'+path+'.json'+(token?'?auth='+encodeURIComponent(token):''),{method,headers:{'Content-Type':'application/json'},...(body?{body:JSON.stringify(body)}:{})});if(!response.ok)throw Error('Firebase HTTP '+response.status+' em teste de corrida');return response.json()}
await request('pedidos/'+code,publisher,'PUT',{codigo:code,destino:'TESTE FICTÍCIO DE CORRIDA SPEEDFOOD',pdvUid:'Lme0kXlHalc9s6av0I9iJIoWxgJ2',status:'Aguardando motoboy',criadoEm:{'.sv':'timestamp'}});
await request('pedidos/'+code+'/motoboyUid',driver,'PUT',driverUid);
await request('pedidos/'+code,driver,'PATCH',{trackingToken:tracking,status:'Em rota'});
await request('rastreamentos/'+tracking,driver,'PUT',{codigo:code,destino:'TESTE FICTÍCIO DE CORRIDA SPEEDFOOD',latitude:-23,longitude:-44.3,status:'Em rota',criadoEm:{'.sv':'timestamp'},atualizadoEm:{'.sv':'timestamp'},historico:{qa:{latitude:-23,longitude:-44.3}}});
let before=await request('rastreamentos/'+tracking,null);assert.equal(before.codigo,code);
const completion={};for(const [path,value] of Object.entries(JSON.parse(fs.readFileSync('qa/speedfood-ride/completion.json','utf8')))){completion[path.replace('{code}',code).replace('{token}',tracking)]=value;}
await request('',driver,'PATCH',completion);
const order=await request('pedidos/'+code,driver),publicOrder=await request('rastreamentos/'+tracking,null);
assert.equal(order.status,'Entrega concluída');assert.equal(publicOrder.status,order.status);assert.equal(order.trackingToken,tracking);assert.equal(order.motoboyUid,driverUid);assert.equal(order.destino,publicOrder.destino);assert.equal(publicOrder.latitude,before.latitude);assert.equal(publicOrder.longitude,before.longitude);assert.deepEqual(publicOrder.historico,before.historico);
console.log('PASS: payload extraído do APK-fonte finaliza atomicamente a mesma entrega e o acompanhamento público, preservando token, destino, posição e histórico.');
})().catch(e=>{console.error(e.message);process.exitCode=1});
