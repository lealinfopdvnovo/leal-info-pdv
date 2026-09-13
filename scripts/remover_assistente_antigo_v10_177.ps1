$ErrorActionPreference='Stop'
$path='MainForm.cs'
$c=Get-Content $path -Raw

# Campo e inicializacao.
$c=[regex]::Replace($c,'(?m)^\s*private LiaOrbLauncher\? liaAiFloatingButton;\r?\n','')
$c=[regex]::Replace($c,'(?m)^\s*BuildLiaAiFloatingButton\(\);\r?\n','')

# Menu superior: retira o item LIA da lista e o bloco que o trata.
$c=$c.Replace(', "LIA"','')
$menuInicio=$c.IndexOf('            else if (title == "LIA")')
$menuFim=$c.IndexOf('            else if (title == "Utilitários")',[Math]::Max(0,$menuInicio))
if($menuInicio -ge 0 -and $menuFim -gt $menuInicio){$c=$c.Substring(0,$menuInicio)+$c.Substring($menuFim)}

# Remove em um bloco unico o launcher e toda a apresentacao/voz/orbe,
# preservando exatamente o metodo normal que vem depois.
$inicio=$c.IndexOf('    private void BuildLiaAiFloatingButton()')
$fim=$c.IndexOf('    private void ShowCadastroHelp()', [Math]::Max(0,$inicio))
if($inicio -ge 0 -and $fim -gt $inicio){$c=$c.Substring(0,$inicio)+$c.Substring($fim)}

# Pontes exclusivas para automacao do assistente; a logica real do PDV permanece.
$inicioPontes=$c.IndexOf('    // PONTES DA LIA INTELIGENTE')
$fimPontes=$c.IndexOf('    private void OpenProducts()', [Math]::Max(0,$inicioPontes))
if($inicioPontes -ge 0 -and $fimPontes -gt $inicioPontes){$c=$c.Substring(0,$inicioPontes)+$c.Substring($fimPontes)}

$c=$c.Replace('LIA • LEAL AI','LEAL INFO PDV')
$c=$c.Replace('LIA — conversar','')

Set-Content $path $c -Encoding UTF8

$check=Get-Content $path -Raw
$forbidden=@('LiaOrbLauncher','AbrirLiaCompleta','BuildLiaAiFloatingButton','LiaAbrirProdutos','LiaAbrirVendas','LiaAbrirClientes','LiaAbrirRelatorios','LiaAbrirFinanceiro','LiaVoiceController','LiaOrbForm','new LiaForm')
foreach($x in $forbidden){
    if($check.Contains($x)){
        $pos=$check.IndexOf($x)
        $ini=[Math]::Max(0,$pos-180)
        $tam=[Math]::Min(420,$check.Length-$ini)
        Write-Host $check.Substring($ini,$tam)
        throw "Referencia residual encontrada no MainForm: $x"
    }
}
Write-Host 'Assistente antigo removido do MainForm.'
