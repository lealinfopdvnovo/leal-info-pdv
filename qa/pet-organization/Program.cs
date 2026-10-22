using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using LealInfoPDV;
using PetOrganization;
static class Tests
{
 static List<string> checks=new();
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);Console.WriteLine("PASS "+text);}
 static string Sha(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant();
 static void Reject(Action action,string text){bool ok=false;try{action();}catch(InvalidDataException){ok=true;}Check(ok,text);}
 [STAThread]static int Main(string[] args)
 {
  var catalog=PetCatalog.Load();
  if(args.Length==0){Application.EnableVisualStyles();Application.Run(new PreviewForm(catalog));return 0;}
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")throw new Exception("Isolated Windows only");
  Directory.CreateDirectory("evidence");
  try{
   Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
   var names=new Dictionary<string,string>{{"001","LOJA LEAL INFO"},{"002","GELAMAC"},{"003","CASA LEAL INFO"},{"004","CASA GENILSON"}};
   Check(catalog.Profiles.Length==4&&catalog.Profiles.All(p=>names[p.InternalId]==p.DisplayName&&p.PetVersion=="1.1"),"four exact approved names start independently at PET V1.1");
   Check(catalog.GlobalReferenceVersion=="10.443"&&catalog.GlobalReferenceCommit=="4d9128cf8a64b487f836af6c12db14d6a7eb7fd3","approved GLOBAL reference unchanged");
   var changed=catalog.Advance("001","1.6").Advance("003","1.26");
   Check(changed.For("001").PetVersion=="1.6"&&changed.For("003").PetVersion=="1.26"&&changed.For("002").PetVersion=="1.1"&&changed.For("004").PetVersion=="1.1","independent evolution keeps other PET versions intact");
   Check(changed.GlobalReferenceVersion==catalog.GlobalReferenceVersion&&catalog.Profiles.All(p=>p.PetVersion=="1.1"),"changing local drafts mutates neither original catalog nor GLOBAL");
   Check(changed.Profiles.All(p=>p.InternalId==catalog.For(p.InternalId).InternalId&&p.DisplayName==catalog.For(p.InternalId).DisplayName),"internal compatibility IDs and approved names remain stable");
   Reject(()=>catalog.Advance("001","1.1"),"same PET version cannot be advanced twice");Reject(()=>catalog.Advance("001","1.0"),"PET downgrade refused");Reject(()=>catalog.Advance("001","10.444"),"GLOBAL version cannot become a PET version");
   Reject(()=>(catalog with{Profiles=new[]{catalog.Profiles[0],catalog.Profiles[0]}}).Validate(),"duplicate internal identity refused");
   Reject(()=>(catalog with{Profiles=new[]{catalog.Profiles[0] with{DisplayName=" "}}}).Validate(),"blank display name refused");
   string input=args[1];
   foreach(var p in catalog.Profiles){
    var dll=Path.Combine(input,p.InternalId,"LealInfoPDV.Pet."+p.InternalId+".dll");
    var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dll));
    Check(assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="PetIdentity").Value==p.InternalId,"compiled extension retains correct internal identity: "+p.DisplayName);
    var type=assembly.GetExportedTypes().Single(t=>typeof(IPetExtension).IsAssignableFrom(t));var extension=(IPetExtension)Activator.CreateInstance(type)!;
    using var form=new MainForm{Text="LEAL INFO PDV PRO — GLOBAL 10.443"};extension.Customize(form);extension.Customize(form);
    Check(form.Text=="LEAL INFO PDV PRO — GLOBAL 10.443 — "+p.DisplayName+" • PET V1.1","display extension uses name and PET version exactly once: "+p.DisplayName);
    using var ordinary=new Form{Text="PRODUTOS"};extension.Customize(ordinary);Check(ordinary.Text=="PRODUTOS"&&ordinary.Controls.Count==0,"unrelated business window untouched: "+p.DisplayName);
    var outDir=Path.Combine("evidence","prepared-pets",p.InternalId);Directory.CreateDirectory(outDir);var target=Path.Combine(outDir,Path.GetFileName(dll));File.Copy(dll,target,true);
    File.WriteAllText(Path.Combine(outDir,"manifest.json"),JsonSerializer.Serialize(new{Identity=p.InternalId,DisplayName=p.DisplayName,Version=p.PetVersion,ApiVersion=1,Assembly=Path.GetFileName(dll),Sha256=Sha(target),CandidateOnly=true,MigrationApplied=false,PreserveExistingPersonalizations=true}));
   }
   using var preview=new PreviewForm(catalog);preview.Show();Application.DoEvents();
   Check(preview.Before.Rows.Count==4&&preview.After.Rows.Count==4,"native before/after preview lists all four clients");
   Check(preview.After.Rows.Cast<DataGridViewRow>().All(r=>names.Values.Contains(Convert.ToString(r.Cells[0].Value))&&Convert.ToString(r.Cells[1].Value)=="V1.1"),"prepared table shows exact names and initial versions");
   Check(preview.After.Columns.Count==2&&!preview.After.Columns.Cast<DataGridViewColumn>().Any(c=>c.HeaderText.Contains("CÓDIGO")),"new presentation uses names while IDs stay internal");
   using(var shot=new Bitmap(preview.Width,preview.Height)){preview.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save("evidence/pets-antes-depois.png");}
   preview.Close();
   File.WriteAllText("evidence/results.json",JsonSerializer.Serialize(new{Success=true,Source=Environment.GetEnvironmentVariable("GITHUB_SHA"),checks,GlobalModified=false,Published=false,MigrationApplied=false,RealCustomerInstallation=false}));
   Console.WriteLine("PASS PET NAMES AND INDEPENDENT V1.1; GLOBAL UNCHANGED; NOT PUBLISHED");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);File.WriteAllText("evidence/failure.txt",e.ToString());return 1;}
 }
 // Disposable title-only fixture. No operational MainForm, database or licensing is opened.
 sealed class MainForm:Form{}
}
