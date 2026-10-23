import hashlib,io,json,os,re,subprocess,urllib.request,zipfile
from pathlib import Path
channel=os.environ['CHANNEL'];root=Path('candidate');assets=Path('controller/qa/global-453/assets')
BASES={'GLOBAL':'98915ae5a9eb02825d77f74bb179e0ce27d65bd9','CASA':'874bc1e77fc3fb9b643ed17ceb0e1e1c91ad2ac8','001':'7aa5d38336e94659c07f0afc6ddfe40169c41a3a','002':'735f3f6b0f933f25585c0c9e7e50551dae9a056d','004':'2787ea2541261bd68cf1e931ceaa6ed537ddc832'}
PINS={'GLOBAL':('10.445','4f8b2d79d7b533bce9004994f1d186192cbd0e668c650f000c47c10666bbf3f2'),'CASA':('10.446','7019c715477d5b972f6fe75f4fd457fe0babd524bf3d737056c3e900a8c54ba7'),'001':('10.452','d3deeefb63aa41f54b4a355daaa52b3f15ed2f6efe38281842896b8c2cdce6e1'),'002':('10.445','9843d15cee801b5ceef69cd092979a3575605b91cecf44348b4fbb5c5112a239'),'004':('10.445','e256a4992cf2b7de801a54b5d1a2f2314cc17f88c8682d8be61852c858f1217f')}
feedpath='version.json' if channel=='GLOBAL' else ('clients/003/global/version.json' if channel=='CASA' else f'clients/{channel}/version.json')
def get(url):
 with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'LEAL-GLOBAL-453'}),timeout=90) as r:return r.read()
assert subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD'],text=True).strip()==BASES[channel]
feed=json.loads(get('https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/'+feedpath));assert (feed['Version'],feed['Sha256'])==PINS[channel]
data=get(feed['PackageUrl']);assert hashlib.sha256(data).hexdigest()==feed['Sha256'];z=zipfile.ZipFile(io.BytesIO(data));Path('legacy').mkdir(exist_ok=True)
for n in ('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'):Path('legacy',n).write_bytes(z.read(n))
deps=json.loads(Path('legacy/LealInfoPDV.deps.json').read_text(encoding='utf-8-sig'));sdk=next(k.split('/')[1] for k in deps['libraries'] if k.startswith('runtimepack.Microsoft.Windows.SDK.NET.Ref/'));Path('sdk-version.txt').write_text(sdk)
Path('baseline.json').write_text(json.dumps(feed));Path('feed-path.txt').write_text(feedpath)
# Apply reviewed features only; keep every customer's business and authentication implementation.
for n in ('Auth.cs','Database.cs','Licensing/InstallationLicense.cs'):
 p=root/n
 if p.exists():
  original=subprocess.check_output(['git','-C',str(root),'show','HEAD:'+n]);assert p.read_bytes()==original
p=root/'UpdateManager.cs';s=p.read_text(encoding='utf-8-sig');s=re.sub(r'CurrentVersion="[^"]+"','CurrentVersion="10.453"',s)
if 'NotifierRoute =>' not in s:s=s.replace(' private const string DistributionChannel=', ' internal static string NotifierRoute => '+('"COMMON";' if channel in ('GLOBAL','CASA') else '"CLIENT";')+'\n private const string DistributionChannel=',1)
if 'private static bool checking;' not in s:
 a=s.index(' public static async Task CheckForUpdatesAsync');b=s.index(' private static DialogResult ConfirmUpdate',a);snippet=(assets/'update-notification-method.txt').read_text();s=s[:a]+snippet+s[b:]
if channel in ('002','004'):
 a=s.index(' private static async Task DownloadAndApplyAsync');s=s[:a]+(assets/'download-protected.txt').read_text()+'\n}\n'
p.write_text(s)
for n in ('UpdateNotifierHost.cs','MainForm.Notificador.cs','UpdateNotificationForm.cs'):(root/n).write_bytes((assets/n).read_bytes())
p=root/'MainForm.cs';s=p.read_text(encoding='utf-8-sig');s=s.replace('public sealed class MainForm :','public sealed partial class MainForm :')
if '--central-atualizacoes' not in s:s=s.replace('            _ = UpdateManager.CheckForUpdatesAsync(this, true);','            if (Environment.GetCommandLineArgs().Contains("--central-atualizacoes"))\n                _ = UpdateManager.ShowUpdateCenterAsync(this);\n            else _ = UpdateManager.CheckForUpdatesAsync(this, true);',1)
s=s.replace('            OpenFirstAccessTutorial(true);','            // Tutorial disponível sob demanda em Ajuda.').replace('if (automatic && GetSetting("first_access_tutorial_completed", "0") == "1") return;','if (automatic) return;')
if 'bool isRedAction' not in s:
 a=s.index('    private void AddTool(');b=s.index('            using var glow =',a);part=s[a:b];needle='bool isSales = normalizedText.Replace("\\n", " ") == "TELA DE VENDAS";';assert needle in part;part=part.replace(needle,needle+'\n        bool isRedAction = isSales || normalizedText == "SAIR";').replace(': isSales',': isRedAction');s=s[:a]+part+s[b:]
