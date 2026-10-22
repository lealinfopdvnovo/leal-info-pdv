namespace LealInfoPDV;

// Keep confirmation outside the scrolling fields and measure its actual viewport.
internal static class KitchenPaymentLayout
{
    internal static void Apply(Form dialog, TabControl tabs, Panel footer, Button cancel, params Button[] confirmations)
    {
        foreach (var button in confirmations)
        {
            var page = (TabPage)button.Parent!;
            var fields = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var actions = new Panel { Dock = DockStyle.Bottom, Height = button.Height + 24 };
            foreach (var field in page.Controls.Cast<Control>().Where(c => c != button).ToArray()) fields.Controls.Add(field);
            actions.Controls.Add(button);
            page.Controls.Add(fields);
            page.Controls.Add(actions);
            actions.BringToFront();
            void Align()
            {
                if (actions.IsDisposed || button.IsDisposed) return;
                button.Location = new Point(Math.Max(0, (actions.ClientSize.Width - button.Width) / 2), Math.Max(0, (actions.ClientSize.Height - button.Height) / 2));
                foreach (var label in fields.Controls.OfType<Label>().Where(l => l.TextAlign == ContentAlignment.MiddleCenter))
                {
                    int margin = Math.Max(12, (int)Math.Round(24 * fields.DeviceDpi / 96d));
                    label.SetBounds(margin, label.Top, Math.Max(1, fields.ClientSize.Width - 2 * margin), label.Height);
                }
            }
            actions.SizeChanged += (_, _) => Align();
            fields.SizeChanged += (_, _) => Align();
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
