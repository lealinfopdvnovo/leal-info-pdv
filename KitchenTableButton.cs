using System.Drawing.Drawing2D;
namespace LealInfoPDV;

// Native drawing avoids external icon files and preserves state colors at any DPI.
internal sealed class KitchenTableButton : Button
{
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
        using var shape = KitchenPremiumStyle.Round(box, 10);
        using var fill = new LinearGradientBrush(box, Color.FromArgb(accent.R / 7, accent.G / 7, accent.B / 7), Color.FromArgb(3, 9, 12), 90f);
        g.Clear(KitchenPremiumStyle.Background); g.FillPath(fill, shape);
        for (int glow = 5; glow >= 1; glow--)
        {
            using var pen = new Pen(Color.FromArgb(14 + (5 - glow) * 8, accent), glow + 1);
            g.DrawPath(pen, shape);
        }
        using (var border = new Pen(accent, Focused ? 2.4f : 1.3f)) g.DrawPath(border, shape);
        float scale = Math.Min((Width - 16) / 100f, Height / 110f);
        var saved = g.Save(); g.TranslateTransform(Width / 2f, Math.Max(9, Height * .07f)); g.ScaleTransform(scale, scale);
        if (state == "RESERVADA")
        {
            using var pen = new Pen(accent, 3);
            g.DrawRectangle(pen, -21, 2, 42, 33); g.DrawLine(pen, -21, 12, 21, 12);
            g.DrawLine(pen, -11, -2, -11, 7); g.DrawLine(pen, 11, -2, 11, 7);
            using var font = new Font("Segoe UI", 16, FontStyle.Bold);
            g.DrawString("R", font, Brushes.White, new PointF(-10, 11));
        }
        else
        {
            using var chair = new SolidBrush(ControlPaint.Dark(accent, .45f));
            using var edge = new Pen(Color.FromArgb(130, accent), 1.5f);
            foreach (int x in new[] { -32, 22 }) { g.FillRectangle(chair, x, 4, 12, 23); g.DrawRectangle(edge, x, 4, 12, 23); }
            foreach (int x in new[] { -30, 21 }) { g.FillRectangle(chair, x, 34, 14, 14); g.DrawRectangle(edge, x, 34, 14, 14); }
            PointF[] top = { new(-32, 19), new(0, 7), new(34, 19), new(1, 33) };
            using var tabletop = new LinearGradientBrush(new Rectangle(-32, 7, 66, 26), ControlPaint.Light(accent, .75f), ControlPaint.Dark(accent, .2f), 75f);
            g.FillPolygon(tabletop, top); using var topEdge = new Pen(Color.FromArgb(210, Color.White), 1); g.DrawPolygon(topEdge, top);
            using var legs = new Pen(ControlPaint.Light(accent, .5f), 3);
            g.DrawLine(legs, -25, 24, -22, 44); g.DrawLine(legs, 26, 25, 23, 45); g.DrawLine(legs, 1, 33, 1, 51);
        }
        g.Restore(saved);
        bool showAge=since.HasValue && Height>150;
        int titleY = (int)(Height * (showAge ? .43f : .49f));
        int numberHeight = Math.Max(1,(int)(Height*(showAge ? .22f : .27f)));
        int stateHeight = Math.Max(1,(int)(Height*(showAge ? .14f : .18f)));
        KitchenPremiumStyle.DrawFittedText(g,number.ToString("00"),new Rectangle(6,titleY,Width-12,numberHeight),Color.White,numberHeight*.86f);
        KitchenPremiumStyle.DrawFittedText(g,state,new Rectangle(6,titleY+numberHeight,Width-12,stateHeight),accent,stateHeight*.82f);
        if (showAge)
        {
            var age = DateTimeOffset.UtcNow - since.Value; string elapsed = age.TotalHours >= 1 ? $"{(int)age.TotalHours:00}:{age.Minutes:00}" : $"{Math.Max(0, (int)age.TotalMinutes):00}:{age.Seconds:00}";
            KitchenPremiumStyle.DrawFittedText(g,elapsed,new Rectangle(6,Height-stateHeight-3,Width-12,stateHeight),accent,stateHeight*.75f);
        }
    }
}

