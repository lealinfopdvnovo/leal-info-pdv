import os,re,subprocess
base={'001':'2cb6f0714884b895d8e574f174353c99840ffebf','003':'209b3b7f67262feb4893304c653a89c1782881ad'}[os.environ['CHANNEL']]
def git(*a):return subprocess.check_output(['git',*a]).decode('utf-8').replace('\r\n','\n')
allowed={'KitchenForm.cs','KitchenOrderPanel.cs','UpdateManager.cs','LealInfoPDV.csproj','qa/kitchen-01-02/Program.cs','qa/kitchen-01-02/CorrectionsTests.csproj','qa/kitchen-01-02/scope.py','.github/workflows/cozinha_01_02_candidata.yml'}
changed=set(git('diff','--name-only',base,'HEAD').splitlines());assert changed<=allowed,changed-allowed
for f in ['UpdateManager.cs','LealInfoPDV.csproj']:
 assert re.sub('10\\.(432|433)','VERSION',git('show','HEAD:'+f))==re.sub('10\\.(432|433)','VERSION',git('show',base+':'+f)),f
print('PASS targeted scope: only two kitchen UI corrections, candidate version and tests; own channel preserved')
