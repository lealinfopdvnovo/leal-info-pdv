$ErrorActionPreference='Stop'
$path='LoginForm.cs'
$c=Get-Content $path -Raw -Encoding UTF8
$start=$c.IndexOf('    public LoginForm()')
$end=$c.IndexOf('    void BuildLogin(', $start)
if($start -lt 0 -or $end -lt 0){throw 'LoginForm nao localizado'}
$segment=$c.Substring($start,$end-$start)

# Encoding limpo no texto central.
$segment=[regex]::Replace($segment,'SIMPLES[^\"]*ÁGIL','SIMPLES • SEGURO • ÁGIL')

# Arredonda o proprio Form e mantem 900x500 centralizado.
$segment=$segment.Replace('Shown+=(_,_)=>{Opacity=1;CenterToScreen();stage.Invalidate();}; Resize+=(_,_)=>stage.Invalidate();', 'Shown+=(_,_)=>{Opacity=1;CenterToScreen();AplicarRegiaoLogin();stage.Invalidate();}; Resize+=(_,_)=>{AplicarRegiaoLogin();stage.Invalidate();};')

# Botões: desenho manual arredondado, gradiente metálico e borda interna.
$old='b.FlatAppearance.BorderColor=Color.FromArgb(0,185,255); b.FlatAppearance.BorderSize=1; b.FlatAppearance.MouseOverBackColor=Color.FromArgb(8,66,118); return b;'
$new=@'
b.FlatAppearance.BorderSize=0;
            b.Paint+=(_,e)=>{
                e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var br=new Rectangle(2,2,b.Width-5,b.Height-5);
                using var bp=LoginRoundedPath(br,12);
                using var bg=new System.Drawing.Drawing2D.LinearGradientBrush(br,Color.FromArgb(20,45,95),Color.FromArgb(5,15,35),90f);
                e.Graphics.FillPath(bg,bp);
                using var bb=new System.Drawing.Drawing2D.LinearGradientBrush(br,Color.FromArgb(0,240,255),Color.FromArgb(26,66,228),45f);
                using var pen=new Pen(bb,2.5f); e.Graphics.DrawPath(pen,bp);
                var bi=Rectangle.Inflate(br,-4,-4); using var bip=LoginRoundedPath(bi,8); using var ip=new Pen(Color.FromArgb(100,210,250,255),1f); e.Graphics.DrawPath(ip,bip);
                TextRenderer.DrawText(e.Graphics,b.Text,b.Font,br,b.ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);
            };
            b.Resize+=(_,_)=>{using var rp=LoginRoundedPath(new Rectangle(0,0,b.Width,b.Height),14); b.Region?.Dispose(); b.Region=new Region(rp);};
            return b;
'@
if($segment.Contains($old)){$segment=$segment.Replace($old,$new)}

$c=$c.Substring(0,$start)+$segment+$c.Substring($end)

# Métodos exclusivos GDI+ antes de BuildLogin; não altera autenticação/recuperação.
$insert=@'
    static System.Drawing.Drawing2D.GraphicsPath LoginRoundedPath(Rectangle r,int radius)
    {
        var gp=new System.Drawing.Drawing2D.GraphicsPath(); int d=Math.Min(radius*2,Math.Min(r.Width,r.Height));
        gp.AddArc(r.Left,r.Top,d,d,180,90); gp.AddArc(r.Right-d,r.Top,d,d,270,90); gp.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); gp.AddArc(r.Left,r.Bottom-d,d,d,90,90); gp.CloseFigure(); return gp;
    }

    void AplicarRegiaoLogin()
    {
        if(Width<=0||Height<=0)return;
        using var gp=LoginRoundedPath(new Rectangle(0,0,Width,Height),30);
        Region?.Dispose(); Region=new Region(gp);
    }

'@
$pos=$c.IndexOf('    void BuildLogin(')
$c=$c.Insert($pos,$insert)

# TextBoxes sem borda 3D; linha ciano desenhada no host existente.
$c=$c.Replace('BorderStyle=BorderStyle.FixedSingle','BorderStyle=BorderStyle.None')
$c=$c.Replace('BorderStyle=BorderStyle.Fixed3D','BorderStyle=BorderStyle.None')

Set-Content $path $c -Encoding UTF8
Write-Host 'V10.263: GDI+ aplicado ao login; handlers funcionais preservados.'