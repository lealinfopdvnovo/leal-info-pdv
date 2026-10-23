import hashlib,json,os,re,subprocess,urllib.request,zipfile,io
from pathlib import Path
c=os.environ['CHANNEL'];assert c in ('002','CASA','004')
# Reproduce the last published 10.453 package of EACH client, not Loja 001.
p=Path('controller/qa/global-453/prepare.py');s=p.read_text()
pins={'002':('10.453','d1d90be97627f1def476781ac6c3f2cb94ab0b6f1e4f6266dfd2bfcf9dc8d382'),'CASA':('10.453','a646da0c86ed8f8647b520f8e17819bfa87de97ecf269daf7ce2c1d8cf2e46cc'),'004':('10.453','5f7254476bee8f9fce54c95b0ae7014d7b0102cd980f843fed767b8f16835bb7')}
old={'002':('10.445','9843d15cee801b5ceef69cd092979a3575605b91cecf44348b4fbb5c5112a239'),'CASA':('10.446','7019c715477d5b972f6fe75f4fd457fe0babd524bf3d737056c3e900a8c54ba7'),'004':('10.445','e256a4992cf2b7de801a54b5d1a2f2314cc17f88c8682d8be61852c858f1217f')}
for channel,(v,h) in pins.items():s=s.replace(repr(old[channel]).replace(' ',''),repr((v,h)).replace(' ',''))
s=s.replace("/main/'+feedpath","/7fe58b99b9d04abc91b01173a89e72fe320ddfa1/'+feedpath")
p.write_text(s)
subprocess.run(['python',str(p)],check=True)

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
if os.environ.get('FINISH_LOGIN')=='1':finish()
