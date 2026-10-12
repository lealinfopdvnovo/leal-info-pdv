using LealInfoPDV;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Drawing.Imaging;
using System.Globalization;
static class Program
{
    static IEnumerable<Control> All(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[]{x}.Concat(All(x)));
    static T Find<T>(Control c,string name) where T:Control => All(c).OfType<T>().Single(x=>x.Name==name);
    static void Check(bool b,string text){if(!b)throw new Exception("FAIL "+text);Console.WriteLine("PASS "+text);}
    static void Pump(){Application.DoEvents();Thread.Sleep(30);Application.DoEvents();}
    static void Mouse(Control c,string method,int x,int y,int delta=0)=>c.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(c,new object[]{new MouseEventArgs(MouseButtons.Left,1,x,y,delta)});
    static void Style(Form f)
    {
        var main=RuntimeHelpers.GetUninitializedObject(typeof(MainForm));
        typeof(MainForm).GetMethod("ApplyFloatingTheme",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,new object[]{f});
        typeof(MainForm).Assembly.GetType("LealInfoPDV.SalesDialogTheme")!.GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object?[]{f,"Futurista Azul",null});
    }
    [STAThread] static void Main()
    {
        Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","Windows descartavel exigido");
        CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("pt-BR");
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();
        Database.Initialize();
        using(var c=Database.Open()){using var q=c.CreateCommand();q.CommandText="INSERT INTO customers(name) VALUES('CLIENTE FICTICIO VIDRACARIA ABAS')";q.ExecuteNonQuery();}
        Directory.CreateDirectory("glass-tabs-evidencias");
        foreach(var size in new[]{new Size(1366,768),new Size(1600,900),new Size(1920,1080)})
        foreach(var factor in new[]{1f,1.25f,1.5f})
        {
            using var f=new GlassWorkshopForm(Style);f.Show();f.Scale(new SizeF(factor,factor));
            f.Size=new Size(Math.Min(f.Width,size.Width),Math.Min(f.Height,size.Height-60));Pump();
            var tabs=Find<TabControl>(f,"GlassSections");
            Check(tabs.TabPages.Cast<TabPage>().Select(p=>p.Text).SequenceEqual(new[]{"Projetos","Materiais","Medidas","Cálculos","Desenho","Visualização","Impressão"}),"Abas mantidas");
            Check(!All(f).OfType<Button>().Any(b=>b.Name=="NewGlassProject"||b.Name=="GlassQuote"),"Sem outro Novo Projeto ou janela de orçamento");
            Find<Button>(f,"LoadQuoteCustomers").PerformClick();
            var customers=Find<ComboBox>(f,"QuoteCustomer");
            customers.SelectedIndex=Enumerable.Range(0,customers.Items.Count).Single(i=>customers.Items[i]!.ToString()=="CLIENTE FICTICIO VIDRACARIA ABAS");
            Check(customers.Region!=null && Find<TextBox>(f,"Description").Region!=null,"Acabamento existente em campos");
            Find<TextBox>(f,"Description").Text="PROJETO QA ABAS";Find<TextBox>(f,"QuoteService").Text="SERVIÇO QA";
            tabs.SelectedIndex=2;Pump();
            var w=Find<NumericUpDown>(f,"WidthMm");var h=Find<NumericUpDown>(f,"HeightMm");var n=Find<NumericUpDown>(f,"Quantity");
            foreach(var value in new[]{(1200m,1000m,1,1.2m),(1800m,1000m,1,1.8m),(1000m,1500m,2,3m)})
            {
                w.Value=value.Item1;h.Value=value.Item2;n.Value=value.Item3;
                tabs.SelectedIndex=3;Pump();
                Find<NumericUpDown>(f,"QuotePrice").Value=100;Find<NumericUpDown>(f,"QuoteServiceValue").Value=50;
                Find<NumericUpDown>(f,"QuoteDiscount").Value=10;Find<NumericUpDown>(f,"QuoteAddition").Value=5;
                var summary=Find<TextBox>(f,"QuoteSummary");
                Check(summary.Text.Contains("ÁREA TOTAL: "+value.Item4.ToString("0.######")+" m²"),"Área automática "+value);
                Check(summary.Text.Contains("TOTAL: "+(value.Item4*100+45).ToString("C2")),"Total automático "+value);
                tabs.SelectedIndex=4;Pump();var drawing=Find<GlassDrawingView>(f,"Drawing");
                Check(drawing.Project==new GlassProject("PROJETO QA ABAS",value.Item1,value.Item2,value.Item3),"2D recebe fonte única");
                var bounds=GlassWindowRenderer.WindowBounds(drawing.ClientRectangle,drawing.Project!);
                Check(Math.Abs(bounds.Width/bounds.Height-(float)(value.Item1/value.Item2))<.001,"Proporção 2D preservada");
                tabs.SelectedIndex=5;Pump();var three=Find<GlassBox3DView>(f,"Drawing3D");
                Check(three.Visible && three.Project==drawing.Project,"3D recebe a mesma fonte");
                float yaw=three.YawDegrees;Mouse(three,"OnMouseDown",10,10);Mouse(three,"OnMouseMove",140,60);Mouse(three,"OnMouseUp",140,60);Check(three.YawDegrees!=yaw,"Rotação preservada");
                Mouse(three,"OnMouseWheel",0,0,120);Check(three.ZoomFactor>1,"Zoom preservado");Find<Button>(f,"Reset3D").PerformClick();Check(three.ZoomFactor==1&&three.YawDegrees==-25,"Reset preservado");
                tabs.SelectedIndex=6;Pump();Check(Find<Button>(f,"Preview").Enabled,"Impressão técnica disponível");
                using var page=GlassProjectPrint.RenderPage(drawing.Project!);Check(page.Width==794&&page.Height==1123,"A4 existente preservado");
                tabs.SelectedIndex=2;Pump();
            }
            foreach(TabPage page in tabs.TabPages)
            {
                tabs.SelectedTab=page;Pump();
                foreach(var c in All(page).Where(c=>c.Visible && c is TextBox or ComboBox or NumericUpDown or Button))
                {
                    Check(c.Width>40 && c.Height>=20,"Controle sem colapso "+c.Name);
                    if(c is Button b)Check(TextRenderer.MeasureText(b.Text,b.Font).Width<=b.Width-4,"Texto completo do botão "+b.Name);
                    var sc=page.Controls.OfType<Panel>().FirstOrDefault(p=>p.AutoScroll);
                    if(sc!=null){sc.ScrollControlIntoView(c);Pump();Check(sc.RectangleToScreen(sc.ClientRectangle).IntersectsWith(c.RectangleToScreen(c.ClientRectangle)),"Controle acessível por rolagem "+c.Name);}
                }
                using var bmp=new Bitmap(f.Width,f.Height);f.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save($"glass-tabs-evidencias/{size.Width}-{factor:0.00}-{page.TabIndex}-{page.Text}.png",ImageFormat.Png);
            }
            tabs.SelectedIndex=2;w.Value=0;tabs.SelectedIndex=3;
            Check(Find<TextBox>(f,"QuoteSummary").Text.Contains("Total indisponível"),"Inválido limpa total");
            tabs.SelectedIndex=4;Check(Find<GlassDrawingView>(f,"Drawing").Project==null,"Inválido limpa desenho");
            f.Close();Check(!Application.OpenForms.OfType<GlassQuoteForm>().Any(),"Nenhuma segunda janela");
        }
        Console.WriteLine("PASS CENTRAL: sete abas, fonte única, cliente existente, cálculo, 2D/3D, A4 e nove layouts");
    }
}
