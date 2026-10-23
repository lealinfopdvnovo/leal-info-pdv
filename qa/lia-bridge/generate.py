import hashlib,json,re,zipfile
from pathlib import Path
lia=Path('controller/lia-candidate')
files={ 'LIC-AI/'+p.relative_to(lia).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(lia.rglob('*')) if p.is_file() and p.suffix.lower() in ('.dll','.exe','.json') }
assert 'LIC-AI/LicAi.dll' in files and 'LIC-AI/LicAi.exe' in files
assert all(re.fullmatch(r'LIC-AI/[A-Za-z0-9_.-]+(?:/[A-Za-z0-9_.-]+)*',n) for n in files)
assert not any(n.lower().endswith(('/settings.json','/license.json','/secrets.json')) for n in files)
worker=Path('controller/qa/lia-bridge/worker.ps1').read_text().replace('__LIA_HASHES__',json.dumps(files,separators=(',',':')))
Path('candidate/TransactionalUpdate.ps1').write_text(worker)
Path('lia-hashes.json').write_text(json.dumps(files,indent=2))
p=Path('candidate/UpdateManager.cs');p.write_text(p.read_text().replace('CurrentVersion="10.453"','CurrentVersion="10.454"'))
p=Path('candidate/LealInfoPDV.csproj');s=p.read_text().replace('10.453.0','10.454.0');p.write_text(s)
