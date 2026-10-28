import hashlib,io,json,os,shutil,subprocess,sys,zipfile
from pathlib import Path
assert os.environ['CHANNEL']=='CASA'
r=Path('candidate');mode=sys.argv[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def sources():return {p.relative_to(r).as_posix():sha(p) for p in r.rglob('*') if p.is_file() and not any(x in ('.git','qa','bin','obj','publish','evidence') for x in p.relative_to(r).parts)}
if mode=='download':
 for run,name,dest in [('40609046605','gemini-live-casa-EXPERIMENTAL-NAO-PUBLICADO','approved-gemini'),('40606501484','casa-gemini-10-459-CANDIDATA-NAO-PUBLICADA','approved-pdv')]:
  subprocess.run(['gh','run','download',run,'--repo','lealinfopdvnovo/leal-info-pdv','--name',name,'--dir',dest],check=True)
 files=list(Path('approved-gemini').rglob('integrity.json'));assert len(files)==1
 index=json.loads(files[0].read_text());assert index['Source']=='47eb0ac79b50b631c1b019e73303fc4d26285f97' and not index['Published']
 src=files[0].parent
 for n,h in index['Files'].items():assert sha(src/n)==h,n
 shutil.copytree(src,'approved-sidecar');Path('approved-sidecar/integrity.json').unlink()
 m=list(Path('approved-pdv').rglob('candidate.json'));assert len(m)==1
 d=json.loads(m[0].read_text());assert d['SourceCommit']=='d295fb3f7739d9ed6362f09b9651cb21e031c8e9' and d['Sha256']=='b8b16ea37f0b64f788317dd19bf34d52ac075262c373399f0275b985c0c3dcf6'
 p=m[0].parent/d['Package'];assert sha(p)==d['Sha256'];z=zipfile.ZipFile(p);assert set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'}
 Path('legacy-458').mkdir();z.extractall('legacy-458');Path('approved-sidecar-index.json').write_text(json.dumps(index))
 print('PASS exact approved Gemini source/artifact file hashes and Casa10.458 package recovered')
elif mode=='sidecar':
 # Derive only the installation locator; original Live audio/protocol implementation is reused unchanged.
 p=Path('qa/gemini-casa/Program.cs');s=p.read_text();old='readonly string installation=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,".."));';assert old in s
 s=s.replace(old,'''readonly string installation=ResolveInstallation();
 static string ResolveInstallation(){var a=Environment.GetCommandLineArgs();if(a.Length!=3||a[1]!="--pdv-root"||!Path.IsPathFullyQualified(a[2]))throw new InvalidOperationException("Abra esta candidata pela seleção de LIA no PDV da Casa.");return Path.GetFullPath(a[2]);}''');s=s.replace('await pipe.ConnectAsync(timeout.Token);using var writer=', 'await pipe.ConnectAsync(timeout.Token);VerifyNavigationPeer(pipe);using var writer=')
 anchor=' async Task<string> ExecuteAsync(string command){'
 assert anchor in s
 method=' [System.Runtime.InteropServices.DllImport("kernel32.dll",SetLastError=true)]\n static extern bool GetNamedPipeServerProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe,out uint pid);\n void VerifyNavigationPeer(NamedPipeClientStream pipe){\n  if(!GetNamedPipeServerProcessId(pipe.SafePipeHandle,out var pid))throw new InvalidOperationException("Não foi possível identificar o PDV receptor.");\n  using var peer=Process.GetProcessById(checked((int)pid));\n  if(!string.Equals(peer.MainModule?.FileName,Path.Combine(installation,"LealInfoPDV.exe"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Receptor de navegação pertence a outra instalação; comando recusado.");\n }\n'
 s=s.replace(anchor,method+anchor);p.write_text(s)
elif mode=='bundle':
 # Keep every runtime/dependency from the approved artifact. Only the sidecar executable, DLL and symbols may change.
 base=Path('approved-sidecar');new=Path('sidecar-built');own={'LiaGeminiCasa.Experimental.exe','LiaGeminiCasa.Experimental.dll','LiaGeminiCasa.Experimental.pdb'}
 for p in base.rglob('*'):
  if p.is_file() and p.relative_to(base).as_posix() not in own:assert sha(p)==sha(new/p.relative_to(base)),str(p)
 for n in own:shutil.copyfile(new/n,base/n)
 hashes={p.relative_to(base).as_posix():sha(p) for p in base.rglob('*') if p.is_file()};assert all(Path(n).suffix.lower() in ('.exe','.dll','.json','.pdb') for n in hashes)
 Path('bundle').mkdir();Path('bundle/hashes.json').write_text(json.dumps(hashes))
 with zipfile.ZipFile('bundle/casa-gemini.zip','w',zipfile.ZIP_DEFLATED) as z:
  for n in sorted(hashes):z.write(base/n,n)
 Path('bundle-proof.json').write_text(json.dumps(dict(ApprovedRun=40609046605,ApprovedSource='47eb0ac79b50b631c1b019e73303fc4d26285f97',DerivedFiles=sorted(own),Sha256=sha(Path('bundle/casa-gemini.zip')),FileHashes=hashes),indent=2))
elif mode=='candidate':
 before=sources();Path('before-delivery.json').write_text(json.dumps(before))
 for n in ('LoginForm.cs','UpdateManager.cs','LealInfoPDV.csproj','qa/login457/Program.cs'):
  p=r/n;p.write_text(p.read_text(encoding='utf-8-sig').replace('10.458','10.460').replace('Minor==458','Minor==460'))
 p=r/'MainForm.cs';s=p.read_text(encoding='utf-8-sig');a=s.index('async void btnAssistenteAI_Click');left=s[:a];right=s[a:];anchor='            if (!Licensing.InstallationLicense.HasLia) return;';assert anchor in right
 right=right.replace(anchor,anchor+'\n            if (await CasaGeminiDelivery.TryHandleAsync(this)) return;',1);p.write_text(left+right)
 for n in ('BundleStore.cs','CasaGeminiDelivery.cs'):shutil.copyfile(Path('controller/qa/gemini-delivery')/n,r/n)
 proof=json.loads(Path('bundle-proof.json').read_text());(r/'CasaGeminiPayload.cs').write_text('namespace LealInfoPDV;\ninternal static class CasaGeminiPayload { internal const string BundleSha="'+proof['Sha256']+'"; internal const string FileHashes="""'+json.dumps(proof['FileHashes'],separators=(',',':'))+'"""; }')
 p=r/'LealInfoPDV.csproj';p.write_text(p.read_text().replace('</Project>','<ItemGroup><EmbeddedResource Include="'+str(Path('bundle/casa-gemini.zip').resolve()).replace('\\','/')+'" LogicalName="Casa.Gemini.Bundle" /></ItemGroup></Project>'))
 for n in ('DeliveryProbe.cs','DeliveryProbe.csproj'):dest=r/'qa/gemini-delivery'/n;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(Path('controller/qa/gemini-delivery')/n,dest)
 after=sources();allowed={'MainForm.cs','LoginForm.cs','UpdateManager.cs','LealInfoPDV.csproj','BundleStore.cs','CasaGeminiDelivery.cs','CasaGeminiPayload.cs'}
 changed={n for n in set(before)|set(after) if before.get(n)!=after.get(n)};assert changed==allowed-{'LoginForm.cs'},changed
 Path('delivery-scope-proof.json').write_text(json.dumps(dict(Target='Casa do Adriano',Changes=sorted(changed),Protected={n:h for n,h in before.items() if n not in changed},UpdaterWorkerUnchanged=True,LicenseUnchanged=True,PetUnchanged=True,Published=False),indent=2))
elif mode=='package':
 proof=json.loads(Path('delivery-scope-proof.json').read_text())
 for n,h in proof['Protected'].items():assert sha(r/n)==h,n
 assert json.loads(Path('evidence/gemini-tests.json').read_text())['Pass']
 assert json.loads(Path('evidence/gemini-delivery-tests.json').read_text())['Pass']
 out=Path('output');out.mkdir(exist_ok=True)
 with zipfile.ZipFile(out/'LEAL_INFO_PDV_CASA_V10_459_UPDATE.zip','w',zipfile.ZIP_DEFLATED) as z:
  for n in ('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'):z.write(r/'publish'/n,n)
 for n in ('delivery-scope-proof.json','bundle-proof.json'):shutil.copyfile(n,out/n)
 for folder in (Path('evidence'),r/'evidence'):
  for p in folder.glob('*.json'):shutil.copyfile(p,out/p.name)
 (out/'candidate.json').write_text(json.dumps(dict(Version='10.460',TargetClientCode='003',Component='GLOBAL',Delivery='Casa scoped compiled package only; common reference unchanged',SourceCommit=os.environ['GITHUB_SHA'],Baseline='10.459',ApprovedGeminiRun=40609046605,Package='LEAL_INFO_PDV_CASA_V10_459_UPDATE.zip',Sha256=sha(out/'LEAL_INFO_PDV_CASA_V10_459_UPDATE.zip'),Published=False,RealApiTested=False),indent=2))
 print('PASS Casa-only candidate packaged; no feed or customer installation changed')
elif mode=='finish':
 assert json.loads((r/'evidence/gemini-delivery-probe.json').read_text())['Pass']
 assert json.loads((r/'evidence/login457-results.json').read_text())['Success']
 assert json.loads((r/'evidence/lia-results.json').read_text())['Success']
 tests=json.loads(Path('release-output/transaction-results.json').read_text(encoding='utf-8-sig'));assert len(tests)>=19 and all(x['pass'] for x in tests)
 proof=json.loads(Path('delivery-scope-proof.json').read_text())
 for n,h in proof['Protected'].items():assert sha(r/n)==h,n
 for p in (r/'evidence').glob('*.json'):shutil.copyfile(p,Path('output')/p.name)
 shutil.copyfile('release-output/transaction-results.json','output/transaction-results.json')
 Path('output/validation-gate.json').write_text(json.dumps(dict(Pass=True,TransactionScenarios=len(tests),Target='Casa do Adriano',OriginalLiaPreserved=True,GlobalReferenceUnchanged=True,UpdaterMechanismUnchanged=True,RealApiTested=False,FreeQuotaConfirmed=False,Published=False,PublicationBlockedUntilAuthorization=True),indent=2))
 print('PASS all automated delivery gates; authenticated Gemini and quota remain external pending checks')
else:raise ValueError(mode)
