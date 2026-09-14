$ErrorActionPreference = 'Stop'
$path = 'MainForm.cs'
$text = Get-Content $path -Raw
$anchor = @'
        Controls.Add(menu);
'@
if (-not $text.Contains($anchor)) { throw 'Ponto de insercao LIC AI nao encontrado' }
$insert = @'
        Controls.Add(menu);

        // V10.196: botao LIC AI com a logica do antigo orbe, corrigindo a ordem da transparencia.
        var licHeart = new Control
        {
            Size = new Size(112, 100),
            Cursor = Cursors.Hand,
            TabStop = false,
            Anchor = AnchorStyles.Bottom
        };
        var setStyle = licHeart.GetType().GetMethod("SetStyle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        setStyle!.Invoke(licHeart, new object[] { ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true });
        var updateStyles = licHeart.GetType().GetMethod("UpdateStyles", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        updateStyles?.Invoke(licHeart, null);
        licHeart.BackColor = Color.Transparent;

        void PositionLicHeart()
        {
            licHeart.Location = new Point((ClientSize.Width - licHeart.Width) / 2, ClientSize.Height - licHeart.Height - 18);
            licHeart.BringToFront();
        }

        double licHeartPhase = 0;
        licHeart.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            float pulse = (float)((Math.Sin(licHeartPhase) + 1.0) / 2.0);
            float scale = 0.90f + pulse * 0.10f;
            float w = 92f * scale;
            float h = 82f * scale;
            float x = (licHeart.ClientSize.Width - w) / 2f;
            float y = (licHeart.ClientSize.Height - h) / 2f;

            using var heart = new System.Drawing.Drawing2D.GraphicsPath();
            var p0 = new PointF(x + w * .50f, y + h * .93f);
            heart.StartFigure();
            heart.AddBezier(p0,
                new PointF(x + w * .43f, y + h * .83f),
                new PointF(x + w * .06f, y + h * .60f),
                new PointF(x + w * .08f, y + h * .32f));
            heart.AddBezier(
                new PointF(x + w * .08f, y + h * .32f),
                new PointF(x + w * .10f, y + h * .08f),
                new PointF(x + w * .34f, y + h * .02f),
                new PointF(x + w * .50f, y + h * .24f));
            heart.AddBezier(
                new PointF(x + w * .50f, y + h * .24f),
                new PointF(x + w * .66f, y + h * .02f),
                new PointF(x + w * .90f, y + h * .08f),
                new PointF(x + w * .92f, y + h * .32f));
            heart.AddBezier(
                new PointF(x + w * .92f, y + h * .32f),
                new PointF(x + w * .94f, y + h * .60f),
                new PointF(x + w * .57f, y + h * .83f),
                p0);
            heart.CloseFigure();

            using var halo = new Pen(Color.FromArgb(55 + (int)(pulse * 80), 255, 35, 65), 5f + pulse * 2f);
            e.Graphics.DrawPath(halo, heart);
            using var fill = new System.Drawing.Drawing2D.PathGradientBrush(heart)
            {
                CenterColor = Color.FromArgb(255, 242, 35, 64),
                SurroundColors = new[] { Color.FromArgb(255, 165, 0, 30) }
            };
            fill.CenterPoint = new PointF(x + w * .38f, y + h * .32f);
            e.Graphics.FillPath(fill, heart);
            using var gloss = new SolidBrush(Color.FromArgb(58, 255, 255, 255));
            e.Graphics.FillEllipse(gloss, x + w * .23f, y + h * .18f, w * .17f, h * .11f);

            using var font = new Font("Segoe UI", 13.5f * scale, FontStyle.Bold, GraphicsUnit.Point);
            using var textBrush = new SolidBrush(Color.White);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            var textRect = new RectangleF(x + w * .12f, y + h * .25f, w * .76f, h * .42f);
            e.Graphics.DrawString("LIC AI", font, textBrush, textRect, sf);
        };

        licHeart.Click += (_, _) =>
        {
            try
            {
                var licExe = Path.Combine(AppContext.BaseDirectory, "LIC-AI", "LicAi.exe");
                if (!File.Exists(licExe)) { MessageBox.Show("LIC AI nao foi encontrada nesta instalacao. Atualize o PDV e tente novamente.", "LIC AI", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = licExe, WorkingDirectory = Path.GetDirectoryName(licExe) ?? AppContext.BaseDirectory, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show("Nao foi possivel abrir a LIC AI.\n\n" + ex.Message, "LIC AI", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };

        var licPulseTimer = new System.Windows.Forms.Timer { Interval = 35 };
        licPulseTimer.Tick += (_, _) => { licHeartPhase += 0.10; licHeart.Invalidate(); };
        Controls.Add(licHeart);
        PositionLicHeart();
        Resize += (_, _) => PositionLicHeart();
        Shown += (_, _) => { PositionLicHeart(); licPulseTimer.Start(); };
        FormClosed += (_, _) => { licPulseTimer.Stop(); licPulseTimer.Dispose(); };
'@
$text = $text.Replace($anchor, $insert)
Set-Content $path $text -Encoding UTF8
Write-Host 'LIC AI V10.196: transparencia corrigida sem perder o coracao pulsante.'
