"""Offline transform of an isolated assembled candidate. Never runs on an installation.

No network, process launch, licence, settings or publishing operations.
Unknown button variants fail closed before any source write.
"""
from pathlib import Path
import hashlib
import json
import re
import sys

START = '        // LIC ASSISTENTE AI: controle criado em runtime; o evento e vinculado explicitamente aqui.'
ENDINGS = (
    '        FormClosed += (_, _) => { licPulseTimer.Stop(); licPulseTimer.Dispose(); };',
    '        FormClosed += (_, _) => { licStatusCts.Cancel(); licStatusCts.Dispose(); licPulseTimer.Stop(); licPulseTimer.Dispose(); };',
    '        FormClosed += (_, _) => { EncerrarLicAiComPdv(); licStatusCts.Cancel(); licStatusCts.Dispose(); licPulseTimer.Stop(); licPulseTimer.Dispose(); };',
)
LISTENER = '            if (Licensing.InstallationLicense.HasLia) StartNavigationListener();'


def known_blocks():
    """Reconstruct only the three byte-known historical generator variants."""
    archive = Path(__file__).resolve().parents[2] / 'archive/ui/botao-lia/original'
    generator = (archive/'aplicar_lic_ai_botao_v10_186.ps1.source').read_text()
    block = generator.split("$insert = @'\n", 1)[1].split("\n'@", 1)[0].split('\n', 1)[1]
    state = (archive/'aplicar_lic_ai_estado_v10_210.ps1.source').read_text()
    updated = block.replace('double licPhase = 0;',
                            'double licPhase = 0;\n        string licAiState = "IDLE";')
    for old, new in re.findall(r"\$t=\$t\.Replace\('([^']*)','([^']*)'\)", state):
        updated = updated.replace(old, new)
    status_insert = state.split("$insert=@'\n", 1)[1].split("\n'@", 1)[0]
    updated = updated.replace('        var licPulseTimer = new System.Windows.Forms.Timer { Interval = 35 };', status_insert)
    lifecycle = (archive/'corrigir_lic_ai_encerrar_com_pdv_v10_223.ps1.source').read_text()
    final_insert = lifecycle.split("$new=@'\n", 1)[1].split("\n'@", 1)[0]
    final = updated.replace(ENDINGS[1], final_insert)
    # Exact additional launcher line from the published Casa10.461 recipe.
    gemini = final.replace('            if (!Licensing.InstallationLicense.HasLia) return;',
        '            if (!Licensing.InstallationLicense.HasLia) return;\n            if (await CasaGeminiDelivery.TryHandleAsync(this)) return;', 1)
    return tuple(value.lstrip('\n') for value in (block, updated, final, gemini))


def transform(text):
    newline = '\r\n' if '\r\n' in text else '\n'
    source = text.replace('\r\n', '\n')
    if source.count(START) != 1:
        raise ValueError('Expected exactly one known LIA button block; source unchanged')
    start = source.index(START)
    ends = [(source.find(e, start), e) for e in ENDINGS]
    ends = [(i, e) for i, e in ends if i >= 0]
    if len(ends) != 1:
        raise ValueError('Unknown or ambiguous LIA lifecycle; source unchanged')
    end, marker = ends[0]
    end += len(marker)
    block = source[start:end]
    if block not in known_blocks():
        raise ValueError('Button differs from archived generators; source unchanged pending review')
    for required in ('Name = "btnAssistenteAI"', 'licAiButton.Click +=',
                     'Controls.Add(licAiButton);', 'PositionLicAiButton();'):
        if required not in block:
            raise ValueError('Incomplete LIA block; source unchanged')
    # Do not swallow commercial code added inside the historical generated block.
    for forbidden in ('Database.', 'CashFlow.', 'ShowKitchen', 'ShowGlass',
                      'UpdateManager.', 'TransactionalUpdate.', 'PetRuntime.'):
        if forbidden in block:
            raise ValueError('Shared/operational code inside LIA block; manual review required')
    output = source[:start] + source[end:]
    if output.count(LISTENER) != 1:
        raise ValueError('Unknown navigation listener wiring; source unchanged')
    output = output.replace(LISTENER, '', 1)
    for token in ('btnAssistenteAI', 'licAiButton', 'licStatusCts',
                  'licPulseTimer', 'EncerrarLicAiComPdv', 'LicAi.exe',
                  'LealInfoPDV.LicAiStatus'):
        if token in output:
            raise ValueError('LIA entry point outside known block: ' + token)
    # Leave navigation methods, licence policy and shared dependencies untouched.
    return output.replace('\n', newline), block.replace('\n', newline)


def apply(root):
    root = Path(root).resolve()
    # Explicit disposable-copy marker prevents accidental operation on Program Files.
    marker = root / '.lia-removal-isolated'
    if not marker.is_file() or marker.read_text().strip() != 'ISOLATED-NOT-PRODUCTION':
        raise ValueError('Disposable candidate marker missing; no changes')
    path = root / 'MainForm.cs'
    original = path.read_bytes()
    bom = original.startswith(b'\xef\xbb\xbf')
    output, block = transform(original.decode('utf-8-sig'))
    encoded = (b'\xef\xbb\xbf' if bom else b'') + output.encode('utf-8')
    evidence = root / 'lia-removal-evidence'
    if evidence.exists():
        raise ValueError('Evidence already exists; refusing to overwrite')
    # All validation above precedes mutation. Only MainForm.cs is edited.
    evidence.mkdir()
    (evidence / 'MainForm.cs.original.source').write_bytes(original)
    (evidence / 'button.cs.source').write_text(block, encoding='utf-8')
    proof = {'before_sha256': hashlib.sha256(original).hexdigest(),
             'after_sha256': hashlib.sha256(encoded).hexdigest(),
             'changed_source_files': ['MainForm.cs'],
             'login_splash_assets_untouched': True,
             'windows_tested': False, 'published': False}
    (evidence / 'proof.json').write_text(json.dumps(proof, indent=2)+'\n')
    temporary = path.with_suffix('.cs.lia-removal.tmp')
    temporary.write_bytes(encoded)
    temporary.replace(path)
    return proof


if __name__ == '__main__':
    if len(sys.argv) != 2:
        raise SystemExit('Usage: remove_button.py DISPOSABLE_ASSEMBLED_CANDIDATE')
    print(json.dumps(apply(sys.argv[1]), indent=2))
