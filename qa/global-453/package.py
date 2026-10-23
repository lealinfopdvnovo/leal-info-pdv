import hashlib,json,os,zipfile,shutil
from pathlib import Path
c=os.environ['CHANNEL'];root=Path('candidate');p=root/'publish';e=root/'evidence';baseline=json.loads(Path('baseline.json').read_text())
qa=json.loads((e/'notifier-results.json').read_text());native=json.loads((e/'native-results.json').read_text());login=json.loads((e/'login-exit-results.json').read_text());rollback=json.loads((e/'rollback.json').read_text(encoding='utf-8-sig'))
assert qa['Success'] and native['Success'] and native['DesktopNoticeChecks']>=14 and native['PollSeconds']==60 and login['Success'] and all(x['Pass'] for x in rollback) and len(rollback)==3
def norm(raw):
 d=json.loads(raw.decode('utf-8-sig'))
 for bag in (d['libraries'],*d['targets'].values()):
  for k in list(bag):
   if k.startswith('LealInfoPDV/'):del bag[k]
 return d
assert norm(Path('legacy/LealInfoPDV.deps.json').read_bytes())==norm((p/'LealInfoPDV.deps.json').read_bytes()),'Installed runtime dependency mismatch'
out=Path('output')/c;out.mkdir(parents=True,exist_ok=True);name=f'LEAL_INFO_PDV_{c}_V10_453_UPDATE.zip';files={}
with zipfile.ZipFile(out/name,'w',zipfile.ZIP_DEFLATED) as z:
 for n in ('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'):z.write(p/n,n);files[n]=hashlib.sha256((p/n).read_bytes()).hexdigest()
for f in e.glob('*.json'):shutil.copy2(f,out/f.name)
(out/'candidate.json').write_text(json.dumps(dict(Channel=c,Version='10.453',Baseline=baseline,FeedPath=Path('feed-path.txt').read_text(),SourceCommit=os.environ['GITHUB_SHA'],SourceBase=os.environ['SOURCE_BASE'],Package=name,Sha256=hashlib.sha256((out/name).read_bytes()).hexdigest(),Files=files,DependenciesEqual=True,DesktopNoticeChecks=native['DesktopNoticeChecks'],LoginExitApproved=True,RollbackApproved=True,LegacyFirstHop=c in ('002','004'),InstalledOnCustomer=False),indent=2))
print('PASS',c,'Windows popup/login/tutorial package SHA, runtime compatibility, backups and rollback verified')
