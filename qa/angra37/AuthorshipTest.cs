using System.Text.Json.Nodes;using System.Reflection;
[assembly:System.Reflection.AssemblyMetadata("PetIdentity","005")]
record Link(string Entity,JsonObject? Baseline);
static class Program{static void Main(string[] args){
 var author=new JsonObject{["uid"]="qa-owner",["matricula"]="EMP01",["name"]="Pessoa Teste"};var baseline=new JsonObject{["authorship"]=author.DeepClone()};
 var link=new Link("projects",baseline);var payload=new JsonObject{["title"]="Teste"};var result=(JsonObject)AngraQaIntegration.Authorship.Merge(link,payload)!;
 if(!JsonNode.DeepEquals(result["authorship"],author)||!JsonNode.DeepEquals(baseline["authorship"],author))throw new Exception("Author lost");
 if(AngraQaIntegration.Authorship.Merge(link,null)!=null)throw new Exception("Deleted record recreated");
 var customer=new JsonObject{["name"]="QA"};AngraQaIntegration.Authorship.Merge(new Link("clients",baseline),customer);if(customer.ContainsKey("authorship"))throw new Exception("Customer changed");
 var pet=Assembly.LoadFrom(Path.GetFullPath(args[0]));var bridge=pet.GetType("AngraIntegrationQaBridge")!.GetMethod("MergeAuthor",BindingFlags.Static|BindingFlags.Public)!;
 var actual=(JsonObject)bridge.Invoke(null,new object[]{link,new JsonObject{["title"]="QA"}})!;if(!JsonNode.DeepEquals(actual["authorship"],author))throw new Exception("Patched bridge failed");
 var pairing=pet.GetType("LealInfoPDV.AngraPairingForm")!;
 var svc=pairing.GetField("service",BindingFlags.Instance|BindingFlags.NonPublic)!.FieldType;
 var tp=svc.GetProperty("Transport",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.PropertyType??svc.GetField("transport",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.FieldType;
 if(tp==null||tp.GetProperty("DeviceId",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)==null)throw new Exception("PDV owner administration transport contract not found");
 using(var form=new LealInfoPDV.AngraPairingForm()){
 AngraQaIntegration.Resume.Apply(form);AngraQaIntegration.Resume.Apply(form);
 if(form.Controls.Find("AngraOwnerAdministration",true).Length!=1)throw new Exception("Owner administration entry missing or duplicated");
 }
 Console.WriteLine("PASS Windows helper and actual patched PET bridge preserve author, legacy null and unrelated customers");
}}

namespace LealInfoPDV { public sealed class AngraPairingForm:System.Windows.Forms.Form {private readonly object service=new object();public AngraPairingForm(){Controls.Add(new System.Windows.Forms.FlowLayoutPanel());}}}
