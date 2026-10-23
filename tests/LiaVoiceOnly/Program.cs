using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using LicAi;
using LicAi.Core;
using LicAi.Memory;
using LicAi.Security;

class Probe
{
 [STAThread] static void Main()
 {
  ApplicationConfiguration.Initialize();
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var type=typeof(MainForm);
  var getGemini=type.GetMethod("GetGeminiApiKey",BindingFlags.Static|BindingFlags.NonPublic)!;
  if(!string.IsNullOrWhiteSpace((string?)getGemini.Invoke(null,null)))throw new Exception("Fixture must not contain API keys");
  string root=Path.Combine(Path.GetTempPath(),"lia-voice-only-"+Guid.NewGuid());Directory.CreateDirectory(root);
  var secrets=new LocalSecretStore(Path.Combine(root,"secret.dat"));
  var memory=new ConversationMemory(Path.Combine(root,"memory.db"));
  var engine=new ConversationEngine(memory,new OpenAiClient(()=>secrets.GetApiKey()),new NavigationAssistantClient(()=>secrets.GetApiKey()));
  using var form=new MainForm(engine,secrets,true);
  Exception? failure=null;
  void Check(string scenario){
   if(form.Opacity!=0||form.ShowInTaskbar||form.WindowState!=FormWindowState.Minimized||form.TopMost||form.OwnedForms.Length!=0)throw new Exception("Voice sphere exposed chat/key UI: "+scenario);
   if(File.Exists(Path.Combine(root,"secret.dat")))throw new Exception("Secret storage changed");
   Console.WriteLine("PASS VOICE-ONLY: hidden, no taskbar/chat/prompt, saved-key store preserved: "+scenario);
  }
  form.Shown+=async (_,_)=>{
   try{
    var busy=type.GetField("_voiceFallbackStarting",flags)!;
    for(int i=0;i<100&&(int)busy.GetValue(form)!!=0;i++)await Task.Delay(50);
    Check("startup without key");
    foreach(string reason in new[]{"OPENAI_KEY_MISSING","OPENAI_CONNECTION_FAILED","OPENAI_START_FAILED"}){
     await (Task)type.GetMethod("ActivateVoiceFallbackAsync",flags)!.Invoke(form,new object[]{reason})!;
     Check(reason);
    }
    type.GetMethod("RevealLocalChat",flags)!.Invoke(form,null);Check("legacy reveal request");
    var result=await (Task<string>)type.GetMethod("PromptApiKeyAsync",flags)!.Invoke(form,new object[]{"Configurar OpenAI","QA"})!;
    if(result!="")throw new Exception("Voice mode opened credential flow");
    Check("implicit key prompt rejected");
    type.GetMethod("ShowFatalError",flags)!.Invoke(form,new object[]{new InvalidOperationException("QA failure")});Check("error reported without popup");
   }catch(Exception ex){failure=ex;}
   finally{form.Close();}
  };
  Application.Run(form);
  if(failure!=null)throw failure;
 }
}
