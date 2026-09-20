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
            double animationScale = 0.90;
            double visualAlpha = 0.0;

            var bubble = new Panel
            {
                Width = (int)(targetW * animationScale),
                Height = (int)(targetH * animationScale),
                BackColor = Color.Transparent
            };

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

            void CenterBubble()
            {
                bubble.Left = Math.Max(18, (ClientSize.Width - bubble.Width) / 2);
                bubble.Top = Math.Max(70, (ClientSize.Height - bubble.Height) / 2 - 32);
            }

            void ApplyRegion()
            {
                if (bubble.Width < 20 || bubble.Height < 20) return;
                var rect = new Rectangle(0, 0, bubble.Width, bubble.Height);
                using var path = RoundedPath(rect, Math.Min(42, bubble.Height / 3));
                var old = bubble.Region;
                bubble.Region = new Region(path);
                old?.Dispose();
            }

            bubble.Paint += (_, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                int a = Math.Clamp((int)(255 * visualAlpha), 0, 255);
                if (a <= 0) return;

                var rect = new Rectangle(8, 8, bubble.Width - 17, bubble.Height - 17);
                if (rect.Width <= 0 || rect.Height <= 0) return;
                using var path = RoundedPath(rect, 38);

                using (var glass = new System.Drawing.Drawing2D.LinearGradientBrush(
                    rect,
                    Color.FromArgb((int)(225 * visualAlpha), 4, 20, 42),
                    Color.FromArgb((int)(205 * visualAlpha), 4, 88, 132),
                    18f))
                    g.FillPath(glass, path);

                var highlightRect = new Rectangle(rect.Left + 16, rect.Top + 10, rect.Width - 32, Math.Max(28, rect.Height / 3));
                using (var highlightPath = RoundedPath(highlightRect, 24))
                using (var highlight = new System.Drawing.Drawing2D.LinearGradientBrush(
                    highlightRect,
                    Color.FromArgb((int)(48 * visualAlpha), 255, 255, 255),
                    Color.FromArgb(0, 255, 255, 255),
                    90f))
                    g.FillPath(highlight, highlightPath);

                using (var glowWide = new Pen(Color.FromArgb((int)(55 * visualAlpha), 70, 225, 255), 10f))
                    g.DrawPath(glowWide, path);
                using (var glow = new Pen(Color.FromArgb((int)(180 * visualAlpha), 70, 225, 255), 3.2f))
                    g.DrawPath(glow, path);
                using (var chrome = new Pen(Color.FromArgb((int)(225 * visualAlpha), 205, 248, 255), 1.1f))
                    g.DrawPath(chrome, path);

                int orbX = rect.Left + 66;
                int orbY = rect.Top + rect.Height / 2;
                for (int r = 42; r >= 14; r -= 9)
                {
                    int alpha = Math.Max(18, (int)((80 - r / 2) * visualAlpha));
                    using var orbBrush = new SolidBrush(Color.FromArgb(alpha, 55, 218, 255));
                    g.FillEllipse(orbBrush, orbX - r, orbY - r, r * 2, r * 2);
                }
                using (var orbCore = new SolidBrush(Color.FromArgb((int)(235 * visualAlpha), 220, 252, 255)))
                    g.FillEllipse(orbCore, orbX - 10, orbY - 10, 20, 20);
                using (var orbRing = new Pen(Color.FromArgb((int)(220 * visualAlpha), 85, 235, 255), 2f))
                {
                    g.DrawEllipse(orbRing, orbX - 48, orbY - 48, 96, 96);
                    g.DrawArc(orbRing, orbX - 56, orbY - 56, 112, 112, -55, 225);
                }

                using var techPen = new Pen(Color.FromArgb((int)(100 * visualAlpha), 85, 235, 255), 1.2f);
                g.DrawLine(techPen, rect.Left + 125, rect.Top + 42, rect.Left + 190, rect.Top + 42);
                g.DrawLine(techPen, rect.Right - 190, rect.Bottom - 42, rect.Right - 125, rect.Bottom - 42);
                for (int i = 0; i < 4; i++)
                {
                    using var dot = new SolidBrush(Color.FromArgb((int)(150 * visualAlpha), 120, 240, 255));
                    g.FillEllipse(dot, rect.Right - 70 - i * 15, rect.Top + 28, 5, 5);
                }
            };

            var badge = new Label
            {
                Text = "  LIC • INTELIGÊNCIA ATIVA  ",
                AutoSize = true,
                Left = 150,
                Top = 24,
                ForeColor = Color.FromArgb(150, 238, 255),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            var title = new Label
            {
                Text = "BEM-VINDO AO FUTURO DO SEU NEGÓCIO",
                Left = 135,
                Top = 62,
                Width = targetW - 175,
                Height = 54,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 20.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            var message = new Label
            {
                Text = "TECNOLOGIA E INTELIGÊNCIA TRABALHANDO COM VOCÊ.",
                Left = 135,
                Top = 116,
                Width = targetW - 175,
                Height = 42,
                ForeColor = Color.FromArgb(115, 225, 255),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 11.8f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            var divider = new Label
            {
                Left = 170,
                Top = 166,
                Width = targetW - 245,
                Height = 1,
                BackColor = Color.FromArgb(90, 185, 235)
            };
            var brand = new Label
            {
                Text = "LEAL INFO PDV PRO   •   Inteligência que simplifica. Tecnologia que conecta.",
                Left = 135,
                Top = 178,
                Width = targetW - 175,
                Height = 35,
                ForeColor = Color.FromArgb(225, 245, 255),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10.2f),
                TextAlign = ContentAlignment.MiddleCenter
            };

            bubble.Controls.Add(badge);
            bubble.Controls.Add(title);
            bubble.Controls.Add(message);
            bubble.Controls.Add(divider);
            bubble.Controls.Add(brand);
            Controls.Add(bubble);
            CenterBubble();
            ApplyRegion();
            bubble.BringToFront();

            var started = DateTime.UtcNow;
            var timer = new System.Windows.Forms.Timer { Interval = 16 };
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
                    timer.Dispose();
                    Controls.Remove(bubble);
                    bubble.Dispose();
                    return;
                }

                if (ms < 480)
                {
                    double t = ms / 480.0;
                    double eased = 1.0 - Math.Pow(1.0 - t, 3.0);
                    visualAlpha = eased;
                    animationScale = 0.90 + 0.10 * eased;
                }
                else if (ms > 4300)
                {
                    double t = (ms - 4300.0) / 700.0;
                    visualAlpha = Math.Max(0, 1.0 - t);
                    animationScale = 1.0 - 0.025 * t;
                }
                else
                {
                    visualAlpha = 1.0;
                    animationScale = 1.0;
                }

                bubble.Width = (int)(targetW * animationScale);
                bubble.Height = (int)(targetH * animationScale);
                CenterBubble();
                ApplyRegion();

                int alpha = Math.Clamp((int)(255 * visualAlpha), 0, 255);
                title.ForeColor = Color.FromArgb(alpha, 255, 255, 255);
                message.ForeColor = Color.FromArgb(alpha, 115, 225, 255);
                brand.ForeColor = Color.FromArgb(alpha, 225, 245, 255);
                badge.ForeColor = Color.FromArgb(alpha, 150, 238, 255);
                divider.BackColor = Color.FromArgb(Math.Min(alpha, 90), 185, 235);
                bubble.Invalidate();
            };
            bubble.Disposed += (_, _) =>
            {
                if (timer.Enabled) timer.Stop();
                timer.Dispose();
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
Write-Host 'Boas-vindas holografica premium aplicada.'
