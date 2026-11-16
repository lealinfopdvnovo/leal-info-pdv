/** QA37 individual accounts. No passwords, sessions or activation secrets are logged. */
export const Accounts=(()=>{
 const encoder=new TextEncoder(), OWNER='001';
 const PERMISSIONS=['create','send','images','history','reports','finance','edit','customers','suppliers','materials','cut','drawing','documents','calculator'];
 const EMPLOYEE=['create','send','images'];const lifetime=8*3600000;
 class Denied extends Error{constructor(status,message){super(message);this.status=status;}}
 const reply=(status,data)=>new Response(JSON.stringify(data),{status,headers:{'Content-Type':'application/json; charset=utf-8','Cache-Control':'no-store'}});
 const hex=b=>Array.from(new Uint8Array(b),v=>v.toString(16).padStart(2,'0')).join('');
 const random=()=>hex(crypto.getRandomValues(new Uint8Array(32)));
 const hash=async s=>hex(await crypto.subtle.digest('SHA-256',encoder.encode(s)));
 const key=t=>'access/v1/'+t+'/directory';
 const cleanId=id=>{if(typeof id!=='string'||!/^[A-Za-z0-9][A-Za-z0-9._-]{1,31}$/.test(id))throw new Denied(400,'Matrícula: use 2 a 32 letras, números, ponto ou hífen.');return id.toUpperCase()==='PROPRIETARIO'?OWNER:id.toUpperCase();};
 const fullName=n=>{if(typeof n!=='string'||n.trim().length<5||n.length>120||n.trim().split(/\s+/).length<2)throw new Denied(400,'Informe o nome completo.');return n.trim();};
 function profile(u){return {uid:u.uid,matricula:u.matricula,name:u.name,role:u.role,permissions:u.role==='owner'?PERMISSIONS:Array.isArray(u.permissions)?u.permissions:u.role==='employee'?EMPLOYEE:[]};}
 function permission(u,p){return !!u&&(u.role==='owner'||profile(u).permissions.includes(p));}
 async function load(env,t){const r=await env.BUCKET.get(key(t));const book=r?await r.json():{version:1,users:{},sessions:{},audit:[]};
  // Upgrade the identifier only; preserve UID, password, revision and active sessions.
  const legacy=book.users.PROPRIETARIO;
  if(legacy){if(book.users[OWNER])throw new Denied(409,'Matrícula 001 já utilizada. Cadastro preservado; contate o suporte.');
   if(legacy.role!=='owner')throw new Denied(409,'Cadastro proprietário incompatível. Nenhum dado foi alterado.');
   legacy.matricula=OWNER;book.users[OWNER]=legacy;delete book.users.PROPRIETARIO;
   for(const session of Object.values(book.sessions))if(session.matricula==='PROPRIETARIO')session.matricula=OWNER;
  }
  return {etag:r?.etag,book};}
 function audit(b,who,action,target){b.audit.push({at:Date.now(),by:who?.uid||'activation',action,target});b.audit=b.audit.slice(-2000);}
 async function edit(env,t,fn){for(let i=0;i<8;i++){const {book,etag}=await load(env,t);const result=await fn(book);const r=await env.BUCKET.put(key(t),JSON.stringify(book),{onlyIf:etag?{etagMatches:etag}:{etagDoesNotMatch:'*'}});if(r)return result;}throw new Denied(409,'Cadastro em atualização. Tente novamente.');}
 async function digestPassword(password,salt){if(typeof password!=='string'||password.length<8||password.length>128)throw new Denied(400,'Senha: use 8 a 128 caracteres.');const k=await crypto.subtle.importKey('raw',encoder.encode(password),'PBKDF2',false,['deriveBits']);return hex(await crypto.subtle.deriveBits({name:'PBKDF2',hash:'SHA-256',salt:encoder.encode(salt),iterations:100000},k,256));}
 function equal(a,b){if(typeof a!=='string'||typeof b!=='string'||a.length!==b.length)return false;let d=0;for(let i=0;i<a.length;i++)d|=a.charCodeAt(i)^b.charCodeAt(i);return d===0;}
 async function authenticated(env,g,device,token){if(typeof token!=='string'||!/^\w{64}$/.test(token))throw new Denied(401,'Entre com sua matrícula e senha.');return fromSessionHash(env,g.tenant,device,await hash(token));}
 async function fromSessionHash(env,t,device,h){const {book}=await load(env,t),s=book.sessions[h],u=s&&book.users[s.matricula];if(!s||s.expires<Date.now()||s.device!==device||!u||u.status!=='active'||s.revision!==u.revision)throw new Denied(401,'Sessão encerrada ou acesso revogado. Entre novamente.');return {...profile(u),sessionHash:h,revision:u.revision,tenant:t};}
 async function session(b,u,device){const token=random(),h=await hash(token);for(const [k,s]of Object.entries(b.sessions))if(s.expires<Date.now()||(s.matricula===u.matricula&&s.device===device))delete b.sessions[k];b.sessions[h]={matricula:u.matricula,device,revision:u.revision,expires:Date.now()+lifetime};return {session:token,user:profile(u),expires:Date.now()+lifetime};}
 function revoke(b,u){u.revision++;for(const [k,s]of Object.entries(b.sessions))if(s.matricula===u.matricula)delete b.sessions[k];}
 function assertCurrent(b,u){const current=b.users[u.matricula],session=b.sessions[u.sessionHash];if(!current||current.status!=='active'||current.revision!==u.revision||!session||session.expires<Date.now())throw new Denied(401,'Sessão encerrada. Entre novamente.');}
 function requireOwner(u){if(u.role!=='owner')throw new Denied(403,'Somente o proprietário pode administrar usuários.');}
 async function api(request,env,g,device,path,body){if(!path.startsWith('/sync/access/'))return null;if(g.role!=='app')throw new Denied(403,'Use o aplicativo vinculado ao PDV.');const action=path.slice('/sync/access/'.length);
  if(action==='bootstrap'){const {book}=await load(env,g.tenant),u=book.users[OWNER];return reply(200,{ownerExists:u?.status==='active',activationReady:!!u&&u.status==='pending'&&u.activationDevice===device&&u.activationExpires>Date.now(),authorizationUrl:new URL(request.url).origin+'/access/owner?device='+device});}
  if(action==='activate'){const id=cleanId(body.matricula),code=typeof body.code==='string'?body.code:'',salt=random();const passwordHash=await digestPassword(body.password,salt);const result=await edit(env,g.tenant,async b=>{const u=b.users[id];if(!u||u.status!=='pending'||u.activationExpires<Date.now()||(u.activationDevice?u.activationDevice!==device:!equal(await hash(code),u.activationHash)))throw new Denied(403,'Ativação inválida, expirada ou já utilizada.');u.status='active';u.salt=salt;u.passwordHash=passwordHash;delete u.activationHash;delete u.activationDevice;delete u.activationExpires;revoke(b,u);audit(b,u,'activate',id);return session(b,u,device);});return reply(200,result);}
  if(action==='login'){const id=cleanId(body.matricula);const result=await edit(env,g.tenant,async b=>{const u=b.users[id];if(!u||u.status!=='active')return {invalid:true};if((u.lockedUntil||0)>Date.now())return {locked:true};let candidate='';try{candidate=await digestPassword(body.password,u.salt);}catch{}if(!equal(candidate,u.passwordHash)){u.failures=(u.failures||0)+1;if(u.failures>=5){u.lockedUntil=Date.now()+900000;u.failures=0;}return {invalid:true};}u.failures=0;u.lockedUntil=0;audit(b,u,'login',id);return session(b,u,device);});if(result.invalid||result.locked)throw new Denied(401,'Credenciais inválidas ou acesso indisponível.');return reply(200,result);}
  const actor=await authenticated(env,g,device,request.headers.get('X-Angra-Session'));
  if(action==='me')return reply(200,{user:profile(actor)});
  if(action==='logout'){await edit(env,g.tenant,b=>{delete b.sessions[actor.sessionHash];audit(b,actor,'logout',actor.matricula);});return reply(200,{revoked:true});}
  if(action==='submissions'){
   if(!permission(actor,'history')&&!permission(actor,'reports'))throw new Denied(403,'Consulta não autorizada.');const rows=[];let cursor;do{const p=await env.BUCKET.list({prefix:'sync/data/'+g.tenant+'/projects/',limit:200,...cursor?{cursor}:{}});for(const x of p.objects){const r=await env.BUCKET.get(x.key),v=await r.json();rows.push({id:v.id,title:v.payload?.title||'',author:v.authorship||v.payload?.authorship||null,lastSentBy:v.lastSentBy||null,at:v.updatedAt,receivedByPdv:!!v.receivedByPdv,officialId:v.officialId||null});}cursor=p.truncated?p.cursor:undefined;}while(cursor&&rows.length<2000);return reply(200,{submissions:rows,truncated:!!cursor});}
  requireOwner(actor);
  if(action==='users'){const {book}=await load(env,g.tenant);return reply(200,{users:Object.values(book.users).filter(u=>u.role!=='owner').map(u=>({...profile(u),status:u.status})),permissions:PERMISSIONS,activationLifetimeHours:24});}
  if(action==='audit'){const {book}=await load(env,g.tenant);return reply(200,{audit:book.audit});}
  if(action==='create'){const id=cleanId(body.matricula),name=fullName(body.name);if(id===OWNER||!['employee','manager'].includes(body.role))throw new Denied(400,'Perfil inválido.');const perms=Array.isArray(body.permissions)?body.permissions:[];if(perms.some(p=>!PERMISSIONS.includes(p)))throw new Denied(400,'Permissão inválida.');const activation=random(),activationHash=await hash(activation);await edit(env,g.tenant,b=>{assertCurrent(b,actor);if(b.users[id])throw new Denied(409,'Esta matrícula já foi utilizada e não pode ser reutilizada.');b.users[id]={uid:crypto.randomUUID(),matricula:id,name,role:body.role,permissions:body.role==='manager'||body.permissionSchema===2?[...new Set(perms)]:EMPLOYEE,status:'pending',revision:1,activationHash,activationExpires:Date.now()+86400000};audit(b,actor,'create',id);});return reply(200,{matricula:id,activationCode:activation,expires:Date.now()+86400000});}
  if(action==='update'){const id=cleanId(body.matricula);if(id===actor.matricula||id===OWNER)throw new Denied(403,'Não é permitido alterar os próprios privilégios.');const result=await edit(env,g.tenant,async b=>{assertCurrent(b,actor);const u=b.users[id];if(!u||u.status==='deleted')throw new Denied(404,'Funcionário não encontrado.');if(!['block','unblock','delete','permissions','activation'].includes(body.operation))throw new Denied(400,'Operação inválida.');let activation;
    if(body.operation==='block')u.status='blocked';
    if(body.operation==='unblock'){if(!u.passwordHash)throw new Denied(409,'Reemita a ativação deste cadastro.');u.status='active';}
    if(body.operation==='delete'){u.status='deleted';delete u.passwordHash;delete u.salt;delete u.activationHash;delete u.activationDevice;delete u.activationExpires;}
    if(body.operation==='permissions'){if(!['employee','manager'].includes(body.role)||!Array.isArray(body.permissions)||body.permissions.some(p=>!PERMISSIONS.includes(p)))throw new Denied(400,'Perfil ou permissões inválidos.');u.role=body.role;u.permissions=u.role==='employee'&&body.permissionSchema!==2?EMPLOYEE:[...new Set(body.permissions)];}
    if(body.operation==='activation'){activation=random();u.status='pending';u.activationHash=await hash(activation);u.activationExpires=Date.now()+86400000;delete u.activationDevice;delete u.passwordHash;delete u.salt;}
    revoke(b,u);audit(b,actor,body.operation,id);return {...profile(u),status:u.status,...activation?{activationCode:activation}:{}};});return reply(200,result);}
  throw new Denied(404,'Operação de acesso inexistente.');
 }
 function author(u){return {uid:u.uid,matricula:u.matricula,name:u.name};}
 function canView(u,record){if(!u)return true;const own=(record.authorship||record.payload?.authorship)?.uid===u.uid;return u.role==='owner'||permission(u,'history')||own;}
 async function writing(env,g,device,actor,entity,current,payload){await fromSessionHash(env,g.tenant,device,actor.sessionHash);if(entity==='projects'){
   if(!permission(actor,current?'edit':'create')&&!(current&&canView(actor,current)&&(current.authorship||current.payload?.authorship)?.uid===actor.uid&&permission(actor,'create')&&current.receivedByPdv!==true))throw new Denied(403,'Criação ou edição não autorizada.');
   if(!canView(actor,current||{authorship:author(actor)}))throw new Denied(403,'Orçamento de outro usuário.');if(!permission(actor,'send'))throw new Denied(403,'Envio ao PDV não autorizado.');
  }else if(!permission(actor,entity==='clients'?'customers':entity))throw new Denied(403,'Cadastro não autorizado.');
 }
 // Owner authority belongs to the workshop account, never to a handset.
 async function ownerEmail(env,t){const {book}=await load(env,t);return book.ownerAccountEmail||env.OWNER_EMAIL||'';}
 const escape=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
 async function ownerPage(request,env){
  const url=new URL(request.url);if(!['/access/owner','/access/admin'].includes(url.pathname))return null;
  const device=url.searchParams.get('device');if(!/^[a-f0-9]{64}$/.test(device||''))throw new Denied(400,'Dispositivo inválido.');
  const read=async id=>{const r=await env.BUCKET.get('sync/grants/'+id);return r&&await r.json();};
  const g=await read(device),parent=g?.role==='app'?await read(g.pdv):g;
  if(!g?.approved||!parent?.approved||parent.role!=='pdv'||(g.expires&&g.expires<Date.now())||(parent.expires&&parent.expires<Date.now())||(g.role==='app'&&(parent.linkEpoch||'')!==(g.linkEpoch||'')))throw new Denied(403,'Conecte primeiro ao PDV ANGRA.');
  const email=(request.headers.get('oai-authenticated-user-email')||'').trim().toLowerCase(),identity=request.headers.get('oai-authenticated-user-id');
  if(!identity)return Response.redirect(url.origin+'/signin-with-chatgpt?return_to='+encodeURIComponent(url.pathname+url.search),302);
  const {book}=await load(env,g.tenant),invite=url.searchParams.get('transfer');
  const accepting=!!invite&&book.transfer&&book.transfer.expires>Date.now()&&equal(await hash(invite),book.transfer.hash)&&email===book.transfer.email;
  if(!email||(!accepting&&email!==(await ownerEmail(env,g.tenant)).trim().toLowerCase()))throw new Denied(403,'Entre com a conta proprietária desta vidraçaria. Nenhum dado foi alterado.');
  const csrf=random(),base=url.pathname+'?device='+device;
  const guideRaw=url.searchParams.get('guide'),guide=guideRaw===null?-1:Math.max(0,Math.min(6,Number(guideRaw)||0));
  const guideTexts=['Proprietário: configure 001 somente se ainda não estiver ativo. Não compartilhe sua senha.','Cadastre matrícula fixa (002 em diante), nome completo e perfil.','Selecione as opções autorizadas. Funcionários e gerentes podem receber permissões individuais; só o proprietário administra acessos.','Após cadastrar, guarde o código único de ativação. Ele aparece uma vez e vale 24 horas.','No APK do funcionário: conecte ao PDV, abra PRIMEIRO ACESSO / ATIVAR e use matrícula e código para criar a própria senha.','Perdeu o código? Localize a pessoa abaixo e use REEMITIR ATIVAÇÃO. O anterior será invalidado; senha e sessões anteriores serão revogadas.','Revise as opções em ALTERAR PERFIL / PERMISSÕES. Use BLOQUEAR para suspender ou EXCLUIR ACESSO para revogar. Dados comerciais permanecem.'];
  const guideBox=guide<0?'<p><a href="'+base+'&guide=0">COMO CADASTRAR FUNCIONÁRIOS</a></p>':'<aside class="guide-box" role="region" aria-label="Tutorial de funcionários"><b>Como cadastrar funcionários • '+(guide+1)+' de 7</b><p>'+guideTexts[guide]+'</p><nav>'+(guide>0?'<a href="'+base+'&guide='+(guide-1)+'">VOLTAR</a> ':'')+(guide<6?'<a href="'+base+'&guide='+(guide+1)+'">PRÓXIMO</a> ':'')+'<a href="'+base+'&guide=0">VER NOVAMENTE</a> <a href="'+base+'">SAIR</a></nav><p><small>Somente orientação: navegar no tutorial não cria contas nem emite códigos.</small></p></aside>';
  const focus=(action,label)=>guide===0&&action==='bootstrap'||guide===1&&action==='create'||guide===5&&label==='REEMITIR ATIVAÇÃO'||guide===6&&label==='ALTERAR PERFIL / PERMISSÕES';
  const form=(action,fields,label)=>'<form method="post"'+(focus(action,label)?' class="guide-focus"':'')+'><input type="hidden" name="csrf" value="'+csrf+'"><input type="hidden" name="action" value="'+action+'">'+fields+'<button>'+label+'</button></form>';
  const input=(name,label,type='text')=>'<label>'+label+'<input required type="'+type+'" name="'+name+'" maxlength="128"></label>';
  const page=content=>{const r=new Response('<!doctype html><html lang="pt-BR"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>ANGRA VIDROS — Administração</title><style>body{background:#081224;color:#e1eff8;font:18px system-ui;max-width:760px;margin:30px auto;padding:20px}input,select,button{display:block;padding:12px;margin:8px 0;max-width:100%;box-sizing:border-box}form{border:1px solid #547087;padding:18px;margin:18px 0}label{display:block}button{cursor:pointer}a{color:#75e4f5}code{overflow-wrap:anywhere}.guide-box{border:1px solid #00daff;border-radius:16px;padding:16px;background:#0a2840;margin:18px 0}.guide-box nav{display:flex;flex-wrap:wrap;gap:14px}.guide-focus{position:relative;outline:3px solid #ff3e46;box-shadow:0 0 16px #ff3e4688;border-radius:28px}.guide-focus::before{content:"➜";color:#00daff;text-shadow:0 0 9px #00daff;position:absolute;right:10px;top:-23px;font-size:30px;pointer-events:none}fieldset{border:1px solid #547087;border-radius:12px;padding:12px}input[type=checkbox]{display:inline-block;padding:0;margin:10px 12px 10px 0;width:24px;height:24px;vertical-align:middle;accent-color:#00b1c9}label{min-height:44px}</style><h1>Administração da vidraçaria</h1><p>'+escape(g.name)+' • QA37</p>'+guideBox+content+'</html>',{headers:{'Content-Type':'text/html; charset=utf-8','Cache-Control':'no-store','Referrer-Policy':'strict-origin','Content-Security-Policy':"default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; frame-ancestors 'none'",'X-Content-Type-Options':'nosniff'}});r.headers.set('Set-Cookie','angra_owner_csrf='+csrf+'; Secure; HttpOnly; SameSite=Strict; Max-Age=600; Path=/access');return r;};
  let notice='';
  if(request.method==='POST'){
   const f=await request.formData(),cookie=(request.headers.get('Cookie')||'').match(/(?:^|; )angra_owner_csrf=([a-f0-9]{64})/);
   if(request.headers.get('Origin')!==url.origin||!cookie||!equal(f.get('csrf'),cookie[1]))throw new Denied(403,'Confirmação inválida.');
   const action=f.get('action');
   if(accepting){
    if(action!=='accept-transfer')throw new Denied(403,'Confirme a transferência.');
    const name=fullName(f.get('name')),salt=random(),passwordHash=await digestPassword(f.get('password'),salt);
    if(f.get('password')!==f.get('confirm'))throw new Denied(400,'As senhas não coincidem.');
    await edit(env,g.tenant,async b=>{if(!b.transfer||b.transfer.expires<Date.now()||b.transfer.email!==email||!equal(await hash(invite),b.transfer.hash))throw new Denied(409,'Transferência expirada ou utilizada.');
     const old=b.users[OWNER];if(old)b.previousOwners=[...(b.previousOwners||[]),{uid:old.uid,name:old.name,account:b.ownerAccountEmail||env.OWNER_EMAIL,ended:Date.now()}];
     b.users[OWNER]={uid:crypto.randomUUID(),matricula:OWNER,name,role:'owner',permissions:PERMISSIONS,status:'active',revision:(old?.revision||0)+1,salt,passwordHash};b.ownerAccountEmail=email;b.ownerAccountId=identity;b.sessions={};delete b.transfer;audit(b,b.users[OWNER],'owner-transferred',OWNER);
    });return page('<h2>Transferência concluída</h2><p>O proprietário anterior perdeu o acesso administrativo. Dados, funcionários e autoria foram preservados. Entre no aplicativo com 001 e sua nova senha.</p><a href="'+base+'">GERENCIAR FUNCIONÁRIOS</a>');
   }
   if(invite)throw new Denied(403,'Transferência inválida ou expirada.');
   if(action==='bootstrap'){
    const salt=random(),name=fullName(f.get('name')),passwordHash=await digestPassword(f.get('password'),salt);if(f.get('password')!==f.get('confirm'))throw new Denied(400,'As senhas não coincidem.');
    await edit(env,g.tenant,b=>{if(b.users[OWNER]?.status==='active')throw new Denied(409,'Proprietário já ativo.');b.ownerAccountEmail=email;b.ownerAccountId=identity;b.users[OWNER]={uid:crypto.randomUUID(),matricula:OWNER,name,role:'owner',permissions:PERMISSIONS,status:'active',revision:(b.users[OWNER]?.revision||0)+1,salt,passwordHash};b.sessions={};audit(b,b.users[OWNER],'owner-created',OWNER);});
    notice='<p>Proprietário cadastrado. Use 001 e sua senha no aplicativo, em qualquer aparelho conectado e autorizado.</p>';
   }else if(!action&&url.pathname==='/access/owner'){
    // Compatibility with APK 0.4.0: identity is verified above; only this owner can activate.
    await edit(env,g.tenant,b=>{if(b.users[OWNER]?.status==='active')throw new Denied(409,'Proprietário já ativo.');b.ownerAccountEmail=email;b.ownerAccountId=identity;b.users[OWNER]={uid:crypto.randomUUID(),matricula:OWNER,name:'Proprietário ANGRA VIDROS',role:'owner',status:'pending',revision:1,activationDevice:device,activationExpires:Date.now()+600000};});return page('<p>Ativação autorizada. Volte ao aplicativo e ative 001.</p>');
   }else{
    if(book.users[OWNER]?.status!=='active')throw new Denied(403,'Configure primeiro o proprietário.');
    if(action==='transfer'){
     const target=String(f.get('email')||'').trim().toLowerCase();if(!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(target)||target===email)throw new Denied(400,'Informe o e-mail do novo proprietário.');
     const secret=random();await edit(env,g.tenant,b=>{if((b.ownerAccountEmail||env.OWNER_EMAIL).toLowerCase()!==email)throw new Denied(403,'Proprietário alterado.');b.transfer={email:target,hash:null,expires:Date.now()+86400000};return hash(secret).then(h=>{b.transfer.hash=h;audit(b,b.users[OWNER],'owner-transfer-invited',target);});});
     const link=url.origin+base+'&transfer='+secret;return page('<h2>Convite preparado</h2><p>Entregue este link somente ao novo proprietário ('+escape(target)+'). Válido por 24 horas. Ele precisa entrar na própria conta e confirmar; até lá, seu acesso continua ativo.</p><code>'+escape(link)+'</code><p>O histórico comercial será preservado.</p><a href="'+base+'">VOLTAR</a>');
    }
    if(action==='cancel-transfer'){await edit(env,g.tenant,b=>{if((b.ownerAccountEmail||env.OWNER_EMAIL).toLowerCase()!==email)throw new Denied(403,'Proprietário alterado.');delete b.transfer;});notice='<p>Convite cancelado.</p>';}
    else{
     // Run the exact same user administration rules as the app, with a server-only ephemeral session.
     let ephemeral;await edit(env,g.tenant,async b=>{if((b.ownerAccountEmail||env.OWNER_EMAIL).toLowerCase()!==email)throw new Denied(403,'Proprietário alterado.');ephemeral=await session(b,b.users[OWNER],device);});
     try{const body={matricula:f.get('matricula'),name:f.get('name'),role:f.get('role'),permissions:f.getAll('permissions'),permissionSchema:2,operation:f.get('operation')};
      if(!['create','update'].includes(action))throw new Denied(400,'Operação inválida.');
      const r=await api(new Request(url,{headers:{'X-Angra-Session':ephemeral.session}}),env,{...g,role:'app'},device,'/sync/access/'+action,body),result=await r.json();
      notice=result.activationCode?'<h2>Ativação individual</h2><p>Matrícula: '+escape(result.matricula)+'. Entregue o código somente à pessoa cadastrada. Uso único, 24 horas.</p><code>'+escape(result.activationCode)+'</code>':'<p>Cadastro atualizado; sessões anteriores foram revogadas.</p>';
     }finally{await edit(env,g.tenant,async b=>{delete b.sessions[await hash(ephemeral.session)];});}
    }
   }
  }else if(request.method!=='GET')throw new Denied(405,'Método não permitido.');
  if(accepting)return page('<h2>Assumir a propriedade</h2><p>Ao confirmar, você assume esta vidraçaria e o antigo proprietário perde o acesso. Os dados e a autoria são preservados.</p>'+form('accept-transfer',input('name','Seu nome completo')+input('password','Nova senha (mínimo 8 caracteres)','password')+input('confirm','Confirme a senha','password'),'CONFIRMAR TRANSFERÊNCIA'));
  if(invite)throw new Denied(403,'Transferência inválida ou expirada.');
  const fresh=(await load(env,g.tenant)).book;
  if(fresh.users[OWNER]?.status!=='active')return page(form('bootstrap',input('name','Nome completo do proprietário')+input('password','Senha (mínimo 8 caracteres)','password')+input('confirm','Confirme a senha','password'),'CONFIGURAR PROPRIETÁRIO'));
  let rows='';for(const u of Object.values(fresh.users).filter(u=>u.role!=='owner')){
   rows+='<h3>'+escape(u.matricula)+' • '+escape(u.name)+' • '+escape(u.status)+'</h3>';
   for(const [op,label]of [['block','BLOQUEAR'],['unblock','DESBLOQUEAR'],['activation','REEMITIR ATIVAÇÃO'],['delete','EXCLUIR ACESSO']])if(u.status!=='deleted')rows+=form('update','<input type="hidden" name="matricula" value="'+escape(u.matricula)+'"><input type="hidden" name="operation" value="'+op+'">',label);
  }
  const roles='<label>Perfil<select name="role"><option value="employee">Funcionário</option><option value="manager">Gerente com permissões selecionadas</option></select></label>';
  const permissions='<fieldset'+(guide===2?' class="guide-focus"':'')+'><legend>Permissões individuais</legend>'+PERMISSIONS.map(p=>'<label><input type="checkbox" name="permissions" value="'+p+'"'+(EMPLOYEE.includes(p)?' checked':'')+'>'+escape(p)+'</label>').join('')+'</fieldset>';
  return page(notice+'<h2>001 — Proprietário</h2><p>Entre no aplicativo com a matrícula 001 e sua senha atual. A matrícula 001 é reservada ao proprietário.</p><h2>Equipe</h2><p>Use 002 em diante para funcionários ou gerentes. Funcionários ativam apenas a matrícula criada pelo proprietário. Não podem cadastrar pessoas nem assumir propriedade.</p>'+form('create',input('matricula','Matrícula fixa')+input('name','Nome completo')+roles+permissions,'CADASTRAR PESSOA')+form('update',input('matricula','Matrícula existente')+'<input type="hidden" name="operation" value="permissions">'+roles+permissions,'ALTERAR PERFIL / PERMISSÕES')+rows+'<h2>Entrega ao proprietário definitivo</h2>'+form('transfer',input('email','E-mail da conta do novo proprietário','email'),'PREPARAR TRANSFERÊNCIA')+(fresh.transfer?form('cancel-transfer','','CANCELAR TRANSFERÊNCIA PENDENTE'):'')+'<p>A matrícula 001 — Proprietário funciona em outro aparelho autorizado. Nenhum celular é o proprietário da empresa.</p>');
 }
 async function capAllowed(env,g){if(!g.userSessionHash)return false;try{const u=await fromSessionHash(env,g.tenant,g.device,g.userSessionHash);return (permission(u,'send')||(g.mode!=='put'&&permission(u,'history')))&&(!g.authorUid||u.role==='owner'||permission(u,'history')||u.uid===g.authorUid);}catch{return false;}}
 return {Error:Denied,api,ownerPage,ownerEmail,authenticated,permission,profile,author,canView,writing,capAllowed,PERMISSIONS};
})();
