using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LicAi.Core;
namespace LiaGeminiCasa;
static class Program{
 [STAThread]static void Main(){ApplicationConfiguration.Initialize();Application.Run(new ExperimentForm());}
}
sealed class ExperimentForm:Form{
 readonly TextBox key=new(){UseSystemPasswordChar=true,Width=420};
 readonly ComboBox model=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=420};
 readonly CheckBox navigation=new(){Text="Modo Navegação (cada ação exige confirmação)",AutoSize=true};
 readonly CheckBox consent=new(){Text="Projeto confirmado no Free Tier. Usarei somente dados fictícios; o Google pode usar o conteúdo para melhorar produtos.",AutoSize=true};
 readonly Button start=new(){Text="Iniciar teste",AutoSize=true};readonly Button stop=new(){Text="Parar / voltar",AutoSize=true};
 readonly RichTextBox log=new(){Dock=DockStyle.Fill,ReadOnly=true};LiveSession? session;bool busy;bool closing;int generation;
 readonly string installation=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,".."));
 public ExperimentForm(){
  Text="LIA experimental — somente Casa do Adriano — NÃO PUBLICADA";Size=new(1000,680);
  model.Items.AddRange(LiveProtocol.Models);model.SelectedIndex=0;
  var panel=new FlowLayoutPanel{Dock=DockStyle.Top,Height=225,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};
  panel.Controls.AddRange(new Control[]{new Label{Text="Gemini Live — chave apenas neste campo local (não é salva)",AutoSize=true},key,model,navigation,consent,start,stop});Controls.Add(log);Controls.Add(panel);
  try{var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LealInfoPDV","gemini.key");if(File.Exists(path))key.Text=Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser)).Trim();}catch{}
  start.Click+=async(_,_)=>{if(busy||session!=null)return;busy=true;start.Enabled=false;try{
   if(!consent.Checked)throw new InvalidOperationException("Confirme Free Tier e dados fictícios antes do teste.");
   VerifyCasa();await VerifyLicenseAsync();
   var next=new LiveSession(navigation.Checked,ExecuteAsync);session=next;next.Status+=s=>Ui(()=>Append(s));next.Transcript+=s=>Ui(()=>Append(s));
   navigation.Enabled=model.Enabled=key.Enabled=consent.Enabled=false;
   await next.StartAsync(key.Text,(string)model.SelectedItem!,PdvKnowledge.SystemPrompt);
  }catch(Exception e){Append(e is InvalidOperationException?e.Message:"Teste não iniciado; verifique a instalação local.");await StopAsync();}finally{busy=false;start.Enabled=session==null;}};
  stop.Click+=async(_,_)=>{if(busy)return;busy=true;try{await StopAsync();}finally{busy=false;}};
  FormClosing+=async(_,e)=>{if(closing)return;e.Cancel=true;closing=true;await StopAsync();Close();};
 }
 void VerifyCasa(){var path=Path.Combine(installation,"Extensions","PET","manifest.json");using var d=JsonDocument.Parse(File.ReadAllText(path));if(d.RootElement.GetProperty("Identity").GetString()!="003")throw new InvalidOperationException("Experimento recusado: instalação não é Casa do Adriano.");}
 async Task VerifyLicenseAsync(){using var p=Process.Start(new ProcessStartInfo(Path.Combine(installation,"LealInfoPDV.exe"),"--check-lia"){UseShellExecute=false,CreateNoWindow=true})??throw new InvalidOperationException("PDV não localizado.");using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));try{await p.WaitForExitAsync(timeout.Token);}catch{try{p.Kill();}catch{}throw new InvalidOperationException("Validação de licença não respondeu.");}if(p.ExitCode!=0)throw new InvalidOperationException("LIA exige licença ativa Plus ou Pro.");}
 async Task<string> ExecuteAsync(string command){
  // Neither model nor speech transcription can bypass the local confirmation.
  int expected=generation;
  var approved=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
  Ui(()=>approved.TrySetResult(!closing&&MessageBox.Show(this,"Executar no PDV: "+command+"?","Confirmar navegação experimental",MessageBoxButtons.YesNo)==DialogResult.Yes));
  if(!await approved.Task.WaitAsync(TimeSpan.FromSeconds(30)))return "Operador recusou; nenhuma ação executada.";
  if(expected!=generation||closing||session?.IsActive!=true)return "Sessão encerrada; ação recusada.";
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(4));
  try{using var pipe=new NamedPipeClientStream(".","LealInfoPDV.Navigation",PipeDirection.InOut,PipeOptions.Asynchronous);await pipe.ConnectAsync(timeout.Token);using var writer=new StreamWriter(pipe,Encoding.UTF8,1024,true){AutoFlush=true};using var reader=new StreamReader(pipe,Encoding.UTF8,true,1024,true);await writer.WriteLineAsync(command.AsMemory(),timeout.Token);return await reader.ReadLineAsync(timeout.Token)??"PDV não confirmou a ação.";}catch{return "PDV não confirmou a ação; confira a tela. Não repetir automaticamente.";}
 }
 async Task StopAsync(){generation++;if(session!=null){var previous=session;session=null;await previous.DisposeAsync();}navigation.Enabled=model.Enabled=key.Enabled=consent.Enabled=start.Enabled=true;Append("Teste parado. LIA original não foi substituída; use o botão original do PDV.");}
 void Ui(Action action){if(!IsDisposed&&IsHandleCreated)try{BeginInvoke(action);}catch{}}
 void Append(string text){if(!IsDisposed)log.AppendText(text+Environment.NewLine);}
}
