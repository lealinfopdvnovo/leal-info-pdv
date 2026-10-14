using LealInfoPDV;
static class Program
{
    static IEnumerable<Control> All(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(All(x)));
    static T Find<T>(Control c, string name) where T : Control => All(c).OfType<T>().Single(x => x.Name == name);
    static void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); }
    [STAThread] static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles();
        using var form = new GlassProjectForm(); form.Show(); form.UpdateDrawing(); Application.DoEvents();
        Find<Button>(form,"GlassQuote").PerformClick(); Application.DoEvents();
        var quote = form.OwnedForms.OfType<GlassQuoteForm>().Single();
        Find<NumericUpDown>(quote,"QuotePrice").Value = 100;
        var summary = Find<TextBox>(quote,"QuoteSummary");
        Check(summary.Text.Contains("1200 × 1000 mm") && summary.Text.Contains("1,20 m²"),"Projeto transferido sem redigitação");
        foreach (var measure in new[] { (1800m,1000m,1,"1,80 m²"), (1000m,1500m,2,"3,00 m²") })
        {
            Find<NumericUpDown>(form,"WidthMm").Value = measure.Item1;
            Find<NumericUpDown>(form,"HeightMm").Value = measure.Item2;
            Find<NumericUpDown>(form,"Quantity").Value = measure.Item3;
            Check(summary.Text.Contains(measure.Item4),"Atualização automática " + measure);
            form.UpdateDrawing();
            Find<Button>(form,"Mode3D").PerformClick(); Application.DoEvents();
            Find<Button>(form,"Reset3D").PerformClick();
            Find<Button>(form,"Mode2D").PerformClick(); Application.DoEvents();
        }
        Find<NumericUpDown>(form,"WidthMm").Value = 0;
        Check(summary.Text.Contains("Total indisponível"),"Medidas inválidas não mantêm total antigo");
        quote.Close(); form.Close();
        Console.WriteLine("QA UI concluído sem acesso ao banco, licença ou rede.");
    }
}
