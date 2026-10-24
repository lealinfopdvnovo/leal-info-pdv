using System.Drawing.Drawing2D;
namespace LealInfoPDV;

// Native drawing avoids external icon files and preserves state colors at any DPI.
internal sealed class KitchenTableButton : Button
{
    private static readonly Lazy<Bitmap> Furniture=new(()=>
    {
        using var input=typeof(KitchenTableButton).Assembly.GetManifestResourceStream("Kitchen.TablesPremium")??throw new InvalidDataException("Ícones premium não encontrados.");
        using var source=Image.FromStream(input);return new Bitmap(source);
    });
    private readonly int number;
    private readonly string state;
    private readonly DateTimeOffset? since;
    internal KitchenTableButton(int number, string state, DateTimeOffset? since)
    {
        this.number = number; this.state = state; this.since = since;
        Name = "KitchenTable_" + number; Text = $"MESA {number:00}\n{state}";
        AccessibleName = $"Mesa {number}, {state}"; Cursor = Cursors.Hand;
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Margin = new Padding(5);
        DoubleBuffered = true; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
        BackColor = StateColor(state); ForeColor = Color.White;
    }
    internal static Color StateColor(string state) => state switch
    {
        "OCUPADA" => Color.FromArgb(255, 48, 52), "PREPARANDO" => Color.FromArgb(0, 163, 255),
        "PRONTA" => Color.FromArgb(255, 207, 38), "RESERVADA" => Color.FromArgb(191, 36, 255),
        _ => Color.FromArgb(0, 224, 103)
    };
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        Color accent = StateColor(state);
        var box = new Rectangle(4, 4, Math.Max(1, Width - 9), Math.Max(1, Height - 9));
        g.Clear(KitchenPremiumStyle.Background);
        KitchenLighting.Surface(g,box,accent);
        int slot=state switch{"OCUPADA"=>1,"PREPARANDO"=>2,"PRONTA"=>3,_=>0};
        var image=Furniture.Value;int cell=image.Width/2;
        int side=Math.Max(1,Math.Min(Width-8,(int)(Height*.69f)));
        var destination=new Rectangle((Width-side)/2,1,side,side);
        g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        g.DrawImage(image,destination,new Rectangle((slot%2)*cell,(slot/2)*cell,cell,cell),GraphicsUnit.Pixel);
        if(state=="RESERVADA")KitchenPremiumStyle.DrawFittedText(g,"R",new Rectangle(Width-28,6,22,22),accent,18);
        bool showAge=since.HasValue && Height>150;
        int titleY = (int)(Height * (showAge ? .55f : .62f));
        int numberHeight = Math.Max(18,(int)(Height*(showAge ? .18f : .20f)));
        int stateHeight = Math.Max(14,(int)(Height*(showAge ? .13f : .14f)));
        if(!showAge)titleY=Math.Min(titleY,Height-numberHeight-stateHeight-5);
        KitchenPremiumStyle.DrawFittedText(g,number.ToString("00"),new Rectangle(6,titleY,Width-12,numberHeight),Color.White,numberHeight*.86f);
        KitchenPremiumStyle.DrawFittedText(g,state,new Rectangle(6,titleY+numberHeight,Width-12,stateHeight),accent,stateHeight*.82f);
        if (showAge)
        {
            var age = DateTimeOffset.UtcNow - since.Value; string elapsed = age.TotalHours >= 1 ? $"{(int)age.TotalHours:00}:{age.Minutes:00}" : $"{Math.Max(0, (int)age.TotalMinutes):00}:{age.Seconds:00}";
            KitchenPremiumStyle.DrawFittedText(g,elapsed,new Rectangle(6,Height-stateHeight-3,Width-12,stateHeight),accent,stateHeight*.75f);
        }
    }
}


