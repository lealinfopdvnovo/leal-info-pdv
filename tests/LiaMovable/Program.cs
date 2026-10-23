using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using LicAi;
using LicAi.Core;
using LicAi.Memory;
using LicAi.Security;

class Probe
{
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr hwnd);
 [STAThread] static void Main()
 {
  ApplicationConfiguration.Initialize();
  string root=Path.Combine(Path.GetTempPath(),"lia-mobile-"+Guid.NewGuid());Directory.CreateDirectory(root);
  var secrets=new LocalSecretStore(Path.Combine(root,"secret.dat"));
  secrets.SaveApiKey("sk-isolated-fake-not-for-network-123456789");
  var memory=new ConversationMemory(Path.Combine(root,"memory.db"));
  var engine=new ConversationEngine(memory,new OpenAiClient(()=>secrets.GetApiKey()),new NavigationAssistantClient(()=>secrets.GetApiKey()));
  using var form=new MainForm(engine,secrets,false){TopMost=true};
  Exception? failure=null;
  form.Shown+=async (_,_)=>{
   try{
    foreach(string action in new[]{"Save","Cancel","Close","Background"}){
     var method=typeof(MainForm).GetMethod("PromptApiKeyAsync",BindingFlags.Instance|BindingFlags.NonPublic)!;
     var pending=action=="Background"
       ? Task.Run(()=>(Task<string>)method.Invoke(form,new object[]{"Configurar OpenAI","Chave"})!)
       : (Task<string>)method.Invoke(form,new object[]{"Configurar OpenAI","Chave"})!;
     if(action=="Background")await Task.Delay(150);
     if(pending.IsCompleted)throw new Exception("Prompt closed prematurely");
     var dialog=form.OwnedForms.Single(x=>x.GetType().Name=="ApiKeyDialog");
     if(!IsWindowEnabled(form.Handle)||!form.Enabled||!dialog.Visible||!dialog.TopMost||dialog.Owner!=form)throw new Exception("Main window blocked or dialog hidden");
     var before=form.Location;form.Left+=70;form.Top+=20;
     if(form.Location==before||!dialog.Visible)throw new Exception("Owner cannot move");
     var second=(Task<string>)method.Invoke(form,new object[]{"Configurar OpenAI","Chave"})!;
     if(!second.IsCompleted||await second!=""||form.OwnedForms.Length!=1)throw new Exception("Duplicate credential editor");
     var input=(TextBox)dialog.Controls.Find("ApiKey",true).Single();
     if(!input.UseSystemPasswordChar)throw new Exception("Unmasked credential");
     input.Text="sk-isolated-fake-entry-123456789";
     if(action=="Close"||action=="Background")dialog.Close();else ((Button)dialog.Controls.Find(action,true).Single()).PerformClick();
     string result=await pending;
     if((action=="Save")!=(result.Length>=20))throw new Exception("Incorrect save/cancel result");
     Console.WriteLine("PASS: actual MainForm remains movable/enabled, editor owned/topmost/single, "+action);
    }
    Console.WriteLine("PASS: no API requests, real key or operational PDV data used");
   }catch(Exception ex){failure=ex;}
   finally{form.Close();}
  };
  Application.Run(form);
  if(failure!=null)throw failure;
 }
}
