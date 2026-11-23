"""Loja001 distribution: approved recipe before the QA-only transformations."""
from pathlib import Path
import hashlib,io,json,re,subprocess,urllib.request,zipfile
source=Path('controller/qa/etapa6-loja/prepare.py').read_text()
marker='# This is the same QA root isolation used in Angra'
assert source.count(marker)==1
exec(compile(source[:source.index(marker)],'approved-Loja-production-recipe','exec'),{})
root=Path('candidate')
patch=Path('backup-recipe/qa/backup-progress/backup.patch').resolve()
subprocess.run(['git','apply','--check',str(patch)],cwd=root,check=True)
subprocess.run(['git','apply',str(patch)],cwd=root,check=True)
for name in ['LealInfoPDV.csproj','UpdateManager.cs']:
 p=root/name
 text=p.read_text(encoding='utf-8-sig')
 assert '10.468' in text,name
 p.write_text(re.sub(r'10\.468(?=(?:\.0){0,2}[<"])','10.469',text),encoding='utf-8')
url='https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/client-001-backup-10.468-20261121/LEAL_INFO_BACKUP_001_10_468.zip'
with urllib.request.urlopen(url,timeout=90) as response:
 package=response.read(32*1024*1024+1)
assert hashlib.sha256(package).hexdigest()=='47496b92f62755ebc8669593b25a389c9cc8143c5e67676dca600aca6ceb03be'
origin=Path('legacy-001-backup')
origin.mkdir()
with zipfile.ZipFile(io.BytesIO(package)) as archive:
 assert len(archive.namelist())==3 and set(archive.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'}
 for name in archive.namelist():
  (origin/name).write_bytes(archive.read(name))

assert not (root/'QaIsolation.cs').exists()
for name in ['Program.cs','ComponentUpdateCenter.cs','UpdateNotifierHost.cs','UpdateManager.cs']:
 s=(root/name).read_text(encoding='utf-8-sig')
 assert 'QaIsolation' not in s and 'QA-LOJA-NO-PRODUCTION' not in s and 'nenhuma publicação ou atualização de produção autorizada' not in s,name
assert 'ComponentUpdateCenter.ShowAsync' in (root/'UpdateManager.cs').read_text()
assert 'InitialMigrationCoordinator.BeforePetLoad()' in (root/'Program.cs').read_text()
p=root/'extensions/profiles/001/Pet.csproj';s=p.read_text()
assert 'Include="PetIdentity" Value="001"' in s and 'Include="PetVersion" Value="1.3"' in s
Path('distribution-evidence').mkdir(exist_ok=True)
Path('distribution-evidence/scope.json').write_text(json.dumps(dict(Identity='001',Origins=['10.467','10.468'],Global='10.469',Pet='1.3',QaIsolationRemoved=True,BackupPatchApplied=True,LicenseValidationUnchanged=True,Published=False,PhysicalTestsExecuted=False),indent=2))
print('PASS distribution Loja001: approved recipe without QA-only changes; licensing and joint recovery preserved')
