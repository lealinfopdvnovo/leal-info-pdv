import subprocess
base='ca4b83c484be8a21c68b963f1fb43dc2c3674ca9'
def git(*a):return subprocess.check_output(['git',*a]).decode('utf-8').replace('\r\n','\n')
allowed={'UpdateManager.cs','PetRuntime.cs','TransactionalUpdate.cs','TransactionalUpdate.ps1','LealInfoPDV.csproj','Program.cs','Licensing/UpdatePayloadPolicy.cs','docs/arquitetura-global-pet.md','.github/workflows/preparar_global_core.yml'}
changed=set(git('diff','--name-only',base,'HEAD').splitlines())
assert all(p in allowed or p.startswith('qa/global-core/') for p in changed),changed
for p in ['MainForm.cs','Database.cs','ProductRegistrationForm.cs','Licensing/InstallationLicense.cs','Licensing/LicenseStore.cs']:
 assert git('rev-parse','HEAD:'+p)==git('rev-parse',base+':'+p),'Business or activation changed: '+p
p=git('show','HEAD:Program.cs');old=git('show',base+':Program.cs')
p=p.replace('        try { TransactionalUpdate.BeforeStartup(); }\n        catch(Exception ex) { Environment.ExitCode=1; System.Diagnostics.Trace.WriteLine(ex); return; }\n','').replace('            PetRuntime.Initialize(Licensing.InstallationLicense.Current?.ClientCode ?? "");\n            TransactionalUpdate.ReportReady();\n','')
assert p==old,'Only startup recovery and optional PET hook allowed'
s=git('show','HEAD:UpdateManager.cs');assert 'DistributionChannel="GLOBAL"' in s and 'Component=="GLOBAL"' in s
assert s.count('https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/clients/')==0
print('PASS neutral GLOBAL only; business data activation and all client channels unchanged; no PET publisher')
