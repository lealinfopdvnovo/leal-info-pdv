"""Reconstitute published Casa10.461 using its pinned recipe and embedded payload.

No rebuild of the voice sidecar, no AI/network API calls, no feeds or releases.
"""
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys
import urllib.request
import zipfile

SHA = 'd0e78ae6e9baef1aad05b4ce4847d156940cd8a9322a7d8b909aa2f0ac1a58fc'
URL = 'https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/v10.461-casa-gemini/LEAL_INFO_PDV_CASA_V10_461_UPDATE.zip'
mode = sys.argv[1]
if mode == 'download':
    req=urllib.request.Request(URL,headers={'User-Agent':'Leal-Lia-Removal-Isolated'})
    with urllib.request.urlopen(req,timeout=90) as r: raw=r.read()
    assert hashlib.sha256(raw).hexdigest()==SHA
    z=zipfile.ZipFile(io.BytesIO(raw))
    assert set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'}
    Path('published-461').mkdir()
    for n in z.namelist(): Path('published-461',n).write_bytes(z.read(n))
    deps=json.loads(Path('published-461/LealInfoPDV.deps.json').read_text(encoding='utf-8-sig'))
    sdk=next(k.split('/')[1] for k in deps['libraries'] if k.startswith('runtimepack.Microsoft.Windows.SDK.NET.Ref/'))
    Path('sdk-version.txt').write_text(sdk)
elif mode == 'payload':
    raw=Path('bundle/casa-gemini.zip').read_bytes()
    z=zipfile.ZipFile(io.BytesIO(raw))
    hashes={n:hashlib.sha256(z.read(n)).hexdigest() for n in z.namelist()}
    assert len(hashes)==len(z.namelist()) and all(not n.startswith('/') and '..' not in n.split('/') for n in hashes)
    Path('bundle-proof.json').write_text(json.dumps(dict(Sha256=hashlib.sha256(raw).hexdigest(),FileHashes=hashes)))
    subprocess.run(['python','controller/qa/gemini-delivery/prepare.py','candidate'],check=True)
    Path('current-casa-provenance.json').write_text(json.dumps(dict(
        PublishedPackageSha256=SHA,RecipeCommit='be7c9adb6ca04b88637eb323d96b344a660d07a5',
        EmbeddedVoicePayloadReused=True,VoiceCodeChanged=False,Published=False),indent=2))
else:
    raise ValueError(mode)
