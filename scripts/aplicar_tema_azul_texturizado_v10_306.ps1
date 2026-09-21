$ErrorActionPreference = 'Stop'
$path = 'MainForm.cs'
$main = Get-Content $path -Raw -Encoding UTF8

# Nova opcao de tema usando a textura azul fornecida pelo usuario.
if (-not $main.Contains('case "Azul Texturizado":')) {
    $case = @'
                case "Azul Texturizado":
                    bg = Color.FromArgb(3, 18, 38);
                    headerBg = Color.FromArgb(4, 45, 95);
                    accent = Color.FromArgb(0, 125, 255);
                    accentHover = Color.FromArgb(45, 175, 255);
                    leftBg = Color.FromArgb(4, 28, 65);
                    rightBg = Color.FromArgb(10, 36, 74);
                    fieldBg = Color.FromArgb(238, 247, 255);
                    textDark = Color.FromArgb(5, 42, 82);
                    soft = Color.FromArgb(205, 226, 245);
                    secondary = Color.FromArgb(8, 55, 105);
                    break;

'@
    $anchor = '                case "PDV Rosa":'
    if ($main.Contains($anchor)) {
        $main = $main.Replace($anchor, $case + $anchor)
    }
}

# Aplica a imagem somente como plano de fundo. Controles, icones, textos e eventos ficam por cima intactos.
if (-not $main.Contains('tema_azul_texturizado.jpg')) {
    $block = @'
            if (theme == "Azul Texturizado")
            {
                var azulTexturePath = Path.Combine(AppContext.BaseDirectory, "Assets", "tema_azul_texturizado.jpg");
                if (File.Exists(azulTexturePath))
                {
                    using var azulOriginal = Image.FromFile(azulTexturePath);
                    foreach (var c in new Control[] { f, body, header, photoShowcase, left, right })
                    {
                        c.BackgroundImage?.Dispose();
                        c.BackgroundImage = new Bitmap(azulOriginal);
                        c.BackgroundImageLayout = ImageLayout.Stretch;
                    }
                }
            }

'@
    $anchor = '            if (theme == "PDV Rosa")'
    if ($main.Contains($anchor)) {
        $main = $main.Replace($anchor, $block + $anchor)
    }
}

# Mantem a grade em 3 linhas e posiciona Azul Texturizado ao lado do PDV Rosa.
$main = $main.Replace('RowCount = 4,', 'RowCount = 3,')
$main = $main.Replace('options.Controls.Add(ThemeCard("Azul Texturizado", "Textura azul profunda", Color.FromArgb(4, 28, 65), Color.FromArgb(0, 125, 255)), 0, 3);', 'options.Controls.Add(ThemeCard("Azul Texturizado", "Textura azul profunda", Color.FromArgb(4, 28, 65), Color.FromArgb(0, 125, 255)), 1, 2);')

if (-not $main.Contains('ThemeCard("Azul Texturizado"')) {
    $card = '            options.Controls.Add(ThemeCard("Azul Texturizado", "Textura azul profunda", Color.FromArgb(4, 28, 65), Color.FromArgb(0, 125, 255)), 1, 2);'
    $anchor = '            tf.ShowDialog(f);'
    if ($main.Contains($anchor)) {
        $main = $main.Replace($anchor, $card + "`r`n`r`n" + $anchor)
    }
}

Set-Content $path $main -Encoding UTF8
Write-Host 'Tema Azul Texturizado alinhado na terceira linha ao lado do PDV Rosa.'
