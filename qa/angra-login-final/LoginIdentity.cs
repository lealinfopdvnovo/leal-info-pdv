using System.Reflection;
using LealInfoPDV;
namespace AngraLoginIdentity;
public static class Identity {
 static bool installed;
 static IEnumerable<Control> Walk(Control root){yield return root;foreach(Control c in root.Controls)foreach(var x in Walk(c))yield return x;}
 static bool IsAngraQa(){
  var t=typeof(LoginForm).Assembly.GetType("LealInfoPDV.Licensing.InstallationLicense",true)!;
  var current=t.GetProperty("Current",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public)!.GetValue(null);
  return QaIsolation.Isolated&&current!=null&&(string?)current.GetType().GetProperty("ClientCode")!.GetValue(current)=="005";
 }
 public static void Apply(Form? form){
  if(!IsAngraQa())return;
  if(form==null){if(installed)return;installed=true;Application.Idle+=Observe;Observe(null,EventArgs.Empty);return;}
  if(form is not LoginForm)return;
  foreach(var label in Walk(form).OfType<Label>()){
   var text=label.Text;string? replacement=null;
   int secure=text.IndexOf("ACESSO SEGURO",StringComparison.Ordinal);
   if(secure>=0)replacement="LEAL INFO CONECTADO — ACESSO SEGURO"+text[(secure+13)..];
   else if(text=="LEAL INFO")replacement="ANGRA";
   else if(text=="CONECTADO")replacement="VIDROS";
   else if(text=="LEAL INFO CONECTADO")replacement="ANGRA VIDROS";
   if(replacement!=null&&replacement!=text){label.Text=replacement;label.Invalidate();}
  }
 }
 static void Observe(object? sender,EventArgs args){
  foreach(var login in Application.OpenForms.OfType<LoginForm>().ToArray())if(!login.IsDisposed)Apply(login);
 }
}
