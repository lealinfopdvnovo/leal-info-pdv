import subprocess,json
from pathlib import Path
base='4d9128cf8a64b487f836af6c12db14d6a7eb7fd3'
changed=subprocess.check_output(['git','diff','--name-only',base,'HEAD']).decode().splitlines()
assert all(p.startswith('qa/pet-organization/') or p.startswith('distribution/pets/') or p in {'.github/workflows/preparar_pet_nomes.yml','docs/pets-nomes-v1-1.md'} for p in changed),changed
c=json.loads(Path('distribution/pets/organization.json').read_text())
assert c['GlobalReferenceCommit']==base and c['GlobalReferenceVersion']=='10.443'
assert {p['InternalId']:p['DisplayName'] for p in c['Profiles']}=={'001':'LOJA LEAL INFO','002':'GELAMAC','003':'CASA LEAL INFO','004':'CASA GENILSON'}
assert all(p['PetVersion']=='1.1' for p in c['Profiles'])
print('PASS names/V1.1 preparation only; all GLOBAL source business licensing database updater and existing client code unchanged')
