using System.Reflection;
using LealInfoPDV;
namespace AngraTextIdentity;
public static class Identity
{
 static IEnumerable<Control> Walk(Control root){yield return root;foreach(Control child in root.Controls)foreach(var c in Walk(child))yield return c;}
 public static void Apply(Form form,string version){
  var t=typeof(LoginForm).Assembly.GetType("LealInfoPDV.Licensing.InstallationLicense",true)!;
  var current=t.GetProperty("Current",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(null);
  if(!QaIsolation.Isolated||current==null||(string?)current.GetType().GetProperty("ClientCode")!.GetValue(current)!="005")return;
  if(form is LoginForm){
   var plan=((string?)current.GetType().GetProperty("Plan")!.GetValue(current))?.Trim().ToLowerInvariant();
   var name=plan switch{"standard"=>"STANDARD","plus"=>"PLUS","pro"=>"PRO",_=>"NÃO ATIVADO"};
   foreach(var label in Walk(form).OfType<Label>())if(label.Text is "PDV PRO" or "PDV STANDARD" or "PDV PLUS" or "PDV NÃO ATIVADO")label.Text="PDV "+name;
  }
  if(form is MainForm){
   // Window title identifies the establishment; retain the manufacturer's original name.
   if(!form.Text.StartsWith("ANGRA VIDROS",StringComparison.Ordinal))form.Text="ANGRA VIDROS — "+form.Text;
   foreach(var strip in Walk(form).OfType<StatusStrip>())foreach(ToolStripItem item in strip.Items){
    if(item.Text=="LEAL INFO CONECTADO")item.Text="ANGRA VIDROS";
    if(item.Name=="loja_pet_version")item.Text="ANGRA VIDROS • PET V"+version;
   }
  }
 }
}
