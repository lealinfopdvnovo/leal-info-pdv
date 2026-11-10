import base64,hashlib,json,os,subprocess,zipfile,time,urllib.request
from pathlib import Path
C=os.environ['SEAL_CLIENT']; assert C in ('001','003')
assert C == '001'
V='10.466'
F={'001':'clients/001/version.json','003':'clients/003/global/version.json'}[C]
PIN={'001':('10.465','728b20ff2e38d96deaf538ad45e6297bced845a6460c9a827a17ade3960d5fd6'),'003':('10.462','a9e140e1f02d004dbcc4a37897dfc7f832af1d3148e1aca0d083383fa74c2cfe')}[C]
REPO='lealinfopdvnovo/leal-info-pdv-updates'
def api(path,data=None,method='POST'):
 a=['gh','api',path]
 if data is not None:a+=['--method',method,'--input','-']
 return json.loads(subprocess.check_output(a,input=None if data is None else json.dumps(data).encode()))
def snap():
 h=api(f'repos/{REPO}/git/ref/heads/main')['object']['sha'];c=api(f'repos/{REPO}/git/commits/{h}')
 t=api(f'repos/{REPO}/git/trees/{c["tree"]["sha"]}?recursive=1');assert not t.get('truncated')
 return h,c,{x['path']:x['sha'] for x in t['tree'] if x['type']=='blob'}
def read():
 o=api(f'repos/{REPO}/contents/{F}?ref=main');return json.loads(base64.b64decode(o['content']))
def get(url):
 with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'LEAL-seal-release'}),timeout=90) as r:return r.read()
root=Path('proof/release-output')
logon=json.loads(Path('proof/evidence/logon-windows.json').read_text())
scope=json.loads(Path('proof/evidence/logon-scope.json').read_text())
assert logon['Pass'] and not logon['Errors'] and len(logon['Passed'])==10
assert scope['Pass'] and scope['Channel']=='001' and scope['OnlyMethod']=='MainForm_FormClosing'
if C=='001':
 d=json.loads((root/'001.json').read_text());g=json.loads((root/'001-gate.json').read_text())
 assert d['Channel']==C and g['Pass'] and g['TransactionScenarios']==3
 p=root/d['Package']
else:
 d=json.loads((root/'candidate.json').read_text());g=json.loads((root/'validation-gate.json').read_text())
 assert d['TargetClientCode']==C and g['Pass'] and g['TransactionScenarios']==19
 p=root/('LEAL_INFO_PDV_CASA_V'+V.replace('.','_')+'_UPDATE.zip')
assert d['Version']==V and d['SourceCommit']==os.environ['GITHUB_SHA']
digest=hashlib.sha256(p.read_bytes()).hexdigest();assert digest==d['Sha256']
with zipfile.ZipFile(p) as z:assert set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'} and len(z.namelist())==3
h,old,protected=snap();before=read();assert (before['Version'],before['Sha256'])==PIN
tag=f'client-{C}-v{V}-selo-fundo'
notes='Loja: remove o retangulo escuro do selo, mantendo tamanho, arte e posicao. Preserva LOGON, backup, dados, licenca e personalizacoes. GLOBAL e demais clientes inalterados.'
subprocess.run(['gh','release','create',tag,str(p),'--repo',REPO,'--target',h,'--title',f'{C} {V} — selo aprovado','--notes',notes,'--latest=false'],check=True)
url=f'https://github.com/{REPO}/releases/download/{tag}/{p.name}'
assert hashlib.sha256(get(url)).hexdigest()==digest
assert snap()[0]==h
m=dict(before);m.update(Version=V,PackageUrl=url,Sha256=digest,SourceCommit=os.environ['GITHUB_SHA'],BuildRunId=os.environ['GITHUB_RUN_ID'],ValidationRunId=os.environ['GITHUB_RUN_ID'],PublicationRunId=os.environ['GITHUB_RUN_ID'],PublishedBaselineVersion=before['Version'],Notes=notes,CertificationSeal=True,InstalledOnRealComputers=False,PackagesVerifiedBeforeFeed=True)
t=api(f'repos/{REPO}/git/trees',dict(base_tree=old['tree']['sha'],tree=[dict(path=F,mode='100644',type='blob',content=json.dumps(m,ensure_ascii=False,indent=2)+'\n')]))
c=api(f'repos/{REPO}/git/commits',dict(message=f'Authorized seal ONLY {C} {V}; preserve GLOBAL and other channels',tree=t['sha'],parents=[h]))
api(f'repos/{REPO}/git/refs/heads/main',dict(sha=c['sha'],force=False),'PATCH')
try:
 after=snap()[2];assert {p for p in set(protected)|set(after) if protected.get(p)!=after.get(p)}=={F}
 assert read()==m
 Path('publication-proof.json').write_text(json.dumps(dict(Published=True,Feed=F,Version=V,FeedCommit=c['sha'],SourceCommit=os.environ['GITHUB_SHA'],PackageSha256=digest,GlobalRootUnchanged=after['version.json']==protected['version.json'],OtherChannelsUnchanged=True,InstalledOnCustomer=False),indent=2))
 print('PASS PUBLICATION',C,V,c['sha'])
except Exception:
 if snap()[0]==c['sha']:
  rb=api(f'repos/{REPO}/git/commits',dict(message='Restore seal feed after failed verification',tree=old['tree']['sha'],parents=[c['sha']]))
  api(f'repos/{REPO}/git/refs/heads/main',dict(sha=rb['sha'],force=False),'PATCH')
 raise
