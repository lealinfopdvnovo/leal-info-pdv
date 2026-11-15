using System.Security.Cryptography;
using System.Text.Json;
using LealInfo.Components;
class QaStaging {
 static int count;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
 static async Task Reject(Func<Task> op,string name){try{await op();}catch(InvalidDataException){Check(true,name);return;}throw new Exception("Accepted: "+name);}
 static async Task Main(){
  var feed=JsonDocument.Parse(File.ReadAllText("qa/etapa4-setup/channel/version.json")).RootElement;
  var url=feed.GetProperty("PackageUrl").GetString()!;var bytes=File.ReadAllBytes("qa/etapa4-setup/channel/"+url.Split('/').Last());
  var release=new Release(ComponentKind.PET,"1.12","005",feed.GetProperty("Sha256").GetString()!,url,"10.468","10.469",1);
  var installed=new Installed("10.468","1.11","005","10.468","10.469",1);
  var root=Path.Combine(Path.GetTempPath(),"LEAL-ETAPA5-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   var selection=IndependentUpdater.Plan(installed,null,release);Check(selection.Available.Count==1&&selection.Errors.Count==0,"approved QA PET offered independently");
   var staged=await IndependentUpdater.StageAsync(release,"005",root,(_,_)=>Task.FromResult(bytes));Check(staged.Hashes.Count==2,"actual approved QA PET stages only its two permitted files");
   foreach(var id in new[]{"001","002","003","004"}){Check(IndependentUpdater.Plan(installed with{Identity=id},null,release).Available.Count==0,"QA offer refused for "+id);await Reject(()=>IndependentUpdater.StageAsync(release,id,root,(_,_)=>Task.FromResult(bytes)),"QA staging refuses "+id);}
   await Reject(()=>IndependentUpdater.StageAsync(release with{Sha256=new string('0',64)},"005",root,(_,_)=>Task.FromResult(bytes)),"corrupt archive rejected");
   await Reject(()=>IndependentUpdater.StageAsync(release with{PackageUrl="https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/v10.500/update.zip"},"005",root,(_,_)=>Task.FromResult(bytes)),"production endpoint rejected by QA copy");
   Check(IndependentUpdater.Plan(installed with{PetVersion="1.12"},null,release).Available.Count==0,"installed PET not offered again");
   Check(IndependentUpdater.Plan(installed with{GlobalVersion="10.469"},null,release).Available.Count==0,"incompatible GLOBAL blocks QA PET");
   File.WriteAllText("etapa5-evidence/qa-staging.json",JsonSerializer.Serialize(new{Pass=true,Checks=count,ActualApprovedPackage=true,InstalledOnCustomer=false,RuntimeModified=false}));
  }finally{Directory.Delete(root,true);}
 }
}
