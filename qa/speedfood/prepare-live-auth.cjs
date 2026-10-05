const fs = require('node:fs');
const crypto = require('node:crypto');
const path = require('node:path');
const apiKey = 'AIzaSyCnU5br609C2UuUNznxUSq154cbN-yNU78';
const publisherUid = 'Lme0kXlHalc9s6av0I9iJIoWxgJ2';
(async () => {
  if (!process.env.FIREBASE_SERVICE_ACCOUNT) throw new Error('A autenticação de teste do Firebase não está configurada no repositório.');
  const credentials = JSON.parse(process.env.FIREBASE_SERVICE_ACCOUNT);
  if (credentials.project_id !== 'novo-91da7436') throw new Error('A credencial existente pertence a outro projeto. Nenhum pedido foi enviado.');
  const driverUid = 'speedfood-qa-' + process.env.GITHUB_RUN_ID;
  const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
  async function signIn(uid) {
    const now = Math.floor(Date.now() / 1000);
    const unsigned = encode({alg:'RS256',typ:'JWT'}) + '.' + encode({iss:credentials.client_email,sub:credentials.client_email,aud:'https://identitytoolkit.googleapis.com/google.identity.identitytoolkit.v1.IdentityToolkit',iat:now,exp:now+900,uid});
    const signed = unsigned + '.' + crypto.sign('RSA-SHA256',Buffer.from(unsigned),credentials.private_key).toString('base64url');
    const result = await fetch('https://identitytoolkit.googleapis.com/v1/accounts:signInWithCustomToken?key=' + apiKey,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({token:signed,returnSecureToken:true})});
    if (!result.ok) throw new Error('Firebase recusou a autenticação do teste: HTTP ' + result.status);
    const body = await result.json();
    console.log('::add-mask::' + body.idToken);
    return body.idToken;
  }
  const publisher = await signIn(publisherUid);
  const driver = await signIn(driverUid);
  const tokenFile = path.join(process.env.RUNNER_TEMP,'speedfood-test-token.json');
  fs.writeFileSync(tokenFile, JSON.stringify({publisher,driver,driverUid}),{mode:0o600});
  fs.appendFileSync(process.env.GITHUB_ENV,'SPEEDFOOD_TEST_TOKEN_FILE=' + tokenFile + '\n');
  console.log('Autenticação temporária pronta; tokens não são incluídos nos resultados.');
})().catch(error => { console.error(error.message); process.exitCode=1; });
