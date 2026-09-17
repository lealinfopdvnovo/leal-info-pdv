$ErrorActionPreference='Stop'
$path='LoginForm.cs'
$c=Get-Content $path -Raw -Encoding UTF8

# Esta etapa roda depois da V10.259 e altera SOMENTE acabamento visual do LoginForm.
# Nenhum handler de autenticacao, recuperacao, updater, PDV ou LIA/LIC e modificado.

# Moldura externa: troca os dois riscos simples por aro robusto com degradê metalico/cromo.
$old=@'
            using var gp=RoundedPath(r,34);
            using var p1=new Pen(Color.FromArgb(30,150,255),7);
            using var p2=new Pen(Color.FromArgb(210,235,248),2);
            e.Graphics.DrawPath(p1,gp); e.Graphics.DrawPath(p2,gp);
'@
$new=@'
            using var gp=RoundedPath(r,34);
            using var metal=new System.Drawing.Drawing2D.LinearGradientBrush(r,
                Color.FromArgb(20,105,220),Color.FromArgb(205,245,255),
                System.Drawing.Drawing2D.LinearGradientMode.Vertical);
            using var pOuter=new Pen(Color.FromArgb(8,55,125),12);
            using var pMetal=new Pen(metal,8);
            using var pChrome=new Pen(Color.FromArgb(235,250,255),2.2f);
            using var pNeon=new Pen(Color.FromArgb(0,205,255),1.4f);
            e.Graphics.DrawPath(pOuter,gp);
            e.Graphics.DrawPath(pMetal,gp);
            e.Graphics.DrawPath(pChrome,gp);
            var inner=Rectangle.Inflate(r,-7,-7);
            using var gpInner=RoundedPath(inner,28);
            e.Graphics.DrawPath(pNeon,gpInner);
'@
if(-not $c.Contains($old)){throw 'Bloco da moldura V10.259 nao encontrado.'}
$c=$c.Replace($old,$new)

# Painel de vidro: preenchimento em degradê, reflexo superior, neon e recorte arredondado.
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
            var panel=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(2,18,40),Margin=new Padding(10)};
            void RoundPanel()
            {
                if(panel.Width<12||panel.Height<12)return;
                using var rg=RoundedPath(new Rectangle(0,0,panel.Width-1,panel.Height-1),28);
                panel.Region?.Dispose(); panel.Region=new Region(rg);
            }
            panel.Resize+=(_,_)=>RoundPanel();
            panel.Paint+=(_,e)=>
            {
                e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var r=new Rectangle(1,1,panel.Width-3,panel.Height-3);
                if(r.Width<8||r.Height<8)return;
                using var gp=RoundedPath(r,28);
                using var glass=new System.Drawing.Drawing2D.LinearGradientBrush(r,
                    Color.FromArgb(22,72,118),Color.FromArgb(4,24,52),
                    System.Drawing.Drawing2D.LinearGradientMode.Vertical);
                e.Graphics.FillPath(glass,gp);
                using var neon=new Pen(Color.FromArgb(0,205,255),1.8f);
                using var shine=new Pen(Color.FromArgb(175,235,255),1.0f);
                e.Graphics.DrawPath(neon,gp);
                var hi=new Rectangle(r.Left+18,r.Top+10,Math.Max(10,r.Width-36),Math.Max(10,r.Height/3));
                using var hiPath=RoundedPath(hi,18);
                e.Graphics.DrawPath(shine,hiPath);
            };
            return panel;
'@
if(-not $c.Contains($old)){throw 'GlassPanel V10.259 nao encontrado.'}
$c=$c.Replace($old,$new)

