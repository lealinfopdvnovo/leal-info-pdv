using LealInfoPDV;
using System.Text.Json;
using System.Diagnostics;

int count = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL " + name); count++; Console.WriteLine("PASS " + name); }
GlassCutPiece P(string id, decimal w, decimal h, int q = 1, string material = "Vidro teste", bool rotation = true) => new(id, "Descrição " + id, w, h, q, material, rotation);
void Invalid(Action action, string name) { try { action(); } catch (ArgumentException) { Check(true, name); return; } throw new Exception("FAIL recusou inválido: " + name); }
void Verify(GlassCutResult result, IReadOnlyList<GlassCutPiece> pieces)
{
    Check(result.Sheets.Sum(s => s.Pieces.Count) + result.RejectedQuantity == pieces.Sum(p => p.Quantity), "Sem perda/duplicação de unidades");
    foreach (var s in result.Sheets)
    {
        foreach (var p in s.Pieces)
        {
            Check(p.X >= 0 && p.Y >= 0 && p.X + p.WidthMm <= result.SheetWidthMm && p.Y + p.HeightMm <= result.SheetHeightMm, "Peça contida na chapa");
            Check(p.Piece.Material.Trim().Equals(s.Material, StringComparison.OrdinalIgnoreCase), "Materiais separados");
            Check(p.Rotated ? p.WidthMm == p.Piece.HeightMm && p.HeightMm == p.Piece.WidthMm : p.WidthMm == p.Piece.WidthMm && p.HeightMm == p.Piece.HeightMm, "Dimensões e orientação corretas");
        }
        for (int i = 0; i < s.Pieces.Count; i++) for (int j = i + 1; j < s.Pieces.Count; j++)
        {
            var a = s.Pieces[i]; var b = s.Pieces[j];
            Check(a.X + a.WidthMm <= b.X || b.X + b.WidthMm <= a.X || a.Y + a.HeightMm <= b.Y || b.Y + b.HeightMm <= a.Y, "Sem sobreposição");
        }
    }
    Check(result.UsedArea + result.UnusedArea == result.TotalArea && result.UnusedArea >= 0, "Conservação da área");
    Check(result.TotalArea == 0 || result.Utilization + result.Waste == 100m, "Aproveitamento + desperdício = 100%");
}
var one = GlassCutPacking.Calculate(1000, 1000, new[] { P("01", 500, 500) }, false);
Check(one.Sheets.Count == 1 && one.UsedArea == .25m && one.TotalArea == 1 && one.Utilization == 25 && one.Waste == 75, "Uma peça e conversão mm² → m²");
var equal = GlassCutPacking.Calculate(1000, 1000, new[] { P("01", 500, 500, 9) }, false);
Check(equal.Sheets.Count == 3 && equal.UsedArea == 2.25m && equal.Utilization == 75, "Iguais, quantidade e múltiplas chapas");
Verify(equal, new[] { P("01", 500, 500, 9) });
var varying = new[] { P("01", 600, 400), P("02", 300, 200, 2), P("03", 100, 100), P("04", 400, 500) };
Verify(GlassCutPacking.Calculate(1000, 1000, varying, false), varying);
var project = new GlassProject("ORIGINAL", 1200, 1000, 1);
var edited = varying.ToArray(); edited[2] = edited[2] with { WidthMm = 190, HeightMm = 140, Description = "Editada" };
Check(edited[0] == varying[0] && edited[1] == varying[1] && edited[3] == varying[3] && edited[2] != varying[2], "Edição só da PEÇA 03");
Check(project == new GlassProject("ORIGINAL", 1200, 1000, 1), "Plano não altera medidas originais");
Verify(GlassCutPacking.Calculate(1000, 1000, edited, false), edited);
var added = edited.Append(P("05", 50, 50)).ToArray(); Verify(GlassCutPacking.Calculate(1000, 1000, added, false), added);
var removed = added.Where(p => p.Id != "02").ToArray(); Verify(GlassCutPacking.Calculate(1000, 1000, removed, false), removed);
var changedQ = removed.Select(p => p.Id == "03" ? p with { Quantity = 4 } : p).ToArray(); Verify(GlassCutPacking.Calculate(1000, 1000, changedQ, false), changedQ);
var oversized = GlassCutPacking.Calculate(100, 60, new[] { P("BIG", 200, 70, 2) }, true);
Check(oversized.Sheets.Count == 0 && oversized.RejectedQuantity == 2 && oversized.TotalArea == 0, "Peça maior identificada sem chapa vazia");
Check(GlassCutPacking.Calculate(100, 60, new[] { P("ROT", 60, 100) }, false).RejectedQuantity == 1, "90° desativado não gira");
var rotated = GlassCutPacking.Calculate(100, 60, new[] { P("ROT", 60, 100) }, true);
Check(rotated.Sheets.Single().Pieces.Single().Rotated && rotated.Utilization == 100, "90° explicitamente permitido");
Check(GlassCutPacking.Calculate(100, 60, new[] { P("ROT", 60, 100, rotation: false) }, true).RejectedQuantity == 1, "Restrição individual prevalece");
Check(GlassCutPacking.Calculate(1000, 1000, new[] { P("A", 400, 400, material: "VIDRO A"), P("B", 400, 400, material: "VIDRO B") }, true).Sheets.Count == 2, "Materiais diferentes não compartilham chapa");
var fractional = GlassCutPacking.Calculate(1000.5m, 600.2m, new[] { P("F", 500.25m, 300.1m, 4) }, false);
Check(fractional.Utilization == 100 && fractional.UnusedArea == 0, "Precisão decimal sem folgas inventadas");
foreach (var n in new[] { 0m, -1m, decimal.MaxValue })
{
    Invalid(() => GlassCutPacking.Calculate(n, 1000, varying, false), "Largura da chapa inválida " + n);
    Invalid(() => GlassCutPacking.Calculate(1000, n, varying, false), "Altura da chapa inválida " + n);
    Invalid(() => GlassCutPacking.Calculate(1000, 1000, new[] { P("X", n, 10) }, false), "Peça inválida " + n);
}
Invalid(() => GlassCutPacking.Calculate(1000, 1000, new[] { P("X", 10, 10, 0) }, false), "Quantidade zero");
Invalid(() => GlassCutPacking.Calculate(1000, 1000, new[] { P("X", 10, 10, -1) }, false), "Quantidade negativa");
Invalid(() => GlassCutPacking.Calculate(1000, 1000, new[] { P("X", 10, 10, int.MaxValue) }, false), "Quantidade muito grande bloqueada");
Invalid(() => GlassCutPacking.Calculate(1000, 1000, new[] { P("x", 10, 10), P("X", 20, 20) }, false), "IDs duplicados bloqueados");
Invalid(() => GlassCutPacking.Calculate(1000, 1000, Array.Empty<GlassCutPiece>(), false), "Lista vazia");
var large = GlassCutPacking.Calculate(1000000, 1000000, new[] { P("LARGE", 1000000, 1000000) }, false);
Check(large.UsedArea == 1000000 && large.Utilization == 100, "Dimensões grandes válidas sem overflow");
var random = new Random(42);
for (int test = 0; test < 35; test++)
{
    var pieces = Enumerable.Range(1, 12).Select(i => P("P" + i, random.Next(10, 750), random.Next(10, 650), random.Next(1, 4), i % 2 == 0 ? "A" : "B", i % 3 != 0)).ToArray();
    bool rotation = test % 2 == 0;
    var result = GlassCutPacking.Calculate(700, 600, pieces, rotation); Verify(result, pieces);
    Check(JsonSerializer.Serialize(result) == JsonSerializer.Serialize(GlassCutPacking.Calculate(700, 600, pieces, rotation)), "Repetição determinística " + test);
}
var watch = Stopwatch.StartNew(); var maximum = GlassCutPacking.Calculate(1000, 1000, new[] { P("LIMIT", 1000, 1000, 1000) }, false); watch.Stop();
Check(maximum.Sheets.Count == 1000, "Lote máximo controlado"); Console.WriteLine($"TEMPO LOTE 1000: {watch.ElapsedMilliseconds} ms");
Console.WriteLine($"PASS PLANO GEOMÉTRICO: {count} verificações determinísticas, sem regras de fabricação ou estoque.");
