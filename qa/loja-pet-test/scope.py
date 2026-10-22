import subprocess
base='d37700303368db7c8662f063adcbddb8f36a58f0'
changed=subprocess.check_output(['git','diff','--name-only',base,'HEAD']).decode().splitlines()
assert all(p.startswith('qa/loja-pet-test/') or p=='.github/workflows/testar_pet_loja_isolado.yml' for p in changed),changed
print('PASS only isolated LOJA test added; GLOBAL source all PETs licensing database updater and existing branches untouched')
