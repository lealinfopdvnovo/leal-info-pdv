using System.Drawing.Drawing2D;
using System.Drawing.Printing;
namespace LealInfoPDV;

// One native drawing plan is shared by PrintDocument, preview and QA rendering.
internal sealed class ServiceNotePrint : IDisposable
{
    internal const int PageWidth=827, PageHeight=1169;
    private const float Left=50, Width=727, Bottom=1110;
    private readonly ServiceNoteRecord _note;
    private readonly Dictionary<string,string> _company;
    private readonly Image? _logo;
    private readonly Font _body=new("Arial",12,FontStyle.Regular,GraphicsUnit.Pixel);
    private readonly Font _bold=new("Arial",12,FontStyle.Bold,GraphicsUnit.Pixel);
    private readonly Font _title=new("Arial",20,FontStyle.Bold,GraphicsUnit.Pixel);
    private readonly List<List<Cell>> _pages=new();
    private sealed record Cell(RectangleF Bounds,string Text,Font Font,bool Border=false,bool Header=false,bool Right=false);
    internal int PageCount=>_pages.Count;
    internal IReadOnlyList<string> PageTexts=>_pages.Select(p=>string.Join("\n",p.Select(c=>c.Text))).ToList();
    internal bool HasLogo=>_logo!=null;
    internal ServiceNotePrint(ServiceNoteRecord note)
    {
        _note=note;_company=new(StringComparer.OrdinalIgnoreCase);
        using(var db=Database.Open())using(var cmd=db.CreateCommand())
        {cmd.CommandText="SELECT key,value FROM settings WHERE key LIKE 'company_%' OR key='pix_key'";using var r=cmd.ExecuteReader();while(r.Read())_company[r.GetString(0)]=r.IsDBNull(1)?"":r.GetString(1);}
        _logo=Licensing.CompanyBranding.LoadLogo(false);Build();
    }
    private string Company(string key)=>_company.GetValueOrDefault(key)??"";
    private void Build()
    {
        using var bitmap=new Bitmap(1,1);bitmap.SetResolution(100,100);using var g=Graphics.FromImage(bitmap);g.PageUnit=GraphicsUnit.Pixel;
        var y=50f;List<Cell> page=null!;
        void Add(float x,float top,float width,float height,string text,Font? font=null,bool border=false,bool header=false,bool right=false)=>page.Add(new(new(x,top,width,height),text,font??_body,border,header,right));
        void NewPage(bool first=false)
        {
            page=new();_pages.Add(page);y=50;
            if(first)
            {
                var x=_logo==null?Left:Left+125;var w=Width-(x-Left);
                var name=Company("company_trade_name");if(name.Length==0)name=Company("company_name");if(name.Length==0)name="EMPRESA";
                foreach(var line in Wrap(g,name,_title,w)){Add(x,y,w,25,line,_title);y+=25;}
                var lines=new[]{Company("company_name")!=name?Company("company_name"):"",Company("company_activity"),Company("company_document").Length>0?"CPF/CNPJ: "+Company("company_document"):"",Company("company_phone").Length>0?"Telefone: "+Company("company_phone"):"",CompanyAddress()};
                foreach(var line in lines.Where(s=>s.Length>0).SelectMany(s=>Wrap(g,s,_body,w))){Add(x,y,w,17,line);y+=17;}
                y=Math.Max(y,145)+10;
            }
            Add(Left,y,Width,32,"NOTA DE SERVIÇO",_title,border:true);y+=32;
            Add(Left,y,Width,25,$"Nº {_note.Id:000000}    •    {_note.Status}    •    Pedido: {_note.OrderDate:dd/MM/yyyy}",_bold,border:true);y+=32;
        }
        void Ensure(float height){if(y+height>Bottom)NewPage();}
        void Field(string label,string text)
        {
            var lines=Wrap(g,label+text,_body,Width-16);
            foreach(var line in lines){Ensure(25);Add(Left,y,Width,25,line,border:true);y+=25;}
        }
        NewPage(true);
        Field("CLIENTE: ",_note.Detail("name"));Field("CPF/CNPJ: ",_note.Detail("document"));Field("PRODUTO/EQUIPAMENTO: ",_note.Equipment);
        Field("ENDEREÇO: ",_note.Address);Field("REFERÊNCIA: ",_note.Detail("reference"));Field("TELEFONE: ",_note.Detail("phone"));Field("FORMA DE PAGAMENTO: ",_note.Payment);y+=12;
        var widths=new[]{66f,365f,60f,118f,118f};
        void TableHeader()
        {
            Ensure(27);float x=Left;var texts=new[]{"CÓD.","DISCRIMINAÇÃO","QTD.","VALOR UNIT.","TOTAL"};
            for(var i=0;i<5;i++){Add(x,y,widths[i],27,texts[i],_bold,true,true);x+=widths[i];}y+=27;
        }
        TableHeader();
        foreach(var item in _note.Items)
        {
            var texts=new[]{item.Code,item.Description,item.Quantity.ToString("0.####",ServiceNote.Brazilian),ServiceNote.Money(item.UnitCents),ServiceNote.Money(item.TotalCents)};
            var lines=texts.Select((s,i)=>Wrap(g,s,_body,widths[i]-12)).ToArray();var count=lines.Max(l=>l.Count);var offset=0;
            while(offset<count)
            {
                if(y+28>Bottom){NewPage();TableHeader();}
                var capacity=Math.Max(1,(int)((Bottom-y-10)/17));var take=Math.Min(count-offset,capacity);var height=Math.Max(28,take*17+10);float x=Left;
                for(var i=0;i<5;i++){var text=string.Join("\n",lines[i].Skip(offset).Take(take));Add(x,y,widths[i],height,text,border:true,right:i>=2);x+=widths[i];}
                y+=height;offset+=take;
            }
        }
        // Ruled space follows the paper form for short notes, without forcing blank extra pages.
        for(var i=_note.Items.Count;i<5 && y+28+200<Bottom;i++) {float x=Left;foreach(var w in widths){Add(x,y,w,28,"",border:true);x+=w;}y+=28;}
        Ensure(42);Add(Left,y,Width,34,"TOTAL GERAL: "+ServiceNote.Money(_note.TotalCents),_bold,true,right:true);y+=46;
        Field("SERVIÇO FINALIZADO EM: ",_note.CompletedAt?.ToString("dd/MM/yyyy   HH:mm")??"____/____/________   ____:____");
        Field("GARANTIA: ",_note.Warranty);Field("VALOR PAGO: ",ServiceNote.Money(_note.PaidCents));
        Field("OBSERVAÇÕES: ",_note.Observations);
        if(Company("pix_key").Length>0)Field("PAGAMENTOS • PIX: ",Company("pix_key"));
        if(Company("company_footer").Length>0)Field("",Company("company_footer"));
        for(var i=0;i<_pages.Count;i++)_pages[i].Add(new(new(Left,1125,Width,22),$"Nota {_note.Id:000000}  •  Página {i+1} de {_pages.Count}",_body));
    }
    private string CompanyAddress()=>string.Join(", ",new[]{Company("company_address"),Company("company_district"),Company("company_city_state"),Company("company_zip").Length>0?"CEP "+Company("company_zip"):""}.Where(s=>s.Length>0));
    private static List<string> Wrap(Graphics g,string text,Font font,float width)
    {
        var result=new List<string>();
        foreach(var paragraph in text.Replace("\r","").Split('\n'))
        {
            var line="";
            foreach(var word in paragraph.Split(' ',StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate=line.Length==0?word:line+" "+word;
                if(g.MeasureString(candidate,font).Width<=width){line=candidate;continue;}
                if(line.Length>0){result.Add(line);line="";}
                foreach(var c in word)
                {if(line.Length>0&&g.MeasureString(line+c,font).Width>width){result.Add(line);line="";}line+=c;}
            }
            result.Add(line);
        }
        return result.Count==0?new(){""}:result;
    }
    internal void DrawPage(Graphics graphics,int index,float hardMarginX=0,float hardMarginY=0)
    {
        var state=graphics.Save();graphics.PageUnit=GraphicsUnit.Pixel;
        graphics.ScaleTransform(graphics.DpiX/100f,graphics.DpiY/100f);graphics.TranslateTransform(-hardMarginX,-hardMarginY);
        graphics.SmoothingMode=SmoothingMode.HighQuality;graphics.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        if(index==0&&_logo!=null)
        {var scale=Math.Min(110f/_logo.Width,95f/_logo.Height);graphics.DrawImage(_logo,new RectangleF(Left,50,_logo.Width*scale,_logo.Height*scale));}
        using var pen=new Pen(Color.FromArgb(65,65,65),.8f);using var headerBrush=new SolidBrush(Color.FromArgb(235,235,235));
        foreach(var cell in _pages[index])
        {
            if(cell.Header)graphics.FillRectangle(headerBrush,cell.Bounds);
            if(cell.Border)graphics.DrawRectangle(pen,cell.Bounds.X,cell.Bounds.Y,cell.Bounds.Width,cell.Bounds.Height);
            using var format=new StringFormat{Alignment=cell.Right?StringAlignment.Far:StringAlignment.Near,LineAlignment=StringAlignment.Near,Trimming=StringTrimming.None,FormatFlags=StringFormatFlags.NoWrap};
            graphics.DrawString(cell.Text,cell.Font,Brushes.Black,new RectangleF(cell.Bounds.X+6,cell.Bounds.Y+5,cell.Bounds.Width-12,cell.Bounds.Height-7),format);
        }
        graphics.Restore(state);
    }
    internal PrintDocument CreateDocument()
    {
        var document=new PrintDocument{DocumentName=$"Nota de Serviço {_note.Id:000000}",OriginAtMargins=false};
        document.DefaultPageSettings.PaperSize=new PaperSize("A4",PageWidth,PageHeight);document.DefaultPageSettings.Landscape=false;document.DefaultPageSettings.Margins=new Margins(50,50,50,50);
        var index=0;document.BeginPrint+=(_,_)=>index=0;
        document.PrintPage+=(_,e)=>{DrawPage(e.Graphics!,index,e.PageSettings.HardMarginX,e.PageSettings.HardMarginY);e.HasMorePages=++index<_pages.Count;};return document;
    }
    internal void Preview(IWin32Window owner)
    {using var document=CreateDocument();using var dialog=new PrintPreviewDialog{Document=document,Width=1000,Height=800,UseAntiAlias=true};dialog.ShowDialog(owner);}
    internal void Print(IWin32Window owner)
    {
        using var document=CreateDocument();using var dialog=new PrintDialog{Document=document,UseEXDialog=true,AllowSomePages=false,AllowSelection=false};
        if(dialog.ShowDialog(owner)!=DialogResult.OK)return;
        document.DefaultPageSettings.PaperSize=new PaperSize("A4",PageWidth,PageHeight);document.DefaultPageSettings.Landscape=false;document.Print();
    }
    internal void RenderPages(string directory)
    {
        Directory.CreateDirectory(directory);
        for(var i=0;i<_pages.Count;i++){using var bitmap=new Bitmap(PageWidth*2,PageHeight*2);bitmap.SetResolution(200,200);using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.White);DrawPage(g,i);}bitmap.Save(Path.Combine(directory,$"nota-{_note.Id:000000}-pagina-{i+1}.png"),System.Drawing.Imaging.ImageFormat.Png);}
    }
    public void Dispose(){_logo?.Dispose();_body.Dispose();_bold.Dispose();_title.Dispose();}
}
