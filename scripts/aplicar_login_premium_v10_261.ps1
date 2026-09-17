$ErrorActionPreference='Stop'
$path='LoginForm.cs'
$c=Get-Content $path -Raw -Encoding UTF8

# Roda apos a base V10.259. Refatora SOMENTE renderizacao visual do LoginForm.
# Eventos e logica de autenticacao/recuperacao permanecem intactos.

# Moldura externa realmente chanfrada: sombra + azul metalico + cromo + neon interno.
$old=@'
            using var gp=RoundedPath(r,34);
            using var p1=new Pen(Color.FromArgb(30,150,255),7);
            using var p2=new Pen(Color.FromArgb(210,235,248),2);
            e.Graphics.DrawPath(p1,gp); e.Graphics.DrawPath(p2,gp);
'@
$new=@'
            using var gp=RoundedPath(r,34);
            using var shadow=new Pen(Color.FromArgb(0,20,55),18);
            e.Graphics.DrawPath(shadow,gp);
            using var metal=new System.Drawing.Drawing2D.LinearGradientBrush(r,
                Color.FromArgb(0,70,180),Color.FromArgb(225,250,255),90f);
            metal.SetSigmaBellShape(.48f,.9f);
            using var bevel=new Pen(metal,12);
            e.Graphics.DrawPath(bevel,gp);
            using var chrome=new Pen(Color.FromArgb(245,252,255),2.4f);
            e.Graphics.DrawPath(chrome,gp);
            var inner=Rectangle.Inflate(r,-9,-9);
            using var igp=RoundedPath(inner,27);
            using var neon=new Pen(Color.FromArgb(0,220,255),2f);
            e.Graphics.DrawPath(neon,igp);
'@
if(-not $c.Contains($old)){throw 'Moldura base V10.259 nao localizada.'}
$c=$c.Replace($old,$new)

# Shell deixa de ser bloco chapado.
$c=$c.Replace('BackColor=Color.FromArgb(3,18,38)};','BackColor=Color.FromArgb(1,8,20)};')

# Glassmorphism desenhado: superficie translucida simulada, brilho superior e borda neon.
$old=@'
            var panel=new Panel{Dock=DockStyle.Fill,BackColor=baseColor,Margin=new Padding(10)};
            panel.Paint+=(_,e)=>
            {
                e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var r=new Rectangle(1,1,panel.Width-3,panel.Height-3);
                if(r.Width<8||r.Height<8)return;
                using var gp=RoundedPath(r,28);
                using var pen=new Pen(Color.FromArgb(75,190,255),1.6f);
                e.Graphics.DrawPath(pen,gp);
            };
            return panel;
'@
$new=@'
            var panel=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(1,8,20),Margin=new Padding(10)};
            void ClipGlass()
            {
                if(panel.Width<10||panel.Height<10)return;
                using var cp=RoundedPath(new Rectangle(0,0,panel.Width-1,panel.Height-1),30);
                panel.Region?.Dispose(); panel.Region=new Region(cp);
            }
            panel.Resize+=(_,_)=>ClipGlass();
            panel.Paint+=(_,e)=>
            {
                e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var r=new Rectangle(1,1,panel.Width-3,panel.Height-3);
                if(r.Width<8||r.Height<8)return;
                using var gp=RoundedPath(r,30);
                using var glass=new System.Drawing.Drawing2D.LinearGradientBrush(r,
                    Color.FromArgb(42,35,105,165),Color.FromArgb(24,4,25,58),90f);
                e.Graphics.FillPath(glass,gp);
                var gloss=new Rectangle(r.Left+12,r.Top+8,Math.Max(10,r.Width-24),Math.Max(18,r.Height/4));
                using var glossPath=RoundedPath(gloss,22);
                using var glossBrush=new System.Drawing.Drawing2D.LinearGradientBrush(gloss,
                    Color.FromArgb(42,220,250,255),Color.FromArgb(0,220,250,255),90f);
                e.Graphics.FillPath(glossBrush,glossPath);
                using var glow=new Pen(Color.FromArgb(0,225,255),2f);
                using var hair=new Pen(Color.FromArgb(185,245,255),.8f);
                e.Graphics.DrawPath(glow,gp);
                var ir=Rectangle.Inflate(r,-4,-4);
                using var ip=RoundedPath(ir,26);
                e.Graphics.DrawPath(hair,ip);
            };
            return panel;
'@
if(-not $c.Contains($old)){throw 'GlassPanel base nao localizado.'}
$c=$c.Replace($old,$new)

# Botoes laterais: sem caracteres problemáticos; recorte arredondado + degradê 3D + neon.
$old=@'
        Button ToolButton(string icon,string text)
        {
            var b=new Button{Text=icon+"\r\n"+text,Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(6,38,72),ForeColor=Color.White,
                Font=new Font("Segoe UI",10.5f,FontStyle.Bold),Cursor=Cursors.Hand,Margin=new Padding(4)};
            b.FlatAppearance.BorderColor=Color.FromArgb(0,185,255); b.FlatAppearance.BorderSize=1;
            b.FlatAppearance.MouseOverBackColor=Color.FromArgb(8,66,118);
            return b;
        }
        var banco=ToolButton("▣","ALTERAR\r\nBANCO DE DADOS");
        var corrigir=ToolButton("🛠","CORRIGIR\r\nSISTEMA");
        var temas=ToolButton("◉","TEMAS");
