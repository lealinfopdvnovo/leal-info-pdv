import subprocess,json,os,urllib.request,hashlib,base64,tempfile,pathlib,uuid,time
pkg='br.com.lealinfo.vidraceiro.profissional022'
origin='https://angra-vidros-gateway.classyglobe9.chatgpt.site'
def adb(*args):return subprocess.check_output(['adb',*args],stderr=subprocess.STDOUT).decode()
def post(path,body,headers={}):
 req=urllib.request.Request(origin+path,data=json.dumps(body,separators=(',',':')).encode(),headers={'Content-Type':'application/json','User-Agent':'ANGRA-Sync-QA/1.0',**headers})
 with urllib.request.urlopen(req,timeout=45) as r:return json.load(r)
def identity(other):
 with tempfile.TemporaryDirectory() as directory:
  key=pathlib.Path(directory)/'key.pem'
  subprocess.run(['openssl','genpkey','-algorithm','EC','-pkeyopt','ec_paramgen_curve:P-256','-out',str(key)],check=True)
  public=subprocess.check_output(['openssl','pkey','-in',str(key),'-pubout','-outform','DER'])
  private=subprocess.check_output(['openssl','pkcs8','-topk8','-nocrypt','-in',str(key),'-outform','DER'])
  device=hashlib.sha256(public).hexdigest();pub=base64.b64encode(public).decode()
  req=urllib.request.Request(os.environ['ACTIONS_ID_TOKEN_REQUEST_URL']+'&audience=angra-sync-ci:'+device,headers={'Authorization':'Bearer '+os.environ['ACTIONS_ID_TOKEN_REQUEST_TOKEN']})
  with urllib.request.urlopen(req) as r:token=json.load(r)['value']
  grant=post('/sync/ci-grant',{'deviceId':device,'publicKey':pub,'role':'pdv','otherTenant':other},{'Authorization':'Bearer '+token})
  path='/sync/ticket';stamp=str(int(time.time()*1000));nonce=str(uuid.uuid4());raw=b'{}'
  message=('POST\n'+path+'\n'+stamp+'\n'+nonce+'\n'+hashlib.sha256(raw).hexdigest()).encode()
  signature=subprocess.check_output(['openssl','dgst','-sha256','-sign',str(key)],input=message)
  ticket=post(path,{}, {'X-Angra-Device':device,'X-Angra-Time':stamp,'X-Angra-Nonce':nonce,'X-Angra-Signature':base64.b64encode(signature).decode()})
  return {'deviceId':device,'privateKey':base64.b64encode(private).decode(),'tenant':grant['tenant'],'code':ticket['code']}
def phase(name):
 out=adb('shell','am','instrument','-w','-e','phase',name,pkg+'.test/br.com.lealinfo.vidraceiro.MeasureFlowInstrumentation')
 print(out,flush=True)
 assert 'PASS PROFESSIONAL_'+name.upper() in out,out
adb('install','-r','mobile/vidraceiro/app/build/outputs/apk/debug/app-debug.apk')
adb('install','-r','mobile/vidraceiro/app/build/outputs/apk/androidTest/debug/app-debug-androidTest.apk')
# Generate the initial workspace without granting the app direct access: it must really pair by code.
phase('syncIdentity')
fixture={'one':identity(False),'two':identity(True)}
with tempfile.TemporaryDirectory() as d:
 f=pathlib.Path(d)/'unlink-fixture.json';f.write_text(json.dumps(fixture));adb('push',str(f),'/data/local/tmp/unlink-fixture.json');adb('shell','run-as',pkg,'cp','/data/local/tmp/unlink-fixture.json','files/unlink-fixture.json')
phase('syncUnlink')
adb('shell','am','force-stop',pkg)
phase('syncUnlinkRestart')
p=pathlib.Path('mobile/vidraceiro/unlink-evidence');p.mkdir(exist_ok=True)
(p/'PASS.txt').write_text('PASS actual Android confirmation/cancel; offline pending and retry; signed gateway revocation without PDV process; A to B to A tenant isolation; drafts retained; actual process restart; final revoked access. No production customer data.')
adb('shell','run-as',pkg,'rm','files/unlink-fixture.json')
