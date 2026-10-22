using LealInfoPDV;
using System.Drawing.Drawing2D;
public sealed class CasaTestExtension : IPetExtension
{
 public void Customize(Form form)
 {
  if(form.GetType().Name=="MainForm"){
   const string old=" — CASA LEAL INFO • PET V1.1",marker=" — CASA LEAL INFO • PET V1.1";
   if(form.Text.EndsWith(old,StringComparison.Ordinal))form.Text=form.Text[..^old.Length];
   if(!form.Text.EndsWith(marker,StringComparison.Ordinal))form.Text+=marker;
   foreach(var strip in Walk(form).OfType<StatusStrip>())
    foreach(var label in strip.Items.OfType<ToolStripStatusLabel>().Where(l=>l.Text=="V"+(typeof(MainForm).Assembly.GetName().Version!.Major+"."+typeof(MainForm).Assembly.GetName().Version!.Minor)||l.Name=="loja_pet_version")){
     label.Name="loja_pet_version";
     label.Text="PDV "+(typeof(MainForm).Assembly.GetName().Version!.Major+"."+typeof(MainForm).Assembly.GetName().Version!.Minor)+" | CASA LEAL INFO • PET V1.1";
     label.AutoSize=true;label.Spring=false;label.Overflow=ToolStripItemOverflow.Never;
    }
  }
  if(form.Text=="LEAL INFO PDV • RELATÓRIO FINANCEIRO") FinanceReportLayout.Apply(form);
 }
 static IEnumerable<Control> Walk(Control root){foreach(Control c in root.Controls){yield return c;foreach(var n in Walk(c))yield return n;}}
}

