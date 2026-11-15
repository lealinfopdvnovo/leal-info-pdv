"""Bounded component comparison; no copies, feeds, runtime or customer data writes."""
from pathlib import Path
import subprocess,json,hashlib,zipfile,xml.etree.ElementTree as ET
SOURCE='24a58c77a4f052d86fc6681a8c7a540f7f052d03'
CANDIDATE='a9713b8c7810d09d667947c6cca869c9bc5c62c0'
QA='2fd998403a5826bc665ee6fd33c6363cd75a2cfe'
COMPILED_SOURCE='98915ae5a9eb02825d77f74bb179e0ce27d65bd9'
CORE=['qa/organization/'+n for n in ['IndependentUpdater.cs','ComponentUpdateCenter.cs.source','ComponentUpdateForm.cs','PetTransactionalUpdate.cs.source','PetTransactionalUpdate.ps1','prepare_integration.py','prepare_current_common.py','prepare_compatibility.py','prepare_finance_compat.py','PreservedPresentation.cs.source','IFinancialMovementsPet.cs.source','prepare_pet_seal.py','ProfileCertificationSeal.cs.source','common_resource_contract.ps1','prepare_pet_seal.py']]
CORE=list(dict.fromkeys(CORE))
CHECKS=['qa/organization/'+n for n in ['ComponentTests.csproj','Program.cs','collector/InstallationInventory.cs','test_pet_transaction.ps1','test_global_transaction.ps1','ProfileProbe.cs','BootstrapProbe.cs','LifecycleProbe.cs']]
def git(*args):return subprocess.check_output(['git',*args])
def data(ref,path):
 p=subprocess.run(['git','show',ref+':'+path],stdout=subprocess.PIPE,stderr=subprocess.PIPE)
 return p.stdout if p.returncode==0 else None
