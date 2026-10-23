import hashlib,io,json,os,subprocess,urllib.request,zipfile
from pathlib import Path
BASE='98915ae5a9eb02825d77f74bb179e0ce27d65bd9'
def get(url):
 with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'Casa-Consulta-QA'}),timeout=90) as r:return r.read()
changed=subprocess.check_output(['git','diff','--name-only',BASE,'HEAD'],text=True).splitlines()
assert all(p in {'MainForm.cs','MainForm.Consulta.cs','ConsultaCentralForm.cs','UpdateManager.cs','LealInfoPDV.csproj','.github/workflows/casa_consulta_446.yml'} or p.startswith('qa/consulta/') for p in changed),changed
old=subprocess.check_output(['git','show',BASE+':UpdateManager.cs'],text=True)
assert Path('UpdateManager.cs').read_text()==old.replace('CurrentVersion="10.445"','CurrentVersion="10.446"'),'Updater behavior must remain identical'
s=Path('MainForm.cs').read_text();a=s.index('            else if (title == "Consulta")');b=s.index('            else if (title == "Movimentação")',a)
assert s[a:b].count('OpenConsultaCentral()')==1 and 'AddMenu(' not in s[a:b]
feed=json.loads(get('https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/version.json'));assert feed['Version']=='10.445' and feed['SourceCommit']==BASE
raw=get(feed['PackageUrl']);assert hashlib.sha256(raw).hexdigest()==feed['Sha256']
with zipfile.ZipFile(io.BytesIO(raw)) as z:olddeps=z.read('LealInfoPDV.deps.json')
def normalize(raw):
 d=json.loads(raw.decode('utf-8-sig'))
 for bag in (d['libraries'],*d['targets'].values()):
  for k in list(bag):
   if k.startswith('LealInfoPDV/'):del bag[k]
 return d
assert normalize(olddeps)==normalize(Path('publish/LealInfoPDV.deps.json').read_bytes()),'Installed runtime compatibility differs'
files={};name='LEAL_INFO_PDV_CASA_V10_446_UPDATE.zip'
with zipfile.ZipFile(name,'w',zipfile.ZIP_DEFLATED) as z:
 for n in ('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'):
  p=Path('publish')/n;z.write(p,n);files[n]=hashlib.sha256(p.read_bytes()).hexdigest()
Path('evidence/candidate.json').write_text(json.dumps(dict(Version='10.446',TargetClientCode='003',SourceCommit=os.environ['GITHUB_SHA'],Package=name,Sha256=hashlib.sha256(Path(name).read_bytes()).hexdigest(),Files=files,InstalledGlobalBaseline=feed,DependenciesEqual=True,Published=False,InstalledOnCustomer=False),indent=2))
print('PASS Consulta-only source scope, installed updater unchanged, exact dependency compatibility, protected common-only package')
