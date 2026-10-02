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
            foreach(var plan in new[]{"standard","plus","pro"}) {
                var file=Path.Combine(folder,plan);var seats=new NetworkLicense(file,serial,pub);
                var baseTerms=new LicenseTerms(serial,2,"unico",null,"QA",1,plan,plan,"001",true,0);
                var baseKey=Sign(baseTerms);seats.Activate(baseKey);seats.Activate(baseKey);
                InstallationLicense.AcceptServer(baseKey,serial,pub);
                Assert(InstallationLicense.HasNetwork && InstallationLicense.Edition==plan.ToUpperInvariant(),"Terminal recusou servidor "+plan);
                Assert(InstallationLicense.HasLia==(plan!="standard"),"Recursos misturados com pontos "+plan);
                Assert(seats.Terms.TotalComputers==2,"Base não permite 2 em "+plan);
                seats.Register("SECOND","KEY-2","Caixa");
                Reject(()=>seats.Register("THIRD","KEY-3","Caixa 3"));
                seats.Register("SECOND","KEY-2","Caixa");
                seats=new NetworkLicense(file,serial,pub);seats.CheckAccess();seats.Register("SECOND","KEY-2","Caixa");
                Reject(()=>seats.Register("SECOND","CLONE","Clone"));
                Reject(()=>seats.Register("OTHER","KEY-2","Chave reutilizada"));
                var extra=baseTerms with {ComputerLimit=3,AdditionalTerminals=1,Revision=2};
                seats.Import(Sign(extra));seats.Register("THIRD","KEY-3","Caixa 3");
                Assert(seats.Terms.Plan==plan && seats.RegisteredCount==3,"Ponto alterou plano "+plan);
                Reject(()=>seats.Register("FOURTH","KEY-4","Caixa 4"));
                Reject(()=>seats.Activate(baseKey)); // revisão antiga nunca reativa
                Reject(()=>seats.Import(Sign(extra with {ComputerLimit=4,Revision=3}))); // quantidade adulterada
                seats.Import(Sign(extra with {ComputerLimit=4,AdditionalTerminals=2,Revision=3}));
                seats.Register("FOURTH","KEY-4","Caixa 4");Reject(()=>seats.Register("FIFTH","KEY-5","Caixa 5"));
                Reject(()=>seats.Import(Sign(baseTerms with {Revision=4}))); // mantém registrados
                seats.Register("SECOND","KEY-2","Caixa");seats.Register("THIRD","KEY-3","Caixa 3");
                Assert(seats.RegisteredCount==4,"Atualização perdeu computadores "+plan);
                Console.WriteLine("PASS: "+plan+" base=2, terceiro bloqueado, adicional1=3, adicional2=4, reconexão e anti-clone.");
            }
            var terms=new LicenseTerms(serial,1,"unico",null,"QA",1,"qa","standard","001",true);
            store.Import(Sign(terms));store.CheckAccess();store.Activate(Sign(terms));
            Assert(InstallationLicense.IsActivated(store.Terms),"Ativação Standard antiga falhou");
            var persisted=new NetworkLicense(path,serial,pub);Assert(persisted.Terms==terms,"Reinício perdeu edição/código");
            Assert(persisted.Terms.TotalComputers==2,"Licença Standard antiga perdeu a base de 2");
            persisted.Register("SECOND","KEY-2","Terminal");Reject(()=>persisted.Register("THIRD","KEY-3","Terminal"));
            persisted.Import(Sign(terms with {Active=false,Revision=2}));Reject(persisted.CheckAccess);Reject(()=>persisted.Activate(Sign(terms with {Active=false,Revision=2})));
            Reject(()=>persisted.Activate(Sign(terms))); // não reativa com chave antiga
            persisted.Import(Sign(terms with {Plan="pro",ComputerLimit=3,Revision=3}));persisted.CheckAccess();
            Assert(persisted.Terms.ClientCode=="001" && persisted.Terms.Plan=="pro","Upgrade perdeu cliente");
            var legacy=new NetworkLicense(Path.Combine(folder,"legacy"),serial,pub);
            Reject(()=>legacy.Activate(Sign(terms with {ClientCode=""})));
            Assert(legacy.Terms.Revision==0,"Chave sem cliente foi gravada antes de validar ativação");
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
