# Compatibilidade: a versao R2 abre o executavel pre-compilado.
$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'Conectar_iFood_Teste.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Extraia o pacote R2 completo antes de abrir.' }
Start-Process -FilePath $exe -Wait
