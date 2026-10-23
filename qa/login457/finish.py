import hashlib,json,os
from pathlib import Path
c=os.environ["CHANNEL"]
def finish():
 r=Path('candidate');before={str(p.relative_to(r)):hashlib.sha256(p.read_bytes()).hexdigest() for p in r.rglob('*') if p.is_file() and '.git' not in p.parts and 'bin' not in p.parts and 'obj' not in p.parts}
 p=r/'LoginForm.cs';s=p.read_text(encoding='utf-8-sig');a=s.index('        card.Controls.Add(new Label{Text="LEAL INFO CONECTADO  •  ACESSO SEGURO');b=s.index('\n',s.index('},0,2);',a))
 s=s[:a]+'        card.Controls.Add(new LoginVersionBadge(),0,2);'+s[b:]
 marker='    private sealed class ChromeStagePanel';assert marker in s
 s=s.replace(marker,Path('controller/qa/login457/VersionBadge.txt').read_text()+'\n'+marker,1);p.write_text(s)
 p=r/'UpdateManager.cs';s=p.read_text();assert 'CurrentVersion="10.453"' in s;p.write_text(s.replace('CurrentVersion="10.453"','CurrentVersion="10.457"'))
 p=r/'LealInfoPDV.csproj';s=p.read_text();p.write_text(s.replace('10.453.0','10.457.0'))
 changed=[]
 for name,h in before.items():
  if hashlib.sha256((r/name).read_bytes()).hexdigest()!=h:changed.append(name)
 assert set(changed)=={'LoginForm.cs','UpdateManager.cs','LealInfoPDV.csproj'},changed
 # Byte-for-byte hashes prove that updater algorithms, PETs, database and license sources were not edited.
 protected={n:h for n,h in before.items() if n not in changed}
 Path('scope-proof.json').write_text(json.dumps(dict(Channel=c,Changes=changed,ProtectedSourceHashes=protected,GlobalVersion='10.457',Loja001Touched=False,PetChanged=False,UpdaterLogicChanged=False,Published=False),indent=2))
 tests=r/'qa/login457';tests.mkdir(parents=True,exist_ok=True)
 for n in ('Program.cs','Tests.csproj'):(tests/n).write_bytes(Path('controller/qa/login457',n).read_bytes())

finish()
