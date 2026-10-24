$ErrorActionPreference = 'Stop'
$path = 'MainForm.cs'
$main = Get-Content $path -Raw -Encoding UTF8

$start = $main.IndexOf('    private void ShowPostLoginWelcome()')
$end = $main.IndexOf('    private void StartNavigationListener()', $start)
if ($start -lt 0 -or $end -lt 0) { throw 'Metodo ShowPostLoginWelcome nao localizado' }

$method = @'
    private void ShowPostLoginWelcome()
    {
        try
        {
            int targetW = Math.Min(900, Math.Max(680, ClientSize.Width - 160));
            const int targetH = 250;
            double visualAlpha = 0.0;

            var bubble = new Panel
            {
                Width = targetW,
                Height = targetH,
                BackColor = Color.Transparent
            };

            typeof(Panel).GetProperty(
                "DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            )?.SetValue(bubble, true);

            System.Drawing.Drawing2D.GraphicsPath RoundedPath(Rectangle rect, int radius)
            {
                var path = new System.Drawing.Drawing2D.GraphicsPath();
                int d = radius * 2;
                path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                return path;
            }

            var badgeFont = new Font("Segoe UI", 9f, FontStyle.Bold);
            var titleFont = new Font("Segoe UI", 20.5f, FontStyle.Bold);
            var messageFont = new Font("Segoe UI", 11.8f, FontStyle.Bold);
            var brandFont = new Font("Segoe UI", 10.2f);

            bubble.Paint += (_, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                int a = Math.Clamp((int)(255 * visualAlpha), 0, 255);
                if (a <= 0) return;

                var rect = new Rectangle(8, 8, bubble.Width - 17, bubble.Height - 17);
                using var path = RoundedPath(rect, 38);

                using (var glass = new System.Drawing.Drawing2D.LinearGradientBrush(
                    rect,
                    Color.FromArgb(Math.Clamp((int)(225 * visualAlpha), 0, 255), 4, 20, 42),
                    Color.FromArgb(Math.Clamp((int)(205 * visualAlpha), 0, 255), 4, 88, 132),
                    18f))
                {
                    g.FillPath(glass, path);
                }

                var highlightRect = new Rectangle(rect.Left + 16, rect.Top + 10, rect.Width - 32, Math.Max(28, rect.Height / 3));
                using (var highlightPath = RoundedPath(highlightRect, 24))
                using (var highlight = new System.Drawing.Drawing2D.LinearGradientBrush(
                    highlightRect,
                    Color.FromArgb(Math.Clamp((int)(48 * visualAlpha), 0, 255), 255, 255, 255),
                    Color.FromArgb(0, 255, 255, 255),
                    90f))
                {
                    g.FillPath(highlight, highlightPath);
                }

                using (var glowWide = new Pen(Color.FromArgb(Math.Clamp((int)(55 * visualAlpha), 0, 255), 70, 225, 255), 10f))
                    g.DrawPath(glowWide, path);
                using (var glow = new Pen(Color.FromArgb(Math.Clamp((int)(180 * visualAlpha), 0, 255), 70, 225, 255), 3.2f))
                    g.DrawPath(glow, path);
                using (var chrome = new Pen(Color.FromArgb(Math.Clamp((int)(225 * visualAlpha), 0, 255), 205, 248, 255), 1.1f))
                    g.DrawPath(chrome, path);

                int orbX = rect.Left + 66;
                int orbY = rect.Top + rect.Height / 2;
                for (int r = 42; r >= 14; r -= 9)
                {
                    int orbAlpha = Math.Clamp((int)((80 - r / 2) * visualAlpha), 0, 255);
                    using var orbBrush = new SolidBrush(Color.FromArgb(orbAlpha, 55, 218, 255));
                    g.FillEllipse(orbBrush, orbX - r, orbY - r, r * 2, r * 2);
                }
                using (var orbCore = new SolidBrush(Color.FromArgb(Math.Clamp((int)(235 * visualAlpha), 0, 255), 220, 252, 255)))
                    g.FillEllipse(orbCore, orbX - 10, orbY - 10, 20, 20);
                using (var orbRing = new Pen(Color.FromArgb(Math.Clamp((int)(220 * visualAlpha), 0, 255), 85, 235, 255), 2f))
                {
                    g.DrawEllipse(orbRing, orbX - 48, orbY - 48, 96, 96);
                    g.DrawArc(orbRing, orbX - 56, orbY - 56, 112, 112, -55, 225);
                }

                using (var techPen = new Pen(Color.FromArgb(Math.Clamp((int)(100 * visualAlpha), 0, 255), 85, 235, 255), 1.2f))
                {
                    g.DrawLine(techPen, rect.Left + 125, rect.Top + 42, rect.Left + 190, rect.Top + 42);
                    g.DrawLine(techPen, rect.Right - 190, rect.Bottom - 42, rect.Right - 125, rect.Bottom - 42);
                }
                for (int i = 0; i < 4; i++)
                {
                    using var dot = new SolidBrush(Color.FromArgb(Math.Clamp((int)(150 * visualAlpha), 0, 255), 120, 240, 255));
                    g.FillEllipse(dot, rect.Right - 70 - i * 15, rect.Top + 28, 5, 5);
                }

                using var badgeBrush = new SolidBrush(Color.FromArgb(a, 150, 238, 255));
                using var titleBrush = new SolidBrush(Color.FromArgb(a, 255, 255, 255));
                using var messageBrush = new SolidBrush(Color.FromArgb(a, 115, 225, 255));
                using var brandBrush = new SolidBrush(Color.FromArgb(a, 225, 245, 255));
                using var dividerPen = new Pen(Color.FromArgb(Math.Clamp((int)(90 * visualAlpha), 0, 255), 185, 235), 1f);
                using var center = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                using var left = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center
                };

                g.DrawString("LIC • INTELIGÊNCIA ATIVA", badgeFont, badgeBrush,
                    new RectangleF(145, 18, targetW - 210, 30), left);
                g.DrawString("BEM-VINDO AO FUTURO DO SEU NEGÓCIO", titleFont, titleBrush,
                    new RectangleF(135, 55, targetW - 175, 62), center);
                g.DrawString("TECNOLOGIA E INTELIGÊNCIA TRABALHANDO COM VOCÊ.", messageFont, messageBrush,
                    new RectangleF(135, 116, targetW - 175, 42), center);

                g.DrawLine(dividerPen, 170, 168, targetW - 75, 168);

                g.DrawString("LEAL INFO PDV PRO   •   Inteligência que simplifica. Tecnologia que conecta.", brandFont, brandBrush,
                    new RectangleF(135, 176, targetW - 175, 42), center);
            };

            Controls.Add(bubble);
            bubble.Left = Math.Max(18, (ClientSize.Width - bubble.Width) / 2);
            bubble.Top = Math.Max(70, (ClientSize.Height - bubble.Height) / 2 - 32);

            using (var regionPath = RoundedPath(new Rectangle(0, 0, bubble.Width, bubble.Height), 42))
                bubble.Region = new Region(regionPath);

            bubble.BringToFront();

            var started = DateTime.UtcNow;
            var timer = new System.Windows.Forms.Timer { Interval = 20 };
            timer.Tick += (_, _) =>
            {
                if (bubble.IsDisposed)
                {
                    timer.Stop();
                    timer.Dispose();
                    return;
                }

                double ms = (DateTime.UtcNow - started).TotalMilliseconds;
                if (ms >= 5000)
                {
                    timer.Stop();
                    Controls.Remove(bubble);
                    bubble.Dispose();
                    return;
                }

                if (ms < 320)
                {
                    double t = Math.Clamp(ms / 320.0, 0.0, 1.0);
                    visualAlpha = t * t * (3.0 - 2.0 * t);
                }
                else if (ms > 4650)
                {
                    double t = Math.Clamp((ms - 4650.0) / 350.0, 0.0, 1.0);
                    double eased = t * t * (3.0 - 2.0 * t);
                    visualAlpha = 1.0 - eased;
                }
                else
                {
                    visualAlpha = 1.0;
                }

                bubble.Invalidate();
            };

            bubble.Disposed += (_, _) =>
            {
                if (timer.Enabled) timer.Stop();
                timer.Dispose();
                badgeFont.Dispose();
                titleFont.Dispose();
                messageFont.Dispose();
                brandFont.Dispose();
            };

            timer.Start();
        }
        catch
        {
            // A apresentacao e puramente visual e nunca pode impedir a abertura do PDV.
        }
    }

'@

$main = $main.Substring(0, $start) + $method + $main.Substring($end)
Set-Content $path $main -Encoding UTF8
Write-Host 'Boas-vindas V10.303 aplicada: tamanho fixo, fade suave e sem zoom.'