assert 'OpenFirstAccessTutorial(true);' not in s and 'AddMenu("Tutorial de Primeiro Acesso"' in s;p.write_text(s)
p=root/'LoginForm.cs';s=p.read_text(encoding='utf-8-sig')
if 'btnSairLogin' not in s:
 s=s.replace('        Controls.Add(stage);','        Controls.Add(stage);\n'+(assets/'login-button.txt').read_text(),1)
 s=s.replace('            accent.BringToFront();','            accent.BringToFront();\n            int exitWidth=Math.Min(Math.Max(1,sw-32),(int)(150*DeviceDpi/96f));\n            exit.SetBounds(Math.Max(0,sw-exitWidth-16),16,exitWidth,(int)(42*DeviceDpi/96f));\n            exit.BringToFront();',1)
 s=s.replace('        cinematic.Tick+=(_,_)=>','        FormClosed+=(_,_)=>{cinematic.Stop();cinematic.Dispose();};\n        cinematic.Tick+=(_,_)=>',1)
 s=s.replace('    async Task SpeakLoginAssistAsync',(assets/'login-methods.txt').read_text()+'    async Task SpeakLoginAssistAsync',1)
assert 'btnSairLogin' in s and 'internal void ExitLogin()' in s;p.write_text(s)
p=root/'SplashForm.cs';s=p.read_text(encoding='utf-8-sig')
if 'protected override bool ProcessCmdKey' not in s:s=s.replace('    private void LayoutSplash()',(assets/'splash-method.txt').read_text()+'    private void LayoutSplash()',1)
p.write_text(s)
if channel in ('002','004'):
 for n in ('TransactionalUpdate.cs','TransactionalUpdate.ps1'):(root/n).write_bytes((assets/n).read_bytes())
 p=root/'Program.cs';s=p.read_text(encoding='utf-8-sig').replace('            Network.NetworkConfiguration.Load();','            TransactionalUpdate.BeforeStartup();\n            Network.NetworkConfiguration.Load();',1).replace('            Database.Initialize();','            Database.Initialize();\n            TransactionalUpdate.ReportReady();',1);p.write_text(s)
p=root/'LealInfoPDV.csproj';s=p.read_text(encoding='utf-8-sig');s=re.sub(r'<(Version|AssemblyVersion|FileVersion)>[^<]+</\1>',lambda m:f'<{m[1]}>10.453.'+('0' if m[1]=='Version' else '0.0')+f'</{m[1]}>',s)
if 'NotifierPayload' not in s:s=s.replace('</Project>','  <ItemGroup Condition="Exists(\'$(NotifierPayload)\')"><EmbeddedResource Include="$(NotifierPayload)" LogicalName="LealInfoPDV.UpdateNotifier.Native" /></ItemGroup>\n</Project>')
if channel in ('002','004'):s=s.replace('</Project>','  <ItemGroup><EmbeddedResource Include="TransactionalUpdate.ps1" LogicalName="TransactionalUpdateWorker" /></ItemGroup>\n</Project>')
p.write_text(s);(root/'global.json').write_text('{"sdk":{"version":"10.0.401","rollForward":"disable"}}')
q=root/'qa/global453';q.mkdir(parents=True,exist_ok=True)
for n in ('Program.cs','Tests.csproj','LoginExitTests.cs'):(q/n).write_bytes((assets/n).read_bytes())
# Tests run only in the disposable runner profile, with each licensed update route.
p=q/'Program.cs';s=p.read_text().replace('  var fixtures=new[]{','  if(!File.Exists(Database.DbPath))Database.Initialize();\n  if(Auth.UserCount()==0)Auth.CreateUser("QA","qa","qa-test-123!","ADMINISTRADOR","qa@example.invalid","");\n  string qaIdentity=Environment.GetEnvironmentVariable("CHANNEL") is "GLOBAL" or "CASA" ? "003" : Environment.GetEnvironmentVariable("CHANNEL")!;\n  string expectedRoute=qaIdentity=="003" ? "COMMON" : "CLIENT";\n  var fixtures=new[]{').replace('new object?[]{app,"001",Path.Combine(root,"agent")}','new object?[]{app,qaIdentity,Path.Combine(root,"agent")}').replace('File.ReadAllText(config).Contains("Route=CLIENT") && File.ReadAllText(config).Contains("Identity=001")','File.ReadAllText(config).Contains("Route="+expectedRoute) && File.ReadAllText(config).Contains("Identity="+qaIdentity)');p.write_text(s)
# Package-only rollback uses each actual worker and channel, never the customer's Windows.
script=(assets/'rollback.ps1').read_text().replace('Channel 001','Channel '+('GLOBAL' if channel in ('GLOBAL','CASA') else channel)).replace('Version 10.452','Version 10.453');(root/'qa/global453/rollback.ps1').write_text(script)
allowed={'MainForm.cs','UpdateManager.cs','LealInfoPDV.csproj','LoginForm.cs','SplashForm.cs','UpdateNotificationForm.cs','UpdateNotifierHost.cs','MainForm.Notificador.cs','TransactionalUpdate.cs','TransactionalUpdate.ps1','global.json'}
if channel in ('002','004'):allowed.add('Program.cs')
changed=subprocess.check_output(['git','-C',str(root),'diff','--name-only'],text=True).splitlines();assert set(changed)<=allowed,changed
print('PASS',channel,'published package SHA and dependency baseline verified; business/authentication/license files retained; approved login/tutorial/notifier changes only')
