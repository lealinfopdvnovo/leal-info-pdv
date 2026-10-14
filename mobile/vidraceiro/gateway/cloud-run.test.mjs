import {test} from 'node:test';import assert from 'node:assert/strict';import {spawn} from 'node:child_process';import {mkdtemp,readdir,rm} from 'node:fs/promises';import {tmpdir} from 'node:os';import {join} from 'node:path';import {randomUUID} from 'node:crypto';
test('Cloud Run mode: PORT, no PDV/files, auth and honest unavailable IA',{timeout:15000},async()=>{
 const cwd=await mkdtemp(join(tmpdir(),'angra-cloud-'));const port=19000+Math.floor(Math.random()*20000),token=randomUUID()+randomUUID();
 const child=spawn(process.execPath,[new URL('./server.mjs',import.meta.url).pathname],{cwd,env:{PATH:process.env.PATH,PORT:String(port),HOST:'0.0.0.0',GENERATION_ONLY:'true',GATEWAY_TOKEN:token,OPENAI_API_KEY:''},stdio:['ignore','pipe','pipe']});
 try{await new Promise((resolve,reject)=>{child.stdout.once('data',resolve);child.once('exit',()=>reject(Error('Gateway encerrou')));});
 const post=(path,body,credential=token)=>fetch(`http://127.0.0.1:${port}${path}`,{method:'POST',headers:{authorization:'Bearer '+credential,'content-type':'application/json'},body:JSON.stringify(body)});
 assert.equal((await post('/v1/simulations',{},'invalid')).status,401);assert.equal((await post('/v1/simulations',{})).status,503);assert.equal((await post('/v1/projects',{simulationId:randomUUID()})).status,409);assert.deepEqual(await readdir(cwd),[]);
 }finally{child.kill('SIGTERM');await new Promise(resolve=>child.once('exit',resolve));await rm(cwd,{recursive:true,force:true});}
});
