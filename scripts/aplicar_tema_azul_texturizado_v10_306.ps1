$ErrorActionPreference = 'Stop'
$path = 'MainForm.cs'
$main = Get-Content $path -Raw -Encoding UTF8

# Nova opcao de tema usando a textura azul fornecida pelo usuario.
if ($main -notmatch 'case "Azul Texturizado":') {
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
    if (-not $main.Contains($anchor)) { throw 'Anchor do tema PDV Rosa nao localizado' }
    $main = $main.Replace($anchor, $case + $anchor)
}

# Aplica a imagem somente como plano de fundo. Controles, icones, textos e eventos ficam por cima intactos.
if ($main -notmatch 'tema_azul_texturizado\.jpg') {
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
    if (-not $main.Contains($anchor)) { throw 'Bloco do tema PDV Rosa nao localizado' }
    $main = $main.Replace($anchor, $block + $anchor)
}

# Expande a grade do seletor e cria o novo cartao na aba Estilo.
$main = [regex]::Replace($main,
    '(?s)(var options = new TableLayoutPanel\s*\{.*?ColumnCount = 2,\s*)RowCount = 3,',
    '${1}RowCount = 4,', 1)

if ($main -notmatch 'ThemeCard\("Azul Texturizado"') {
    $anchor = '            options.Controls.Add(ThemeCard("PDV Rosa", "Rosé texturizado + vinho acetinado", Color.FromArgb(125, 20, 86), Color.FromArgb(255, 72, 165)), 0, 2);'
    $card = '            options.Controls.Add(ThemeCard("Azul Texturizado", "Textura azul profunda", Color.FromArgb(4, 28, 65), Color.FromArgb(0, 125, 255)), 0, 3);'
    if (-not $main.Contains($anchor)) { throw 'Cartao PDV Rosa nao localizado' }
    $main = $main.Replace($anchor, $anchor + "`r`n" + $card)
}

Set-Content $path $main -Encoding UTF8
Write-Host 'Tema Azul Texturizado aplicado sem alterar os elementos da tela.'
