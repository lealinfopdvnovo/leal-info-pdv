namespace LealInfoPDV;

// Operações geométricas independentes do orçamento, estoque e medidas do vão.
public sealed record GlassCutPiece(string Id, string Description, decimal WidthMm, decimal HeightMm,
    int Quantity, string Material, bool AllowRotation = true);
public sealed record GlassCutPlacement(GlassCutPiece Piece, int Instance, decimal X, decimal Y,
    decimal WidthMm, decimal HeightMm, bool Rotated);
public sealed record GlassCutSheet(int Number, string Material, IReadOnlyList<GlassCutPlacement> Pieces);
public sealed record GlassCutRejected(GlassCutPiece Piece, string Reason);
public sealed record GlassCutResult(decimal SheetWidthMm, decimal SheetHeightMm,
    IReadOnlyList<GlassCutSheet> Sheets, IReadOnlyList<GlassCutRejected> Rejected, bool RotationAllowed = false)
{
    public decimal TotalArea => Sheets.Count * SheetWidthMm * SheetHeightMm / 1000000m;
    public decimal UsedArea => Sheets.Sum(s => s.Pieces.Sum(p => p.WidthMm * p.HeightMm / 1000000m));
    public decimal UnusedArea => TotalArea - UsedArea;
    public decimal Utilization => TotalArea == 0 ? 0 : UsedArea / TotalArea * 100m;
    public decimal Waste => TotalArea == 0 ? 0 : 100m - Utilization;
    public int RejectedQuantity => Rejected.Sum(r => r.Piece.Quantity);
}

public static class GlassCutPacking
{
    public const int MaximumPieces = 1000;
    public const decimal MaximumDimension = 1000000m;
    public const string Disclaimer = "SIMULAÇÃO GEOMÉTRICA: não considera kerf, margens, folgas, sentido do material, têmpera ou regras de máquina/fabricação. Não é ordem de produção. Medidas do vão não são medidas de corte. Confirme as peças com o profissional.";
    private sealed record Free(decimal X, decimal Y, decimal W, decimal H);
    private sealed class WorkingSheet
    {
        public required string Material;
        public readonly List<Free> Free = new();
        public readonly List<GlassCutPlacement> Placed = new();
    }
    private sealed record Candidate(int Sheet, int Free, decimal W, decimal H, bool Rotated,
        decimal AreaLeft, decimal ShortSide);
    public static GlassCutResult Calculate(decimal width, decimal height, IReadOnlyList<GlassCutPiece> pieces, bool allowRotation)
    {
        Validate(width, "Largura da chapa"); Validate(height, "Altura da chapa");
        if (pieces.Count == 0) throw new ArgumentException("Adicione pelo menos uma peça.");
        if (pieces.Count > MaximumPieces) throw new ArgumentException($"Limite de {MaximumPieces} peças por simulação.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long quantity = 0;
        foreach (var p in pieces)
        {
            if (string.IsNullOrWhiteSpace(p.Id) || !ids.Add(p.Id.Trim())) throw new ArgumentException("Cada peça precisa de uma identificação única.");
            Validate(p.WidthMm, "Largura de " + p.Id); Validate(p.HeightMm, "Altura de " + p.Id);
            if (p.Quantity <= 0 || p.Quantity > MaximumPieces) throw new ArgumentException("Quantidade inválida em " + p.Id);
            quantity += p.Quantity;
            if (quantity > MaximumPieces) throw new ArgumentException($"Limite de {MaximumPieces} unidades por simulação; divida o trabalho em lotes.");
        }
        var sheets = new List<WorkingSheet>(); var rejected = new List<GlassCutRejected>();
        // Ordenação estável: maior área, maior lado, ordem original. Heurística, não ótimo global garantido.
        foreach (var p in pieces.Select((piece, index) => (piece, index))
            .OrderByDescending(x => x.piece.WidthMm * x.piece.HeightMm)
            .ThenByDescending(x => Math.Max(x.piece.WidthMm, x.piece.HeightMm)).ThenBy(x => x.index).Select(x => x.piece))
        {
            bool rotate = allowRotation && p.AllowRotation;
            if (!Fits(p.WidthMm, p.HeightMm, width, height) && !(rotate && Fits(p.HeightMm, p.WidthMm, width, height)))
            { rejected.Add(new(p, "Não cabe na chapa com a rotação selecionada.")); continue; }
            for (int unit = 1; unit <= p.Quantity; unit++)
            {
                Candidate? best = null;
                for (int si = 0; si < sheets.Count; si++)
                {
                    if (!string.Equals(sheets[si].Material, p.Material.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                    for (int fi = 0; fi < sheets[si].Free.Count; fi++)
                    {
                        var f = sheets[si].Free[fi];
                        Consider(si, fi, f, p.WidthMm, p.HeightMm, false);
                        if (rotate && p.WidthMm != p.HeightMm) Consider(si, fi, f, p.HeightMm, p.WidthMm, true);
                    }
                }
                if (best == null)
                {
                    var sheet = new WorkingSheet { Material = p.Material.Trim() }; sheet.Free.Add(new(0, 0, width, height)); sheets.Add(sheet);
                    Consider(sheets.Count - 1, 0, sheet.Free[0], p.WidthMm, p.HeightMm, false);
                    if (rotate && p.WidthMm != p.HeightMm) Consider(sheets.Count - 1, 0, sheet.Free[0], p.HeightMm, p.WidthMm, true);
                }
                var b = best!; var target = sheets[b.Sheet]; var cell = target.Free[b.Free]; target.Free.RemoveAt(b.Free);
                target.Placed.Add(new(p, unit, cell.X, cell.Y, b.W, b.H, b.Rotated));
                decimal dw = cell.W - b.W, dh = cell.H - b.H;
                // Partição guilhotinada em retângulos disjuntos. Não define sequência industrial de corte.
                if (dw > dh) { Add(cell.X + b.W, cell.Y, dw, cell.H); Add(cell.X, cell.Y + b.H, b.W, dh); }
                else { Add(cell.X + b.W, cell.Y, dw, b.H); Add(cell.X, cell.Y + b.H, cell.W, dh); }
                void Add(decimal x, decimal y, decimal w, decimal h) { if (w > 0 && h > 0) target.Free.Add(new(x, y, w, h)); }
                void Consider(int si, int fi, Free f, decimal w, decimal h, bool rotated)
                {
                    if (!Fits(w, h, f.W, f.H)) return;
                    var candidate = new Candidate(si, fi, w, h, rotated, f.W * f.H - w * h, Math.Min(f.W - w, f.H - h));
                    if (best == null || candidate.AreaLeft < best.AreaLeft || candidate.AreaLeft == best.AreaLeft && candidate.ShortSide < best.ShortSide) best = candidate;
                }
            }
        }
        return new(width, height, sheets.Select((s, i) => new GlassCutSheet(i + 1, s.Material, s.Placed.ToArray())).ToArray(), rejected.ToArray(), allowRotation);
    }
    private static bool Fits(decimal w, decimal h, decimal availableW, decimal availableH) => w <= availableW && h <= availableH;
    private static void Validate(decimal n, string label)
    { if (n <= 0 || n > MaximumDimension) throw new ArgumentException($"{label}: informe valor maior que zero e até {MaximumDimension:0} mm."); }
}
