using System.Drawing.Printing;

namespace LealInfoPDV;

public static class GlassCutPlanPrint
{
    private static IReadOnlyList<string> Inventory(GlassCutResult result)
    {
        var lines = new List<string>();
        foreach (var p in result.Sheets.SelectMany(s => s.Pieces).Select(p => p.Piece).Concat(result.Rejected.Select(r => r.Piece)).Distinct())
        {
            Wrap($"{p.Id} • {p.WidthMm:0.##} × {p.HeightMm:0.##} mm • {p.Quantity} un. • 90° por peça: {(p.AllowRotation ? "permitida se habilitada" : "não")}");
            Wrap("Descrição: " + p.Description); Wrap("Material: " + (p.Material == "" ? "não informado" : p.Material));
        }
        foreach (var r in result.Rejected) Wrap($"NÃO ACOMODADA: {r.Piece.Id} — {r.Reason}");
        return lines;
        void Wrap(string text)
        {
            using var font = new Font("Segoe UI", 14, FontStyle.Regular, GraphicsUnit.Pixel);
            while (TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width > 680)
            {
                int fit = Math.Min(80, text.Length);
                while (fit > 1 && TextRenderer.MeasureText(text[..fit], font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width > 680) fit--;
                if (char.IsHighSurrogate(text[fit - 1])) fit--;
                int split = text.LastIndexOf(' ', fit - 1); if (split < 1) split = fit;
                lines.Add(text[..split]); text = text[split..].TrimStart();
            }
            lines.Add(text);
        }
    }
    public static int PageCount(GlassCutResult result) => result.Sheets.Count + Math.Max(1, (Inventory(result).Count + 29) / 30);
    public static void DrawPage(Graphics g, RectangleF page, GlassCutContext context, GlassCutResult result, int index)
    {
        if (index < 0 || index >= PageCount(result)) throw new ArgumentOutOfRangeException(nameof(index));
        var state = g.Save();
        try
        {
            float scale = Math.Min(page.Width / GlassProjectPrint.PageWidth, page.Height / GlassProjectPrint.PageHeight);
            g.TranslateTransform(page.X, page.Y); g.ScaleTransform(scale, scale);
            g.FillRectangle(Brushes.White, 0, 0, GlassProjectPrint.PageWidth, GlassProjectPrint.PageHeight);
            using var title = new Font("Segoe UI", 22, FontStyle.Bold, GraphicsUnit.Pixel);
            using var body = new Font("Segoe UI", 14, FontStyle.Regular, GraphicsUnit.Pixel);
            using var ink = new SolidBrush(Color.FromArgb(4, 55, 94));
            g.DrawString("LEAL INFO PDV • PLANO GEOMÉTRICO", title, ink, 42, 34);
            g.DrawString("Projeto: " + context.Project.Description, body, ink, new RectangleF(42, 75, 700, 60));
            g.DrawString("Cliente: " + context.CustomerName, body, ink, new RectangleF(42, 136, 700, 60));
            if (index < result.Sheets.Count)
            {
                g.DrawString("Material: " + (result.Sheets[index].Material == "" ? "não informado" : result.Sheets[index].Material), body, ink, new RectangleF(42, 197, 700, 76));
                GlassCutPlanRenderer.Draw(g, new RectangleF(42, 274, 710, 452), result, index, body, ink.Color, true);
                g.DrawString("Identificações e medidas completas nas páginas de peças.", body, ink, 42, 747);
            }
            else
            {
                g.DrawString("PEÇAS DO PLANO • MEDIDAS INFORMADAS PELO PROFISSIONAL", body, ink, 42, 205);
                float y = 242;
                foreach (var line in Inventory(result).Skip((index - result.Sheets.Count) * 30).Take(30))
                { g.DrawString(line, body, Brushes.Black, 42, y); y += 19; }
            }
            string summary = $"Chapas: {result.Sheets.Count} • total: {result.TotalArea:0.######} m² • utilizada: {result.UsedArea:0.######} m²\nNão utilizada: {result.UnusedArea:0.######} m² • aproveitamento: {result.Utilization:0.##}% • desperdício: {result.Waste:0.##}%\nPeças não acomodadas: {result.RejectedQuantity}. Sobra não é cadastro de estoque.\nRotação 90° global: {(result.RotationAllowed ? "SIM" : "NÃO")}.";
            g.DrawString(summary, body, ink, new RectangleF(42, 857, 700, 85));
            g.DrawString(GlassCutPacking.Disclaimer, body, ink, new RectangleF(42, 950, 700, 115));
            g.DrawString($"SIMULAÇÃO • Página {index + 1} de {PageCount(result)}", body, ink, 42, 1080);
        }
        finally { g.Restore(state); }
    }
    public static Bitmap RenderPage(GlassCutContext context, GlassCutResult result, int index = 0)
    {
        var image = new Bitmap(GlassProjectPrint.PageWidth * 2, GlassProjectPrint.PageHeight * 2);
        try { using var g = Graphics.FromImage(image); DrawPage(g, new RectangleF(0, 0, image.Width, image.Height), context, result, index); return image; }
        catch { image.Dispose(); throw; }
    }
    public static void Preview(Form owner, GlassCutContext context, GlassCutResult result)
    {
        using var dialog = new Form { Text = "PRÉVIA A4 — PLANO DE CORTE", ClientSize = new Size(800, 730), MinimumSize = new Size(420, 360), AutoScaleMode = AutoScaleMode.Dpi, StartPosition = FormStartPosition.CenterParent };
        var picture = new PictureBox { Name = "CutPrintPage", Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom }; int index = 0;
        void RefreshPage() { var old = picture.Image; picture.Image = RenderPage(context, result, index); old?.Dispose(); }
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true };
        void Add(string title, Action action) { var b = new SalesVisualButton { Text = title, AutoSize = true, Height = 42, Padding = new Padding(12, 5, 12, 5) }; b.Click += (_, _) => action(); buttons.Controls.Add(b); }
        Add("ANTERIOR", () => { index = Math.Max(0, index - 1); RefreshPage(); }); Add("PRÓXIMA", () => { index = Math.Min(PageCount(result) - 1, index + 1); RefreshPage(); });
        Add("IMPRIMIR PLANO A4", () => Print(dialog, context, result));
        var close = new SalesVisualButton { Text = "FECHAR", DialogResult = DialogResult.Cancel, AutoSize = true, Height = 42 }; close.Click += (_, _) => dialog.Close(); buttons.Controls.Add(close); dialog.CancelButton = close;
        dialog.Controls.Add(picture); dialog.Controls.Add(buttons); SalesDialogTheme.Apply(dialog, "Futurista Azul", null); RefreshPage();
        var area = Screen.FromControl(owner).WorkingArea; dialog.Size = new(Math.Min(dialog.Width, area.Width), Math.Min(dialog.Height, area.Height));
        try { dialog.ShowDialog(owner); } finally { picture.Image?.Dispose(); picture.Image = null; }
    }
    private static void Print(Form owner, GlassCutContext context, GlassCutResult result)
    {
        try
        {
            using var document = new PrintDocument { DocumentName = "Plano geométrico de corte" }; using var dialog = new PrintDialog { Document = document, UseEXDialog = true, AllowSomePages = false };
            if (dialog.ShowDialog(owner) != DialogResult.OK) return;
            var a4 = document.PrinterSettings.PaperSizes.Cast<PaperSize>().FirstOrDefault(p => p.Kind == PaperKind.A4) ?? throw new InvalidOperationException("Papel A4 não disponível.");
            document.DefaultPageSettings.PaperSize = a4; document.DefaultPageSettings.Landscape = false;
            int page = 0; document.PrintPage += (_, e) => { DrawPage(e.Graphics!, e.MarginBounds, context, result, page++); e.HasMorePages = page < PageCount(result); }; document.Print();
        }
        catch (Exception) { MessageBox.Show(owner, "Não foi possível imprimir. Confira a impressora e o papel A4.", "Plano de corte"); }
    }
}
