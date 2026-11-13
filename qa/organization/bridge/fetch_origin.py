"""Obtain already-identified public bytes needed to build/test the NEW bridge.
No inventory collection or 257-file reconstruction is performed here.
"""
from pathlib import Path
import urllib.request,hashlib,io,zipfile
url='https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/client-001-v10.467-pet-display/LEAL_INFO_PDV_001_V10_463_UPDATE.zip'
with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'LEAL-isolated-initial-bridge'}),timeout=90) as r:b=r.read(32*1024*1024+1)
assert hashlib.sha256(b).hexdigest()=='fd979804f5de82c58e1b911a2f0a449e6d8937cae06ef0c0fa39917c48dd161b'
root=Path('legacy-001');root.mkdir(exist_ok=True)
with zipfile.ZipFile(io.BytesIO(b)) as z:
 assert len(z.infolist())==3 and set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'}
 for n in z.namelist():(root/n).write_bytes(z.read(n))
print('PASS pinned 10.467 public origin available for new handoff/rollback tests only')
