$ErrorActionPreference='Stop'
$path='MainForm.cs'
$c=Get-Content $path -Raw

# Remove campo, inicializacao, menu, botao flutuante e pontes do assistente antigo.
$c=[regex]::Replace($c,'(?m)^\s*private LiaOrbLauncher\? liaAiFloatingButton;\r?\n','')
$c=[regex]::Replace($c,'(?m)^\s*BuildLiaAiFloatingButton\(\);\r?\n','')
$c=$c.Replace(', "LIA"','')
$c=[regex]::Replace($c,'(?s)\s*else if \(title == "LIA"\)\s*\{.*?\}\s*(?=else if \(title == "Utilitários"\))',"`r`n            ")

# Métodos exclusivos do assistente.
$c=[regex]::Replace($c,'(?s)\r?\n\s*private void BuildLiaAiFloatingButton\(\).*?(?=\r?\n\s*private void AbrirLiaCompleta\(\))','')
$c=[regex]::Replace($c,'(?s)\r?\n\s*private void AbrirLiaCompleta\(\).*?(?=\r?\n\s*private (?:void|string|bool|Form|static|sealed|class))','')

# Pontes de automacao do assistente para funcoes normais do PDV.
$c=[regex]::Replace($c,'(?s)\r?\n\s*// PONTES DA LIA INTELIGENTE.*?(?=\r?\n\s*private void OpenProducts\(\))','')

# Remove qualquer texto residual de menu/tooltips conhecido.
$c=$c.Replace('LIA • LEAL AI','LEAL INFO PDV')
$c=$c.Replace('LIA — conversar','')

Set-Content $path $c -Encoding UTF8

# Falha a build se qualquer classe/launcher antigo continuar referenciado no MainForm.
$check=Get-Content $path -Raw
$forbidden=@('LiaOrbLauncher','AbrirLiaCompleta','BuildLiaAiFloatingButton','LiaAbrirProdutos','LiaAbrirVendas','LiaAbrirClientes','LiaAbrirRelatorios','LiaAbrirFinanceiro')
foreach($x in $forbidden){if($check.Contains($x)){throw "Referencia residual encontrada no MainForm: $x"}}
Write-Host 'Assistente antigo removido do MainForm.'
