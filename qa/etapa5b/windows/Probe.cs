using System.Reflection;
using System.Windows.Forms;
using AngraQaIntegration;
[assembly:AssemblyMetadata("PetIdentity","005")]
namespace LealInfoPDV {
class DeliveryManagementForm:Form {}
class AngraPairingForm:Form {
 readonly Fake service=new(); bool busy=false; public Fake Service=>service;
 public int Updates; void UpdateFields(){Updates++;}
 public void ActivateForTest()=>OnActivated(EventArgs.Empty);
 public sealed class Fake {
  public string Status{get;private set;}="";public string Code{get;private set;}="";
  public bool Approved,Offline;public int Generates,Refreshes;
  public Task Refresh(){Refreshes++;Status=Offline?"DESCONECTADO / SEM COMUNICAÇÃO":Approved?"DESCONECTADO — aguardando o código ser informado no aplicativo.":"DESCONECTADO — autorize este computador uma única vez para gerar o código.";return Task.CompletedTask;}
  public Task<bool> Generate(){if(!Approved||Offline)throw new Exception("Unapproved generate");Generates++;Code="123456";return Task.FromResult(true);}
 }
}
static class Probe {
 [STAThread] static void Main(string[] args){Application.EnableVisualStyles();
  using var form=new AngraPairingForm();Resume.Apply(form);Resume.Apply(form);form.Show();Pump(100);
  Check(form.Service.Generates==0,"unapproved never generates");form.Service.Offline=true;form.ActivateForTest();Pump(100);Check(form.Service.Generates==0,"offline never reports success");
  form.Service.Offline=false;form.Service.Approved=true;form.ActivateForTest();Pump(100);Check(form.Service.Generates==1&&form.Service.Code=="123456"&&form.Updates==1,"return from approval generates full code and refreshes native fields");
  form.ActivateForTest();Pump(2200);Check(form.Service.Generates==1,"no duplicate code generation / renewal loop");form.Close();int before=form.Service.Refreshes;Pump(2200);Check(form.Service.Refreshes==before,"closed form stops polling");
  using(var production=new DeliveryManagementForm()){Resume.Apply(production);Check(production.Controls.Count==0,"SpeedFood hook inert without QA isolation/configuration");}
  if(args.Length==1){var pet=Assembly.LoadFrom(Path.GetFullPath(args[0]));var boot=pet.GetType("AngraIntegrationQaBridge",true)!.GetMethod("Install")!;using var actual=new AngraPairingForm();actual.Show();boot.Invoke(null,new object[]{actual});boot.Invoke(null,new object[]{actual});actual.Service.Approved=true;actual.ActivateForTest();Pump(100);Check(actual.Service.Generates==1,"actual patched PET resource bridge loads repeatedly and resumes approval once");actual.Close();}
  Console.WriteLine("PASS WINDOWS_QA_PAIRING_RESUME");
 }
 static void Pump(int ms){var end=Environment.TickCount64+ms;while(Environment.TickCount64<end){Application.DoEvents();Thread.Sleep(10);}}
 static void Check(bool b,string s){if(!b)throw new Exception(s);Console.WriteLine("PASS "+s);}
}}
