"""Validate only the new 001 package against its protected installed updater baseline."""
import json,hashlib,subprocess,re,os
from pathlib import Path
import sys
sys.path.insert(0,str(Path.cwd()))
import scripts.distribute_channels as controller

def read(path):return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def require(value,message):
    if not value:raise RuntimeError(message)
plan=read('release-plan.json');require(len(plan['Entries'])==1,'Only one channel permitted')
entry=plan['Entries'][0];require(entry['Channel']=='001' and entry['Version']=='10.422','Wrong release identity')
candidate=read('release-output/candidate.json')
require(hashlib.sha256((Path('release-output')/entry['Package']).read_bytes()).hexdigest()==candidate['PackageSha256'],'Package bytes changed')
def old_file(path):
    return subprocess.check_output(['git','show',entry['BaselineSource']+':'+path],cwd='candidate').decode('utf-8-sig').replace('\r\n','\n')
def current(path):return (Path('candidate')/path).read_text(encoding='utf-8-sig').replace('\r\n','\n')
for name in ('TransactionalUpdate.ps1','TransactionalUpdate.cs'):
    require(old_file(name)==current(name),'Protected installed updater changed: '+name)
normalize=lambda s:re.sub(r'CurrentVersion\s*=\s*"[\d.]+"','CurrentVersion="VERSION"',s)
require(normalize(old_file('UpdateManager.cs'))==normalize(current('UpdateManager.cs')),'Updater routing/apply path changed')
require('TransactionalUpdate.BeginAsync' in old_file('UpdateManager.cs'),'Installed baseline does not have protected bootstrap')
require(controller.ver(entry['Baseline']['Version'])>=controller.ver('10.416'),'Legacy first transition is not authorized')
results=read('release-output/transaction-results.json')
required={'production-ui-bootstrap-under-protected-program-files','success-and-preservation','bad-package-hash','locked-dll-and-retry','concurrent-startup-lease','staging-tamper','corrupt-backup-fail-closed','native-startup-failure-and-rollback','controlled-startup-production-receipt-synthetic-host'}
required.update('interruption-'+p for p in ('prepared','hidden','copy-LealInfoPDV.deps.json','copy-LealInfoPDV.dll','copy-LealInfoPDV.exe','published'))
required.update('interrupted-'+p for p in ('recover-hidden','recover-LealInfoPDV.deps.json','recover-LealInfoPDV.dll','recover-LealInfoPDV.exe'))
require(required<={r['scenario'] for r in results} and all(r.get('pass') is True for r in results),'Required rollback scenarios did not pass')
for ch in controller.CHANNELS:
    require(controller.readfeed(ch)['sha']==plan['Before'][ch]['sha'],'Channel changed during validation: '+ch)
ui=read('new-order-evidence/fixed.json');require(ui['success'] is True,'Windows UI tests failed')
visual=read('new-order-evidence/ui-visual.json')
require(len(visual['Hashes'])==2 and visual['SourceCommit']==entry['SourceCommit'],'Missing bound UI captures')
Path('release-output/ui-visual.json').write_text(json.dumps(visual,indent=2),encoding='utf-8')
gate=dict(Channel='001',Version=entry['Version'],SourceCommit=entry['SourceCommit'],BuildRunId=os.environ['GITHUB_RUN_ID'],PackageSha256=candidate['PackageSha256'],SafeForPublication=True,ProtectedBaselineVersion=entry['Baseline']['Version'],InstalledUpdaterUnchanged=True,LegacyFirstTransitionApplicable=False,RollbackScenarios=len(results),ActualStoreProfileTouched=False,PhysicalPowerCutTested=False,ActualWindowsRebootTested=False)
Path('release-output/premium-release-gate.json').write_text(json.dumps(gate,indent=2),encoding='utf-8')
print('PASS premium 001 package, protected baseline, real rollback and preserved feeds')
