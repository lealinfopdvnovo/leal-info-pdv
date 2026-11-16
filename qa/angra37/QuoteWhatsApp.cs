using System.Collections;using System.Diagnostics;using System.Globalization;using System.Reflection;using System.Runtime.CompilerServices;using System.Text.RegularExpressions;using System.Windows.Forms;
namespace AngraQaIntegration;
public static class QuoteWhatsApp {
 public sealed record Draft(string Phone,string Message);
 static bool watching;static readonly ConditionalWeakTable<Form,Button> buttons=new();
 static Action<string> launch=url=>Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
 const BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static bool Scope()=>AppDomain.CurrentDomain.GetAssemblies().Any(a=>!a.IsDynamic&&a.GetManifestResourceNames().Contains("ANGRA.IntegrationQA.Helper")&&a.GetCustomAttributes<AssemblyMetadataAttribute>().Any(m=>m.Key=="PetIdentity"&&m.Value=="005"));
 static Type Core(string name)=>AppDomain.CurrentDomain.GetAssemblies().Where(a=>!a.IsDynamic).Select(a=>a.GetType("LealInfoPDV."+name,false)).FirstOrDefault(t=>t!=null)??throw new InvalidOperationException("Recurso do PDV indisponível.");
 static bool Owner()=>Core("Auth").GetProperty("IsOwner",BindingFlags.Public|BindingFlags.Static)?.GetValue(null) is true;
 static void DemandOwner(){if(!Scope()||!Owner())throw new UnauthorizedAccessException("Somente o proprietário pode enviar o orçamento ao cliente.");}
 static IEnumerable<Control> All(Control c){yield return c;foreach(Control n in c.Controls)foreach(var x in All(n))yield return x;}
 public static void Observe(){if(!Scope())return;if(!watching){watching=true;Application.Idle+=Scan;}Scan(null,EventArgs.Empty);}
 static void Scan(object? s,EventArgs e){foreach(Form f in Application.OpenForms.Cast<Form>().ToArray())if(f.GetType().FullName=="LealInfoPDV.GlassWorkshopForm")try{Attach(f);}catch(Exception error){Debug.WriteLine("QA quote share unavailable: "+error.GetType().Name);}}
 public static void Attach(Form form){
  if(!Scope()||form.GetType().FullName!="LealInfoPDV.GlassWorkshopForm")return;
  if(buttons.TryGetValue(form,out var prior)){prior.Visible=Owner();prior.Enabled=Owner();return;}
  var flow=All(form).OfType<FlowLayoutPanel>().FirstOrDefault(x=>x.Name=="GlassCustomerActions");if(flow==null)return;
  var sample=flow.Controls.OfType<Button>().FirstOrDefault();if(sample==null)return;
  var button=(Button)Activator.CreateInstance(sample.GetType(),true)!;button.Name="AngraQuoteWhatsApp";button.Text="ENVIAR ORÇAMENTO PELO WHATSAPP";button.AutoSize=true;button.MinimumSize=new System.Drawing.Size(320,42);button.Margin=sample.Margin;button.BackColor=System.Drawing.Color.FromArgb(24,135,78);button.ForeColor=System.Drawing.Color.White;button.Font=new System.Drawing.Font(sample.Font,System.Drawing.FontStyle.Bold);button.Visible=Owner();button.Enabled=Owner();
  button.Click+=(_,_)=>{try{var url=CreateUrl(Prepare(form));DemandOwner();launch(url);}catch(Exception error){var cause=error is TargetInvocationException{InnerException:not null} x?x.InnerException:error;MessageBox.Show(form,cause!.Message,"Orçamento pelo WhatsApp",MessageBoxButtons.OK,MessageBoxIcon.Information);}};
  flow.Controls.Add(button);buttons.Add(form,button);
 }
 static object Member(object value,string name)=>value.GetType().GetProperty(name,Instance)?.GetValue(value)??value.GetType().GetField(name,Instance)?.GetValue(value)??throw new InvalidOperationException("Orçamento indisponível.");
 public static Draft Prepare(Form form){
  DemandOwner();if(form.GetType().FullName!="LealInfoPDV.GlassWorkshopForm")throw new InvalidOperationException("Abra a Central de Vidraçaria.");
  form.ValidateChildren();var project=Member(form,"project");var material=Member(form,"materials");var snapshot=project.GetType().GetMethod("CommercialSnapshot",Instance)!.Invoke(project,new[]{Member(material,"Materials")})!;
  long id=Convert.ToInt64(Member(snapshot,"CustomerId"),CultureInfo.InvariantCulture);if(id<=0)throw new InvalidOperationException("Selecione um cliente cadastrado.");
  var database=Core("Database");using var connection=(IDisposable)database.GetMethod("Open",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,null)!;using var command=(IDisposable)connection.GetType().GetMethod("CreateCommand")!.Invoke(connection,null)!;
  command.GetType().GetProperty("CommandText")!.SetValue(command,"SELECT phone FROM customers WHERE id="+id.ToString(CultureInfo.InvariantCulture));
  string phone=Convert.ToString(command.GetType().GetMethod("ExecuteScalar")!.Invoke(command,null))??"";
  var document=snapshot.GetType().Assembly.GetType("LealInfoPDV.GlassCommercialDocument")!;var rows=(IEnumerable)document.GetMethod("Lines",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,new[]{snapshot})!;
  var text="Olá, "+Member(snapshot,"CustomerName")+"! Segue seu orçamento.\n\n"+string.Join("\n",rows.Cast<string>());
  return new Draft(phone,text);
 }
 public static string NormalizePhone(string raw){
  raw=raw.Trim();if(raw.Length==0)throw new InvalidOperationException("Cadastre o telefone com DDD do cliente antes de enviar pelo WhatsApp.");
  if(!Regex.IsMatch(raw,@"^\+?[0-9\s().-]+$"))throw new InvalidOperationException("Confira o telefone do cliente e informe o DDD.");
  string digits=new(raw.Where(c=>c>='0'&&c<='9').ToArray());if(digits.StartsWith("00",StringComparison.Ordinal))digits=digits[2..];if(digits.Length is 10 or 11)digits="55"+digits;
  if(!Regex.IsMatch(digits,@"^55[1-9][0-9][2-9][0-9]{7,8}$"))throw new InvalidOperationException("Informe um telefone brasileiro válido com DDD, por exemplo (24) 99999-1234.");
  return digits;
 }
 public static string CreateUrl(Draft draft){DemandOwner();var phone=NormalizePhone(draft.Phone);var url="https://wa.me/"+phone+"?text="+Uri.EscapeDataString(draft.Message);if(url.Length>16000)throw new InvalidOperationException("Orçamento muito extenso para uma mensagem. Gere o PDF na aba Impressão e anexe no WhatsApp.");return url;}
}
