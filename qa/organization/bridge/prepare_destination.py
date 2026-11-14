"""Add the initial-migration handoff to the existing PR37 modular candidate.
No feed/release/installed program is written; only the disposable candidate tree.
10.468 is an UNPUBLISHED candidate with real coordinator/worker changes.
"""
from pathlib import Path
import hashlib,json,re,shutil,xml.etree.ElementTree as ET
root=Path('candidate');source=Path('controller/qa/organization/bridge')
protected=[root/'Auth.cs',root/'Database.cs',root/'Licensing/InstallationLicense.cs']
before={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
origin_sources={'Auth.cs':'233437d2fa7fc4bf181750430073a9d970a58a25aea1766d45d5c6b2e5a9c881','Database.cs':'434653be7d70e5bf4260812ac7c0668fe8bbb805feb80ff9be9ac924c0c62aed','Licensing/InstallationLicense.cs':'53edac7183650ca22759b66235e2583e0d0bd1832b21bd898488ae273d159f6c'}
normalized_origin={name:hashlib.sha256((root/name).read_text(encoding='utf-8-sig').replace('\r\n','\n').encode('utf-8')).hexdigest() for name in origin_sources}
assert normalized_origin==origin_sources,'Origin 10.467 authentication/database/licensing source differs (ignoring only checkout CRLF/BOM)'

profiles={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in (root/'extensions').rglob('*') if p.is_file()}
for a,b in [('InitialMigrationBridge.cs','InitialMigrationBridge.cs'),('InitialMigrationCoordinator.cs.source','InitialMigrationCoordinator.cs'),('JointInitialRecovery.ps1','JointInitialRecovery.ps1')]:shutil.copyfile(source/a,root/b)
p=root/'LealInfoPDV.csproj';tree=ET.parse(p);pr=tree.getroot();items=ET.SubElement(pr,'ItemGroup');ET.SubElement(items,'EmbeddedResource',Include='JointInitialRecovery.ps1',LogicalName='JointInitialRecoveryWorker')
for name in ['Version','AssemblyVersion','FileVersion']:
 for value in pr.iter(name):value.text='10.468.0' if name=='Version' else '10.468.0.0'
tree.write(p,encoding='utf-8',xml_declaration=True)
p=root/'UpdateManager.cs';s=p.read_text(encoding='utf-8-sig');assert 'CurrentVersion="10.463"' in s;s=s.replace('CurrentVersion="10.463"','CurrentVersion="10.468"');p.write_text(s,encoding='utf-8')
p=root/'Program.cs';s=p.read_text(encoding='utf-8-sig')
a='TransactionalUpdate.BeforeStartup(); PetTransactionalUpdate.BeforeStartup();';assert s.count(a)==1;s=s.replace(a,a+' InitialMigrationCoordinator.GuardStartup();')
a='            Database.Initialize();';assert s.count(a)==1;s=s.replace(a,'            InitialMigrationCoordinator.BeforePetLoad();\n'+a)
a='                Application.Run(main);';assert s.count(a)==1
s=s.replace(a,'''                if(InitialMigrationCoordinator.Active){
                    main.Enabled=false;
                    main.Shown+=async (_,_)=>{
                        try{await InitialMigrationCoordinator.CommitAfterLoginAsync(main);}
                        catch(Exception ex){Environment.ExitCode=1;System.Diagnostics.Trace.WriteLine(ex);}
                    };
                }
'''+a)
p.write_text(s,encoding='utf-8')
p=root/'TransactionalUpdate.cs';s=p.read_text(encoding='utf-8-sig');a='    internal static void ReportReady()\n    {';assert s.count(a)==1
s=s.replace(a,a+'\n        if(InitialMigrationCoordinator.Active&&!InitialMigrationCoordinator.ReadyForCoreHealth)return;')
p.write_text(s,encoding='utf-8')
# Reject an incompatible first capsule BEFORE the existing bootstrap backs up or writes.
p=root/'PetBootstrap.cs';s=p.read_text(encoding='utf-8-sig');a='  if(Directory.Exists(target))throw new InvalidDataException';assert s.count(a)==1
s=s.replace(a,'''  using(var compatibility=JsonDocument.Parse(manifestBytes)){
   var m=compatibility.RootElement;
   LealInfo.Components.IndependentUpdater.ValidateCompatibility(UpdateManager.CurrentVersion,m.GetProperty("MinGlobalVersion").GetString(),m.GetProperty("MaxGlobalVersionExclusive").GetString());
  }
'''+a)
p.write_text(s,encoding='utf-8')
assert all(hashlib.sha256(p.read_bytes()).hexdigest()==before[str(p)] for p in protected)
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==sha for p,sha in profiles.items())
Path('pair-evidence').mkdir(exist_ok=True)
Path('pair-evidence/destination-source.json').write_text(json.dumps(dict(CandidateVersion='10.468',SourceBase='PR37 existing modular reconstruction, not an identical historical binary',Origin='10.467',ProtectedSources=before,MatchesPublishedOriginSource='7aa5d38336e94659c07f0afc6ddfe40169c41a3a',NormalizedOriginSourceHashes=normalized_origin,SourceNormalization='UTF8 BOM removed and CRLF to LF only; raw before/after hashes also compared',AllExistingPetSourcesUnchanged=True,NewCode=['InitialMigrationBridge','InitialMigrationCoordinator','JointInitialRecoveryWorker'],ProductionPublished=False),indent=2))
print('PASS existing modular candidate extended to UNPUBLISHED 10.468; authentication/database/license and all PET sources unchanged')
