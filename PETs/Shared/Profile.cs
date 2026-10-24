using System.Reflection;
using System.Runtime.CompilerServices;
using LealInfoPDV;
[assembly:InternalsVisibleTo("LealInfoPDV.Pet.CompatibilityTests")]
public sealed class ProfileExtension : IPetExtension,IPetStartup,IPetReady
{
 internal static string Identity=>typeof(ProfileExtension).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(x=>x.Key=="PetIdentity").Value!;
 internal static string Version=>typeof(ProfileExtension).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(x=>x.Key=="PetVersion").Value!;
 readonly IPetExtension presentation=ProfilePresentation.Create();
 readonly ConditionalWeakTable<Form,object> applied=new();
 public void ReportReady(){
#if PET_LOJA
  UpdateProtectionReceipt.ReportReady();
#endif
 }
 public void Initialize(){
#if PET_LOJA
  PremiumGlobalTheme.Install();
#endif
#if HAS_BUSINESS
  InternalWorkspace.Install();
#endif
 }
 public void Customize(Form form){
  presentation.Customize(form);
  if(applied.TryGetValue(form,out _))return;applied.Add(form,new object());
#if HAS_BUSINESS
  if(form is MainForm main){
#if PET_LOJA
   main.PetShortcutOverflow=true;
#endif
   main.PetTool("VIDRAÇARIA","COZINHA",GlassWorkshopIcon.Create,()=>{using var workshop=new GlassWorkshopForm(main.PetTheme);workshop.ShowDialog(main);},Color.FromArgb(27,174,217),Color.FromArgb(7,49,92));
   var sync=LealInfoPDV.Network.NetworkConfiguration.Current.Mode=="terminal"?null:AngraSyncService.TryStart();
   if(sync!=null){var alert=new AngraProjectAlert(main,sync);main.FormClosed+=(_,_)=>{alert.Dispose();sync.Dispose();};_=sync.Connect();}
  }
  else if(form.Text.Contains("TELA DE VENDA",StringComparison.OrdinalIgnoreCase)&&AngraSyncService.Current is {} sync&&LealInfoPDV.Network.NetworkConfiguration.Current.Mode!="terminal"){
   var alert=new AngraProjectAlert(form,sync,true);form.FormClosed+=(_,_)=>alert.Dispose();
  }
#endif
 }
}
