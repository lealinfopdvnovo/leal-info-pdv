using System.Drawing.Printing;
using System.Globalization;
namespace LealInfoPDV;
internal sealed record GlassA4Content(string Title,string Subtitle,string Reference,IReadOnlyList<string> Lines);
internal static class GlassLinkedDocument
{
    private static string Provided(string value)=>string.IsNullOrWhiteSpace(value)?"Não informado":value;
    private static IEnumerable<string> Party(string role,GlassDocumentParty p)=>new[]{role+": "+Provided(p.Name),"Documento: "+Provided(p.Document),"Telefone: "+Provided(p.Phone)+" • E-mail: "+Provided(p.Email),"Endereço: "+Provided(p.Address)+(p.CityState.Length>0?" • "+p.CityState:"")};
    private static IEnumerable<string> Project(GlassDocumentContext c)
    {
        var p=c.Project;return new[]{"Projeto: "+p.Description+" • ID "+c.ProjectId+" • revisão "+c.ProjectRevision,"Modelo: "+p.Model,"Serviço: "+Provided(p.Quote.Service),$"Medidas: {GlassDimensionUnits.Convert(p.WidthMm,"mm",p.WidthUnit).ToString("0.#####",GlassBrazilianMoney.Culture)} {p.WidthUnit} × {GlassDimensionUnits.Convert(p.HeightMm,"mm",p.HeightUnit).ToString("0.#####",GlassBrazilianMoney.Culture)} {p.HeightUnit} • quantidade {p.Quantity}",$"Dimensões físicas: {p.WidthMm.ToString("0.#####",GlassBrazilianMoney.Culture)} × {p.HeightMm.ToString("0.#####",GlassBrazilianMoney.Culture)} mm"};
    }
    internal static GlassA4Content Receipt(GlassReceiptSource s)
    {
        if(s.Amount<=0||s.MovementId<=0)throw new ArgumentException("Recebimento inválido.");
        var rows=new List<string>();rows.AddRange(Party("Empresa",s.Context.Company));rows.Add("");rows.AddRange(Party("Cliente (ID "+s.Context.Customer.Id+")",s.Context.Customer));rows.Add("");rows.AddRange(Project(s.Context));rows.Add("");
        rows.Add("VALOR RECEBIDO NESTE LANÇAMENTO: "+GlassBrazilianMoney.Format(s.Amount));rows.Add("Data/hora do recebimento: "+s.ReceivedAt.ToString("dd/MM/yyyy HH:mm:ss",GlassBrazilianMoney.Culture));rows.Add("Referência financeira: cash_movements ID "+s.MovementId);rows.Add("Descrição do lançamento: "+s.Description);rows.Add("Pagamento: "+(s.PaymentMethod.Length>0?s.PaymentMethod:"Forma não registrada neste lançamento."));rows.Add("");rows.Add("Este recibo corresponde exclusivamente ao recebimento acima. Não declara quitação de outros valores do projeto.");rows.Add("Emitir ou reimprimir este documento não cria nova movimentação financeira.");
        return new("RECIBO DE RECEBIMENTO — VIDRAÇARIA","Recebimento registrado no Fluxo de Caixa do PDV","VID-REC-"+s.MovementId,rows);
    }
    internal static GlassA4Content Contract(GlassStoredContract s)
    {
        var c=s.Content;var rows=new List<string>{"Contrato: "+s.ProjectId+" • revisão "+s.Revision+" • "+s.State,"Salvo em: "+DateTime.Parse(s.CreatedUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind).ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss",GlassBrazilianMoney.Culture),"Hash do conteúdo: "+s.ContentHash};rows.AddRange(Party("Empresa",c.Context.Company));rows.Add("");rows.AddRange(Party("Cliente (ID "+c.Context.Customer.Id+")",c.Context.Customer));rows.Add("");rows.AddRange(Project(c.Context));rows.Add("Valor comercial capturado: "+GlassBrazilianMoney.Format(c.Context.CommercialValue));rows.Add("Condição financeira capturada: "+c.Context.FinancialCondition);rows.Add("");
        var v=c.Clauses;foreach(var item in new[]{("OBJETO DO SERVIÇO",v.Object),("VALOR",v.Value),("CONDIÇÃO DE PAGAMENTO",v.Payment),("PRAZO",v.Deadline),("INSTALAÇÃO",v.Installation),("RESPONSABILIDADES",v.Responsibilities),("OBSERVAÇÕES",v.Notes)}){rows.Add(item.Item1);rows.Add(Provided(item.Item2));rows.Add("");}
        rows.Add("CONTRATO NÃO COMPROVA PAGAMENTO. Recebimentos são registrados exclusivamente no financeiro do PDV.");
        return new("CONTRATO COMERCIAL — VIDRAÇARIA",s.State+" • conteúdo comercial revisado pelo operador",$"VID-CON-{s.ProjectId}-R{s.Revision}",rows);
    }
    internal static IReadOnlyList<string> Lines(GlassA4Content c)
    {
        var lines=new List<string>();using var font=new Font("Segoe UI",15,FontStyle.Regular,GraphicsUnit.Pixel);
        foreach(var block in c.Lines)foreach(var raw in block.Replace("\r\n","\n").Replace('\r','\n').Split('\n'))
        {
            string text=raw;while(TextRenderer.MeasureText(text,font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix).Width>690)
            {int fit=Math.Min(78,text.Length);while(fit>1 && TextRenderer.MeasureText(text[..fit],font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix).Width>690)fit--;if(char.IsHighSurrogate(text[fit-1]))fit--;int split=text.LastIndexOf(' ',fit-1);if(split<1)split=fit;lines.Add(text[..split]);text=text[split..].TrimStart();}lines.Add(text);
        }return lines;
    }
    internal static int PageCount(GlassA4Content c)=>(Lines(c).Count+32)/33;
    internal static void DrawPage(Graphics g,RectangleF page,GlassA4Content c,int index)
    {
        int count=PageCount(c);if(index<0||index>=count)throw new ArgumentOutOfRangeException(nameof(index));var saved=g.Save();
        try{float scale=Math.Min(page.Width/GlassProjectPrint.PageWidth,page.Height/GlassProjectPrint.PageHeight);g.TranslateTransform(page.Left,page.Top);g.ScaleTransform(scale,scale);g.FillRectangle(Brushes.White,0,0,GlassProjectPrint.PageWidth,GlassProjectPrint.PageHeight);using var title=new Font("Segoe UI",23,FontStyle.Bold,GraphicsUnit.Pixel);using var body=new Font("Segoe UI",15,FontStyle.Regular,GraphicsUnit.Pixel);using var ink=new SolidBrush(Color.FromArgb(4,55,94));g.DrawString(c.Title,title,ink,42,42);g.DrawString(c.Subtitle,body,ink,42,80);g.DrawLine(Pens.SteelBlue,42,114,750,114);float y=142;foreach(var line in Lines(c).Skip(index*33).Take(33)){g.DrawString(line,body,Brushes.Black,42,y);y+=25;}g.DrawLine(Pens.SteelBlue,42,1010,750,1010);g.DrawString($"Documento • Página {index+1} de {count}",body,ink,42,1034);}
        finally{g.Restore(saved);}
    }
    internal static Bitmap RenderPage(GlassA4Content c,int index)
    {var image=new Bitmap(GlassProjectPrint.PageWidth*2,GlassProjectPrint.PageHeight*2);try{using var g=Graphics.FromImage(image);DrawPage(g,new(0,0,image.Width,image.Height),c,index);return image;}catch{image.Dispose();throw;}}
    internal static byte[] PdfBytes(GlassA4Content c)=>GlassCommercialDocument.PdfPages(PageCount(c),i=>RenderPage(c,i));
    internal static void Preview(Form owner,Func<GlassA4Content> source,Action<Form>? theme)
    {
        var content=source();using var f=new Form{Name="GlassLinkedDocumentPreview",Text=content.Title,Tag=content,ClientSize=new(780,730),MinimumSize=new(420,360),AutoScaleMode=AutoScaleMode.Dpi,StartPosition=FormStartPosition.CenterParent};
        var picture=new PictureBox{Name="GlassLinkedDocumentPage",Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom};int page=0;
        void Update(){var old=picture.Image;picture.Image=RenderPage(content,page);old?.Dispose();}
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,WrapContents=true,Padding=new(10)};
        void Button(string name,string text,Action click){var b=new SalesVisualButton{Name=name,Text=text,AutoSize=true,MinimumSize=new(120,42)};b.Click+=(_,_)=>{try{click();}catch(Exception e){MessageBox.Show(f,e.Message,content.Title,MessageBoxButtons.OK,MessageBoxIcon.Warning);}};actions.Controls.Add(b);}
        Button("LinkedDocumentPrevious","ANTERIOR",()=>{page=Math.Max(0,page-1);Update();});Button("LinkedDocumentNext","PRÓXIMA",()=>{page=Math.Min(PageCount(content)-1,page+1);Update();});
        Button("LinkedDocumentPdf","GERAR PDF",()=>{var live=source();using var save=new SaveFileDialog{Filter="Documento PDF (*.pdf)|*.pdf",FileName=live.Reference+".pdf"};if(save.ShowDialog(f)!=DialogResult.OK)return;string temp=save.FileName+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllBytes(temp,PdfBytes(live));File.Move(temp,save.FileName,true);}finally{if(File.Exists(temp))File.Delete(temp);}});
        Button("LinkedDocumentPrint","IMPRIMIR A4",()=>Print(f,source()));Button("LinkedDocumentClose","FECHAR",f.Close);f.Controls.Add(picture);f.Controls.Add(actions);picture.BringToFront();Update();theme?.Invoke(f);if(theme==null)SalesDialogTheme.Apply(f,"Futurista Azul",null);var area=Screen.FromControl(owner).WorkingArea;f.Size=new(Math.Min(f.Width,area.Width),Math.Min(f.Height,area.Height));try{f.ShowDialog(owner);}finally{picture.Image?.Dispose();picture.Image=null;}
    }
    private static void Print(Form owner,GlassA4Content content)
    {
        using var document=new PrintDocument{DocumentName=content.Title+" "+content.Reference};using var dialog=new PrintDialog{Document=document,UseEXDialog=true,AllowSomePages=false};if(dialog.ShowDialog(owner)!=DialogResult.OK)return;
        var a4=document.PrinterSettings.PaperSizes.Cast<PaperSize>().FirstOrDefault(p=>p.Kind==PaperKind.A4)??throw new InvalidOperationException("A impressora selecionada não disponibiliza papel A4.");document.DefaultPageSettings.PaperSize=a4;document.DefaultPageSettings.Landscape=false;int page=0;document.PrintPage+=(_,e)=>{DrawPage(e.Graphics!,e.MarginBounds,content,page++);e.HasMorePages=page<PageCount(content);};document.Print();
    }
}
