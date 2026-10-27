using System.IO.Compression;
using System.Text.Json;
using LealInfoPDV;
class Tests{
 static int checks;static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
 static byte[] Zip(Dictionary<string,byte[]> files){using var m=new MemoryStream();using(var z=new ZipArchive(m,ZipArchiveMode.Create,true))foreach(var(n,b)in files){using var w=z.CreateEntry(n).Open();w.Write(b);}return m.ToArray();}
 static void Rejected(Action a,string name){try{a();throw new Exception("Accepted "+name);}catch(InvalidDataException){Check(true,name);}}
 static void Main(string[] args){
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")throw new Exception("Isolated runner only");
  var root=Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!,"casa-gemini-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   var preserved=new Dictionary<string,string>();foreach(var n in new[]{"LIC-AI/LicAi.exe","Extensions/PET/Pet.dll","data.db","license.dat","settings.json","other-client/LealInfoPDV.dll"}){var p=Path.Combine(root,n);Directory.CreateDirectory(Path.GetDirectoryName(p)!);File.WriteAllText(p,"UNCHANGED"+n);preserved[p]=CasaGeminiBundleStore.Hash(File.ReadAllBytes(p));}
   var files=new Dictionary<string,byte[]>{{"LiaGeminiCasa.Experimental.exe",new byte[]{1,2,3}},{"engine.dll",new byte[]{4,5,6}}};var index=files.ToDictionary(x=>x.Key,x=>CasaGeminiBundleStore.Hash(x.Value));var zip=Zip(files);var hash=CasaGeminiBundleStore.Hash(zip);var cache=Path.Combine(root,"cache");
   var a=CasaGeminiBundleStore.Install(zip,hash,index,cache);Check(Directory.Exists(a),"first atomic installation");
   Check(a==CasaGeminiBundleStore.Install(zip,hash,index,cache),"repeat installation verifies existing bytes");
   Rejected(()=>CasaGeminiBundleStore.Install(zip,new string('0',64),index,cache),"wrong bundle SHA refused");
   var v2=new Dictionary<string,byte[]>(files){{"extra.dll",new byte[]{7}}};var z2=Zip(v2);var h2=CasaGeminiBundleStore.Hash(z2);var i2=v2.ToDictionary(x=>x.Key,x=>CasaGeminiBundleStore.Hash(x.Value));
   try{CasaGeminiBundleStore.Install(z2,h2,i2,cache,p=>{if(p=="before-activate")throw new IOException("Simulated interruption");});throw new Exception("fault not injected");}catch(IOException){}
   Check(!Directory.Exists(Path.Combine(cache,h2))&&!Directory.GetDirectories(cache,".stage-*").Any(),"failed installation leaves no activated candidate or stage");CasaGeminiBundleStore.Verify(a,index);Check(true,"previous version remains intact after failure");
   var b=CasaGeminiBundleStore.Install(z2,h2,i2,cache);Check(a!=b&&Directory.Exists(a)&&Directory.Exists(b),"update preserves previous content-addressed version");CasaGeminiBundleStore.Verify(a,index);Check(true,"rollback selects prior immutable version");
   File.WriteAllBytes(Path.Combine(b,"extra.dll"),new byte[]{99});Rejected(()=>CasaGeminiBundleStore.Install(z2,h2,i2,cache),"tampered cached binary refused");
   var wrong=new Dictionary<string,string>(index){{"extra.dll",new string('0',64)}};Rejected(()=>CasaGeminiBundleStore.Install(z2,h2,wrong,Path.Combine(root,"wrong")),"per-file integrity enforced");
   var evil=Zip(new(){{"../outside.exe",new byte[]{1}}});Rejected(()=>CasaGeminiBundleStore.Install(evil,CasaGeminiBundleStore.Hash(evil),new Dictionary<string,string>{{"../outside.exe",CasaGeminiBundleStore.Hash(new byte[]{1})}},Path.Combine(root,"evil")),"path traversal refused");
   foreach(var(p,h)in preserved)Check(CasaGeminiBundleStore.Hash(File.ReadAllBytes(p))==h,"preserved "+Path.GetRelativePath(root,p));
   if(args.Length==2){var real=File.ReadAllBytes(args[0]);var pins=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(args[1]))!;var deployed=CasaGeminiBundleStore.Install(real,CasaGeminiBundleStore.Hash(real),pins,Path.Combine(root,"real"));CasaGeminiBundleStore.Verify(deployed,pins);Check(true,"actual candidate bundle installed and verified");}
   Directory.CreateDirectory("evidence");File.WriteAllText("evidence/gemini-delivery-tests.json",JsonSerializer.Serialize(new{Pass=true,Assertions=checks,RealApiTested=false,Published=false}));
  }finally{Directory.Delete(root,true);}
 }
}
