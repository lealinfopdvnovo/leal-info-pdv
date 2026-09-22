$ErrorActionPreference = 'Stop'
$path = 'MainForm.cs'
$text = Get-Content $path -Raw -Encoding UTF8
$anchor = "    private void EditProduct(long? id)`r`n    {"
if (-not $text.Contains($anchor)) {
    $anchor = "    private void EditProduct(long? id)`n    {"
}
if (-not $text.Contains($anchor)) { throw 'Metodo EditProduct nao localizado' }
if (-not $text.Contains('ProductRegistrationForm.Show(this, id);')) {
    $replacement = $anchor + "`r`n        ProductRegistrationForm.Show(this, id);`r`n        return;"
    $text = $text.Replace($anchor, $replacement)
}
Set-Content $path $text -Encoding UTF8
Write-Host 'Cadastro avancado de produtos V10.312 aplicado.'
