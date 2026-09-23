$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot '..\MainForm.cs'
$content = Get-Content $path -Raw -Encoding UTF8

if ($content -notmatch 'var equipmentTab = new TabPage') {
    $old = 'var systemTab = new TabPage("SISTEMA E SEGURANÇA") { BackColor = Color.FromArgb(224,239,248), Padding = new Padding(26) };'
    $new = $old + [Environment]::NewLine + '        var equipmentTab = new TabPage("EQUIPAMENTOS") { BackColor = Color.FromArgb(224,239,248), Padding = new Padding(26) };'
    if (-not $content.Contains($old)) { throw 'Ponto da aba de configuracoes nao encontrado.' }
    $content = $content.Replace($old, $new)

    $old = 'tabs.TabPages.Add(systemTab);'
    $new = $old + [Environment]::NewLine + '        tabs.TabPages.Add(equipmentTab);'
    $content = $content.Replace($old, $new)

    $old = @'
        f.Controls.Add(tabs);
        tabs.BringToFront();
'@
    $new = @'
        f.Controls.Add(tabs);
        tabs.BringToFront();

        var embeddedEquipment = new EquipmentSettingsForm(embedded: true)
        {
            TopLevel = false,
            FormBorderStyle = FormBorderStyle.None,
            Dock = DockStyle.Fill,
            MinimumSize = Size.Empty
        };
        equipmentTab.Padding = new Padding(4);
        equipmentTab.Controls.Add(embeddedEquipment);
        embeddedEquipment.Show();
'@
    if (-not $content.Contains($old)) { throw 'Ponto do painel de equipamentos nao encontrado.' }
    $content = $content.Replace($old, $new)
}

if ($content -notmatch 'ThermalPrinterService.PrintAutomaticallyIfEnabled') {
    $old = '                ShowReceipt(receipt);'
    $new = $old + [Environment]::NewLine + '                ThermalPrinterService.PrintAutomaticallyIfEnabled(receipt, this);'
    if (-not $content.Contains($old)) { throw 'Ponto de impressao automatica nao encontrado.' }
    $content = $content.Replace($old, $new)
}

Set-Content $path $content -Encoding UTF8
Write-Host 'Central de equipamentos aplicada com sucesso.'
