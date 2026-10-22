using LealInfoPDV;
using System.Text.Json.Nodes;
static partial class Tests
{
 static void ManifestChecks(string folder,string id,string version){
  string temp=Path.Combine(Path.GetTempPath(),"pet-invalid-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
  try{
   File.Copy(Path.Combine(folder,"Pet.dll"),Path.Combine(temp,"Pet.dll"));
   var original=JsonNode.Parse(File.ReadAllText(Path.Combine(folder,"manifest.json")))!;
   void Reject(string key,string value,string identity,string name){
    var data=original.DeepClone();data[key]=value;File.WriteAllText(Path.Combine(temp,"manifest.json"),data.ToJsonString());
    bool blocked=false;try{PetRuntime.Load(temp,identity);}catch(InvalidDataException){blocked=true;}
    Check(blocked&&PetRuntime.CurrentVersion==null,name);
   }
   Reject("Identity","999",id,"cross-client manifest rejected before startup");
   Reject("Sha256",new string('0',64),id,"modified PET assembly rejected before startup");
   Reject("Assembly","../Pet.dll",id,"PET path traversal rejected before startup");
   // Use a valid hash but mismatched declared version: manifest alone cannot relabel a compiled profile.
   Reject("Version","1.99",id,"compiled profile version bound to manifest version");
  }finally{File.Delete(Path.Combine(temp,"manifest.json"));} // Windows may keep a validated assembly mapped until this isolated process exits.
 }
}
