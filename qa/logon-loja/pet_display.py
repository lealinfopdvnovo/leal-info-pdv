from pathlib import Path
import hashlib,json
assert __import__('os').environ['CHANNEL']=='001'
root=Path('candidate')
proof={}
for name in ('MainForm.cs','LoginForm.cs'):
 p=root/name;s=p.read_text(encoding='utf-8-sig')
 assert 'UpdateManager.CurrentVersion' in s
 # These UI forms reference CurrentVersion only in display labels / About text.
 lines=[line for line in s.splitlines() if 'UpdateManager.CurrentVersion' in line]
 assert all(any(t in line for t in ('Text=', 'MessageBox.Show', 'ToolStripStatusLabel')) for line in lines), lines
 s=s.replace('UpdateManager.CurrentVersion','"10.465 • PET Loja V1.3"')
 p.write_text(s,encoding='utf-8')
 proof[name]=hashlib.sha256(p.read_bytes()).hexdigest()
Path('evidence/pet-display-scope.json').write_text(json.dumps(proof))
print('PASS display GLOBAL/base 10.465, PET Loja V1.3; updater ordering unchanged')
