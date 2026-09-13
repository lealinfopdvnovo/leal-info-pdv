$ErrorActionPreference='Stop'
$path='MainForm.cs'
$c=Get-Content $path -Raw

# Campo e inicializacao.
$c=[regex]::Replace($c,'(?m)^\s*private LiaOrbLauncher\? liaAiFloatingButton;\r?\n','')
$c=[regex]::Replace($c,'(?m)^\s*BuildLiaAiFloatingButton\(\);\r?\n','')
$c=$c.Replace(', "LIA"','')

# Remove o bloco do menu linha a linha para nao depender de acentos/CRLF.
$linhas=$c -split "`r?`n"
$saida=New-Object System.Collections.Generic.List[string]
$pularMenu=$false
foreach($linha in $linhas){
    if(-not $pularMenu -and $linha -match 'else if \(title == "LIA"\)'){$pularMenu=$true;continue}
    if($pularMenu -and $linha -match 'else if \(title == "Utilit'){$pularMenu=$false;$saida.Add($linha);continue}
    if(-not $pularMenu){$saida.Add($linha)}
}
$c=[string]::Join([Environment]::NewLine,$saida)

# Remove em bloco o launcher e toda apresentacao/voz/orbe.
$inicio=$c.IndexOf('private void BuildLiaAiFloatingButton()')
$fim=$c.IndexOf('private void ShowCadastroHelp()', [Math]::Max(0,$inicio))
if($inicio -ge 0 -and $fim -gt $inicio){
    $lineStart=$c.LastIndexOf([Environment]::NewLine,$inicio)
    if($lineStart -lt 0){$lineStart=$inicio}else{$lineStart += [Environment]::NewLine.Length}
    $c=$c.Substring(0,$lineStart)+$c.Substring($fim-4)
}

# Pontes exclusivas para automacao do assistente.
$inicioPontes=$c.IndexOf('// PONTES DA LIA INTELIGENTE')
$fimPontes=$c.IndexOf('private void OpenProducts()', [Math]::Max(0,$inicioPontes))
if($inicioPontes -ge 0 -and $fimPontes -gt $inicioPontes){
    $lineStart=$c.LastIndexOf([Environment]::NewLine,$inicioPontes)
    if($lineStart -lt 0){$lineStart=$inicioPontes}else{$lineStart += [Environment]::NewLine.Length}
    $c=$c.Substring(0,$lineStart)+$c.Substring($fimPontes-4)
}

$c=$c.Replace('LIA • LEAL AI','LEAL INFO PDV')
$c=$c.Replace('LIA — conversar','')
Set-Content $path $c -Encoding UTF8

$check=Get-Content $path -Raw
$forbidden=@('LiaOrbLauncher','AbrirLiaCompleta','BuildLiaAiFloatingButton','LiaAbrirProdutos','LiaAbrirVendas','LiaAbrirClientes','LiaAbrirRelatorios','LiaAbrirFinanceiro','LiaVoiceController','LiaOrbForm','new LiaForm','title == "LIA"')
foreach($x in $forbidden){
    if($check.Contains($x)){
        $pos=$check.IndexOf($x);$ini=[Math]::Max(0,$pos-180);$tam=[Math]::Min(420,$check.Length-$ini)
        Write-Host $check.Substring($ini,$tam)
        throw "Referencia residual encontrada no MainForm: $x"
    }
}
Write-Host 'Assistente antigo removido do MainForm.'
