import subprocess
base='4d9128cf8a64b487f836af6c12db14d6a7eb7fd3'
def git(*args):return subprocess.check_output(['git',*args]).decode('utf-8').replace('\r\n','\n')
changed=git('diff','--name-only',base,'HEAD').splitlines()
allowed={'LoginForm.cs','PetHost.cs','MainForm.cs','PetRuntime.cs','LealInfoPDV.csproj','.github/workflows/consolidar_global.yml','docs/consolidacao-global.md'}
assert all(p in allowed or p.startswith(('extensions/','qa/consolidation/')) for p in changed),changed
for p in ['Program.cs','Database.cs','Auth.cs','CashFlow.cs','Licensing/InstallationLicense.cs','Licensing/ActivationForm.cs','UpdateManager.cs','TransactionalUpdate.cs','TransactionalUpdate.ps1']:
 assert git('rev-parse','HEAD:'+p)==git('rev-parse',base+':'+p),p
assert all(not p.startswith(('Glass','Angra')) for p in changed),'Domain files belong in profile module'
print('PASS isolated GLOBAL integration; original authentication/database/license/updater/common cash logic unchanged; no publication workflow')

# The only login delta is the already approved shared visual; authentication handlers remain identical.
assert git('show','HEAD:LoginForm.cs')==git('show','3907959a57b7fa032b8a58392f27d4962ab64f54:LoginForm.cs'),'approved login presentation'

import json
provenance=json.loads(open('extensions/business/source-provenance.json',encoding='utf-8').read())
for name in provenance['Files']:
 assert git('show','HEAD:extensions/business/'+name)==git('show',provenance['Source']+':'+name),name
print('PASS all extracted business sources match approved operational source exactly')
