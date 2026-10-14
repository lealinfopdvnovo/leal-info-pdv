using System.Drawing.Printing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;

namespace LealInfoPDV;

public sealed record GlassCommercialSnapshot(GlassQuote Quote, long CustomerId, string CustomerName,
    string ServiceDescription, string CompanyName, DateTime IssuedAt, IReadOnlyList<GlassMaterialLink> Materials);

public static class GlassCommercialDocument
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");
    public static IReadOnlyList<string> Lines(GlassCommercialSnapshot s)
    {
        if (s.Quote.ValidationError != null || s.CustomerId <= 0 || string.IsNullOrWhiteSpace(s.CustomerName))
            throw new ArgumentException("Projeto e cliente válidos são obrigatórios.");
        string Money(decimal v) => GlassBrazilianMoney.Format(v);
        var q = s.Quote; var p = q.Project;
        var rows = new List<string> {
            "Empresa: " + (string.IsNullOrWhiteSpace(s.CompanyName) ? "Não configurada" : s.CompanyName),
            "Data: " + s.IssuedAt.ToString("dd/MM/yyyy HH:mm", Pt), "Cliente: " + s.CustomerName,
            "Projeto: " + p.Description, "Modelo: " + GlassProject.ModelName,
            "Serviço: " + s.ServiceDescription,
            $"Medidas: {p.WidthMm.ToString("0.##", Pt)} x {p.HeightMm.ToString("0.##", Pt)} mm",
            "Quantidade: " + p.Quantity,
            "Área unitária: " + q.Area.ToString("0.00####", Pt) + " m²",
            "Área total: " + q.TotalArea.ToString("0.00####", Pt) + " m²",
            "Preço por m²: " + Money(q.PricePerSquareMeter), "Valor por área: " + Money(q.MaterialValue),
            "Serviço adicional (total): " + Money(q.ServiceValue), "Subtotal (área + serviço): " + Money(q.BaseValue), "Desconto: " + Money(q.Discount),
            "Acréscimo: " + Money(q.Addition), "TOTAL DO ORÇAMENTO: " + Money(q.Total), "",
            "Materiais vinculados (informativo; não compõem o preço acima):" };
        foreach (var m in s.Materials) rows.Add($"ID {m.ProductId} — {m.Name} — {m.Category} — preço cadastrado {Money(m.Price)}");
        if (s.Materials.Count == 0) rows.Add("Nenhum material vinculado.");
        rows.Add(""); rows.Add("ORÇAMENTO NÃO É PAGAMENTO. Este documento não é recibo de quitação.");
        rows.Add("Prévia sem registro financeiro. Fabricação/corte dependem de regras profissionais.");
        var wrapped = new List<string>();
        using var measureFont = new Font("Segoe UI", 15, FontStyle.Regular, GraphicsUnit.Pixel);
        foreach (var row in rows)
        {
            var text = row;
            while (TextRenderer.MeasureText(text, measureFont, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width > 690)
            {
                int fit = Math.Min(78, text.Length);
                while (fit > 1 && TextRenderer.MeasureText(text[..fit], measureFont, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width > 690) fit--;
                if (char.IsHighSurrogate(text[fit - 1])) fit--;
                int split = text.LastIndexOf(' ', fit - 1); if (split < 1) split = fit;
                wrapped.Add(text[..split]); text = text[split..].TrimStart();
            }
            wrapped.Add(text);
        }
        return wrapped;
    }
    public static int PageCount(GlassCommercialSnapshot s) => (Lines(s).Count + 32) / 33;
    public static void DrawPage(Graphics g, RectangleF page, GlassCommercialSnapshot s, int index)
    {
        var rows = Lines(s); int count = (rows.Count + 32) / 33;
        if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
        var saved = g.Save();
        try
        {
            float scale = Math.Min(page.Width / GlassProjectPrint.PageWidth, page.Height / GlassProjectPrint.PageHeight);
            g.TranslateTransform(page.Left, page.Top); g.ScaleTransform(scale, scale);
            g.FillRectangle(Brushes.White, 0, 0, GlassProjectPrint.PageWidth, GlassProjectPrint.PageHeight);
            using var title = new Font("Segoe UI", 23, FontStyle.Bold, GraphicsUnit.Pixel);
            using var body = new Font("Segoe UI", 15, FontStyle.Regular, GraphicsUnit.Pixel);
            using var ink = new SolidBrush(Color.FromArgb(4, 55, 94));
            g.DrawString("ORÇAMENTO COMERCIAL — VIDRAÇARIA", title, ink, 42, 42);
            g.DrawString("LEAL INFO PDV • NÃO COMPROVA PAGAMENTO", body, ink, 42, 80);
            g.DrawLine(Pens.SteelBlue, 42, 114, 750, 114);
            float y = 142;
            foreach (var line in rows.Skip(index * 33).Take(33))
            { g.DrawString(line, body, Brushes.Black, 42, y); y += 25; }
            g.DrawLine(Pens.SteelBlue, 42, 1010, 750, 1010);
            g.DrawString($"Orçamento comercial • Página {index + 1} de {count}", body, ink, 42, 1034);
        }
        finally { g.Restore(saved); }
    }
    public static Bitmap RenderPage(GlassCommercialSnapshot s, int index = 0)
    {
        var image = new Bitmap(GlassProjectPrint.PageWidth * 2, GlassProjectPrint.PageHeight * 2);
        try { using var g = Graphics.FromImage(image); DrawPage(g, new RectangleF(0, 0, image.Width, image.Height), s, index); return image; }
        catch { image.Dispose(); throw; }
    }
    public static byte[] PdfBytes(GlassCommercialSnapshot s) => PdfPages(PageCount(s), i => RenderPage(s, i));
    // O mesmo motor PDF aprovado serve aos documentos vinculados; conteúdo comercial permanece igual.
    internal static byte[] PdfPages(int pages, Func<int, Bitmap> renderPage)
    {
        var offsets = new List<long> { 0 };
        using var stream = new MemoryStream();
        void Text(string value) { var data = Encoding.ASCII.GetBytes(value); stream.Write(data); }
        void Object(int id, Action write) { offsets.Add(stream.Position); Text(id + " 0 obj\n"); write(); Text("\nendobj\n"); }
        Text("%PDF-1.4\n");
        Object(1, () => Text("<< /Type /Catalog /Pages 2 0 R >>"));
        Object(2, () => Text($"<< /Type /Pages /Count {pages} /Kids [{string.Join(" ", Enumerable.Range(0, pages).Select(i => $"{3 + i * 3} 0 R"))}] >>"));
        for (int i = 0; i < pages; i++)
        {
            int id = 3 + i * 3;
            Object(id, () => Text($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.28 841.89] /Resources << /XObject << /PageImage {id + 2} 0 R >> >> /Contents {id + 1} 0 R >>"));
            string command = "q\n595.28 0 0 841.89 0 0 cm\n/PageImage Do\nQ\n";
            Object(id + 1, () => Text($"<< /Length {Encoding.ASCII.GetByteCount(command)} >>\nstream\n{command}endstream"));
            using var bitmap = renderPage(i); using var image = new MemoryStream(); bitmap.Save(image, ImageFormat.Jpeg); var jpeg = image.ToArray();
            Object(id + 2, () => { Text($"<< /Type /XObject /Subtype /Image /Width {bitmap.Width} /Height {bitmap.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n"); stream.Write(jpeg); Text("\nendstream"); });
        }
        long xref = stream.Position; Text($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (long offset in offsets.Skip(1)) Text(offset.ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
        Text($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n"); return stream.ToArray();
    }
    public static void Preview(Form owner, GlassCommercialSnapshot snapshot)
    {
        using var dialog = new Form { Text = "ORÇAMENTO COMERCIAL — VIDRAÇARIA", Tag = snapshot, ClientSize = new Size(780, 730), MinimumSize = new Size(420, 360), AutoScaleMode = AutoScaleMode.Dpi, StartPosition = FormStartPosition.CenterParent };
        int pageIndex = 0; var picture = new PictureBox { Name = "CommercialPage", Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
        void Update() { var old = picture.Image; picture.Image = RenderPage(snapshot, pageIndex); old?.Dispose(); }
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true };
        void Button(string text, Action click) { var b = new SalesVisualButton { Text = text, AutoSize = true, Height = 42, Padding = new Padding(12, 5, 12, 5) }; b.Click += (_, _) => click(); buttons.Controls.Add(b); }
        Button("ANTERIOR", () => { pageIndex = Math.Max(0, pageIndex - 1); Update(); });
        Button("PRÓXIMA", () => { pageIndex = Math.Min(PageCount(snapshot) - 1, pageIndex + 1); Update(); });
        Button("GERAR PDF", () =>
        {
            using var save = new SaveFileDialog { Filter = "Orçamento PDF (*.pdf)|*.pdf", FileName = "Orcamento_Vidracaria.pdf" };
            if (save.ShowDialog(dialog) != DialogResult.OK) return;
            string temporary = save.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(temporary, PdfBytes(snapshot)); File.Move(temporary, save.FileName, true); }
            catch (Exception) { MessageBox.Show(dialog, "Não foi possível salvar o PDF."); }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        });
        Button("IMPRIMIR COMERCIAL", () => Print(dialog, snapshot));
        var close = new SalesVisualButton { Text = "FECHAR", AutoSize = true, Height = 42, DialogResult = DialogResult.Cancel }; close.Click += (_, _) => dialog.Close(); buttons.Controls.Add(close); dialog.CancelButton = close;
        dialog.Controls.Add(picture); dialog.Controls.Add(buttons); Update();
        SalesDialogTheme.Apply(dialog, "Futurista Azul", null);
        var area = Screen.FromControl(owner).WorkingArea; dialog.Size = new Size(Math.Min(dialog.Width, area.Width), Math.Min(dialog.Height, area.Height));
        try { dialog.ShowDialog(owner); } finally { picture.Image?.Dispose(); picture.Image = null; }
    }
    private static void Print(Form owner, GlassCommercialSnapshot s)
    {
        try
        {
            using var document = new PrintDocument { DocumentName = "Orçamento comercial Vidraçaria" };
            using var dialog = new PrintDialog { Document = document, UseEXDialog = true, AllowSomePages = false };
            if (dialog.ShowDialog(owner) != DialogResult.OK) return;
            var a4 = document.PrinterSettings.PaperSizes.Cast<PaperSize>().FirstOrDefault(p => p.Kind == PaperKind.A4);
            if (a4 == null) throw new InvalidOperationException("A impressora selecionada não disponibiliza papel A4.");
            document.DefaultPageSettings.PaperSize = a4; document.DefaultPageSettings.Landscape = false;
            int page = 0; document.PrintPage += (_, e) => { DrawPage(e.Graphics!, e.MarginBounds, s, page++); e.HasMorePages = page < PageCount(s); }; document.Print();
        }
        catch (Exception) { MessageBox.Show(owner, "Não foi possível imprimir. Confira a impressora e o papel A4."); }
    }
}
