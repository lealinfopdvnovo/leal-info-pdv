from pathlib import Path
import json,zipfile,hashlib
out=Path('candidate-only');dll=(out/'Pet.dll').read_bytes()
with zipfile.ZipFile('qa/etapa4-setup/channel/ANGRA_005_PET_1_12.zip') as z: manifest=json.loads(z.read('Extensions/PET/manifest.json'))
print(manifest)
manifest['Version']='1.13'
manifest['Sha256']=hashlib.sha256(dll).hexdigest()
# Match existing casing; never produce a channel feed.
if 'version' in manifest: manifest['version']='1.13';manifest.pop('Version',None)
(out/'manifest.json').write_text(json.dumps(manifest,indent=2))
with zipfile.ZipFile(out/'ANGRA_005_PET_1_13_CANDIDATE.zip','w',zipfile.ZIP_DEFLATED) as z:
 z.writestr('Extensions/PET/Pet.dll',dll);z.writestr('Extensions/PET/manifest.json',json.dumps(manifest))
(out/'NOT-PUBLISHED.json').write_text(json.dumps(dict(Identity='005',Version='1.13',CandidateOnly=True,FeedChanged=False,Published=False,RealOwnerDeviceTestPending=True,PackageSHA256=hashlib.sha256((out/'ANGRA_005_PET_1_13_CANDIDATE.zip').read_bytes()).hexdigest()),indent=2))
