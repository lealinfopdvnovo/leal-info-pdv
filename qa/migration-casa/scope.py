import subprocess
base='599e308d7ee7534914ef9a0225096f5440955cc8'
def git(*a):return subprocess.check_output(['git',*a]).decode('utf-8').replace('\r\n','\n')
files=git('diff','--name-only',base,'HEAD').splitlines()
allowed={'PetBootstrap.cs','PetRuntime.cs','LealInfoPDV.csproj','UpdateManager.cs','.github/workflows/migracao_casa_isolada.yml','docs/migracao-casa.md'}
assert all(p in allowed or p.startswith('qa/migration-casa/') for p in files),files
before=git('show',base+':UpdateManager.cs').replace('CurrentVersion="10.443"','CurrentVersion="10.444"')
after=git('show','HEAD:UpdateManager.cs')
start=' private static async Task<UpdateManifest?> LoadManifestAsync(HttpClient'
end=' private static bool IsValidManifest'
assert before[:before.index(start)]==after[:after.index(start)] and before[before.index(end):]==after[after.index(end):],'only neutral scoped selection may change; download/payload/recovery policy stays identical' 
assert git('show','HEAD:LealInfoPDV.csproj')==git('show',base+':LealInfoPDV.csproj').replace('10.443.0','10.444.0')
for p in ['Program.cs','Database.cs','Auth.cs','Licensing/InstallationLicense.cs','CashFlow.cs','TransactionalUpdate.cs','TransactionalUpdate.ps1','MainForm.cs','extensions/profiles/003/Presentation.cs']:
 assert git('rev-parse','HEAD:'+p)==git('rev-parse',base+':'+p),p
print('PASS migration scope isolated; no published channel changes; business/data/license and existing transactional worker unchanged')
