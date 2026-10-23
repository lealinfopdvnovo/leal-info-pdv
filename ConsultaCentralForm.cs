using System.Drawing;
using System.Windows.Forms;

namespace LealInfoPDV;

internal sealed class ConsultaCentralForm : Form
{
    internal ConsultaCentralForm(Action<string, Action> runAllowed,
        Action products, Action customers, Action history, Action orders, Action deliveries, Action quotes)
    {
        Text = "Consultas";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(584, 680);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(5, 24, 43);
        Font = new Font("Segoe UI", 10);
        KeyPreview = true;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7,
            Padding = new Padding(22, 10, 22, 6), BackColor = BackColor };
        for (int i = 0; i < 6; i++) root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 6));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        Controls.Add(root);
        var entries = new[] {
            ("Produtos", "Preços e controle de estoque", "products.png", "products", products),
            ("Clientes", "Dados e histórico do cliente", "customers.png", "customers", customers),
            ("Histórico de vendas", "Consultar vendas realizadas", "history.png", "sales_history", history),
            ("Ordens / OS", "Consultar ordens de serviço", "orders.png", "orders", orders),
            ("Motoboy / Entregas", "Consultar e acompanhar entregas", "orders.png", "deliveries", deliveries),
            ("Orçamentos", "Consultar orçamentos existentes", "quotes.png", "quotes", quotes)
        };
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var button = new Button { Text = entry.Item1.ToUpperInvariant() + "\n" + entry.Item2,
                Tag = entry.Item4, AccessibleName = entry.Item1,
                Dock = DockStyle.Fill, Margin = new Padding(14, 6, 14, 6),
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(8, 59, 98), ForeColor = Color.White,
                UseVisualStyleBackColor = false, Font = new Font("Segoe UI", 12, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft, ImageAlign = ContentAlignment.MiddleLeft,
                TextImageRelation = TextImageRelation.ImageBeforeText, Padding = new Padding(16, 4, 8, 4),
                Cursor = Cursors.Hand, TabIndex = i };
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(0, 150, 210);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 118, 178);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 98, 155);
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", entry.Item3);
            if (File.Exists(iconPath))
            {
                using var source = Image.FromFile(iconPath);
                button.Image = new Bitmap(source, new Size(52, 52));
                button.Disposed += (_, _) => button.Image?.Dispose();
            }
            button.MouseEnter += (_, _) => { button.FlatAppearance.BorderSize = 3; button.FlatAppearance.BorderColor = Color.FromArgb(90, 225, 255); };
            button.MouseLeave += (_, _) => { button.FlatAppearance.BorderSize = 1; button.FlatAppearance.BorderColor = Color.FromArgb(0, 150, 210); };
            button.Click += (_, _) =>
            {
                Hide();
                try { runAllowed(entry.Item4, entry.Item5); }
                finally { if (!IsDisposed) { Show(); Activate(); } }
            };
            root.Controls.Add(button, 0, i);
        }
        root.Controls.Add(new Label { Text = "CONSULTAS • ESC PARA VOLTAR", ForeColor = Color.FromArgb(125, 210, 240),
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9, FontStyle.Bold) }, 0, 6);
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) { e.Handled = true; Close(); } };
        Shown += (_, _) =>
        {
            var work = Screen.FromControl(Owner ?? this).WorkingArea;
            Size = new Size(Math.Min(Width, work.Width - 24), Math.Min(Height, work.Height - 24));
            var anchor = Owner is null ? work : Rectangle.Intersect(Owner.Bounds, work);
            if (anchor.IsEmpty) anchor = work;
            Location = new Point(Math.Clamp(anchor.Left + (anchor.Width - Width) / 2, work.Left, work.Right - Width),
                Math.Clamp(anchor.Top + (anchor.Height - Height) / 2, work.Top, work.Bottom - Height));
        };
    }
}