# Botoes laterais: texto limpo, sem caracteres/emoji sujeitos a mojibake, cantos arredondados.
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
            var b=new Button{Text=text,Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(7,48,86),ForeColor=Color.White,
                Font=new Font("Segoe UI",10.5f,FontStyle.Bold),Cursor=Cursors.Hand,Margin=new Padding(4),TextAlign=ContentAlignment.MiddleCenter};
            b.FlatAppearance.BorderColor=Color.FromArgb(0,210,255); b.FlatAppearance.BorderSize=1;
            b.FlatAppearance.MouseOverBackColor=Color.FromArgb(12,82,135);
            b.FlatAppearance.MouseDownBackColor=Color.FromArgb(8,105,165);
            b.Resize+=(_,_)=>
            {
                if(b.Width<8||b.Height<8)return;
                using var gp=RoundedPath(new Rectangle(0,0,b.Width-1,b.Height-1),22);
                b.Region?.Dispose(); b.Region=new Region(gp);
            };
            return b;
        }
        var banco=ToolButton("ALTERAR\r\nBANCO DE DADOS");
        var corrigir=ToolButton("CORRIGIR\r\nSISTEMA");
        var temas=ToolButton("TEMAS");
'@
if(-not $c.Contains($old)){throw 'Botoes laterais V10.259 nao encontrados.'}
$c=$c.Replace($old,$new)

# Inputs do login: remove branco/caixa pesada e deixa superficie escura com somente underline ciano.
$c=$c.Replace('box.BackColor=Color.White;`r`n            box.ForeColor=Color.FromArgb(8,38,68);','box.BackColor=Color.FromArgb(4,31,62);`r`n            box.ForeColor=Color.White;')
$c=$c.Replace('BackColor=Color.White,`r`n                Padding=password ? new Padding(16,13,0,10) : new Padding(16,13,16,10),','BackColor=Color.FromArgb(4,31,62),`r`n                Padding=password ? new Padding(12,13,0,10) : new Padding(12,13,12,10),')
$c=$c.Replace('BackColor=Color.White,ForeColor=Color.FromArgb(4,70,112),','BackColor=Color.FromArgb(4,31,62),ForeColor=Color.FromArgb(130,225,255),')
$c=$c.Replace('eye.FlatAppearance.MouseOverBackColor=Color.FromArgb(232,247,252);','eye.FlatAppearance.MouseOverBackColor=Color.FromArgb(8,66,118);')

# No Paint do host dos inputs, elimina contorno completo e desenha somente linha inferior.
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
                using var pen=new Pen(focused ? Color.FromArgb(0,220,255) : Color.FromArgb(70,155,205), focused ? 2.4f : 1.2f);
                e.Graphics.DrawLine(pen,8,y,host.Width-9,y);
'@
if(-not $c.Contains($old)){throw 'Paint dos inputs nao encontrado.'}
$c=$c.Replace($old,$new)

# Links e botoes do bloco direito recebem acabamento coerente, sem alterar eventos.
$c=$c.Replace('var forgot=new LinkLabel{Text="Esqueci minha senha",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight};','var forgot=new LinkLabel{Text="Esqueci minha senha",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,LinkColor=Color.FromArgb(90,205,255),ActiveLinkColor=Color.White,BackColor=Color.Transparent};')
$c=$c.Replace('var emergency=new LinkLabel{Text="Acesso Mestre",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight};','var emergency=new LinkLabel{Text="Acesso Mestre",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,LinkColor=Color.FromArgb(90,205,255),ActiveLinkColor=Color.White,BackColor=Color.Transparent};')
$c=$c.Replace('BackColor=Color.FromArgb(0,110,245),ForeColor=Color.White,','BackColor=Color.FromArgb(5,105,210),ForeColor=Color.White,')
$c=$c.Replace('BackColor=Color.FromArgb(4,70,112),ForeColor=Color.White,FlatStyle=FlatStyle.Flat','BackColor=Color.FromArgb(6,55,92),ForeColor=Color.White,FlatStyle=FlatStyle.Flat')

Set-Content $path $c -Encoding UTF8
Write-Host 'V10.260: acabamento premium vidro/cromo aplicado somente ao LoginForm.'
