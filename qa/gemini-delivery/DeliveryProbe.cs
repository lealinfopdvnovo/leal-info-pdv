using LealInfoPDV;
using LealInfoPDV.Network;
using System.Reflection;
using System.Text.Json;
class DeliveryProbe{
 [STAThread]static void Main(string[] args){
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")throw new Exception("Isolated only");int count=0;
  void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
  var prop=typeof(LealInfoPDV.Licensing.InstallationLicense).GetProperty("Current",BindingFlags.Static|BindingFlags.NonPublic)!;var old=prop.GetValue(null);
  try{foreach(var id in new[]{"001","002","003","004"})foreach(var plan in new[]{"standard","plus","pro"})foreach(var active in new[]{true,false}){
   prop.SetValue(null,new LicenseTerms("QA",2,"QA",null,"QA",1,"QA",plan,id,active));Check(CasaGeminiDelivery.Eligible==(id=="003"&&active&&plan is "plus" or "pro"),"scope/license "+id+" "+plan+" "+active);
  }}finally{prop.SetValue(null,old);}
  var type=typeof(MainForm).Assembly.GetType("LealInfoPDV.UpdateManager")!;var manifest=type.GetNestedType("UpdateManifest",BindingFlags.NonPublic)!;
  var m=Activator.CreateInstance(manifest)!;void Set(string n,object v)=>manifest.GetProperty(n)!.SetValue(m,v);
  Set("Version","10.459");Set("TargetClientCode","003");Set("Component","GLOBAL");Set("PetApiVersion",1);Set("Sha256",new string('a',64));Set("PackageUrl","https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/v10.459-casa/test.zip");
  var select=type.GetMethod("SelectManifest",BindingFlags.NonPublic|BindingFlags.Static)!;
  foreach(var id in new[]{"001","002","003","004"})Check((select.Invoke(null,new object?[]{id,"10.458",null,m})!=null)==(id=="003"),"manifest routing "+id);
  var policy=typeof(MainForm).Assembly.GetType("LealInfoPDV.Licensing.UpdatePayloadPolicy")!;
  policy.GetMethod("Validate",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{args[0]});Check(true,"existing package policy accepts exact payload without widening");
  using var stream=typeof(MainForm).Assembly.GetManifestResourceStream("Casa.Gemini.Bundle");Check(stream!=null,"embedded experimental candidate present");
  Directory.CreateDirectory("evidence");File.WriteAllText("evidence/gemini-delivery-probe.json",JsonSerializer.Serialize(new{Pass=true,Assertions=count,RealApiTested=false,Published=false}));
 }
}
