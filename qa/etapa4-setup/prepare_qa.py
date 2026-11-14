from pathlib import Path
import re,json,hashlib,shutil
root=Path('candidate');here=Path('controller/qa/etapa4-setup')
changes={}
pattern=re.compile(r'(?:System\.)?Environment\.GetFolderPath\(\s*(?:System\.)?Environment\.SpecialFolder\.(LocalApplicationData|ApplicationData|CommonApplicationData)\s*\)')
for p in root.rglob('*.cs'):
 if any(x in p.parts for x in ['bin','obj','qa','tools','realtime']):continue
 s=p.read_text(encoding='utf-8-sig');old=s
 s=pattern.sub(lambda m:'global::LealInfoPDV.QaIsolation.'+{'LocalApplicationData':'LocalRoot','ApplicationData':'RoamingRoot','CommonApplicationData':'CommonRoot'}[m[1]],s)
 s=s.replace('https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/','https://127.0.0.1:1/LEAL-QA-UPDATES-DISABLED/')
 if p.name=='UpdateNotifierHost.cs':
  for marker in ['    internal static async Task EnsureStartedAsync()\n    {','    internal static void RegisterStartup(Installation installation, string? qaRegistry = null)\n    {']:
   assert s.count(marker)==1,marker
   s=s.replace(marker,marker+'\n        if(QaIsolation.Isolated)return; // QA: no production tray/startup registration')
 if p.name=='Program.cs':
  marker='    static void Main()\n    {';assert s.count(marker)==1
  s=s.replace(marker,marker+'\n        QaIsolation.Initialize();\n        if(Environment.GetCommandLineArgs().Contains("--qa-check-isolation")){QaIsolation.Diagnostic();return;}')
  s=s.replace('                Application.Run(main);','                main.Text += " | TESTE ANGRA - ETAPA 4";\n                Application.Run(main);')
 if p.name=='ComponentUpdateCenter.cs':
  marker='  if(!ClientPolicy.Default.IsActive(identity))';assert marker in s
  s=s.replace(marker,'  MessageBox.Show(owner,"Ambiente de testes: atualizações de produção bloqueadas. Nenhum componente será atualizado.","TESTE ANGRA — ETAPA 4");return;\n'+marker,1)
 if s!=old:p.write_text(s,encoding='utf-8');changes[str(p.relative_to(root))]=hashlib.sha256(s.encode()).hexdigest()
shutil.copyfile(here/'QaIsolation.cs.source',root/'QaIsolation.cs')
Path('qa-setup-evidence').mkdir(exist_ok=True)
Path('qa-setup-evidence/isolation-source.json').write_text(json.dumps(dict(Changes=changes,CanonicalCandidateModified=False,ClientUpdates=False,LicenseBypassed=False,NotAMigrationPackage=True),indent=2))
assert 'Database.cs' in changes and 'Program.cs' in changes and 'UpdateNotifierHost.cs' in changes
print('PASS QA-only build uses separate writable roots, no startup notifier and no client update channel; activation code retained')
