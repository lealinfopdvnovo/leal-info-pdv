"""Loja001 distribution: approved recipe before the QA-only transformations."""
from pathlib import Path
import hashlib,json
source=Path('controller/qa/etapa6-loja/prepare.py').read_text()
marker='# This is the same QA root isolation used in Angra'
assert source.count(marker)==1
exec(compile(source[:source.index(marker)],'approved-Loja-production-recipe','exec'),{})
root=Path('candidate')
assert not (root/'QaIsolation.cs').exists()
for name in ['Program.cs','ComponentUpdateCenter.cs','UpdateNotifierHost.cs','UpdateManager.cs']:
 s=(root/name).read_text(encoding='utf-8-sig')
 assert 'QaIsolation' not in s and 'QA-LOJA-NO-PRODUCTION' not in s and 'nenhuma publicação ou atualização de produção autorizada' not in s,name
assert 'ComponentUpdateCenter.ShowAsync' in (root/'UpdateManager.cs').read_text()
assert 'InitialMigrationCoordinator.BeforePetLoad()' in (root/'Program.cs').read_text()
p=root/'extensions/profiles/001/Pet.csproj';s=p.read_text()
assert 'Include="PetIdentity" Value="001"' in s and 'Include="PetVersion" Value="1.3"' in s
Path('distribution-evidence').mkdir(exist_ok=True)
Path('distribution-evidence/scope.json').write_text(json.dumps(dict(Identity='001',Origin='10.467',Global='10.468',Pet='1.3',QaIsolationRemoved=True,LicenseValidationUnchanged=True,Published=False,PhysicalTestsExecuted=False),indent=2))
print('PASS distribution Loja001: approved recipe without QA-only changes; licensing and joint recovery preserved')
