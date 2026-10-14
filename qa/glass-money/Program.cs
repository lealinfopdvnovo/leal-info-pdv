using LealInfoPDV;
using System.Reflection;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Text.Json;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
static class Program
{
    const BindingFlags I=BindingFlags.NonPublic|BindingFlags.Instance;
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, uint message, IntPtr w, IntPtr l);
    static void TypeMoney(NumericUpDown field, string input)
    {
        field.Focus(); field.Select(0,field.Text.Length); Pump();
        foreach(char ch in input) { SendMessage(field.Controls.OfType<TextBox>().Single().Handle,0x102,(IntPtr)ch,IntPtr.Zero); Pump(); }
    }
    static int checks;
    static void Check(bool b,string name){if(!b)throw new Exception("FAIL "+name);checks++;Console.WriteLine("PASS "+name);}
    static IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
    static T Find<T>(Control c,string name)where T:Control=>All(c).OfType<T>().Single(x=>x.Name==name);
    static void Pump(){Application.DoEvents();Thread.Sleep(30);Application.DoEvents();}
    static void Style(Form f){typeof(MainForm).GetMethod("ApplyFloatingTheme",I)!.Invoke(RuntimeHelpers.GetUninitializedObject(typeof(MainForm)),new object[]{f});typeof(MainForm).Assembly.GetType("LealInfoPDV.SalesDialogTheme")!.GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object?[]{f,"Futurista Azul",null});}
    static object? Sql(string sql){using var c=Database.Open();using var q=c.CreateCommand();q.CommandText=sql;return q.ExecuteScalar();}
    static GlassCommercialSnapshot Snapshot(GlassWorkshopForm f){var p=typeof(GlassWorkshopForm).GetField("project",I)!.GetValue(f)!;return (GlassCommercialSnapshot)p.GetType().GetMethod("CommercialSnapshot",I)!.Invoke(p,new object[]{Array.Empty<GlassMaterialLink>()})!;}
    static GlassStoredProject Save(GlassWorkshopForm f)=>(GlassStoredProject)f.GetType().GetMethod("SaveCurrentProject",I)!.Invoke(f,null)!;
    static void Load(GlassWorkshopForm f,string id)=>f.GetType().GetMethod("LoadSavedProject",I)!.Invoke(f,new object[]{id});
    static void Scenario(GlassWorkshopForm f)
    {
        var snap=Snapshot(f);var q=snap.Quote;var text=Find<TextBox>(f,"QuoteSummary").Text;var lines=GlassCommercialDocument.Lines(snap);
        Check(q.Area==1.2m && q.TotalArea==1.2m && q.MaterialValue==120m && q.Total==132m,"Cenario homologado 1200x1000mm, 100/m2 +12 =132");
        foreach(var expected in new[]{"1,20 m²","R$ 100,00","R$ 120,00","R$ 12,00","R$ 132,00"}){Check(text.Contains(expected),"Tela apresenta "+expected);Check(lines.Any(x=>x.Contains(expected)),"A4 apresenta "+expected);}
        Check(text.Contains("TOTAL: R$ 132,00") && lines.Contains("TOTAL DO ORÇAMENTO: R$ 132,00"),"MESMO TOTAL REAL NA TELA E ORCAMENTO A4");
        Check(lines.Contains("Serviço adicional (total): R$ 0,00") && lines.Contains("Desconto: R$ 0,00") && lines.Contains("Acréscimo: R$ 12,00"),"Detalhamento comercial real A4");
        Check(lines.Contains("Subtotal (área + serviço): R$ 120,00"),"Subtotal usa BaseValue aprovado");
    }
    static void Preview(GlassWorkshopForm f,GlassCommercialSnapshot expected)
    {
        var tabs=Find<TabControl>(f,"GlassSections");int previous=tabs.SelectedIndex;tabs.SelectedIndex=6;Pump();
        bool opened=false;Exception? error=null;using var timer=new System.Windows.Forms.Timer{Interval=60};
        timer.Tick+=(_,_)=>{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="ORÇAMENTO COMERCIAL — VIDRAÇARIA");if(dialog==null)return;timer.Stop();opened=true;try{
            Check(dialog.Modal && dialog.Owner==f,"Previa comercial real pertence ao projeto");var image=Find<PictureBox>(dialog,"CommercialPage").Image!;
            var real=(GlassCommercialSnapshot)dialog.Tag!;Check(real.Quote==expected.Quote && real.CustomerId==expected.CustomerId,"Previa consome mesmo modelo/cliente da tela");using var rendered=GlassCommercialDocument.RenderPage(real);using var a=new MemoryStream();using var b=new MemoryStream();image.Save(a,ImageFormat.Png);rendered.Save(b,ImageFormat.Png);Check(a.ToArray().SequenceEqual(b.ToArray()),"Previa A4 renderiza exatamente snapshot comercial real");
            Check(All(dialog).OfType<Button>().Any(x=>x.Text=="GERAR PDF"),"Documento comercial inclui PDF");image.Save("glass-money-evidencias/previa-comercial-132.png");
        }catch(Exception e){error=e;}dialog.Close();};timer.Start();Find<Button>(f,"CommercialPreview").PerformClick();Check(opened,"Botao correto abre orcamento comercial A4");if(error!=null)throw error;tabs.SelectedIndex=previous;Pump();
    }
    [STAThread]static void Main(string[] args)
    {
        Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","Windows descartavel");Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Database.Initialize();Directory.CreateDirectory("glass-money-evidencias");
        CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("en-US");
        if(args.Length>0 && args[0]=="--reopen")
        {using var reopened=new GlassWorkshopForm(Style);reopened.Show();Load(reopened,args[1]);Scenario(reopened);Check(Snapshot(reopened).CustomerId==996 && Snapshot(reopened).CustomerName=="CLIENTE QA MOEDA" && Find<Label>(reopened,"SelectedGlassCustomer").Text.Contains("ID 996"),"Cliente vinculado persistido em NOVO PROCESSO e reconhecido pelo A4/PDF");Preview(reopened,Snapshot(reopened));Check(Find<NumericUpDown>(reopened,"QuotePrice").Text=="R$ 100,00" && Find<NumericUpDown>(reopened,"QuoteAddition").Text=="R$ 12,00","Campos salvos brasileiros em processo/cultura distintos");File.WriteAllBytes("glass-money-evidencias/reaberto-132.pdf",GlassCommercialDocument.PdfBytes(Snapshot(reopened)));reopened.Close();return;}
        Sql("INSERT INTO customers(id,name) VALUES(996,'CLIENTE QA MOEDA'); SELECT 1");long customers=Convert.ToInt64(Sql("SELECT COUNT(*) FROM customers"));long sales=Convert.ToInt64(Sql("SELECT COUNT(*) FROM sales"));
        using var f=new GlassWorkshopForm(Style);f.Show();Pump();Find<Button>(f,"LoadQuoteCustomers").PerformClick();
        Find<ComboBox>(f,"QuoteCustomer").SelectedIndex=1;
        Check(Snapshot(f).CustomerId==996 && Find<Label>(f,"SelectedGlassCustomer").Text.Contains("CLIENTE QA MOEDA (ID 996)"),"Seletor existente confirma cliente REAL e ID");
        var tabs=Find<TabControl>(f,"GlassSections");tabs.SelectedIndex=3;Pump();
        foreach(string culture in new[]{"pt-BR","en-US","de-DE"})
        {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);
            foreach(string name in new[]{"QuotePrice","QuoteServiceValue","QuoteDiscount","QuoteAddition"})
            {
                var field=Find<NumericUpDown>(f,name);
                foreach(var example in new[]{("100,50",100.50m,"R$ 100,50"),("100",1m,"R$ 1,00"),("10050",100.50m,"R$ 100,50"),("R$ 12,00",12m,"R$ 12,00"),("1.234,56",1234.56m,"R$ 1.234,56"),("100,5",100.5m,"R$ 100,50")})
                {field.Text=example.Item1;Check(field.Value==example.Item2 && field.Text==example.Item3,"Entrada/exibicao "+name+" "+culture+" "+example.Item1);}
                field.Value=19.90m;field.Text="100.50";Check(field.Value==19.90m,"Formato ambiguo nao vira10050 "+name+" "+culture);
                field.Text="-10,00";Check(field.Value==19.90m,"Valor negativo nao gravado "+name);
                field.Value=0;
                foreach(var example in new[]{("1",0.01m),("10",0.10m),("100",1m),("1000",10m),("10050",100.50m),("100,50",100.50m),("0",0m)})
                { TypeMoney(field,example.Item1);Check(field.Value==example.Item2 && field.Text==example.Item2.ToString("C2",CultureInfo.GetCultureInfo("pt-BR")).Replace('\u00a0',' '),"Teclado nativo imediato "+name+" "+culture+" "+example.Item1); }
                TypeMoney(field,"10050");SendMessage(field.Controls.OfType<TextBox>().Single().Handle,0x102,(IntPtr)'\b',IntPtr.Zero);Pump();Check(field.Value==10.05m,"Backspace remove ultimo centavo "+name);
                TypeMoney(field,"\b");Check(field.Value==0,"Limpar selecao zera "+name);
                TypeMoney(field,"250");Check(field.Value==2.50m,"Nova digitacao apos limpar "+name);
                field.Text="";Check(field.Value==0 && field.Text=="R$ 0,00","Vazio nunca restaura dinheiro antigo "+name);
                field.Value=0;
            }
            Check(CultureInfo.CurrentCulture.Name==culture,"Cultura global do PDV preservada");
        }
        CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("pt-BR");
        Find<NumericUpDown>(f,"WidthMm").Value=1200;Find<NumericUpDown>(f,"HeightMm").Value=1000;Find<NumericUpDown>(f,"Quantity").Value=1;
        Find<NumericUpDown>(f,"QuotePrice").Text="100,00";Find<NumericUpDown>(f,"QuoteServiceValue").Text="0,00";Find<NumericUpDown>(f,"QuoteDiscount").Text="0,00";Find<NumericUpDown>(f,"QuoteAddition").Text="12,00";
        // Snapshot deve confirmar texto pendente e alimentar ambos os resultados.
        Snapshot(f);Scenario(f);var snapshot=Snapshot(f);Preview(f,snapshot);
        File.WriteAllBytes("glass-money-evidencias/orcamento-132.pdf",GlassCommercialDocument.PdfBytes(snapshot));using(var page=GlassCommercialDocument.RenderPage(snapshot))page.Save("glass-money-evidencias/orcamento-132.png");
        var saved=Save(f);Check(saved.Data.Quote.Price==100 && saved.Data.Quote.Addition==12,"Persistencia existente recebe decimais100/12");string serialized=JsonSerializer.Serialize(GlassProjectStore.Load(saved.Id));
        // Verifica que o documento não usa números fixos nem fórmula paralela.
        Find<NumericUpDown>(f,"Quantity").Value=2;Find<NumericUpDown>(f,"QuotePrice").Text="100,50";Find<NumericUpDown>(f,"QuoteServiceValue").Text="10,00";Find<NumericUpDown>(f,"QuoteDiscount").Text="5,00";Find<NumericUpDown>(f,"QuoteAddition").Text="12,00";
        var another=Snapshot(f);Check(another.Quote.Total==258.20m && Find<TextBox>(f,"QuoteSummary").Text.Contains("R$ 258,20") && GlassCommercialDocument.Lines(another).Contains("TOTAL DO ORÇAMENTO: R$ 258,20"),"Outro caso dinamico258,20 com area/quantidade/servico/desconto/acrescimo");
        Load(f,saved.Id);Scenario(f);Check(JsonSerializer.Serialize(GlassProjectStore.Load(saved.Id))==serialized,"Reabrir restaura sem mudar registro/revisao");Preview(f,Snapshot(f));
        foreach(var size in new[]{new Size(1366,768),new Size(1600,900),new Size(1920,1080)})foreach(float factor in new[]{1f,1.25f,1.5f})
        {
            using var layout=new GlassWorkshopForm(Style);layout.Show();layout.Scale(new SizeF(factor,factor));layout.Size=new(Math.Min(layout.Width,size.Width),Math.Min(layout.Height,size.Height-60));Load(layout,saved.Id);Find<TabControl>(layout,"GlassSections").SelectedIndex=3;Pump();
            var scroll=All(layout).OfType<Panel>().Single(x=>x.Name=="FieldsCálculos");foreach(var name in new[]{"QuotePrice","QuoteServiceValue","QuoteDiscount","QuoteAddition"}){var input=Find<NumericUpDown>(layout,name);scroll.ScrollControlIntoView(input);Pump();Check(input.Visible && input.Width>120 && input.Height>20 && !input.Text.Contains("."),"Moeda acessivel "+name+" "+size+" "+factor);}
            using var image=new Bitmap(layout.Width,layout.Height);layout.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save($"glass-money-evidencias/calculos-{size.Width}-{factor}.png");layout.Close();
        }
        f.Close();var psi=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};psi.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);psi.ArgumentList.Add("--reopen");psi.ArgumentList.Add(saved.Id);
        using(var child=Process.Start(psi)!){var output=child.StandardOutput.ReadToEndAsync();var error=child.StandardError.ReadToEndAsync();Check(child.WaitForExit(60000),"Processo separado terminou");Console.Write(output.GetAwaiter().GetResult());Console.Write(error.GetAwaiter().GetResult());Check(child.ExitCode==0,"Reabertura consistente entre processos Windows");}
        Check(Convert.ToInt64(Sql("SELECT COUNT(*) FROM customers"))==customers && Convert.ToInt64(Sql("SELECT COUNT(*) FROM sales"))==sales,"Cadastro e financeiro intactos");Console.WriteLine("PASS MOEDA/ORCAMENTO132: "+checks+" verificacoes Windows");
    }
}
