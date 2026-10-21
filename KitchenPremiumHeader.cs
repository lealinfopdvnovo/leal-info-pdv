using System.Drawing.Drawing2D;
namespace LealInfoPDV;

internal sealed class KitchenPremiumHeader : UserControl
{
    private readonly Label pending;
    private readonly Label preparing;
    private readonly Label ready;
    private readonly Label clock;
    private readonly System.Windows.Forms.Timer timer=new(){Interval=1000};
    internal event Action? ImportRequested;
    internal event Action? RefreshRequested;
    internal event Action? SettingsRequested;
    internal event Action? ExitRequested;
    internal KitchenPremiumHeader()
    {
        Dock=DockStyle.Top;Height=104;
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Padding=new Padding(8)};
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,34));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,28));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var brand=new KitchenHeaderBrand{Name="KitchenHeaderBrand",Dock=DockStyle.Fill};
        brand.Paint+=(_,e)=>{
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            int middle=brand.Width/2;
            int h=brand.ClientSize.Height;
            var upper=new Rectangle(4,4,Math.Max(1,middle-8),Math.Max(1,h*55/100-4));
            var lower=new Rectangle(4,h*55/100,Math.Max(1,middle-8),Math.Max(1,h*40/100));
            float pixels=Math.Min(h*.34f,brand.Width*.048f);
            KitchenPremiumStyle.DrawFittedText(g,"LEAL INFO",upper,KitchenPremiumStyle.Gold,pixels);
            KitchenPremiumStyle.DrawFittedText(g,"PDV PRO",lower,Color.White,h*.17f);
            using var edge=new Pen(Color.FromArgb(90,KitchenPremiumStyle.Gold));g.DrawLine(edge,middle,8,middle,h-8);
            upper.Offset(middle,0);lower.Offset(middle,0);
            KitchenPremiumStyle.DrawFittedText(g,"COZINHA",upper,KitchenPremiumStyle.Gold,pixels);
            KitchenPremiumStyle.DrawFittedText(g,"PEDIDOS EM TEMPO REAL",lower,Color.White,h*.15f);
        };
        var stats=new TableLayoutPanel{Name="KitchenLiveCounters",Dock=DockStyle.Fill,ColumnCount=4,RowCount=1};
        for(int i=0;i<4;i++)stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        stats.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        Label Badge()=>new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",11,FontStyle.Bold),Margin=new Padding(3),BorderStyle=BorderStyle.FixedSingle};
        clock=Badge();pending=Badge();preparing=Badge();ready=Badge();
        stats.Controls.Add(clock,0,0);stats.Controls.Add(pending,1,0);stats.Controls.Add(preparing,2,0);stats.Controls.Add(ready,3,0);
        var actions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Padding=new Padding(0,4,0,0)};
        for(int i=0;i<4;i++)actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        foreach(var action in new[]{"IMPORTAR\nPDV","ATUALIZAR","MESAS /\nCONFIG.","SAIR"})
        {
            var b=new Button{Text=action,Dock=DockStyle.Fill,Margin=new Padding(2)};
            b.Click+=(_,_)=>{if(action.StartsWith("IMPORTAR"))ImportRequested?.Invoke();else if(action=="ATUALIZAR")RefreshRequested?.Invoke();else if(action=="SAIR")ExitRequested?.Invoke();else SettingsRequested?.Invoke();};
            actions.Controls.Add(b);
        }
        root.Controls.Add(brand,0,0);root.Controls.Add(stats,1,0);root.Controls.Add(actions,2,0);Controls.Add(root);
        KitchenPremiumStyle.Apply(this);KitchenPremiumStyle.Frame(this);
        clock.Text=DateTime.Now.ToString("HH:mm")+Environment.NewLine+DateTime.Now.ToString("dd/MM/yy");
        SizeChanged+=(_,_)=>{
            float size=Width>=1200?11:8;
            foreach(var badge in new[]{clock,pending,preparing,ready})badge.Font=new Font("Segoe UI",size,FontStyle.Bold);
            foreach(Button action in actions.Controls)action.Font=new Font("Segoe UI",Width>=1200?8.5f:7.5f,FontStyle.Bold);
        };
        timer.Tick+=(_,_)=>clock.Text=DateTime.Now.ToString("HH:mm")+Environment.NewLine+DateTime.Now.ToString("dd/MM/yyyy");timer.Start();
    }
    internal void UpdateCounts(int waiting,int cooking,int done)
    {
        if(InvokeRequired){Invoke((Action)(()=>UpdateCounts(waiting,cooking,done)));return;}
        pending.Text=waiting.ToString("00")+Environment.NewLine+"PENDENTES";preparing.Text=cooking.ToString("00")+Environment.NewLine+"PREPARANDO";ready.Text=done.ToString("00")+Environment.NewLine+"PRONTOS";
        pending.ForeColor=KitchenPremiumStyle.Gold;preparing.ForeColor=KitchenTableButton.StateColor("PREPARANDO");ready.ForeColor=KitchenTableButton.StateColor("LIVRE");
    }
    protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
}

internal sealed class KitchenMoreTablesButton : Button
{
    internal KitchenMoreTablesButton(){Text="VER MAIS\nMESAS";Name="MoreKitchenTables";Dock=DockStyle.Fill;Cursor=Cursors.Hand;DoubleBuffered=true;}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        g.Clear(KitchenPremiumStyle.Background);
        var box=new Rectangle(5,6,Math.Max(1,Width-11),Math.Max(1,Height-13));
        using var shape=KitchenPremiumStyle.Round(box,10);
        using var fill=new LinearGradientBrush(box,Color.FromArgb(107,76,12),Color.FromArgb(15,13,5),90f);
        g.FillPath(fill,shape);
        using(var glow=new Pen(Color.FromArgb(55,KitchenPremiumStyle.Gold),7))g.DrawPath(glow,shape);
        using(var edge=new Pen(KitchenPremiumStyle.Gold,2))g.DrawPath(edge,shape);
        int x=Width/2-18,y=Math.Max(20,Height/3-20);
        using var gold=new SolidBrush(KitchenPremiumStyle.Gold);
        foreach(int dx in new[]{0,21})foreach(int dy in new[]{0,21})g.FillRectangle(gold,x+dx,y+dy,15,15);
        using var font=new Font("Segoe UI",11,FontStyle.Bold);
        KitchenPremiumStyle.DrawFittedText(g,Text,new Rectangle(8,y+53,Width-16,Math.Max(1,Height-y-65)),Color.FromArgb(255,235,153),Math.Min(18,Width*.20f),true);
    }
}



// Redraw the whole brand after resize/DPI changes, so old centered glyphs cannot remain.
internal sealed class KitchenHeaderBrand : Panel
{
    internal KitchenHeaderBrand() { DoubleBuffered=true; ResizeRedraw=true; }
    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(KitchenPremiumStyle.Background);
}
