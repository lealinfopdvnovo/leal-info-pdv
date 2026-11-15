"""Private, bounded backup primitive. No default source paths or key storage.
Caller supplies an ephemeral 32-byte key from private storage; never log it.
Segments up to 256 MiB plaintext. This is not an operational database snapshot.
"""
from pathlib import Path,PurePosixPath
import io,json,hashlib,os,zipfile
from cryptography.hazmat.primitives.ciphers.aead import AESGCM
MAGIC=b'LEAL34QA1';LIMIT=256*1024*1024

def safe(name):
 p=PurePosixPath(name)
 if p.is_absolute() or '..' in p.parts or str(p)!=name or '\\' in name or ':' in name or '\x00' in name:raise ValueError('unsafe path')
 return name

def pack(source,destination,key):
 source=Path(source).resolve();destination=Path(destination)
 if len(key)!=32:raise ValueError('private 256-bit key required')
 if source in destination.resolve().parents:raise ValueError('backup must be outside source')
 manifest={};memory=io.BytesIO();total=0
 with zipfile.ZipFile(memory,'w',zipfile.ZIP_DEFLATED) as z:
  for p in sorted(source.rglob('*')):
   if p.is_symlink():raise ValueError('symlinks are not backed up')
   if not p.is_file():continue
   name=safe(p.relative_to(source).as_posix())
   if name=='INVENTORY.json':raise ValueError('reserved path')
   total+=p.stat().st_size
   if total>LIMIT:raise ValueError('segment too large; split categories')
   data=p.read_bytes();manifest[name]=hashlib.sha256(data).hexdigest();z.writestr(name,data)
  z.writestr('INVENTORY.json',json.dumps(manifest,sort_keys=True))
 nonce=os.urandom(12);encrypted=AESGCM(key).encrypt(nonce,memory.getvalue(),MAGIC)
 destination.parent.mkdir(parents=True,exist_ok=True)
 with os.fdopen(os.open(destination,os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600),'wb') as f:f.write(MAGIC+nonce+encrypted)
 return dict(files=len(manifest),archive_sha256=hashlib.sha256(destination.read_bytes()).hexdigest())

def restore(archive,destination,key):
 raw=Path(archive).read_bytes()
 if len(raw)>LIMIT+1024*1024 or not raw.startswith(MAGIC):raise ValueError('invalid bounded archive')
 plaintext=AESGCM(key).decrypt(raw[len(MAGIC):len(MAGIC)+12],raw[len(MAGIC)+12:],MAGIC)
 with zipfile.ZipFile(io.BytesIO(plaintext)) as z:
  names=z.namelist()
  if len({n.casefold() for n in names})!=len(names):raise ValueError('duplicate paths')
  if sum(x.file_size for x in z.infolist())>LIMIT:raise ValueError('oversized archive')
  for n in names:safe(n)
  inventory=json.loads(z.read('INVENTORY.json'))
  if set(names)!=(set(inventory)|{'INVENTORY.json'}):raise ValueError('inventory mismatch')
  verified={}
  for name,sha in inventory.items():
   data=z.read(name)
   if hashlib.sha256(data).hexdigest()!=sha:raise ValueError('content hash mismatch')
   verified[name]=data
  destination=Path(destination)
  if destination.exists():raise ValueError('restore requires a fresh isolated directory')
  destination.mkdir(parents=True,mode=0o700)
  for name,data in verified.items():
   p=destination/name;p.parent.mkdir(parents=True,exist_ok=True)
   with os.fdopen(os.open(p,os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600),'wb') as f:f.write(data)
 return dict(verified_files=len(verified),Restored=True)
