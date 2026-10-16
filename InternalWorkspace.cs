using System.Drawing.Drawing2D;
using System.Globalization;
using System.Data;
using System.Runtime.CompilerServices;

namespace LealInfoPDV;

// Shared presentation shell. Original controls and their actions are retained.
internal static class InternalWorkspace
{
    private static readonly ConditionalWeakTable<Form, object> Ready = new();
    private static readonly ConditionalWeakTable<FlowLayoutPanel, Action> ActionLayouts = new();
    private static bool installed;
    internal static void Install()
    {
        if (installed) return;
        installed = true;
        Application.Idle += (_, _) =>
        {
            foreach (var f in Application.OpenForms.Cast<Form>().ToArray()) Apply(f);
        };
    }

    internal static void Apply(Form f)
    {
        if (f.IsDisposed || f is MainForm or LoginForm || f.GetType().Name.Contains("Splash", StringComparison.Ordinal) ||
            f.Text.Contains("TELA DE VENDA", StringComparison.OrdinalIgnoreCase) || Ready.TryGetValue(f, out _)) return;
        Ready.Add(f, new object());
        f.SuspendLayout();
        try
        {
            var grids = f.Controls.OfType<DataGridView>().Where(g => g.Dock == DockStyle.Fill).ToArray();
            var grid = grids.Length == 1 ? grids[0] : null;
            if (grid != null && CanReframe(f, grid))
                List(f, grid);
            else if (f.Tag is List<TextBox> fields && fields.Count > 0)
                Editor(f, fields);
            Fit(f);
            foreach (var bar in Descendants(f).OfType<FlowLayoutPanel>().ToArray()) AdaptActions(bar);
            // Branded drawings, photos, document previews and the operational sales
            // layout retain their own painters. Native fields/tables share the palette.
            PremiumGlobalTheme.Apply(f);
        }
        finally { f.ResumeLayout(true); }
        // The initial dock layout may still be using the footer's pre-layout
        // width. A queued pass applies its final measured height before use.
        var root = f.Controls.OfType<TableLayoutPanel>().FirstOrDefault(c => c.Name == "InternalWorkspaceRoot");
        if (root != null)
        {
            void Finish()
            {
                if (f.IsDisposed || root.IsDisposed) return;
                foreach (var bar in Descendants(root).OfType<FlowLayoutPanel>())
                    if (ActionLayouts.TryGetValue(bar, out var measure)) measure();
                root.PerformLayout();
            }
            if (f.IsHandleCreated) f.BeginInvoke((Action)Finish);
            else f.Shown += (_, _) => Finish();
        }
    }

