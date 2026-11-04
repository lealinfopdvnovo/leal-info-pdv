import base64,hashlib,json,subprocess
from pathlib import Path
REPO='lealinfopdvnovo/leal-info-pdv-updates'
FEED='clients/003/global/version.json'
OLD='client-003-v10.463-selo-original'
NEW='v10.463-casa-selo-original'
NAME='LEAL_INFO_PDV_CASA_V10_462_UPDATE.zip'
SHA='fb329c8520339ec8171946c7b4b2e8bae7036c9d49ed0823ed25e8a122680d24'
def api(p,d=None):
 a=['gh','api',p]
 if d is not None:a+=['--method','PUT','--input','-']
 return json.loads(subprocess.check_output(a,input=None if d is None else json.dumps(d).encode()))
def tree():
 h=api(f'repos/{REPO}/git/ref/heads/main')['object']['sha']
 c=api(f'repos/{REPO}/git/commits/{h}')
 t=api(f'repos/{REPO}/git/trees/{c["tree"]["sha"]}?recursive=1')
 assert not t.get('truncated')
 return {x['path']:x['sha'] for x in t['tree'] if x['type']=='blob'}
before=tree()
o=api(f'repos/{REPO}/contents/{FEED}?ref=main')
m=json.loads(base64.b64decode(o['content']))
assert m['Version']=='10.463' and m['Sha256']==SHA and m['TargetClientCode']=='003'
assert '/'+OLD+'/' in m['PackageUrl']
subprocess.run(['gh','release','download',OLD,'--repo',REPO,'--pattern',NAME,'--dir','original'],check=True)
p=Path('original')/NAME
assert hashlib.sha256(p.read_bytes()).hexdigest()==SHA
subprocess.run(['gh','release','create',NEW,str(p),'--repo',REPO,'--title','Casa 10.463 — selo original, rota compativel','--notes','Mesmo pacote Windows aprovado. Correcao exclusiva da URL do canal Casa.','--latest=false'],check=True)
subprocess.run(['gh','release','download',NEW,'--repo',REPO,'--pattern',NAME,'--dir','verified'],check=True)
assert hashlib.sha256((Path('verified')/NAME).read_bytes()).hexdigest()==SHA
m['PackageUrl']=m['PackageUrl'].replace('/'+OLD+'/', '/'+NEW+'/')
# Reproduce the exact existing Casa manifest compatibility predicate.
from urllib.parse import urlparse
u=urlparse(m['PackageUrl'])
assert u.scheme=='https' and u.hostname=='github.com'
assert u.path.startswith('/lealinfopdvnovo/leal-info-pdv-updates/releases/download/v')
assert m['Component']=='GLOBAL' and m['PetApiVersion']==1 and m['TargetClientCode']=='003'
r=api(f'repos/{REPO}/contents/{FEED}',dict(message='Casa ONLY: corrigir URL do selo para formato aceito pelo atualizador existente',sha=o['sha'],branch='main',content=base64.b64encode((json.dumps(m,ensure_ascii=False,indent=2)+'\n').encode()).decode()))
after=tree()
assert {k for k in set(before)|set(after) if before.get(k)!=after.get(k)}=={FEED}
out=api(f'repos/{REPO}/contents/{FEED}?ref=main')
assert json.loads(base64.b64decode(out['content']))==m
Path('route-proof.json').write_text(json.dumps(dict(Pass=True,PackageBytesUnchanged=True,Sha256=SHA,OnlyChangedFeed=FEED,GlobalRootUnchanged=before['version.json']==after['version.json'],Commit=r['commit']['sha'],PackageUrl=m['PackageUrl']),indent=2))
print('PASS Casa URL compatibility, exact package hash, scoped feed only',r['commit']['sha'])
