from pathlib import Path
root=Path("candidate")
login=root/"LoginForm.cs"
s=login.read_text(encoding="utf-8-sig")
patch=Path("removal/LoginForm.cs").read_text(encoding="utf-8-sig")
start=patch.index("        // Selo aprovado:")
end=patch.index("        stage.Controls.Add(certificationSeal);",start)+len("        stage.Controls.Add(certificationSeal);")
a="        CancelButton=exit;"
assert s.count(a)==1
s=s.replace(a,a+"\n"+patch[start:end])
start=patch.index("            int sealSize=")
end=patch.index("            exit.BringToFront();",start)
a="            exit.BringToFront();"
assert s.count(a)==1
s=s.replace(a,patch[start:end]+a)
login.write_text(s,encoding="utf-8")
import shutil
(root/"Certification").mkdir(exist_ok=True)
shutil.copyfile("removal/Certification/Selo_Certificacao_Leal_Info_PDV_Aprovado.png",root/"Certification/Selo_Certificacao_Leal_Info_PDV_Aprovado.png")
proj=root/"LealInfoPDV.csproj"
s=proj.read_text(encoding="utf-8-sig")
s=s.replace("</Project>",'<ItemGroup><EmbeddedResource Include="Certification/Selo_Certificacao_Leal_Info_PDV_Aprovado.png" LogicalName="PET.CertificationSeal" /></ItemGroup>\n</Project>')
proj.write_text(s,encoding="utf-8")
print("Arte original aplicada apenas ao login reconstruido.")

import hashlib,json
Path("evidence/seal-source-scope.json").write_text(json.dumps({p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in ["LoginForm.cs","LealInfoPDV.csproj"]}))
