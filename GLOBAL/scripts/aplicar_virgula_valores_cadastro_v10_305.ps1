$ErrorActionPreference = 'Stop'
$path = 'MainForm.cs'
$source = Get-Content $path -Raw -Encoding UTF8
$needle = '        var minStock = Field("0");'
if (-not $source.Contains($needle)) { throw 'Ponto de insercao do cadastro de produtos nao encontrado.' }
if ($source.Contains('UseBrazilianCurrencyInput(cost);')) {
    Write-Host 'Formato brasileiro dos valores ja aplicado.'
    exit 0
}
$block = @'
        var minStock = Field("0");

        // V10.305: valores monetarios do cadastro usam virgula decimal (pt-BR).
        // Mantem os campos de Custo e Venda sempre no formato 0,00 e converte ponto digitado em virgula.
        void UseBrazilianCurrencyInput(TextBox field)
        {
            field.KeyPress += (_, e) =>
            {
                if (char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar)) return;

                if (e.KeyChar == '.' || e.KeyChar == ',')
                {
                    e.KeyChar = ',';
                    if (field.Text.Contains(',') && field.SelectionLength == 0)
                        e.Handled = true;
                    return;
                }

                e.Handled = true;
            };

            field.Leave += (_, _) =>
            {
                field.Text = Num(field.Text).ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
            };
        }

        UseBrazilianCurrencyInput(cost);
        UseBrazilianCurrencyInput(price);
'@
$source = $source.Replace($needle, $block)
Set-Content $path $source -Encoding UTF8
if ((Get-Content $path -Raw -Encoding UTF8) -notmatch 'UseBrazilianCurrencyInput\(price\);') { throw 'Formato de virgula nao foi aplicado.' }
Write-Host 'Cadastro de produtos configurado para valores com virgula decimal.'
