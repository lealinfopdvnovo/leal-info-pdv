namespace LealInfoPDV;

// Measure after native layout/theme/DPI changes instead of assuming a 720px page.
internal static class KitchenPaymentLayout
{
    internal static void Apply(Form dialog, TabControl tabs, Panel footer, Button cancel, params Button[] confirmations)
    {
        foreach (var button in confirmations)
        {
            var page = (TabPage)button.Parent!;
            void Align()
            {
                if (page.IsDisposed || button.IsDisposed) return;
                int left = Math.Max(0, (page.ClientSize.Width - button.Width) / 2);
                if (button.Left != left) button.Left = left;
                foreach (var label in page.Controls.OfType<Label>().Where(l => l.TextAlign == ContentAlignment.MiddleCenter))
                {
                    int margin = Math.Max(12, (int)Math.Round(24 * page.DeviceDpi / 96d));
                    label.SetBounds(margin, label.Top, Math.Max(1, page.ClientSize.Width - 2 * margin), label.Height);
                }
            }
            page.SizeChanged += (_, _) => Align();
            button.SizeChanged += (_, _) => Align();
            dialog.Shown += (_, _) => Align();
            Align();
        }
        void AlignCancel()
        {
            int margin = Math.Max(12, (int)Math.Round(24 * footer.DeviceDpi / 96d));
            cancel.Location = new Point(Math.Max(0, footer.ClientSize.Width - cancel.Width - margin), Math.Max(0, (footer.ClientSize.Height - cancel.Height) / 2));
        }
        footer.SizeChanged += (_, _) => AlignCancel();
        cancel.SizeChanged += (_, _) => AlignCancel();
        dialog.Shown += (_, _) => AlignCancel();
        AlignCancel();
    }
}
