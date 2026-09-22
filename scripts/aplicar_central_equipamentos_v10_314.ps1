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

        var equipmentPanel = new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(45) };
        equipmentPanel.RowStyles.Add(new RowStyle(SizeType.Percent,30));
        equipmentPanel.RowStyles.Add(new RowStyle(SizeType.Percent,40));
        equipmentPanel.RowStyles.Add(new RowStyle(SizeType.Percent,30));
        var equipmentText = new Label { Text="CONFIGURE BALANÇAS, IMPRESSORAS TÉRMICAS E MODELOS DE BOBINA",Dock=DockStyle.Fill,ForeColor=DarkBlue,Font=new Font("Segoe UI",16,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter };
        var equipmentButton = new Button { Text="ABRIR CENTRAL DE EQUIPAMENTOS",Dock=DockStyle.Fill,Margin=new Padding(60,20,60,20),BackColor=Color.FromArgb(0,145,85),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",14,FontStyle.Bold) };
        equipmentButton.FlatAppearance.BorderSize=0;
        equipmentButton.Click += (_,_) => EquipmentSettingsForm.Open(f);
        var equipmentHint = new Label { Text="Impressoras instaladas no Windows • Bobinas 58, 76, 80 mm ou personalizada\nBalanças COM/RS-232 • TCP/IP • Teclado/HID • Etiqueta com código de barras",Dock=DockStyle.Fill,ForeColor=DarkBlue,Font=new Font("Segoe UI",10),TextAlign=ContentAlignment.MiddleCenter };
        equipmentPanel.Controls.Add(equipmentText,0,0);equipmentPanel.Controls.Add(equipmentButton,0,1);equipmentPanel.Controls.Add(equipmentHint,0,2);
        equipmentTab.Controls.Add(equipmentPanel);
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
