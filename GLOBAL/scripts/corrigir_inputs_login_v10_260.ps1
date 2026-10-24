$ErrorActionPreference='Stop'
$path='LoginForm.cs'
$c=Get-Content $path -Raw -Encoding UTF8
$nl=[Environment]::NewLine
$r1='box.BackColor=Color.FromArgb(4,31,62);'+$nl+'            box.ForeColor=Color.White;'
$c=[regex]::Replace($c,'box\.BackColor=Color\.White;\s*box\.ForeColor=Color\.FromArgb\(8,38,68\);',$r1,1)
$r2='BackColor=Color.FromArgb(4,31,62),'+$nl+'                Padding=password ? new Padding(12,13,0,10) : new Padding(12,13,12,10),'
$c=[regex]::Replace($c,'BackColor=Color\.White,\s*Padding=password \? new Padding\(16,13,0,10\) : new Padding\(16,13,16,10\),',$r2,1)
$c=$c.Replace('BackColor=Color.White,ForeColor=Color.FromArgb(4,70,112),','BackColor=Color.FromArgb(4,31,62),ForeColor=Color.FromArgb(130,225,255),')
$c=$c.Replace('eye.FlatAppearance.MouseOverBackColor=Color.FromArgb(232,247,252);','eye.FlatAppearance.MouseOverBackColor=Color.FromArgb(8,66,118);')
Set-Content $path $c -Encoding UTF8
Write-Host 'V10.260: inputs do login finalizados sem alterar handlers.'
