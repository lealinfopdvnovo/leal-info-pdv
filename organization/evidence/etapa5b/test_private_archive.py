import unittest,tempfile,os
from pathlib import Path
from private_archive import pack,restore
class BackupTests(unittest.TestCase):
 def test_private_roundtrip(self):
  with tempfile.TemporaryDirectory() as td:
   r=Path(td);s=r/'source';s.mkdir();(s/'one.txt').write_text('SYNTHETIC');(s/'db').mkdir();(s/'db/data.bin').write_bytes(b'\0\1\2');k=os.urandom(32);a=r/'private.leal'
   self.assertEqual(pack(s,a,k)['files'],2);self.assertNotIn(b'SYNTHETIC',a.read_bytes());self.assertEqual(a.stat().st_mode&0o777,0o600);self.assertEqual(restore(a,r/'restored',k)['verified_files'],2);self.assertEqual((r/'restored/db/data.bin').read_bytes(),b'\0\1\2')
 def test_tamper_never_extracts(self):
  with tempfile.TemporaryDirectory() as td:
   r=Path(td);s=r/'s';s.mkdir();(s/'one').write_text('fixture');k=os.urandom(32);a=r/'private.leal';pack(s,a,k);b=bytearray(a.read_bytes());b[-1]^=1;a.write_bytes(b)
   with self.assertRaises(Exception):restore(a,r/'restore',k)
   self.assertFalse((r/'restore').exists())
 def test_no_symlink_or_overwrite(self):
  with tempfile.TemporaryDirectory() as td:
   r=Path(td);s=r/'s';s.mkdir();(s/'link').symlink_to('/etc/hosts')
   with self.assertRaises(ValueError):pack(s,r/'archive',os.urandom(32))
if __name__=='__main__':unittest.main()
