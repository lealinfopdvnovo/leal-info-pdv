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
        Dock=DockStyle.Top;Height=86;
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Padding=new Padding(8)};
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,34));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,28));
        var brand=new Panel{Dock=DockStyle.Fill};
        brand.Paint+=(_,e)=>{
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            int middle=brand.Width/2;
            using var title=new Font("Segoe UI",brand.Width>450?20:15,FontStyle.Bold);
            using var small=new Font("Segoe UI",9,FontStyle.Bold);
            TextRenderer.DrawText(g,"LEAL INFO",title,new Rectangle(4,5,middle-8,35),KitchenPremiumStyle.Gold,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g,"PDV PRO",small,new Rectangle(4,42,middle-8,22),Color.White,TextFormatFlags.HorizontalCenter);
            using var edge=new Pen(Color.FromArgb(90,KitchenPremiumStyle.Gold));g.DrawLine(edge,middle,8,middle,brand.Height-8);
            TextRenderer.DrawText(g,"COZINHA",title,new Rectangle(middle+4,5,brand.Width-middle-8,35),KitchenPremiumStyle.Gold,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g,"PEDIDOS EM TEMPO REAL",small,new Rectangle(middle+4,42,brand.Width-middle-8,22),Color.White,TextFormatFlags.HorizontalCenter);
        };
        var stats=new TableLayoutPanel{Name="KitchenLiveCounters",Dock=DockStyle.Fill,ColumnCount=4,RowCount=1};
        for(int i=0;i<4;i++)stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        Label Badge()=>new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",11,FontStyle.Bold),Margin=new Padding(3),BorderStyle=BorderStyle.FixedSingle};
        clock=Badge();pending=Badge();preparing=Badge();ready=Badge();
        stats.Controls.Add(clock,0,0);stats.Controls.Add(pending,1,0);stats.Controls.Add(preparing,2,0);stats.Controls.Add(ready,3,0);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Padding=new Padding(0,4,0,0)};
        foreach(var action in new[]{"IMPORTAR\nPDV","ATUALIZAR","MESAS /\nCONFIG.","SAIR"})
        {
            var b=new Button{Text=action,Width=64,Height=56,Margin=new Padding(2)};
            b.Click+=(_,_)=>{if(action.StartsWith("IMPORTAR"))ImportRequested?.Invoke();else if(action=="ATUALIZAR")RefreshRequested?.Invoke();else if(action=="SAIR")ExitRequested?.Invoke();else SettingsRequested?.Invoke();};
            actions.Controls.Add(b);
        }
        root.Controls.Add(brand,0,0);root.Controls.Add(stats,1,0);root.Controls.Add(actions,2,0);Controls.Add(root);
        KitchenPremiumStyle.Apply(this);KitchenPremiumStyle.Frame(this);
        clock.Text=DateTime.Now.ToString("HH:mm")+Environment.NewLine+DateTime.Now.ToString("dd/MM/yyyy");
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
        TextRenderer.DrawText(g,Text,font,new Rectangle(4,y+53,Width-8,62),Color.FromArgb(255,235,153),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);
    }
}