'@
$new=@'
        Button ToolButton(string text)
        {
            var b=new Button{Text=text,Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(4,34,68),ForeColor=Color.White,
                Font=new Font("Segoe UI",10.5f,FontStyle.Bold),Cursor=Cursors.Hand,Margin=new Padding(4),TextAlign=ContentAlignment.MiddleCenter};
            b.FlatAppearance.BorderSize=0;
            b.FlatAppearance.MouseOverBackColor=Color.FromArgb(10,78,128);
            b.FlatAppearance.MouseDownBackColor=Color.FromArgb(8,98,155);
            b.Resize+=(_,_)=>
            {
                if(b.Width<10||b.Height<10)return;
                using var bp=RoundedPath(new Rectangle(0,0,b.Width-1,b.Height-1),24);
                b.Region?.Dispose(); b.Region=new Region(bp);
            };
            b.Paint+=(_,e)=>
            {
                e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var br=new Rectangle(1,1,b.Width-3,b.Height-3);
                if(br.Width<8||br.Height<8)return;
                using var bp=RoundedPath(br,24);
                using var bg=new System.Drawing.Drawing2D.LinearGradientBrush(br,Color.FromArgb(18,92,150),Color.FromArgb(3,30,66),90f);
                e.Graphics.FillPath(bg,bp);
                using var edge=new Pen(Color.FromArgb(0,220,255),1.7f);
                e.Graphics.DrawPath(edge,bp);
                TextRenderer.DrawText(e.Graphics,b.Text,b.Font,br,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);
            };
            return b;
        }
        var banco=ToolButton("ALTERAR\r\nBANCO DE DADOS");
        var corrigir=ToolButton("CORRIGIR\r\nSISTEMA");
        var temas=ToolButton("TEMAS");
'@
if(-not $c.Contains($old)){throw 'Botoes base V10.259 nao localizados.'}
$c=$c.Replace($old,$new)

# Inputs: fundo escuro integrado ao vidro, sem caixa branca.
$c=[regex]::Replace($c,'box\.BackColor=Color\.White;\s*box\.ForeColor=Color\.FromArgb\(8,38,68\);','box.BackColor=Color.FromArgb(4,31,62);`r`n            box.ForeColor=Color.White;',1)
$c=[regex]::Replace($c,'BackColor=Color\.White,\s*Padding=password \? new Padding\(16,13,0,10\) : new Padding\(16,13,16,10\),','BackColor=Color.FromArgb(4,31,62),`r`n                Padding=password ? new Padding(12,13,0,10) : new Padding(12,13,12,10),',1)
$c=$c.Replace('BackColor=Color.White,ForeColor=Color.FromArgb(4,70,112),','BackColor=Color.FromArgb(4,31,62),ForeColor=Color.FromArgb(120,225,255),')
$c=$c.Replace('eye.FlatAppearance.MouseOverBackColor=Color.FromArgb(232,247,252);','eye.FlatAppearance.MouseOverBackColor=Color.FromArgb(8,66,118);')

# Host do input: remove contorno arredondado e mantém apenas underline ciano.
$old=@'
                var rect=new Rectangle(1,1,host.Width-3,host.Height-3);
                int radius=Math.Min(15,Math.Max(4,host.Height/2-3));
                int d=radius*2;
                using var gp=new System.Drawing.Drawing2D.GraphicsPath();
                gp.AddArc(rect.Left,rect.Top,d,d,180,90);
                gp.AddArc(rect.Right-d,rect.Top,d,d,270,90);
                gp.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);
                gp.AddArc(rect.Left,rect.Bottom-d,d,d,90,90);
                gp.CloseFigure();
                using var pen=new Pen(focused ? Color.FromArgb(0,163,224) : Color.FromArgb(166,196,214), focused ? 2.2f : 1.2f);
                e.Graphics.DrawPath(pen,gp);
'@
$new=@'
                int y=host.Height-3;
                using var glow=new Pen(focused ? Color.FromArgb(0,235,255) : Color.FromArgb(70,165,210), focused ? 2.5f : 1.2f);
                e.Graphics.DrawLine(glow,5,y,host.Width-6,y);
'@
if(-not $c.Contains($old)){throw 'Paint de input base nao localizado.'}
$c=$c.Replace($old,$new)

# Links mantêm handlers; somente cor visual.
$c=$c.Replace('var forgot=new LinkLabel{Text="Esqueci minha senha",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight};','var forgot=new LinkLabel{Text="Esqueci minha senha",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,LinkColor=Color.FromArgb(80,210,255),ActiveLinkColor=Color.White,BackColor=Color.Transparent};')
$c=$c.Replace('var emergency=new LinkLabel{Text="Acesso Mestre",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight};','var emergency=new LinkLabel{Text="Acesso Mestre",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,LinkColor=Color.FromArgb(80,210,255),ActiveLinkColor=Color.White,BackColor=Color.Transparent};')

Set-Content $path $c -Encoding UTF8
Write-Host 'V10.261: renderizacao premium vidro/cromo aplicada exclusivamente ao LoginForm.'