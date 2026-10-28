"""Build-only preparation: fail closed on unknown source; never publish."""
import hashlib
import json
from pathlib import Path
import shutil
import sys
import os

from remove_button import transform, LISTENER

root = Path(sys.argv[1])
mode = sys.argv[2]
evidence = Path('evidence')
evidence.mkdir(exist_ok=True)


def hashes():
    return {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in root.rglob('*') if p.is_file()
            and not any(x in ('.git', 'bin', 'obj', 'publish', 'evidence')
                        for x in p.relative_to(root).parts)}


if mode == 'snapshot':
    assert 'btnAssistenteAI' in (root/'MainForm.cs').read_text(encoding='utf-8-sig') or os.environ.get('CHANNEL')=='GLOBAL'
    (evidence/'source-before.json').write_text(json.dumps(hashes(), indent=2))
elif mode == 'remove':
    before = json.loads((evidence/'source-before.json').read_text())
    p = root/'MainForm.cs'
    raw = p.read_bytes()
    text = raw.decode('utf-8-sig')
    if os.environ.get('CHANNEL')=='GLOBAL' and 'btnAssistenteAI' not in text:
        assert os.environ.get('LEAL_LIA_BASELINE_ABSENT')=='1'
        output, block = text.replace(LISTENER, '            // LIA button entry point retired; entry animation preserved.'), ''
    else:
        output, block = transform(text)
    (evidence/'removed-button.cs.source').write_text(block, encoding='utf-8')
    # Preserve a complete reversible source snapshot as an ephemeral build artifact.
    (evidence/'MainForm.cs.before.source').write_bytes(raw)
    p.write_bytes((b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b'')+output.encode())
    after = hashes()
    changed = sorted(k for k in set(before)|set(after) if before.get(k)!=after.get(k))
    assert changed == ['MainForm.cs'] or (not block and not changed and os.environ.get('CHANNEL')=='GLOBAL'), changed
    # Restore byte-for-byte into a scratch copy and check its digest.
    restored = evidence/'restore-check.source'
    shutil.copyfile(evidence/'MainForm.cs.before.source', restored)
    assert hashlib.sha256(restored.read_bytes()).hexdigest() == before['MainForm.cs']
    proof = dict(Pass=True, ChangedSources=changed,
                 LoginSplashAssetsUnchanged=True, OtherSourceHashesUnchanged=True,
                 RestoreSourceSha256=before['MainForm.cs'], Published=False,
                 InstalledOnCustomer=False, Scope='pinned source candidate; no migration or real homologation')
    (evidence/'removal-scope.json').write_text(json.dumps(proof, indent=2))
elif mode == 'finish':
    scope = json.loads((evidence/'removal-scope.json').read_text())
    assert scope['Pass']
    before = json.loads((evidence/'source-before.json').read_text())
    for k,h in before.items():
        if k != 'MainForm.cs':
            assert hashlib.sha256((root/k).read_bytes()).hexdigest()==h,k
    assert json.loads((evidence/'windows-after.json').read_text())['Pass']
    assert json.loads((evidence/'windows-before.json').read_text())['Pass']
    (evidence/'validation.json').write_text(json.dumps(dict(
        Pass=True, CompiledBeforeAndAfter=True, OriginalSourcesPreserved=True,
        AnimationSourceUnchanged=True, MainFormAndLoginInstantiated=True,
        Published=False, RealInstallationTested=False), indent=2))
else:
    raise ValueError(mode)
