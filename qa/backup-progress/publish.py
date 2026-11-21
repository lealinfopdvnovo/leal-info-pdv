import json,hashlib,base64,subprocess,os,urllib.request
from pathlib import Path
REPO='lealinfopdvnovo/leal-info-pdv-updates'
CHANNEL='003'
FEED='clients/003/global/version.json'
PIN=('10.467', 'd577f091fa474a3f396dd3c880de0e464102e60fc20f0270c357f5502d53db8e')
def api(path,data=None):
 a=['gh','api',path]
 if data is not None:a+=['--method','POST','--input','-']
 return json.loads(subprocess.check_output(a,input=None if data is None else json.dumps(data).encode()))
def head():return api(f'repos/{REPO}/git/ref/heads/main')['object']['sha']
def tree(h):
 c=api(f'repos/{REPO}/git/commits/{h}');t=api(f'repos/{REPO}/git/trees/{c["tree"]["sha"]}?recursive=1');assert not t.get('truncated')
 return c,{x['path']:x['sha'] for x in t['tree'] if x['type']=='blob'}
h=head();commit,oldtree=tree(h)
o=api(f'repos/{REPO}/contents/{FEED}?ref={h}');m=json.loads(base64.b64decode(o['content']))
assert (m['Version'],m['Sha256'])==PIN,'Live client feed changed; stop publication'
pf=json.loads(Path('backup-output/proof.json').read_text());assert pf['Channel']==CHANNEL and pf['Version']=='10.468' and pf['WindowsProbePassed'] and pf['DependenciesPreserved']
p=Path('backup-output',pf['Package']);assert hashlib.sha256(p.read_bytes()).hexdigest()==pf['Sha256']
tag=f'client-{CHANNEL}-backup-10.468-20261121'
notes='Somente janela de progresso do backup manual. FECHAR oculta a janela sem cancelar a copia. Dados, licenca, personalizacoes e demais canais preservados. Teste no equipamento do proprietario pendente.'
subprocess.run(['gh','release','create',tag,str(p),'--repo',REPO,'--target',h,'--title',f'{CHANNEL} 10.468 — barra de backup','--notes',notes,'--latest=false'],check=True)
url=f'https://github.com/{REPO}/releases/download/{tag}/{p.name}'
# Verify hosted package through authenticated official API, without displaying tokens.
release=api(f'repos/{REPO}/releases/tags/{tag}');asset=next(x for x in release['assets'] if x['name']==p.name)
raw=subprocess.check_output(['gh','api',f'repos/{REPO}/releases/assets/{asset["id"]}','-H','Accept: application/octet-stream'])
assert hashlib.sha256(raw).hexdigest()==pf['Sha256'],'Hosted package differs'
assert head()==h,'Feed advanced; do not overwrite'
m.update(Version='10.468',PackageUrl=url,Sha256=pf['Sha256'],SourceCommit=pf['SourceCommit'],BuildRunId=os.environ['GITHUB_RUN_ID'],ValidationRunId=os.environ['GITHUB_RUN_ID'],PublicationRunId=os.environ['GITHUB_RUN_ID'],PublishedBaselineVersion=PIN[0],Notes=notes,InstalledOnRealComputers=False,PackagesVerifiedBeforeFeed=True)
t=api(f'repos/{REPO}/git/trees',dict(base_tree=commit['tree']['sha'],tree=[dict(path=FEED,mode='100644',type='blob',content=json.dumps(m,ensure_ascii=False,indent=2)+'\n')]))
c=api(f'repos/{REPO}/git/commits',dict(message=f'Backup progress ONLY {CHANNEL}; preserve shared GLOBAL and other clients',tree=t['sha'],parents=[h]))
subprocess.run(['gh','api',f'repos/{REPO}/git/refs/heads/main','--method','PATCH','--input','-'],input=json.dumps(dict(sha=c['sha'],force=False)).encode(),check=True,stdout=subprocess.DEVNULL)
_,after=tree(c['sha']);assert {x for x in set(oldtree)|set(after) if oldtree.get(x)!=after.get(x)}=={FEED}
print('PASS published ONLY',FEED,'10.468',c['sha'])
