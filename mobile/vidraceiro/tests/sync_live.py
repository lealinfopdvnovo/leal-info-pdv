import subprocess,json,os,urllib.request
pkg='br.com.lealinfo.vidraceiro.profissional022'
def shell(*args):return subprocess.check_output(['adb',*args]).decode()
def phase(name):
 out=shell('shell','am','instrument','-w','-e','phase',name,pkg+'.test/br.com.lealinfo.vidraceiro.MeasureFlowInstrumentation')
 print(out)
 assert 'PASS PROFESSIONAL_'+name.upper() in out,out
phase('syncIdentity')
identity=json.loads(shell('shell','run-as',pkg,'cat','files/sync-identity.json'))
aud='angra-sync-ci:'+identity['deviceId']
req=urllib.request.Request(os.environ['ACTIONS_ID_TOKEN_REQUEST_URL']+'&audience='+aud,headers={'Authorization':'Bearer '+os.environ['ACTIONS_ID_TOKEN_REQUEST_TOKEN']})
with urllib.request.urlopen(req) as r:token=json.load(r)['value']
req=urllib.request.Request('https://angra-vidros-gateway.classyglobe9.chatgpt.site/sync/ci-grant',data=json.dumps({'deviceId':identity['deviceId'],'role':'app'}).encode(),headers={'Authorization':'Bearer '+token,'Content-Type':'application/json'})
with urllib.request.urlopen(req) as r:
 grant=json.load(r)
 assert grant['tenant']=='test-'+os.environ['GITHUB_RUN_ID']
phase('syncLive')
shell('shell','am','force-stop',pkg)
state=json.loads(shell('shell','run-as',pkg,'cat','files/pdv-sync-state.json'))
assert any('pending' in link for link in state['links'].values()),'offline outbox lost after actual force-stop'
phase('syncResume')
shell('shell','am','force-stop',pkg)
phase('syncReopen')