    private static IEnumerable<Control> Descendants(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(Descendants(x)));
    private static bool CanReframe(Form f, DataGridView grid)
    {
        if (!grid.ReadOnly || grid.AllowUserToAddRows || grid.DataSource != null && grid.DataSource is not DataTable) return false;
        if (f.Controls.OfType<FlowLayoutPanel>().Count(c => c.Dock == DockStyle.Bottom) > 1) return false;
        // Specialized catalog filters, toolbars, images and editable grids stay in
        // place; only the original simple list heading is replaced by the shell.
        return f.Controls.Cast<Control>().All(c => c == grid ||
            c is Label && c.Dock == DockStyle.Top ||
            c is FlowLayoutPanel && c.Dock == DockStyle.Bottom && c.Controls.Cast<Control>().All(x => x is Button) ||
            c.GetType() == typeof(Panel) && c.Dock == DockStyle.Top && c.Controls.Count > 0 && Descendants(c).All(x => x is Label));
    }
    private static Color Navy => Color.FromArgb(5, 38, 72);
    private static Color Light => Color.FromArgb(226, 240, 250);
    private static Label MakeLabel(string text, float size = 10, bool bold = false) => new()
    {
        Text = text, Dock = DockStyle.Fill, ForeColor = Light, BackColor = Color.Transparent,
        Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
        TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Margin = new Padding(0)
    };
    private static TableLayoutPanel Frame(Form f, params RowStyle[] rows)
    {
        var root = new TableLayoutPanel { Name = "InternalWorkspaceRoot", Dock = DockStyle.Fill, ColumnCount = 1,
            RowCount = rows.Length, Padding = new Padding(18), BackColor = Color.FromArgb(3, 20, 42) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var row in rows) root.RowStyles.Add(row);
        f.Controls.Add(root); root.BringToFront();
        return root;
    }
    private static Control Header(string title, string subtitle)
    {
        var header = new WorkspaceSurface { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12), Padding = new Padding(20, 8, 20, 8) };
        var lines = new TableLayoutPanel { Name = "WorkspaceTransparent", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent };
        lines.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        lines.RowStyles.Add(new RowStyle(SizeType.Percent, 62)); lines.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        lines.Controls.Add(MakeLabel(title, 20, true), 0, 0);
        lines.Controls.Add(MakeLabel(subtitle, 9.5f), 0, 1);
        header.Controls.Add(lines); return header;
    }
    private static (Control panel, Label value) Card(string caption)
    {
        var p = new WorkspaceSurface { Dock = DockStyle.Fill, Padding = new Padding(12, 4, 12, 4), Margin = new Padding(0, 0, 10, 8) };
        var rows = new TableLayoutPanel { Name = "WorkspaceTransparent", Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.Transparent };
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 19)); rows.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rows.Controls.Add(MakeLabel(caption, 8.5f), 0, 0);
        var value = MakeLabel("—", 12, true); rows.Controls.Add(value, 0, 1); p.Controls.Add(rows); return (p, value);
    }

    private static void List(Form f, DataGridView grid)
    {
        var old = f.Controls.Cast<Control>().ToArray();
        var actions = old.OfType<FlowLayoutPanel>().FirstOrDefault(c => c.Dock == DockStyle.Bottom);
        var context = old.OfType<Label>().ToArray();
        // The former heading contains presentation only; keep it alive until the form closes.
        foreach (var c in old) f.Controls.Remove(c);
        foreach (var c in old.Where(c => c != grid && c != actions && !context.Contains(c)))
            f.Disposed += (_, _) => c.Dispose();
        var root = Frame(f, new RowStyle(SizeType.Absolute, 94), new RowStyle(SizeType.Absolute, 66),
            new RowStyle(SizeType.Absolute, 58), new RowStyle(SizeType.Absolute, context.Sum(c => c.Height)),
            new RowStyle(SizeType.Percent, 100), new RowStyle(SizeType.Absolute, 76));
        root.Controls.Add(Header(f.Text, "LEAL INFO PDV PRO  /  Cadastros e consultas"), 0, 0);
        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0), BackColor = Color.Transparent };
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22)); cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22)); cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        var total = Card("REGISTROS NA LISTA"); var shown = Card("RESULTADO DA BUSCA"); var selected = Card("SELECIONADO");
        cards.Controls.Add(total.panel, 0, 0); cards.Controls.Add(shown.panel, 1, 0); cards.Controls.Add(selected.panel, 2, 0); root.Controls.Add(cards, 0, 1);
        var searchRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 0, 0, 12), BackColor = Color.Transparent };
        searchRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80)); searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        searchRow.Controls.Add(MakeLabel("BUSCAR", 9, true), 0, 0);
        var inputBox = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 9, 10, 5), Margin = new Padding(0, 0, 8, 0), BackColor = Navy };
        var search = new TextBox { Name = "InternalListSearch", AccessibleName = "Buscar nesta lista", PlaceholderText = "Digite nome, código ou outro dado da lista…", Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 11), TabIndex = 0 };
        inputBox.Controls.Add(search); searchRow.Controls.Add(inputBox, 1, 0);
        var clear = new Button { Text = "LIMPAR", Dock = DockStyle.Fill, Margin = new Padding(0), AccessibleName = "Limpar busca" };
        clear.Click += (_, _) => { search.Clear(); search.Focus(); }; searchRow.Controls.Add(clear, 2, 0); root.Controls.Add(searchRow, 0, 2);
        var details = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), AutoScroll = true };
        foreach (var c in context) { c.Dock = DockStyle.Top; details.Controls.Add(c); } root.Controls.Add(details, 0, 3);
        var table = new WorkspaceSurface { Dock = DockStyle.Fill, Padding = new Padding(1), Margin = new Padding(0, 0, 0, 10) };
        grid.Dock = DockStyle.Fill; grid.Margin = new Padding(0); grid.DefaultCellStyle.Padding = new Padding(8, 3, 8, 3);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 3, 8, 3); grid.RowTemplate.Height = Math.Max(36, grid.RowTemplate.Height);
        grid.ShowCellToolTips = true;
        void Columns()
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                var key = column.HeaderText.ToUpperInvariant();
                int minimum = key == "ID" ? 64 : key.Contains("PRODUTO") || key is "NOME" or "DESCRIÇÃO" ? 250 : key.Contains("CÓDIGO") || key.Contains("ENDEREÇO") || key.Contains("EMAIL") ? 175 : 110;
                column.MinimumWidth = minimum;
                if (column.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill) column.FillWeight = minimum;
            }
        }
        Columns(); grid.ColumnAdded += (_, _) => Columns(); table.Controls.Add(grid); root.Controls.Add(table, 0, 4);
        if (actions == null)
        {
            actions = new FlowLayoutPanel(); var close = new Button { Text = "FECHAR", Width = 160, Height = 42, Margin = new Padding(6) };
            close.Click += (_, _) => f.Close(); actions.Controls.Add(close);
        }
        actions.Dock = DockStyle.Fill; actions.Margin = new Padding(0); actions.Padding = new Padding(4, 7, 4, 7); root.Controls.Add(actions, 0, 5);
        bool filtering = false;
        DataTable? bound = null;
        string originalFilter = "";
        void Refresh()
        {
            if (filtering || grid.IsDisposed) return;
            filtering = true;
            try
            {
                var query = search.Text.Trim();
                if (grid.DataSource is DataTable data)
                {
                    if (!ReferenceEquals(bound, data)) { bound = data; originalFilter = data.DefaultView.RowFilter; }
                    string escaped = query.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]");
                    var predicate = query.Length == 0 ? "" : string.Join(" OR ", data.Columns.Cast<DataColumn>().Select(c => "CONVERT([" + c.ColumnName.Replace("]", "\\]") + "], 'System.String') LIKE '%" + escaped + "%'") );
                    var expression = originalFilter.Length == 0 ? predicate : predicate.Length == 0 ? originalFilter : "(" + originalFilter + ") AND (" + predicate + ")";
                    if (data.DefaultView.RowFilter != expression) data.DefaultView.RowFilter = expression;
                    total.value.Text = data.Rows.Count.ToString("N0"); shown.value.Text = data.DefaultView.Count.ToString("N0"); Selection(); return;
                }
                var current = grid.CurrentRow;
                bool Match(DataGridViewRow r) => query.Length == 0 || r.Cells.Cast<DataGridViewCell>().Any(c => CultureInfo.CurrentCulture.CompareInfo.IndexOf(Convert.ToString(c.FormattedValue) ?? "", query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);
                if (current != null && !Match(current)) grid.CurrentCell = null;
                foreach (DataGridViewRow row in grid.Rows) if (!row.IsNewRow) row.Visible = Match(row);
                var visible = grid.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow && r.Visible).ToArray();
                if (grid.CurrentCell == null && visible.Length > 0) grid.CurrentCell = visible[0].Cells.Cast<DataGridViewCell>().First(c => c.Visible);
                total.value.Text = grid.Rows.Cast<DataGridViewRow>().Count(r => !r.IsNewRow).ToString("N0"); shown.value.Text = visible.Length.ToString("N0");
                Selection();
            }
            finally { filtering = false; }
        }
        void Selection()
        {
            var row = grid.CurrentRow;
            int column = grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => c.HeaderText is "Produto" or "Nome" or "Descrição")?.Index ?? Math.Min(1, grid.Columns.Count - 1);
            selected.value.Text = row == null || column < 0 ? "Nenhum registro" : Convert.ToString(row.Cells[column].FormattedValue);
        }
        bool refreshQueued = false;
        void QueueRefresh()
        {
            if (refreshQueued || !f.IsHandleCreated || f.IsDisposed || f.Disposing) return;
            refreshQueued = true;
            f.BeginInvoke((Action)(() => { refreshQueued = false; Refresh(); }));
        }
        search.TextChanged += (_, _) => Refresh(); grid.SelectionChanged += (_, _) => { if (!filtering) Selection(); };
        grid.RowsAdded += (_, _) => QueueRefresh();
        grid.RowsRemoved += (_, _) => QueueRefresh();
        Refresh(); f.AccessibleDescription = "Lista com busca, resumo e ações fixas";
    }

    private static void Editor(Form f, List<TextBox> fields)
    {
        var rows = new List<(Label label, Control field)>();
        for (int i = 0; i < fields.Count; i++)
        {
            var label = f.Controls.OfType<Label>().FirstOrDefault(l => Math.Abs(l.Top - (25 + i * 55)) < 8);
            var field = fields[i].Parent == f ? fields[i] : f.Controls.Cast<Control>().FirstOrDefault(c => c is not Button && c is not Label && Math.Abs(c.Top - (22 + i * 55)) < 8);
            if (label == null || field == null) return; // A specialized editor keeps its existing layout.
            rows.Add((label, field));
        }
        var buttons = f.Controls.OfType<Button>().ToArray();
        if (buttons.Length == 0) return;
        foreach (var pair in rows) { f.Controls.Remove(pair.label); f.Controls.Remove(pair.field); }
        foreach (var b in buttons) f.Controls.Remove(b);
        f.FormBorderStyle = FormBorderStyle.Sizable; f.MinimumSize = new Size(520, 360); f.Size = new Size(740, Math.Min(760, 220 + rows.Count * 78));
        var root = Frame(f, new RowStyle(SizeType.Absolute, 94), new RowStyle(SizeType.Percent, 100), new RowStyle(SizeType.Absolute, 76));
        root.Controls.Add(Header(f.Text.ToUpperInvariant(), "Preencha os campos e use as ações abaixo para concluir."), 0, 0);
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(16), Margin = new Padding(0, 0, 0, 10), BackColor = Navy };
        var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = rows.Count * 2, BackColor = Color.Transparent };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < rows.Count; i++)
        {
            var (label, field) = rows[i]; label.Dock = DockStyle.Fill; label.Margin = new Padding(0); label.AutoSize = false; label.TextAlign = ContentAlignment.MiddleLeft;
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, Math.Max(26, TextRenderer.MeasureText(label.Text, label.Font).Height + 6)));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, Math.Max(48, field.Height + 18)));
            field.Dock = DockStyle.Top; field.Margin = new Padding(0, 3, 0, 12); content.Controls.Add(label, 0, i * 2); content.Controls.Add(field, 0, i * 2 + 1);
        }
        scroll.Controls.Add(content); root.Controls.Add(scroll, 0, 1);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0), Padding = new Padding(4, 7, 4, 7) };
        foreach (var b in buttons) { b.Margin = new Padding(6); actions.Controls.Add(b); } root.Controls.Add(actions, 0, 2);
    }

    private static void Fit(Form f)
    {
        var area = Screen.FromControl(f).WorkingArea;
        f.MinimumSize = new Size(Math.Min(f.MinimumSize.Width, area.Width - 16), Math.Min(f.MinimumSize.Height, area.Height - 16));
        if (f.WindowState == FormWindowState.Maximized && (f.Right > area.Right || f.Bottom > area.Bottom || f.Left < area.Left || f.Top < area.Top))
        {
            f.WindowState = FormWindowState.Normal; f.WindowState = FormWindowState.Maximized;
        }
        if (f.WindowState != FormWindowState.Normal) return;
        int width = Math.Min(f.Width, area.Width - 16), height = Math.Min(f.Height, area.Height - 16);
        f.Bounds = new Rectangle(Math.Clamp(f.Left, area.Left + 8, Math.Max(area.Left + 8, area.Right - width - 8)),
            Math.Clamp(f.Top, area.Top + 8, Math.Max(area.Top + 8, area.Bottom - height - 8)), width, height);
    }
    private static void AdaptActions(FlowLayoutPanel bar)
    {
        if (bar.Controls.Count == 0 || bar.Controls.Cast<Control>().Any(c => c is not Button)) return;
        if (bar.Dock is not (DockStyle.Bottom or DockStyle.Fill)) return;
        var table = bar.Parent as TableLayoutPanel;
        int row = table?.GetRow(bar) ?? -1;
        if (table != null && (row < 0 || row >= table.RowStyles.Count)) return;
        if (table != null && row != table.RowCount - 1) return;
        if (bar.Dock == DockStyle.Fill && (table == null || table.RowStyles[row].SizeType != SizeType.Absolute)) return;
        bar.WrapContents = true;
        foreach (Button b in bar.Controls)
        {
            b.Width = Math.Max(b.Width, TextRenderer.MeasureText(b.Text, b.Font).Width + 28);
            b.Height = Math.Max(b.Height, 42);
        }
        void Layout()
        {
            int available = Math.Max(100, (table == null ? bar.ClientSize.Width : table.ClientSize.Width - table.Padding.Horizontal - bar.Margin.Horizontal) - bar.Padding.Horizontal), used = 0, line = 0, height = bar.Padding.Vertical;
            foreach (Control button in bar.Controls)
            {
                int w = button.Width + button.Margin.Horizontal, h = button.Height + button.Margin.Vertical;
                if (used > 0 && used + w > available) { height += line; used = 0; line = 0; }
                used += w; line = Math.Max(line, h);
            }
            height += line;
            if (table != null && row >= 0) { table.RowStyles[row].SizeType = SizeType.Absolute; if (Math.Abs(table.RowStyles[row].Height - height) > 1) table.RowStyles[row].Height = height; }
            else if (bar.Dock == DockStyle.Bottom && bar.Height != height) bar.Height = height;
        }
        bar.SizeChanged += (_, _) => Layout(); Layout();
        if (table != null) table.SizeChanged += (_, _) => Layout();
        ActionLayouts.Add(bar, Layout);
    }

    private sealed class WorkspaceSurface : Panel
    {
        internal WorkspaceSurface() { DoubleBuffered = true; ResizeRedraw = true; BackColor = Navy; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2) return;
            using var gradient = new LinearGradientBrush(ClientRectangle, Color.FromArgb(8, 52, 88), Color.FromArgb(5, 31, 58), 90f);
            e.Graphics.FillRectangle(gradient, ClientRectangle);
            using var border = new Pen(Color.FromArgb(35, 104, 144)); e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            using var light = new Pen(Color.FromArgb(55, 182, 218)); e.Graphics.DrawLine(light, 1, 0, Width - 2, 0);
        }
    }
}