def digest(b):return None if b is None else hashlib.sha256(b).hexdigest()
def main():
 out=Path('etapa5-evidence');out.mkdir(exist_ok=True)
 assert subprocess.run(['git','merge-base','--is-ancestor',SOURCE,CANDIDATE]).returncode==0
 assert subprocess.run(['git','merge-base','--is-ancestor',CANDIDATE,QA]).returncode==0
 records=[]
 for path in CORE+CHECKS:
  a,b,c=[data(ref,path) for ref in [SOURCE,CANDIDATE,QA]]
  assert b is not None and c==b,('37 component missing or changed in approved QA',path)
  records.append(dict(path=path,sha256_34=digest(a),sha256_37=digest(b),sha256_QA=digest(c),classification='GLOBAL infrastructure' if path in CORE else 'QA test dependency',necessaryNow=True,decision='REUSE_QA_ALREADY_PRESENT',originVersion='identical' if a==b else 'superseded_by_37' if a else 'added_in_37',imported=False))
 # Both PRs are controllers. Their compiled application tree is pinned separately.
 for path in ['PetRuntime.cs','PetHost.cs','PetBootstrap.cs','ClientUpdateIdentity.cs','extensions/profiles/Profile.cs','extensions/profiles/001/Pet.csproj']:
  b=data(COMPILED_SOURCE,path);assert b is not None
  records.append(dict(path=path,pinnedSource=COMPILED_SOURCE,sha256=digest(b),classification='PET' if path.startswith('extensions/') else 'GLOBAL infrastructure',necessaryNow=True,decision='REUSE_PINNED_BUILD_DEPENDENCY',imported=False))
 # Verify the three direct compiler dependencies; do not reconstruct the complete PDV.
 project=ET.fromstring(Path('qa/organization/ComponentTests.csproj').read_text())
 for inc in project.findall('.//Compile'):
  assert (Path('qa/organization')/inc.attrib['Include']).is_file()
 feed=json.loads(Path('qa/etapa4-setup/channel/version.json').read_text());assert feed['TargetClientCode']=='005' and feed['Version']=='1.12' and feed['Component']=='PET'
 url=feed['PackageUrl'];assert '/qa/angra-etapa4-setup-20261114/qa/etapa4-setup/channel/' in url
 p=Path('qa/etapa4-setup/channel')/url.rsplit('/',1)[1];raw=p.read_bytes();assert digest(raw)==feed['Sha256']
 with zipfile.ZipFile(p) as z:
  assert set(z.namelist())=={'Extensions/PET/Pet.dll','Extensions/PET/manifest.json'}
  manifest=json.loads(z.read('Extensions/PET/manifest.json'));assert manifest['Identity']=='005' and manifest['Version']=='1.12' and manifest['ApiVersion']==1
  assert digest(z.read('Extensions/PET/Pet.dll'))==manifest['Sha256']
  assert manifest['MinGlobalVersion']==feed['MinGlobalVersion']=='10.468' and manifest['MaxGlobalVersionExclusive']==feed['MaxGlobalVersionExclusive']=='10.469'
  assert all(not n.lower().endswith(('.exe','.ps1','.db','.sqlite')) for n in z.namelist())
 # Existing approved recovery/visual evidence is reused, not represented as a fresh rerun.
 recovery=json.loads(Path('organization/evidence/angra-pet112/apply-recovery.json').read_text());assert recovery['Pass'] and recovery['DataCanaryUnchanged'] and not recovery['CoreRecompiled'] and not recovery['NotifierModified'] and not recovery['LicenseModified']
 scope=json.loads(Path('organization/evidence/angra-pet112/patch-scope.json').read_text());assert scope['Pass'] and scope['AllOriginalPetMethodsIdentical'] and scope['AllOtherOriginalResourcesIdentical']
 # No runtime/profile/feed edits allowed in this step.
 changed=git('diff','--name-only',QA,'HEAD').decode().splitlines()
 allowed=lambda p:p.startswith('qa/etapa5-selective/') or p=='.github/workflows/etapa5_selective_qa.yml' or p.startswith('BAU-DE-OURO/TRANSBORDO-CLIENTES/ETAPA-05/')
 assert all(allowed(p) for p in changed),changed
 all34=git('ls-tree','-r','--name-only',SOURCE).decode().splitlines()
 selected={r['path'] for r in records if 'pinnedSource' not in r}
 result=dict(Pass=True,Source34=SOURCE,Candidate37=CANDIDATE,ApprovedQA37=QA,Source34IsAncestorOf37=True,Candidate37IsAncestorOfApprovedQA=True,Components=records,RuntimeFilesImported=[],RuntimeFilesModified=[],Source34FilesPreserved=len(all34),Source34SelectedFiles=[p for p in all34 if p in selected],DeferredOriginFiles=[p for p in all34 if p not in selected],DeferredReason='Outside Etapa5 scope; preserved in exact source commit, not declared obsolete',ProductionPublished=False,ClientsMigrated=False,Stage6Started=False,ApprovedPetSHA256=feed['Sha256'],ApprovedMediaSHA256=scope['MediaSHA256'],PreviousWindowsRecoveryEvidenceReused=True,NewRuntimeTests='C# selection/staging tests run separately in workflow')
 (out/'comparison.json').write_text(json.dumps(result,indent=2,ensure_ascii=False))
 print('PASS comparison:',len(records),'selected components, zero runtime imports/modifications;',len(all34),'origin files preserved')
 # Prepare a disposable copy with the exact existing QA route transformation for a real-package staging test.
 root=Path('etapa5-qa-fixture');root.mkdir(exist_ok=True)
 text=Path('qa/etapa4-setup/prepare_channel.py').read_text();start=text.index("p=root/'IndependentUpdater.cs'");end=text.index("p=root/'PetTransactionalUpdate.ps1'",start)
 (root/'IndependentUpdater.cs').write_bytes(Path('qa/organization/IndependentUpdater.cs').read_bytes())
 exec(compile(text[start:end],'existing-qa-route-recipe','exec'),dict(root=root))
 for name in ['QaStaging.cs','QaStaging.csproj']:(root/name).write_bytes((Path('qa/etapa5-selective')/name).read_bytes())
if __name__=='__main__':main()
