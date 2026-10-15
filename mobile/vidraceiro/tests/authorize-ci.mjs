import {readFileSync} from 'node:fs';
const {deviceId}=JSON.parse(readFileSync('app/build/reports/network-device.json','utf8'));
if(!/^[a-f0-9]{64}$/.test(deviceId))throw Error('Device id inválido');
const endpoint=new URL(process.env.ACTIONS_ID_TOKEN_REQUEST_URL);if(endpoint.protocol!=='https:')throw Error('OIDC inseguro');endpoint.searchParams.set('audience','angra-vidros-ci:'+deviceId);
const issued=await fetch(endpoint,{headers:{Authorization:'Bearer '+process.env.ACTIONS_ID_TOKEN_REQUEST_TOKEN}});if(!issued.ok)throw Error('Não foi possível obter identidade de teste GitHub');const {value}=await issued.json();
const response=await fetch('https://angra-vidros-gateway.classyglobe9.chatgpt.site/ci/authorize',{method:'POST',headers:{Authorization:'Bearer '+value,'Content-Type':'application/json'},body:JSON.stringify({deviceId})});
if(!response.ok)throw Error('Emulador não autorizado: HTTP '+response.status+' '+await response.text());console.log('PASS CI identity verified and this emulator public key authorized; no credentials included in APK');
