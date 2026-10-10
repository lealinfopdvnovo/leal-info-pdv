using System.Drawing.Drawing2D;

namespace LealInfoPDV;

// Projeção ortográfica local de uma caixa ilustrativa. Não representa perfis comerciais.
public sealed class GlassBox3DView : Control
{
    private readonly record struct Vertex(float X, float Y, float Z);
    private readonly Vertex[] vertices = new Vertex[8];
    private readonly Vertex[] rotated = new Vertex[8];
    private readonly PointF[] projected = new PointF[8];
    private static readonly int[][] FaceIndices = { new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 },
        new[] { 0, 1, 5, 4 }, new[] { 3, 2, 6, 7 }, new[] { 0, 3, 7, 4 }, new[] { 1, 2, 6, 5 } };
    private readonly PointF[][] facePoints = Enumerable.Range(0, 6).Select(_ => new PointF[4]).ToArray();
    private readonly int[] order = new int[6];
    private readonly float[] depths = new float[6];
    private readonly SolidBrush[] fills = { new(Color.FromArgb(19, 65, 88)), new(Color.FromArgb(29, 93, 122)),
        new(Color.FromArgb(18, 49, 70)), new(Color.FromArgb(47, 112, 139)), new(Color.FromArgb(16, 56, 82)), new(Color.FromArgb(31, 83, 113)) };
    private readonly Pen edge = new(Color.FromArgb(115, 224, 251), 1.5f);
    private readonly PointF[] leafOutline = new PointF[4];
    private readonly SolidBrush dimensionsBackground = new(Color.FromArgb(9, 22, 39));
    private string dimensionsText = string.Empty;
    private GlassProject? project;
    private bool dragging;
    private Point previous;
    private float radius = 1;
    public float YawDegrees { get; private set; } = -25;
    public float PitchDegrees { get; private set; } = 18;
    public float ZoomFactor { get; private set; } = 1;
    public bool IsUnavailable { get; private set; }
    public event EventHandler? RenderingUnavailable;
    public GlassProject? Project
    {
        get => project;
        set
        {
            project = value;
            dimensionsText = value == null ? string.Empty : $"DIMENSÕES DO PROJETO\nLargura: {value.WidthMm:0.##} mm\nAltura: {value.HeightMm:0.##} mm";
            if (value != null && value.ValidationError == null)
            {
                decimal maximum = Math.Max(value.WidthMm, value.HeightMm);
                float w = (float)(value.WidthMm / maximum) * .5f, h = (float)(value.HeightMm / maximum) * .5f;
                float d = Math.Min(w, h) * .08f; // Profundidade visual, não uma medida técnica.
                vertices[0] = new(-w, -h, -d); vertices[1] = new(w, -h, -d);
                vertices[2] = new(w, h, -d); vertices[3] = new(-w, h, -d);
                vertices[4] = new(-w, -h, d); vertices[5] = new(w, -h, d);
                vertices[6] = new(w, h, d); vertices[7] = new(-w, h, d);
                radius = MathF.Sqrt(w * w + h * h + d * d);
            }
            if (Visible) Invalidate();
        }
    }
    public GlassBox3DView()
    {
        Name = "Drawing3D";
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(9, 22, 39);
        DoubleBuffered = true;
        ResizeRedraw = true;
        TabStop = true;
        Cursor = Cursors.Hand;
    }
    public void ResetView()
    {
        YawDegrees = -25; PitchDegrees = 18; ZoomFactor = 1;
        dragging = false; Capture = false;
        if (Visible && !IsUnavailable) Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (IsUnavailable) return;
        try
        {
            base.OnPaint(e);
            if (project == null || project.ValidationError != null || Width < 20 || Height < 20) return;
            Render(e.Graphics);
        }
        catch (Exception)
        {
            IsUnavailable = true; dragging = false; Capture = false;
            RenderingUnavailable?.Invoke(this, EventArgs.Empty);
        }
    }
    private void Render(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float yaw = YawDegrees * MathF.PI / 180, pitch = PitchDegrees * MathF.PI / 180;
        float cy = MathF.Cos(yaw), sy = MathF.Sin(yaw), cp = MathF.Cos(pitch), sp = MathF.Sin(pitch);
        float scale = Math.Min(ClientSize.Width, ClientSize.Height) * .40f / radius * ZoomFactor;
        for (int i = 0; i < 8; i++)
        {
            var v = vertices[i];
            float x = v.X * cy + v.Z * sy, z = -v.X * sy + v.Z * cy;
            var t = new Vertex(x, v.Y * cp - z * sp, v.Y * sp + z * cp);
            rotated[i] = t;
            projected[i] = new(Width * .5f + t.X * scale, Height * .5f - t.Y * scale);
        }
        for (int f = 0; f < 6; f++)
        {
            order[f] = f; depths[f] = 0;
            for (int i = 0; i < 4; i++) { int index = FaceIndices[f][i]; facePoints[f][i] = projected[index]; depths[f] += rotated[index].Z; }
        }
        // Painter's algorithm de seis faces, sem ordenar/gerar listas por frame.
        for (int i = 1; i < 6; i++)
        {
            int face = order[i], j = i - 1;
            while (j >= 0 && depths[order[j]] > depths[face]) { order[j + 1] = order[j]; j--; }
            order[j + 1] = face;
        }
        foreach (int face in order)
        {
            g.FillPolygon(fills[face], facePoints[face]); g.DrawPolygon(edge, facePoints[face]);
            // Detalhes coplanares em ambas as faces largas: a ordenação existente mantém a oclusão.
            if (face == 0 || face == 1)
            {
                DrawLeaf(g, facePoints[face], .045f, .49f);
                DrawLeaf(g, facePoints[face], .51f, .955f);
            }
        }
        // Informação fixa em pixels lógicos; não gira nem sofre zoom com a geometria.
        int margin = Math.Max(8, (int)(10 * DeviceDpi / 96f));
        var flags = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding;
        Size textSize = TextRenderer.MeasureText(dimensionsText, Font, new Size(Math.Max(1, Width - margin * 4), int.MaxValue), flags);
        var info = new Rectangle(margin, margin, Math.Min(Width - margin * 2, textSize.Width + margin * 2), textSize.Height + margin * 2);
        g.FillRectangle(dimensionsBackground, info);
        TextRenderer.DrawText(g, dimensionsText, Font, new Rectangle(info.X + margin, info.Y + margin, info.Width - margin * 2, textSize.Height), Color.FromArgb(175, 231, 247), flags);
    }
    private void DrawLeaf(Graphics g, PointF[] face, float left, float right)
    {
        // Interpolação afim na face já projetada: moldura externa + dois contornos de folhas.
        PointF At(float x, float y) => new(face[0].X + (face[1].X - face[0].X) * x + (face[3].X - face[0].X) * y,
            face[0].Y + (face[1].Y - face[0].Y) * x + (face[3].Y - face[0].Y) * y);
        leafOutline[0] = At(left, .06f); leafOutline[1] = At(right, .06f);
        leafOutline[2] = At(right, .94f); leafOutline[3] = At(left, .94f);
        g.DrawPolygon(edge, leafOutline);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || IsUnavailable) return;
        Focus(); previous = e.Location; dragging = true; Capture = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!dragging || e.Button != MouseButtons.Left || IsUnavailable) return;
        YawDegrees = (YawDegrees + (e.X - previous.X) * .5f) % 360;
        PitchDegrees = Math.Clamp(PitchDegrees - (e.Y - previous.Y) * .5f, -75, 75);
        previous = e.Location; Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left) { dragging = false; Capture = false; }
    }
    protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) dragging = false; }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (IsUnavailable) return;
        ZoomFactor = Math.Clamp(ZoomFactor * MathF.Pow(1.1f, e.Delta / 120f), .55f, 2.5f); Invalidate();
    }
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) { dragging = false; Capture = false; }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { edge.Dispose(); dimensionsBackground.Dispose(); foreach (var fill in fills) fill.Dispose(); }
        base.Dispose(disposing);
    }
}
