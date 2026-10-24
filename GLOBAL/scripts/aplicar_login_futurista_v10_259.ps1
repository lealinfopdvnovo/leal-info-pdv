$ErrorActionPreference='Stop'
$path='LoginForm.cs'
$c=Get-Content $path -Raw -Encoding UTF8
# V10.267: unica alteracao funcional desta versao: centralizacao nativa do Form.
$c=$c.Replace('StartPosition=FormStartPosition.Manual;','StartPosition=FormStartPosition.CenterScreen;')
# Remove reposicionamento manual para nao disputar com o Windows Forms.
$c=[regex]::Replace($c,'(?s)\s*void CentralizarLogin\(\)\s*\{.*?\}\s*(?=var stage=)',"`r`n`r`n        ")
$c=$c.Replace('        HandleCreated+=(_,_)=>CentralizarLogin();'+"`r`n",'')
$c=$c.Replace('        Load+=(_,_)=>CentralizarLogin();'+"`r`n",'')
$c=$c.Replace('        Shown+=(_,_)=>{CentralizarLogin();BeginInvoke(new Action(CentralizarLogin));Opacity=1;stage.Invalidate();};','        Shown+=(_,_)=>{Opacity=1;stage.Invalidate();};')
Set-Content $path $c -Encoding UTF8
Write-Host 'V10.267: login centralizado exclusivamente por FormStartPosition.CenterScreen.'
