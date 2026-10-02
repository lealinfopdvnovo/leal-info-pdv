using System.Security.Cryptography;
using System.Text.Json;
using LealInfoPDV;
using LealInfoPDV.Network;
using LealInfoPDV.Licensing;
internal static class ActivationChecks
{
    internal static void Run()
    {
        var folder=Path.Combine(Path.GetTempPath(),"ActivationQA-"+Guid.NewGuid());Directory.CreateDirectory(folder);
        try {
            using var key=RSA.Create(2048);var pub=key.ExportSubjectPublicKeyInfoPem();const string serial="INSTALLATION-QA";
            string Sign(LicenseTerms t){var bytes=JsonSerializer.SerializeToUtf8Bytes(t);return JsonSerializer.Serialize(new SignedLicense(Convert.ToBase64String(bytes),Convert.ToBase64String(key.SignData(bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))));}
            void Assert(bool ok,string why){if(!ok)throw new Exception(why);}
            void Reject(Action action){try{action();}catch(InvalidOperationException){return;}catch(InvalidDataException){return;}throw new Exception("Licença inválida aceita");}
            var path=Path.Combine(folder,"license");var store=new NetworkLicense(path,serial,pub);
            Assert(!InstallationLicense.IsActivated(store.Terms),"Primeira execução dispensou ativação");
            var terms=new LicenseTerms(serial,1,"unico",null,"QA",1,"qa","standard","001",true);
            store.Import(Sign(terms));store.CheckAccess();Assert(InstallationLicense.IsActivated(store.Terms),"Ativação Standard falhou");
            var persisted=new NetworkLicense(path,serial,pub);Assert(persisted.Terms==terms,"Reinício perdeu edição/código");
            Reject(()=>persisted.Register("THIRD","KEY","Terminal"));
            Reject(()=>persisted.Import(Sign(terms with {Plan="standard",ComputerLimit=2,Revision=2})));
            persisted.Import(Sign(terms with {Active=false,Revision=2}));Reject(persisted.CheckAccess);
            Reject(()=>persisted.Import(Sign(terms))); // não reativa com chave antiga
            persisted.Import(Sign(terms with {Plan="pro",ComputerLimit=3,Revision=3}));persisted.CheckAccess();
            Assert(persisted.Terms.ClientCode=="001" && persisted.Terms.Plan=="pro","Upgrade perdeu cliente");
            foreach(var file in new[]{"lealinfo.db","network.license","Assets/../lealinfo.db","chave-vendedor.pem","clientes.protected","LIC-AI/settings.dat","company.json"})Assert(!UpdatePayloadPolicy.IsProgramFile(file),"Atualização permitiria substituir "+file);
            foreach(var file in new[]{"LealInfoPDV.exe","LealInfoPDV.dll","Assets/logo.png","LIC-AI/LicAi.exe","LealInfoPDV.deps.json"})Assert(UpdatePayloadPolicy.IsProgramFile(file),"Arquivo do programa bloqueado "+file);
            Database.Initialize();using(var cn=Database.Open()){using var cmd=cn.CreateCommand();cmd.CommandText="INSERT OR REPLACE INTO settings(key,value) VALUES('company_name','CLIENTE QA LOCAL');";cmd.ExecuteNonQuery();}
            var logo=Path.Combine(folder,"logo.png");using(var bitmap=new System.Drawing.Bitmap(40,40)){using(var g=System.Drawing.Graphics.FromImage(bitmap))g.Clear(System.Drawing.Color.Gold);bitmap.Save(logo);}
            CompanyBranding.SetLogo(logo);using(var image=CompanyBranding.LoadLogo(false))Assert(image is {Width:40,Height:40},"Logo local falhou");
            Database.Initialize();Assert(CompanyBranding.Get("company_name")=="CLIENTE QA LOCAL","Migração apagou empresa");using(var image=CompanyBranding.LoadLogo(false))Assert(image!=null,"Reinício apagou logo");
            var id=Auth.CreateUser("QA Admin","activation-qa","QA-password-2026","ADMINISTRADOR","","",true);
            Assert(Auth.Login("activation-qa","QA-password-2026")!=null,"Login após ativação falhou");
            Database.Initialize();Auth.Logout();Assert(Auth.Login("activation-qa","QA-password-2026")!=null,"Atualização perdeu usuário");Auth.Logout();
            Console.WriteLine("PASS: primeira execução, ativação, reinício, upgrade, inativa, rollback de licença, logo local e preservação de dados.");
        }finally{Directory.Delete(folder,true);}
    }
}
