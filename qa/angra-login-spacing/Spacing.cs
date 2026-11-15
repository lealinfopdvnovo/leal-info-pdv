using System.Reflection;
using System.Runtime.CompilerServices;
using LealInfoPDV;
namespace AngraLoginSpacing;
public static class Spacing {
 static bool installed;
 static readonly ConditionalWeakTable<LoginForm,Binding> Bindings=new();
 static IEnumerable<Control> Walk(Control root){yield return root;foreach(Control child in root.Controls)foreach(var c in Walk(child))yield return c;}
 static bool IsAngraQa(){
  var t=typeof(LoginForm).Assembly.GetType("LealInfoPDV.Licensing.InstallationLicense",true)!;
  var current=t.GetProperty("Current",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public)!.GetValue(null);
  return QaIsolation.Isolated&&current!=null&&(string?)current.GetType().GetProperty("ClientCode")!.GetValue(current)=="005";
 }
 public static void Apply(Form? form){
  if(!IsAngraQa())return;
  if(form==null){if(installed)return;installed=true;Application.Idle+=Observe;Observe(null,EventArgs.Empty);return;}
  if(form is not LoginForm login)return;
  if(!Bindings.TryGetValue(login,out var binding)){
   var labels=Walk(login).OfType<Label>().ToArray();
   var plan=labels.FirstOrDefault(l=>l.Text is "PDV STANDARD" or "PDV PLUS" or "PDV PRO");
   var tagline=labels.FirstOrDefault(l=>l.Text=="TECNOLOGIA QUE CONECTA");
   if(plan==null||tagline==null||plan.Parent!=tagline.Parent)return;
   binding=new Binding(login,plan,tagline);Bindings.Add(login,binding);
  }
  binding.Arrange();
 }
 static void Observe(object? sender,EventArgs args){foreach(var login in Application.OpenForms.OfType<LoginForm>().ToArray())if(!login.IsDisposed)Apply(login);}
 sealed class Binding {
  readonly LoginForm form;readonly Label plan,tagline;bool arranging;
  public Binding(LoginForm form,Label plan,Label tagline){
   this.form=form;this.plan=plan;this.tagline=tagline;
   plan.SizeChanged+=Changed;plan.LocationChanged+=Changed;plan.FontChanged+=Changed;plan.TextChanged+=Changed;
   tagline.SizeChanged+=Changed;tagline.LocationChanged+=Changed;tagline.FontChanged+=Changed;
  }
  void Changed(object? sender,EventArgs e)=>Arrange();
  public void Arrange(){
   if(arranging||form.IsDisposed||plan.IsDisposed||tagline.IsDisposed)return;
   arranging=true;try{
    int gap=Math.Max(4,(int)Math.Ceiling(form.DeviceDpi/96.0*4));
    int Height(Label label)=>Math.Max(label.Font.Height,TextRenderer.MeasureText(label.Text,label.Font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding).Height)+2*gap;
    // Retain fonts, colors, horizontal bounds, plan position and every other control.
    plan.Height=Math.Max(plan.Height,Height(plan));
    tagline.Height=Math.Max(tagline.Height,Height(tagline));
    tagline.Top=plan.Bottom+gap;
   }finally{arranging=false;}
  }
 }
}
