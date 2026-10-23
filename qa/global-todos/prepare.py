import hashlib,io,json,os,re,subprocess,urllib.request,zipfile
from pathlib import Path
id=os.environ['CHANNEL'];root=Path('candidate');controller=Path('controller')
def get(url):
 with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'LEAL-GLOBAL-445'}),timeout=90) as r:return r.read()
base={'001':'19cd2697fada9e444bef8f23a920aca468abd819','002':'735f3f6b0f933f25585c0c9e7e50551dae9a056d','004':'2787ea2541261bd68cf1e931ceaa6ed537ddc832'}[id]
assert subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD'],text=True).strip()==base
feed=json.loads(get(f'https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/clients/{id}/version.json'))
assert feed['SourceCommit']==base and feed['TargetClientCode']==id,'Baseline changed'
data=get(feed['PackageUrl']);assert hashlib.sha256(data).hexdigest()==feed['Sha256'];archive=zipfile.ZipFile(io.BytesIO(data))
Path('legacy').mkdir(exist_ok=True)
for n in ('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'):
 matches=[p for p in archive.namelist() if p.split('/')[-1]==n];assert len(matches)==1;Path('legacy',n).write_bytes(archive.read(matches[0]))
deps=json.loads(Path('legacy/LealInfoPDV.deps.json').read_text(encoding='utf-8-sig'));sdk=next(k.split('/')[1] for k in deps['libraries'] if k.startswith('runtimepack.Microsoft.Windows.SDK.NET.Ref/'));Path('sdk-version.txt').write_text(sdk)
p=root/'UpdateManager.cs';s=p.read_text(encoding='utf-8-sig');assert f'DistributionChannel="{id}"' in s;s=re.sub(r'CurrentVersion="[^"]+"','CurrentVersion="10.445"',s);a=s.index(' public static async Task CheckForUpdatesAsync');b=s.index(' private static DialogResult ConfirmUpdate',a);s=s[:a]+(controller/'qa/global-todos/notification-method.txt').read_text()+s[b:];p.write_text(s,encoding='utf-8')
p=root/'MainForm.cs';s=p.read_text(encoding='utf-8-sig');a=s.index('    private void AddTool(');b=s.index('            using var glow =',a);part=s[a:b];needle='bool isSales = normalizedText.Replace("\\n", " ") == "TELA DE VENDAS";';assert needle in part;part=part.replace(needle,needle+'\n        bool isRedAction = isSales || normalizedText == "SAIR";').replace(': isSales',': isRedAction');p.write_text(s[:a]+part+s[b:],encoding='utf-8')
p=root/'LealInfoPDV.csproj';s=p.read_text(encoding='utf-8-sig');s=re.sub(r'<(Version|AssemblyVersion|FileVersion)>[^<]+</\1>',lambda m:f'<{m[1]}>10.445.'+('0' if m[1]=='Version' else '0.0')+f'</{m[1]}>',s);p.write_text(s,encoding='utf-8')
(root/'UpdateNotificationForm.cs').write_bytes((controller/'qa/global-todos/UpdateNotificationForm.cs').read_bytes())
q=root/'qa/global-notification';q.mkdir(parents=True,exist_ok=True);(q/'Program.cs').write_bytes((controller/'qa/global-todos/NotificationTests.cs').read_bytes());(q/'Tests.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0-windows10.0.19041.0</TargetFramework><UseWindowsForms>true</UseWindowsForms><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><ProjectReference Include="../../LealInfoPDV.csproj"/></ItemGroup></Project>')
(root/'global.json').write_text('{"sdk":{"version":"10.0.401","rollForward":"disable"}}')
changed=subprocess.check_output(['git','-C',str(root),'diff','--name-only'],text=True).splitlines();assert set(changed)=={'MainForm.cs','UpdateManager.cs','LealInfoPDV.csproj'},changed
Path('baseline.json').write_text(json.dumps(feed,indent=2));print('PASS baseline SHA verified; only GLOBAL notification and exit color changed; all original business/license/data/PET code retained')
