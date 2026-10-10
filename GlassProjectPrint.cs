using System.Drawing.Printing;

namespace LealInfoPDV;

public static class GlassProjectPrint
{
    public const int PageWidth = 794, PageHeight = 1123; // A4 proporcional, prévia local ~96 DPI.
    public static void DrawPage(Graphics g, RectangleF page, GlassProject project)
    {
        if (project.ValidationError != null) throw new ArgumentException(project.ValidationError);
        var state = g.Save();
        try
        {
            g.TranslateTransform(page.Left, page.Top);
            g.ScaleTransform(page.Width / PageWidth, page.Height / PageHeight);
            g.FillRectangle(Brushes.White, 0, 0, PageWidth, PageHeight);
            using var title = new Font("Segoe UI", 22, FontStyle.Bold, GraphicsUnit.Pixel);
            using var body = new Font("Segoe UI", 15, FontStyle.Regular, GraphicsUnit.Pixel);
            g.DrawString("LEAL INFO PDV", title, Brushes.Black, 48, 42);
            g.DrawString("PROJETO DE VIDRAÇARIA", body, Brushes.Black, 48, 78);
            g.DrawString("Descrição: " + project.Description, body, Brushes.Black, new RectangleF(48, 120, 698, 65));
            g.DrawString("Modelo: " + GlassProject.ModelName, body, Brushes.Black, 48, 196);
            g.DrawString($"Largura: {GlassWindowRenderer.Millimeters(project.WidthMm)}    Altura: {GlassWindowRenderer.Millimeters(project.HeightMm)}", body, Brushes.Black, 48, 230);
            g.DrawString($"Quantidade: {project.Quantity}", body, Brushes.Black, 48, 263);
            GlassWindowRenderer.Draw(g, new RectangleF(48, 310, 698, 650), project, true);
            g.DrawString("Representação esquemática do vão. Não é lista de corte nem projeto técnico de fabricação.", body,
                Brushes.DimGray, new RectangleF(48, 1000, 698, 65));
        }
        finally { g.Restore(state); }
    }

    public static Bitmap RenderPage(GlassProject project)
    {
        var bitmap = new Bitmap(PageWidth, PageHeight);
        using var g = Graphics.FromImage(bitmap);
        DrawPage(g, new RectangleF(0, 0, PageWidth, PageHeight), project);
        return bitmap;
    }

    public static void Preview(IWin32Window owner, GlassProject project)
    {
        using var page = RenderPage(project);
        using var preview = new Form { Text = "PRÉVIA A4 — PROJETO DE VIDRAÇARIA", StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(760, 760), MinimumSize = new Size(400, 400), AutoScaleMode = AutoScaleMode.Dpi,
            BackColor = Color.FromArgb(15, 31, 53), ShowInTaskbar = false };
        var area = Screen.FromHandle(owner.Handle).WorkingArea;
        preview.Size = new Size(Math.Min(preview.Width, area.Width), Math.Min(preview.Height, area.Height));
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.Controls.Add(new PictureBox { Name = "A4Page", Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Image = page }, 0, 0);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
        var close = new Button { Text = "FECHAR", Width = 120, Height = 34, DialogResult = DialogResult.Cancel };
        var print = new Button { Text = "IMPRIMIR", Width = 120, Height = 34 };
        close.Click += (_, _) => preview.Close();
        print.Click += (_, _) => Print(preview, project);
        buttons.Controls.AddRange(new Control[] { close, print });
        layout.Controls.Add(buttons, 0, 1);
        preview.Controls.Add(layout);
        preview.CancelButton = close;
        preview.ShowDialog(owner);
    }

    private static void Print(IWin32Window owner, GlassProject project)
    {
        try
        {
            using var document = new PrintDocument { DocumentName = "Projeto de Vidraçaria — " + project.Description };
            using var dialog = new PrintDialog { Document = document, UseEXDialog = true, AllowSomePages = false };
            if (dialog.ShowDialog(owner) != DialogResult.OK) return;
            if (!document.PrinterSettings.IsValid) throw new InvalidOperationException("Selecione uma impressora válida.");
            var a4 = document.PrinterSettings.PaperSizes.Cast<PaperSize>().FirstOrDefault(p => p.Kind == PaperKind.A4);
            if (a4 == null) throw new InvalidOperationException("A impressora selecionada não disponibiliza papel A4.");
            document.DefaultPageSettings.PaperSize = a4;
            document.DefaultPageSettings.Landscape = false;
            document.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);
            document.PrintPage += (_, e) =>
            {
                if (e.Graphics == null) throw new InvalidOperationException("Impressora sem superfície de desenho.");
                DrawPage(e.Graphics, e.MarginBounds, project);
                e.HasMorePages = false;
            };
            document.Print();
        }
        catch (Exception ex) { MessageBox.Show(owner, "Não foi possível imprimir.\n\n" + ex.Message, "Impressão A4", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
