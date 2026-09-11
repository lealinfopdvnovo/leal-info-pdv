using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace LealInfoPDV;

/// <summary>
/// Aplica somente à barra principal de atalhos um layout mais compacto,
/// preservando os ícones, ações e regras já existentes no MainForm.
/// Não altera a abertura da LIA nem o fluxo funcional do PDV.
/// </summary>
internal static class CompactShortcutBarBootstrap
{
    private static readonly ConditionalWeakTable<MainForm, FlowLayoutPanel> Applied = new();
    private static readonly MethodInfo? OnClickMethod = typeof(Control).GetMethod(
        "OnClick",
        BindingFlags.Instance | BindingFlags.NonPublic);

    [ModuleInitializer]
    internal static void Initialize()
    {
        Application.Idle += ApplyWhenReady;
    }

    private static void ApplyWhenReady(object? sender, EventArgs e)
    {
        foreach (var main in Application.OpenForms.OfType<MainForm>())
        {
            if (main.IsDisposed || !main.Visible || Applied.TryGetValue(main, out _))
                continue;

            var original = FindOriginalShortcutBar(main);
            if (original is null)
                continue;

            try
            {
                var compact = BuildCompactBar(original);
                var parent = original.Parent;
                if (parent is null)
                    continue;

                var index = parent.Controls.GetChildIndex(original);
                original.Visible = false;
                parent.Controls.Add(compact);
                parent.Controls.SetChildIndex(compact, index);

                Applied.Add(main, compact);
                LayoutCards(compact);

                compact.SizeChanged += (_, _) => LayoutCards(compact);
                main.Resize += (_, _) => LayoutCards(compact);
            }
            catch
            {
                // Mudança puramente visual: nunca impede a abertura do PDV.
            }
        }
    }

    private static FlowLayoutPanel? FindOriginalShortcutBar(Control root)
    {
        foreach (var flow in Descendants(root).OfType<FlowLayoutPanel>())
        {
            var directCards = flow.Controls.OfType<Panel>().ToList();
            if (directCards.Count < 10)
                continue;

            var titles = directCards
                .Select(GetCardText)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(Normalize)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (titles.Contains("PRODUTOS") &&
                titles.Contains("CLIENTES") &&
                titles.Contains("FORNECEDORES") &&
                titles.Contains("SAIR"))
            {
                return flow;
            }
        }

        return null;
    }

    private static FlowLayoutPanel BuildCompactBar(FlowLayoutPanel original)
    {
        var bar = new FlowLayoutPanel
        {
            Dock = original.Dock,
            Height = 78,
            BackColor = Color.FromArgb(4, 55, 94),
            Padding = new Padding(3, 3, 3, 2),
            Margin = original.Margin,
            WrapContents = false,
            AutoScroll = false,
            FlowDirection = FlowDirection.LeftToRight
        };

        foreach (var originalCard in original.Controls.OfType<Panel>())
        {
            var text = GetCardText(originalCard);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var sourceImage = Descendants(originalCard).OfType<PictureBox>().FirstOrDefault()?.Image;

            var card = new Panel
            {
                Height = 72,
                Margin = new Padding(1, 0, 1, 0),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            var pic = new PictureBox
            {
                Width = 30,
                Height = 30,
                Top = 4,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Image = sourceImage,
                Cursor = Cursors.Hand
            };

            var caption = new Label
            {
                Text = text,
                Top = 36,
                Height = 32,
                TextAlign = ContentAlignment.TopCenter,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 6.8f, FontStyle.Bold),
                AutoEllipsis = false,
                Cursor = Cursors.Hand
            };

            void AlignCard()
            {
                pic.Left = Math.Max(0, (card.ClientSize.Width - pic.Width) / 2);
                caption.Left = 1;
                caption.Width = Math.Max(1, card.ClientSize.Width - 2);
            }

            card.SizeChanged += (_, _) => AlignCard();

            void Run(object? _, EventArgs __)
            {
                try
                {
                    OnClickMethod?.Invoke(originalCard, new object[] { EventArgs.Empty });
                }
                catch
                {
                    // Mantém o comportamento atual se algum atalho não puder ser encaminhado.
                }
            }

            card.Click += Run;
            pic.Click += Run;
            caption.Click += Run;

            void Hover(bool on)
            {
                card.BackColor = on
                    ? Color.FromArgb(0, 92, 145)
                    : Color.Transparent;
            }

            card.MouseEnter += (_, _) => Hover(true);
            pic.MouseEnter += (_, _) => Hover(true);
            caption.MouseEnter += (_, _) => Hover(true);
            card.MouseLeave += (_, _) => Hover(false);
            pic.MouseLeave += (_, _) => Hover(false);
            caption.MouseLeave += (_, _) => Hover(false);

            card.Controls.Add(pic);
            card.Controls.Add(caption);
            bar.Controls.Add(card);
            AlignCard();
        }

        return bar;
    }

    private static void LayoutCards(FlowLayoutPanel bar)
    {
        if (bar.Controls.Count == 0)
            return;

        var usable = Math.Max(900, bar.ClientSize.Width - bar.Padding.Horizontal - 4);
        var each = Math.Max(68, usable / bar.Controls.Count);

        foreach (Control card in bar.Controls)
        {
            card.Width = Math.Max(66, each - card.Margin.Horizontal);
            card.Height = 72;
        }
    }

    private static string GetCardText(Control card)
    {
        return Descendants(card)
            .OfType<Label>()
            .Select(l => l.Text?.Trim())
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? string.Empty;
    }

    private static string Normalize(string text)
    {
        return text
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("  ", " ")
            .Trim();
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }
}
