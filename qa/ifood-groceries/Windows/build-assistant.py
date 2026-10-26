"""Build a portable Windows GUI with official Framework reference assemblies.
No compiler, SDK or PowerShell is required on the user's computer.
"""
import argparse, hashlib, json, shutil, subprocess, tempfile
from pathlib import Path
parser=argparse.ArgumentParser()
for arg in ['dotnet','csc','refs','output']:parser.add_argument('--'+arg,required=True,type=Path)
a=parser.parse_args();root=Path(__file__).resolve().parent.parent;out=a.output.resolve();out.mkdir(parents=True,exist_ok=True)
refs=['mscorlib','System','System.Core','System.Net.Http','System.Runtime.Serialization','System.Xml','System.Windows.Forms','System.Drawing']
with tempfile.TemporaryDirectory(prefix='ifood-build-') as tmp:
 tmp=Path(tmp);sources=[]
 for name in ['Authentication.cs','EventReception.cs']:
  source=tmp/name;source.write_text((root/'Core'/name).read_text().replace('#nullable disable\n',''));sources.append(str(source))
 exe=out/'Conectar_iFood_Teste.exe'
 args=['-nologo','-langversion:5','-nostdlib+','-warnaserror+','-deterministic+','-target:winexe','-platform:anycpu','-main:LealInfo.IFood.Assistant.Program','-win32manifest:'+str(root/'Windows/assistant.manifest'),'-r:'+','.join(str(a.refs.resolve()/(n+'.dll')) for n in refs),'-out:'+str(exe),*sources,str(root/'Windows/AssistantForm.cs'),str(root/'Windows/Program.cs')]
 rsp=tmp/'build.rsp';rsp.write_text('\n'.join('"'+v+'"' for v in args))
 result=subprocess.run([str(a.dotnet.resolve()),str(a.csc.resolve()),'@'+str(rsp)],capture_output=True,text=True)
 report='R5 isolated event receiver Windows EXE; C# 5; official Framework 4.8 references; warnings as errors.\nExit '+str(result.returncode)+'\n'+result.stdout+result.stderr+'\nWindows runtime/UI still requires a real-machine check.\n'
 (root/'evidence/windows-r5-build.txt').write_text(report)
 if result.returncode:raise SystemExit(report)
for name in ['Conectar_iFood_Teste.exe.config','ABRIR_IFOOD_TESTE.cmd','LEIA_PRIMEIRO.txt']:shutil.copyfile(root/'Windows'/name,out/name)
(out/'SHA256.txt').write_text(hashlib.sha256(exe.read_bytes()).hexdigest()+'  '+exe.name+'\n')
print('Compiled portable EXE:',exe.name,exe.stat().st_size,'bytes')
