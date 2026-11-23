"""Selective Loja-only QA, on the approved Angra/PR37 reconstruction."""
from pathlib import Path
import runpy,shutil,hashlib,json,re
root=Path('candidate');source=Path('controller/qa/organization')
# Same approved architecture and original 10.467 payload. No other client payload is imported.
for script in ['prepare_current_common.py','prepare_integration.py']:
 if script=='prepare_current_common.py':
  runpy.run_path(str(source/script))
  import subprocess
  subprocess.run(['pwsh','-NoProfile','-File',str(source/'common_resource_contract.ps1'),'-Mode','Extract'],check=True)
 else:runpy.run_path(str(source/script))
runpy.run_path(str(source/'bridge/fetch_origin.py'))
import subprocess
subprocess.run(['pwsh','-NoProfile','-File',str(source/'extract_loja_worker.ps1')],check=True)
# Apply only the Loja seal/profile recipe, leaving every other profile untouched.
s=(source/'prepare_pet_seal.py').read_text()
s=s.replace("[('001','1.2','1.3'),('003','1.1','1.2')]","[('001','1.2','1.3')]")
assert "[('001','1.2','1.3')]" in s
exec(compile(s,'approved-seal-Loja-only','exec'),{})
subprocess.run(['pwsh','-NoProfile','-File','controller/qa/etapa6-loja/extract_loja.ps1'],check=True)
runpy.run_path(str(source/'prepare_compatibility.py'))
Path('logon-probe').mkdir(exist_ok=True)
runpy.run_path(str(source/'preserve_logon.py'))
subprocess.run(['pwsh','-NoProfile','-File',str(source/'bridge/preserve_origin_notifier.ps1')],check=True)
runpy.run_path(str(source/'bridge/prepare_destination.py'))
# This is the same QA root isolation used in Angra, without identity aliasing or its branding.
pattern=re.compile(r'(?:System\.)?Environment\.GetFolderPath\(\s*(?:System\.)?Environment\.SpecialFolder\.(LocalApplicationData|ApplicationData|CommonApplicationData)\s*\)')
changes={}
protected=['Auth.cs','Database.cs','Licensing/InstallationLicense.cs']
license_before=hashlib.sha256((root/'Licensing/InstallationLicense.cs').read_bytes()).hexdigest()
for p in root.rglob('*.cs'):
 if any(v in p.parts for v in ['bin','obj','qa','tools','realtime','extensions']):continue
 s=p.read_text(encoding='utf-8-sig');old=s
 s=pattern.sub(lambda m:'global::LealInfoPDV.QaIsolation.'+{'LocalApplicationData':'LocalRoot','ApplicationData':'RoamingRoot','CommonApplicationData':'CommonRoot'}[m[1]],s)
 s=s.replace('https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/','https://127.0.0.1:1/QA-LOJA-NO-PRODUCTION/')
 if p.name=='UpdateNotifierHost.cs':
  for marker in ['    internal static async Task EnsureStartedAsync()\n    {','    internal static void RegisterStartup(Installation installation, string? qaRegistry = null)\n    {']:
   assert s.count(marker)==1
   s=s.replace(marker,marker+'\n        if(QaIsolation.Isolated)return; // QA: no installed tray/startup changes')
 if p.name=='Program.cs':
  marker='    static void Main()\n    {';assert s.count(marker)==1
  s=s.replace(marker,marker+'\n        QaIsolation.Initialize();\n        if(Environment.GetCommandLineArgs().Contains("--qa-check-isolation")){QaIsolation.Diagnostic();return;}')
 if s!=old:p.write_text(s,encoding='utf-8');changes[str(p.relative_to(root))]=hashlib.sha256(s.encode()).hexdigest()
qa=Path('controller/qa/etapa4-setup/QaIsolation.cs.source').read_text().replace('ANGRA-ETAPA4-20261114','LOJA-ETAPA6-20261123').replace('LEAL INFO PDV - TESTE ANGRA ETAPA4 CORRIGIDO','LOJA-QA-ETAPA6').replace('TESTE ANGRA ETAPA4','LOJA QA ETAPA6').replace('QA ANGRA ETAPA4','QA LOJA ETAPA6')
(root/'QaIsolation.cs').write_text(qa)
# There is no QA feed to poll: exercise selection with local fixtures, never production.
p=root/'ComponentUpdateCenter.cs';s=p.read_text();marker='  if(!ClientPolicy.Default.IsActive(identity))';assert marker in s
s=s.replace(marker,'  if(identity!="001")throw new InvalidDataException("QA exclusivo Loja 001");\n  MessageBox.Show(owner,"Candidata QA da Loja: nenhuma publicação ou atualização de produção autorizada.","ETAPA 6 — LOJA QA");return;\n'+marker,1);p.write_text(s)
# No signed licence identity, plan, serial or verification algorithm is changed.
assert hashlib.sha256((root/'Licensing/InstallationLicense.cs').read_bytes()).hexdigest()==license_before
p=root/'extensions/profiles/001/Pet.csproj';s=p.read_text();assert 'Include="PetIdentity" Value="001"' in s and 'Include="PetVersion" Value="1.3"' in s
Path('etapa6-evidence').mkdir(exist_ok=True)
Path('etapa6-evidence/scope.json').write_text(json.dumps(dict(Identity='001',Origin='10.467',GlobalCandidate='10.468',PetCandidate='1.3',Recipe='same PR37/Angra handoff, manifests, independent workers',QaRoots=True,IsolatedChanges=changes,OtherClientPayloadImported=False,LicenceValidationUnchanged=True,ProductionPublished=False,PhysicalMigrationApproved=False),indent=2))
print('PASS selective Loja 001 QA: existing architecture, original PET preserved, production routes disabled')
