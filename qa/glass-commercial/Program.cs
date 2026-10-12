using LealInfoPDV;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Drawing.Imaging;
using System.Globalization;
static class Program
{
    static IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
    static T Find<T>(Control c,string n) where T:Control=>All(c).OfType<T>().Single(x=>x.Name==n);
    static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);}
    static void Pump(){Application.DoEvents();Thread.Sleep(40);Application.DoEvents();}
    static object? Scalar(string sql){using var c=Database.Open();using var q=c.CreateCommand();q.CommandText=sql;return q.ExecuteScalar();}
    static void Exec(string sql){using var c=Database.Open();using var q=c.CreateCommand();q.CommandText=sql;q.ExecuteNonQuery();}
    static void Style(Form f)
    {
        typeof(MainForm).GetMethod("ApplyFloatingTheme",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(RuntimeHelpers.GetUninitializedObject(typeof(MainForm)),new object[]{f});
        typeof(MainForm).Assembly.GetType("LealInfoPDV.SalesDialogTheme")!.GetMethod("Apply",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object?[]{f,"Futurista Azul",null});
    }
    [STAThread] static void Main()
    {
        Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","Runner descartável");
        CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("pt-BR");Application.EnableVisualStyles();Database.Initialize();
        Exec("INSERT INTO customers(name) VALUES('CLIENTE FICTICIO COMERCIAL')");
        Exec("INSERT INTO products(name,barcode,category,price,stock,active) VALUES('MATERIAL FICTICIO COMERCIAL','QA-COM-1','TESTE',39.90,7,1)");
        var stock=Scalar("SELECT SUM(stock) FROM products");var count=Scalar("SELECT COUNT(*) FROM sales");var customerCount=Scalar("SELECT COUNT(*) FROM customers");
        Directory.CreateDirectory("glass-commercial-evidencias");
        using var f=new GlassWorkshopForm(Style);f.Show();Pump();var tabs=Find<TabControl>(f,"GlassSections");
        Check(All(f).OfType<Button>().All(b=>b.GetType().Name=="SalesVisualButton"),"Mesmo componente 3D dos botões da venda");
        Check(All(f).Any(c=>c.GetType().Name=="SalesGlowLabel"),"Cabeçalho reutiliza glow da venda");
        Check(All(f).Any(c=>c.Name=="FieldFrameWidthMm"),"Campos com moldura de SalesVisuals.Frame");
        Find<Button>(f,"LoadQuoteCustomers").PerformClick();var customer=Find<ComboBox>(f,"QuoteCustomer");customer.SelectedIndex=Enumerable.Range(0,customer.Items.Count).Single(i=>customer.Items[i]!.ToString()=="CLIENTE FICTICIO COMERCIAL");
        tabs.SelectedIndex=2;Find<NumericUpDown>(f,"WidthMm").Value=1200;Find<NumericUpDown>(f,"HeightMm").Value=1000;Find<NumericUpDown>(f,"Quantity").Value=2;
        tabs.SelectedIndex=3;Find<NumericUpDown>(f,"QuotePrice").Value=100;Find<NumericUpDown>(f,"QuoteServiceValue").Value=50;Find<NumericUpDown>(f,"QuoteDiscount").Value=10;Find<NumericUpDown>(f,"QuoteAddition").Value=5;
        string total=Find<TextBox>(f,"QuoteSummary").Text;
        tabs.SelectedIndex=1;Find<TextBox>(f,"MaterialQuery").Text="QA-COM-1";Find<Button>(f,"FindMaterial").PerformClick();Pump();
        var found=Find<DataGridView>(f,"MaterialResults");Check(found.Rows.Count==1,"Busca no cadastro existente");
        Check(found.Columns.Cast<DataGridViewColumn>().All(c=>c is DataGridViewTextBoxColumn),"Grade sem incompatibilidade texto/imagem");
        Find<Button>(f,"LinkMaterial").PerformClick();Find<Button>(f,"LinkMaterial").PerformClick();Pump();
        var linked=Find<DataGridView>(f,"ProjectMaterials");Check(linked.Rows.Count==1,"Vínculo único por ID de produto");
        Check(Find<TextBox>(f,"QuoteSummary").Text==total,"Material informativo não muda cálculo aprovado");
        var project=typeof(GlassWorkshopForm).GetField("project",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(f)!;
        var mat=new GlassMaterialLink(Convert.ToInt64(Scalar("SELECT id FROM products WHERE barcode='QA-COM-1'")),"MATERIAL FICTICIO COMERCIAL","QA-COM-1","TESTE",39.90m);
        var snapshot=(GlassCommercialSnapshot)typeof(GlassProjectForm).GetMethod("CommercialSnapshot",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(project,new object[]{new[]{mat}})!;
        Check(snapshot.CustomerName=="CLIENTE FICTICIO COMERCIAL"&&snapshot.Quote.Total==285m,"Documento recebe cliente e total reais do projeto");
        var lines=GlassCommercialDocument.Lines(snapshot);Check(lines.Any(x=>x.Contains("NÃO É PAGAMENTO")),"Orçamento sem falsa quitação");
        var pdf=GlassCommercialDocument.PdfBytes(snapshot);Check(Encoding.ASCII.GetString(pdf.Take(8).ToArray()).StartsWith("%PDF-1.4"),"PDF local efetivo");
        File.WriteAllBytes("glass-commercial-evidencias/orcamento-real.pdf",pdf);
        using(var page=GlassCommercialDocument.RenderPage(snapshot)){Check(page.Width==1588&&page.Height==2246,"A4 comercial em resolução ampliada");page.Save("glass-commercial-evidencias/orcamento.png",ImageFormat.Png);}
        var longDoc=snapshot with {Materials=Enumerable.Range(1,55).Select(i=>new GlassMaterialLink(i,new string('W',180)+" ÁÉÍÓÚ material "+i,"COD","TESTE",1)).ToArray()};
        Check(GlassCommercialDocument.PageCount(longDoc)>1,"Paginação para descrições e listas longas");
        File.WriteAllBytes("glass-commercial-evidencias/orcamento-multipagina.pdf",GlassCommercialDocument.PdfBytes(longDoc));
        using(var font=new Font("Segoe UI",15,FontStyle.Regular,GraphicsUnit.Pixel)) foreach(var line in GlassCommercialDocument.Lines(longDoc))Check(TextRenderer.MeasureText(line,font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix).Width<=690,"Linha comercial sem corte");
        tabs.SelectedIndex=6;Check(!Find<Button>(f,"GlassReceiptPending").Enabled,"Recibo bloqueado sem pagamento/venda");
        bool preview=false;
        using(var timer=new System.Windows.Forms.Timer{Interval=50})
        {
            timer.Tick+=(_,_)=>{var modal=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="ORÇAMENTO COMERCIAL — VIDRAÇARIA");if(modal==null)return;timer.Stop();preview=true;Check(modal.Owner==f&&modal.Modal,"Prévia comercial modal real");Check(All(modal).OfType<PictureBox>().Any(p=>p.Name=="CommercialPage"&&p.Image!=null),"Página comercial renderizada na prévia");modal.Close();};
            timer.Start();Find<Button>(f,"CommercialPreview").PerformClick();Check(preview,"Botão comercial abre documento");
        }
        foreach(TabPage page in tabs.TabPages)
        {
            tabs.SelectedTab=page;Pump();
            using(var strip=new Bitmap(tabs.Width,tabs.Height))
            {
                tabs.DrawToBitmap(strip,new Rectangle(Point.Empty,strip.Size));
                for(int i=0;i<tabs.TabPages.Count;i++)
                {
                    var r=tabs.GetTabRect(i);int pixels=0;
                    Check(TextRenderer.MeasureText(tabs.TabPages[i].Text,tabs.Font).Width<=r.Width-4,"Título da aba cabe: "+tabs.TabPages[i].Text);
                    for(int x=r.Left+5;x<r.Right-5;x++)for(int y=r.Top+r.Height/2-4;y<r.Top+r.Height/2+5;y++)
                    {var c=strip.GetPixel(x,y);if(c.R>190&&c.G>190&&c.B>190)pixels++;}
                    Check(pixels>5,"Título desenhado na própria aba: "+tabs.TabPages[i].Text);
                }
            }
            using var image=new Bitmap(f.Width,f.Height);f.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save("glass-commercial-evidencias/aba-"+page.Text+".png",ImageFormat.Png);
        }
        tabs.SelectedIndex=1;Find<Button>(f,"RemoveMaterialLink").PerformClick();Check(linked.Rows.Count==0,"Remover somente vínculo em memória");
        Check(Equals(Scalar("SELECT SUM(stock) FROM products"),stock)&&Equals(Scalar("SELECT COUNT(*) FROM sales"),count)&&Equals(Scalar("SELECT COUNT(*) FROM customers"),customerCount),"Estoque, vendas e clientes preservados");
        Check(!All(f).OfType<Button>().Any(b=>b.Text.Contains("WHATS",StringComparison.OrdinalIgnoreCase)),"Sem WhatsApp ou compartilhamento");
        f.Close();Console.WriteLine("PASS COMERCIAL: fonte real, materiais, PDF multipágina, prévia, sem pagamento fictício e dados preservados");
    }
}
