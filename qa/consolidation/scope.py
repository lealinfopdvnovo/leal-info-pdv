import subprocess
base='4d9128cf8a64b487f836af6c12db14d6a7eb7fd3'
def git(*args):return subprocess.check_output(['git',*args]).decode('utf-8').replace('\r\n','\n')
changed=git('diff','--name-only',base,'HEAD').splitlines()
allowed={'PetHost.cs','MainForm.cs','PetRuntime.cs','LealInfoPDV.csproj','.github/workflows/consolidar_global.yml','docs/consolidacao-global.md'}
assert all(p in allowed or p.startswith(('extensions/','qa/consolidation/')) for p in changed),changed
for p in ['Program.cs','Database.cs','Auth.cs','CashFlow.cs','Licensing/InstallationLicense.cs','Licensing/ActivationForm.cs','UpdateManager.cs','TransactionalUpdate.cs','TransactionalUpdate.ps1']:
 assert git('rev-parse','HEAD:'+p)==git('rev-parse',base+':'+p),p
assert all(not p.startswith(('Glass','Angra')) for p in changed),'Domain files belong in profile module'
print('PASS isolated GLOBAL integration; original authentication/database/license/updater/common cash logic unchanged; no publication workflow')
