"""Only the disposable QA build receives a QA route. Canonical product sources stay untouched."""
from pathlib import Path
import re,shutil,hashlib,json
root=Path('candidate');here=Path('controller/qa/etapa4-setup')
channel='https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv/qa/angra-etapa4-setup-20261114/qa/etapa4-setup/channel/'
# Restore the existing implementation, restricted to a single QA identity and feed.
p=root/'UpdateNotifierHost.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('        if(QaIsolation.Isolated)return; // QA: no production tray/startup registration','')
s=s.replace('Route={UpdateManager.NotifierRoute}', 'Route=CLIENT')
s=s.replace('if (!ClientUpdateIdentity.IsValidClientCode(identity))','if (identity != "005")')
p.write_text(s,encoding='utf-8')
s=(Path('recipe/tools/UpdateNotifier/notifier.cpp')).read_text(encoding='utf-8-sig')
# Same tray engine, native icon, menu, singleton, desktop notifications and Windows startup.
# No production host/path is reachable in this QA binary.
s=s.replace('/lealinfopdvnovo/leal-info-pdv-updates/main/', '/lealinfopdvnovo/leal-info-pdv/qa/angra-etapa4-setup-20261114/qa/etapa4-setup/channel/')
s=s.replace('S full=L"/lealinfopdvnovo/leal-info-pdv/qa/angra-etapa4-setup-20261114/qa/etapa4-setup/channel/"+path', 'if(path!=L"clients/005/version.json")throw std::runtime_error("QA route rejected");S full=L"/lealinfopdvnovo/leal-info-pdv/qa/angra-etapa4-setup-20261114/qa/etapa4-setup/channel/"+S(L"version.json")')
# Reuse strict client manifest parser; QA packages live only in the QA branch.
s=s.replace('S prefix=L"https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/client-"+id+L"-v";', 'if(id!=L"005")return u;S prefix=L"'+channel+'ANGRA_005_PET_";')
start=s.index('static S InstalledVersion(){');end=s.index('\nstruct Http',start)
s=s[:start]+'''static S InstalledVersion(){S file=app.substr(0,app.find_last_of(L"\\\\/"))+L"\\\\Extensions\\\\PET\\\\manifest.json";std::ifstream in(file.c_str(),std::ios::binary);std::string bytes((std::istreambuf_iterator<char>(in)),std::istreambuf_iterator<char>());if(bytes.empty()||bytes.size()>65536)throw std::runtime_error("QA manifest");int n=MultiByteToWideChar(CP_UTF8,0,bytes.data(),(int)bytes.size(),nullptr,0);S json(n,L'\\0');MultiByteToWideChar(CP_UTF8,0,bytes.data(),(int)bytes.size(),json.data(),n);if(!json.empty()&&json[0]==0xFEFF)json.erase(0,1);auto fields=Json{json}.parse();if(fields[L"Identity"]!=L"005")throw std::runtime_error("QA identity");return fields[L"Version"];}
'''+s[end:]
# Retain old self-test as historical source, but disallow its production path calls by the new Fetch allowlist.
s=s.replace('if((route!=L"CLIENT"&&route!=L"COMMON")||', 'if((route!=L"CLIENT"||identity!=L"005")||')
s=s.replace('LEAL INFO PDV • Atualizações','ANGRA VIDROS • Atualizações QA')
(root/'current-embedded/notifier-qa.cpp').write_text(s,encoding='utf-8-sig')
# Only QA identity may plan/apply updates, and only PET packages in this QA branch are allowed.
p=root/'IndependentUpdater.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('{{"001",true},{"002",true},{"003",true},{"004",false}}','{{"005",true}}')
s=s.replace('uri.Host!="github.com"','uri.Host!="raw.githubusercontent.com"')
s=s.replace('string prefix="/lealinfopdvnovo/leal-info-pdv-updates/releases/download/"+(pet ? "client-"+identity+"-pet-v" : "v");','if(!pet||identity!="005")throw new InvalidDataException("QA accepts only PET 005");\n        string prefix="/lealinfopdvnovo/leal-info-pdv/qa/angra-etapa4-setup-20261114/qa/etapa4-setup/channel/ANGRA_005_PET_";')
p.write_text(s,encoding='utf-8')
p=root/'PetTransactionalUpdate.ps1';s=p.read_text(encoding='utf-8-sig').replace("$Identity -notin @('001','002','003')","$Identity -ne '005'");p.write_text(s,encoding='utf-8')
p=root/'ComponentUpdateCenter.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('  MessageBox.Show(owner,"Ambiente de testes: atualizações de produção bloqueadas. Nenhum componente será atualizado.","TESTE ANGRA — ETAPA 4");return;','  if(identity!="005")throw new InvalidDataException("Canal exclusivo QA ANGRA 005");')
s=re.sub(r'const string Root="[^"]+";', 'const string Root="'+channel+'";',s)
s=s.replace('var global=await Load("version.json",ComponentKind.GLOBAL);','Release? global=null; // QA never fetches GLOBAL')
s=s.replace('Load("clients/"+identity+"/pet/version.json",ComponentKind.PET)', 'Load("version.json",ComponentKind.PET)')
p.write_text(s,encoding='utf-8')
# Preserve the genuine login and remove the prior brand intro, which would otherwise play after the PET intro.
p=root/'Program.cs';s=p.read_text(encoding='utf-8-sig');a=s.index('                    if (Licensing.InstallationLicense.Current?.Plan == "standard")');b=s.index('                    firstAccess = false;',a)
s=s[:a]+'                    using var qaLogin = new LoginForm();loginResult = qaLogin.ShowDialog();\n'+s[b:]
s=s.replace('        QaIsolation.Initialize();','        QaIsolation.Initialize();\n        if(Environment.GetCommandLineArgs().Contains("--qa-start-notifier")){QaIsolation.StartNotifier();return;}')
p.write_text(s,encoding='utf-8')
p=root/'QaIsolation.cs';s=p.read_text(encoding='utf-8-sig');marker=' public static void Diagnostic(){';assert marker in s
s=s.replace(marker,''' public static void StartNotifier(){
  var install=UpdateNotifierHost.Prepare(Path.Combine(AppContext.BaseDirectory,"LealInfoPDV.exe"),"005");
  UpdateNotifierHost.RegisterStartup(install);
  System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(install.Agent){Arguments="--config \\\""+install.Config+"\\\"",UseShellExecute=false,WorkingDirectory=install.Folder});
 }
'''+marker);p.write_text(s,encoding='utf-8')
media=b''.join(p.read_bytes() for p in sorted((here/'media').glob('abertura.part*')))
assert hashlib.sha256(media).hexdigest()=='3841b67fe8285afca01610a72f9b89f6ca69f41be85d92d05246f160458c85f9'
Path('angra-abertura.mp4').write_bytes(media)
print('PASS QA-only 005 notifier/feed/worker configured; original license validation retained; original tray implementation reused')

# Reuse only the transaction scenarios affected by the new QA identity.
test=Path('controller/qa/organization/test_pet_transaction.ps1').read_text(encoding='utf-8-sig')
test=test.replace("foreach($owner in @('001','002','003'))", "foreach($owner in @('005'))")
a=test.index("foreach($case in @(");b=test.index(")){",a)
test=test[:a]+"foreach($case in @('success','bad-hash','wrong-identity','wrong-manifest','tampered-stage','interruption-system-recovery','health-failure'"+test[b:]
Path('qa-005-tests').mkdir(exist_ok=True)
Path('qa-005-tests/test.ps1').write_text(test,encoding='utf-8')
shutil.copyfile(root/'PetTransactionalUpdate.ps1','qa-005-tests/PetTransactionalUpdate.ps1')
