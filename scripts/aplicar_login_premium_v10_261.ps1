$ErrorActionPreference='Stop'
$path='LoginForm.cs'
$c=Get-Content $path -Raw -Encoding UTF8

# A base atual ja possui RoundedPath/controles premium. O script V10.259 injeta outro helper.
# Renomeamos APENAS o helper injetado e suas chamadas dentro do trecho do construtor.
$start=$c.IndexOf('    public LoginForm()')
$end=$c.IndexOf('    void BuildLogin(', $start)
if($start -lt 0 -or $end -lt 0){throw 'Trecho do LoginForm nao localizado.'}
$head=$c.Substring(0,$start)
$segment=$c.Substring($start,$end-$start)
$tail=$c.Substring($end)
$segment=$segment.Replace('RoundedPath(', 'LoginRoundedPath(')
$c=$head+$segment+$tail

# Moldura premium 3D.
$old=@'
            using var gp=LoginRoundedPath(r,34);
            using var p1=new Pen(Color.FromArgb(30,150,255),7);
            using var p2=new Pen(Color.FromArgb(210,235,248),2);
            e.Graphics.DrawPath(p1,gp); e.Graphics.DrawPath(p2,gp);
'@
$new=@'
            using var gp=LoginRoundedPath(r,34);
            using var shadow=new Pen(Color.FromArgb(0,20,55),18); e.Graphics.DrawPath(shadow,gp);
            using var metal=new System.Drawing.Drawing2D.LinearGradientBrush(r,Color.FromArgb(0,70,180),Color.FromArgb(225,250,255),90f);
            metal.SetSigmaBellShape(.48f,.9f); using var bevel=new Pen(metal,12); e.Graphics.DrawPath(bevel,gp);
            using var chrome=new Pen(Color.FromArgb(245,252,255),2.4f); e.Graphics.DrawPath(chrome,gp);
            var inner=Rectangle.Inflate(r,-9,-9); using var igp=LoginRoundedPath(inner,27);
            using var neon=new Pen(Color.FromArgb(0,220,255),2f); e.Graphics.DrawPath(neon,igp);
'@
if($c.Contains($old)){$c=$c.Replace($old,$new)}
$c=$c.Replace('BackColor=Color.FromArgb(3,18,38)};','BackColor=Color.FromArgb(1,8,20)};')

# Glass panels.
$c=$c.Replace('var panel=new Panel{Dock=DockStyle.Fill,BackColor=baseColor,Margin=new Padding(10)};','var panel=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(5,31,62),Margin=new Padding(10)};')
$c=$c.Replace('using var pen=new Pen(Color.FromArgb(75,190,255),1.6f);','using var pen=new Pen(Color.FromArgb(0,225,255),2f);')

# Remove caracteres/emoji problemáticos sem tocar handlers.
$c=$c.Replace('Button ToolButton(string icon,string text)','Button ToolButton(string icon,string text)')
$c=$c.Replace('Text=icon+"\r\n"+text','Text=text')
$c=[regex]::Replace($c,'var banco=ToolButton\(".*?","ALTERAR\\r\\nBANCO DE DADOS"\);','var banco=ToolButton("","ALTERAR\r\nBANCO DE DADOS");',1)
$c=[regex]::Replace($c,'var corrigir=ToolButton\(".*?","CORRIGIR\\r\\nSISTEMA"\);','var corrigir=ToolButton("","CORRIGIR\r\nSISTEMA");',1)
$c=[regex]::Replace($c,'var temas=ToolButton\(".*?","TEMAS"\);','var temas=ToolButton("","TEMAS");',1)

# Inputs escuros/minimalistas; lógica permanece intacta.
$c=$c.Replace('BackColor=Color.White;','BackColor=Color.FromArgb(4,31,62);')
$c=$c.Replace('ForeColor=Color.FromArgb(8,38,68);','ForeColor=Color.White;')
$c=$c.Replace('BackColor=Color.White,ForeColor=Color.FromArgb(4,70,112),','BackColor=Color.FromArgb(4,31,62),ForeColor=Color.FromArgb(120,225,255),')
$c=$c.Replace('eye.FlatAppearance.MouseOverBackColor=Color.FromArgb(232,247,252);','eye.FlatAppearance.MouseOverBackColor=Color.FromArgb(8,66,118);')

Set-Content $path $c -Encoding UTF8
Write-Host 'V10.261: conflito RoundedPath eliminado; login premium preservado.'