using System.Diagnostics;
using System.Text.Json;
namespace LealInfoPDV;
internal static class CasaGeminiDelivery
{
 static readonly SemaphoreSlim Gate=new(1,1);static Process? child;
 internal static bool Eligible=>Licensing.InstallationLicense.Current?.ClientCode=="003"&&Licensing.InstallationLicense.HasLia;
 internal static async Task<bool> TryHandleAsync(Form owner){
  if(!Eligible)return false;
  var option=MessageBox.Show(owner,"Qual LIA deseja abrir?\n\nSIM: Gemini Live experimental (requer chave e cota gratuita).\nNÃO: LIA atual, preservada.\nCANCELAR: voltar ao PDV.","LIA — Casa do Adriano",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2);
  if(option==DialogResult.No)return false;if(option!=DialogResult.Yes)return true;
  if(!await Gate.WaitAsync(0))return true;
  try{
   if(!Eligible)throw new InvalidOperationException("Licença ou cliente incompatível.");
   using(var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Extensions","PET","manifest.json"))))if(manifest.RootElement.GetProperty("Identity").GetString()!="003")throw new InvalidOperationException("PET não pertence à Casa do Adriano.");
   if(child is {HasExited:false}){MessageBox.Show(owner,"O teste Gemini já está aberto.");return true;}
   foreach(var p in Process.GetProcessesByName("LicAi"))using(p)if(!p.HasExited&&string.Equals(p.MainModule?.FileName,Path.Combine(AppContext.BaseDirectory,"LIC-AI","LicAi.exe"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Encerre a conversa da LIA atual antes do teste Gemini para liberar o microfone.");
   var asm=typeof(CasaGeminiDelivery).Assembly;
   using var resource=asm.GetManifestResourceStream("Casa.Gemini.Bundle")??throw new InvalidDataException("Candidata experimental ausente.");using var bytes=new MemoryStream();resource.CopyTo(bytes);
   var hashes=JsonSerializer.Deserialize<Dictionary<string,string>>(CasaGeminiPayload.FileHashes)!;
   string cache=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LEAL INFO PDV","LIA-EXPERIMENTAL","CASA");
   var folder=await Task.Run(()=>CasaGeminiBundleStore.Install(bytes.ToArray(),CasaGeminiPayload.BundleSha,hashes,cache));
   if(owner.IsDisposed||!Eligible)return true;CasaGeminiBundleStore.Verify(folder,hashes);
   var info=new ProcessStartInfo(Path.Combine(folder,"LiaGeminiCasa.Experimental.exe")){UseShellExecute=false,WorkingDirectory=folder};info.ArgumentList.Add("--pdv-root");info.ArgumentList.Add(AppContext.BaseDirectory);
   child=Process.Start(info)??throw new IOException("A candidata não iniciou.");var started=child;
   owner.FormClosed+=(_,_)=>{try{if(!started.HasExited)started.Kill(true);}catch{}finally{started.Dispose();}};
  }catch(Exception ex){MessageBox.Show(owner,"Não foi possível iniciar o teste. A LIA original permanece disponível.\n\n"+ex.Message,"LIA experimental",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
  finally{Gate.Release();}return true;
 }
}
