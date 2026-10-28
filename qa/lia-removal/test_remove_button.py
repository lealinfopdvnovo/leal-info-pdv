import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from remove_button import apply, transform, LISTENER, ENDINGS, known_blocks

ROOT = Path(__file__).resolve().parents[2]
ARCHIVE = ROOT / 'archive/ui/botao-lia'


def original_button():
    script = (ARCHIVE / 'original/aplicar_lic_ai_botao_v10_186.ps1.source').read_text()
    insert = script.split("$insert = @'\n", 1)[1].split("\n'@", 1)[0]
    return insert.split('\n', 1)[1]


class RemovalTests(unittest.TestCase):
    def source(self, ending=0, newline='\n'):
        block = known_blocks()[ending]
        return ('// prefix: manual business navigation remains\n' + LISTENER + '\n'
                + block + '\n// suffix: kitchen, glass, sales remain\n').replace('\n', newline)

    def test_original_archive_sha_and_git_recovery(self):
        import subprocess
        manifest = json.loads((ARCHIVE / 'manifest.json').read_text())
        for item in manifest['files']:
            stored = (ROOT / item['archived']).read_bytes()
            recovered = subprocess.check_output(['git', 'show',
                manifest['source_commit'] + ':' + item['origin']], cwd=ROOT)
            self.assertEqual(stored, recovered)
            self.assertEqual(hashlib.sha256(stored).hexdigest(), item['sha256'])

    def test_three_lifecycle_variants_preserve_surrounding_source(self):
        for i in range(4):
            for newline in ('\n', '\r\n'):
                with self.subTest(lifecycle=i, newline=repr(newline)):
                    source = self.source(i, newline)
                    output, removed = transform(source)
                    self.assertEqual(output, source.replace(removed, '', 1).replace(LISTENER, '', 1))
                    self.assertNotIn('licAiButton', output)
                    self.assertIn('kitchen, glass, sales remain', output)

    def test_unknown_source_rejected(self):
        for source in ('no button', self.source()+self.source(),
                       self.source().replace(ENDINGS[0], 'unknown lifecycle'),
                       self.source()+'\nlicAiButton.Click += Other;'):
            with self.assertRaises(ValueError):
                transform(source)

    def test_shared_code_inside_block_rejected(self):
        for token in ('Database.', 'CashFlow.', 'ShowKitchen', 'ShowGlass',
                      'TransactionalUpdate.', 'PetRuntime.', 'UpdateManager.'):
            with self.subTest(token=token), self.assertRaises(ValueError):
                transform(self.source().replace('double licPhase = 0;',
                          'double licPhase = 0; // '+token))

    def test_requires_disposable_marker_no_mutation(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp)/'MainForm.cs'
            path.write_text(self.source())
            before = path.read_bytes()
            with self.assertRaises(ValueError):
                apply(tmp)
            self.assertEqual(before, path.read_bytes())

    def test_only_main_changes_and_exact_original_is_recoverable(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root/'.lia-removal-isolated').write_text('ISOLATED-NOT-PRODUCTION')
            original = b'\xef\xbb\xbf'+self.source(newline='\r\n').encode()
            (root/'MainForm.cs').write_bytes(original)
            protected = ('LoginForm.cs', 'SplashForm.cs', 'Assets/Lia.mp4',
                         'Licensing/InstallationLicense.cs', 'TransactionalUpdate.cs',
                         'PETs/003/extension.dll', 'app.db', 'settings.json')
            for name in protected:
                p=root/name; p.parent.mkdir(parents=True, exist_ok=True)
                p.write_bytes(b'protected-test-content')
            before = {name:(root/name).read_bytes() for name in protected}
            proof = apply(root)
            for name, data in before.items():
                self.assertEqual(data, (root/name).read_bytes(), name)
            backup = root/'lia-removal-evidence/MainForm.cs.original.source'
            self.assertEqual(original, backup.read_bytes())
            self.assertTrue((root/'MainForm.cs').read_bytes().startswith(b'\xef\xbb\xbf'))
            self.assertFalse(proof['windows_tested'])
            with self.assertRaises(ValueError):
                apply(root)


if __name__ == '__main__':
    unittest.main(verbosity=2)
