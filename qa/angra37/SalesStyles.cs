using System.ComponentModel;using System.Drawing;using System.Reflection;using System.Runtime.CompilerServices;using System.Windows.Forms;
namespace AngraQaIntegration;
/// <summary>QA005 sixth sales style. Reuses the original image and the core's existing theme synchronizer.</summary>
public static class SalesStyles {
 const string Theme="Azul Texturizado",Preference="angra_qa005_sales_style";
 static bool watching;static readonly ConditionalWeakTable<Form,View> sales=new();static readonly ConditionalWeakTable<Form,object> choosers=new();
 static bool Scope()=>AppDomain.CurrentDomain.GetAssemblies().Any(a=>!a.IsDynamic&&a.GetManifestResourceNames().Contains("ANGRA.IntegrationQA.Helper")&&a.GetCustomAttributes<AssemblyMetadataAttribute>().Any(m=>m.Key=="PetIdentity"&&m.Value=="005"));
 public static void Observe(){if(!Scope())return;if(!watching){watching=true;Application.Idle+=Scan;}Scan(null,EventArgs.Empty);}
 static IEnumerable<Control> All(Control c){yield return c;foreach(Control n in c.Controls)foreach(var x in All(n))yield return x;}
 static void Scan(object? s,EventArgs e){try{ScanForms();}catch(Exception error){System.Diagnostics.Debug.WriteLine("ANGRA QA style unavailable: "+error.GetType().Name);}}
 static void ScanForms(){
  foreach(Form f in Application.OpenForms.Cast<Form>().ToArray()){
   if(f.IsDisposed)continue;
   if(All(f).OfType<PictureBox>().Any(p=>p.Name=="sales_company_logo")&&!sales.TryGetValue(f,out _)){
    var b=All(f).OfType<Button>().FirstOrDefault(x=>x.Text.Contains("ESTILO"));var v=b==null?null:View.Bind(f,b);
    if(v!=null){sales.Add(f,v);if(v.Get(Preference)==Theme)v.Apply(false);}
   }
   if(f.Text=="Estilo da Tela de Vendas"&&!choosers.TryGetValue(f,out _)){
    var cards=All(f).OfType<Button>().Where(x=>x.Tag is string t&&t is "Futurista Azul" or "Dark Premium" or "Clean Pro" or "Blue Red Racing" or "PDV Rosa").ToArray();
    if(cards.Length!=5||cards[0].Parent is not TableLayoutPanel grid)continue;
    var parent=f.Owner;View? view=null;if(parent!=null)sales.TryGetValue(parent,out view);
    if(view==null)foreach(Form open in Application.OpenForms.Cast<Form>())if(sales.TryGetValue(open,out view))break;
    if(view==null)continue;
    var selected=view;
    foreach(var card in cards){var name=(string)card.Tag!;card.Click+=(_,_)=>selected.Set(Preference,name);}
    var button=(Button)Activator.CreateInstance(cards[0].GetType(),true)!;button.Name="AngraQaBlueTexture";button.Tag=Theme;button.Text="AZUL TEXTURIZADO\n\nTextura azul profunda";button.Dock=DockStyle.Fill;button.Margin=cards[0].Margin;button.BackColor=Color.FromArgb(4,28,65);button.ForeColor=Color.White;button.Font=new Font(cards[0].Font,FontStyle.Bold);button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=3;button.FlatAppearance.BorderColor=Color.FromArgb(0,125,255);button.Cursor=Cursors.Hand;
    button.Click+=(_,_)=>{try{selected.Apply(true);f.Close();}catch(Exception){MessageBox.Show(f,"Não foi possível aplicar o estilo. Escolha novamente; nenhum dado comercial foi alterado.","Estilo ANGRA VIDROS");}};
    grid.Controls.Add(button,1,2);choosers.Add(f,new object());grid.PerformLayout();
   }
  }
 }
 sealed class View {
  readonly Form form;readonly Type main;readonly List<object> graph;readonly object textureOwner,syncOwner;readonly FieldInfo textureField,syncField;
  View(Form f,Type main,List<object> graph,(object,FieldInfo) texture,(object,FieldInfo) sync){form=f;this.main=main;this.graph=graph;textureOwner=texture.Item1;textureField=texture.Item2;syncOwner=sync.Item1;syncField=sync.Item2;}
  const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
  public static View? Bind(Form form,Button style){
   var events=(EventHandlerList)typeof(Control).GetProperty("Events",Flags)!.GetValue(style)!;
   var click=typeof(Control).GetFields(BindingFlags.Static|BindingFlags.NonPublic).FirstOrDefault(x=>x.Name.Equals("s_clickEvent",StringComparison.OrdinalIgnoreCase)||x.Name.Equals("EventClick",StringComparison.OrdinalIgnoreCase));
   if(click==null||events[click.GetValue(null)!] is not Delegate handler)return null;
   var graph=new List<object>();var queue=new Queue<object>();foreach(var h in handler.GetInvocationList())if(h.Target!=null)queue.Enqueue(h.Target);
   while(queue.Count>0&&graph.Count<30){var value=queue.Dequeue();if(graph.Any(x=>ReferenceEquals(x,value)))continue;graph.Add(value);foreach(var field in value.GetType().GetFields(Flags))if(field.FieldType.Name.Contains("DisplayClass")&&field.GetValue(value) is object child)queue.Enqueue(child);}
   (object,FieldInfo)? Find(string name){foreach(var value in graph)if(value.GetType().GetField(name,Flags) is FieldInfo field)return(value,field);return null;}
   var texture=Find("referenceTexture");var sync=Find("synchronizeReferenceTheme");var main=style.GetType().Assembly.GetType("LealInfoPDV.MainForm");
   return texture==null||sync==null||main==null?null:new View(form,main,graph,texture.Value,sync.Value);
  }
  public string Get(string key)=>(string)main.GetMethod("GetSetting",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{key,""})!;
  public void Set(string key,string value)=>main.GetMethod("SetSetting",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{key,value});
  public void Apply(bool remember){
   var reset=graph.SelectMany(owner=>owner.GetType().GetMethods(Flags).Where(m=>m.Name.Contains("g__ApplySalesTheme")&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType==typeof(string)).Select(method=>(Owner:owner,Method:method))).FirstOrDefault();
   if(reset.Method==null)throw new InvalidOperationException("Aplicador de estilo indisponível.");
   reset.Method.Invoke(reset.Owner,new object[]{"Futurista Azul"});
   foreach(var owner in graph)if(owner.GetType().GetField("headerLine",Flags)?.GetValue(owner) is Control line)line.BackColor=Color.FromArgb(0,125,255);
   using var stream=typeof(SalesStyles).Assembly.GetManifestResourceStream("Angra.Qa005.BlueTexture")??throw new InvalidOperationException("Textura não incluída.");
   using var original=Image.FromStream(stream);var next=new Bitmap(original);var previous=textureField.GetValue(textureOwner) as Image;
   textureField.SetValue(textureOwner,next);
   try{((Delegate)syncField.GetValue(syncOwner)!).DynamicInvoke(Theme);}catch{textureField.SetValue(textureOwner,previous);next.Dispose();throw;}
   previous?.Dispose();Set("sales_theme",Theme);if(remember)Set(Preference,Theme);form.Invalidate(true);
  }
 }
}
