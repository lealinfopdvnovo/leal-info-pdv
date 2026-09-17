$ErrorActionPreference='Stop'
$path='LoginForm.cs'
$c=Get-Content $path -Raw -Encoding UTF8
$start=$c.IndexOf('    public LoginForm()')
$end=$c.IndexOf('    void BuildLogin(', $start)
if($start -lt 0 -or $end -lt 0){ throw 'Construtor do LoginForm nao localizado.' }
$new=@'
    public LoginForm()
    {
        Text="LEAL INFO PDV - Sistema de Ponto de Venda";
        StartPosition=FormStartPosition.CenterScreen;
        FormBorderStyle=FormBorderStyle.None;
        WindowState=FormWindowState.Maximized;
        MaximizeBox=false; MinimizeBox=false;
        BackColor=Color.FromArgb(1,8,20);
        Font=new Font("Segoe UI",10);
        Opacity=0;

        var stage=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(1,8,20),Padding=new Padding(26)};
        Controls.Add(stage);

        // Moldura externa unificada azul/cromo. Somente desenho: nenhuma regra do PDV e alterada.
        stage.Paint+=(_,e)=>
        {
            e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var r=new Rectangle(7,7,stage.Width-15,stage.Height-15);
            if(r.Width<10||r.Height<10)return;
            using var gp=RoundedPath(r,34);
            using var p1=new Pen(Color.FromArgb(30,150,255),7);
            using var p2=new Pen(Color.FromArgb(210,235,248),2);
            e.Graphics.DrawPath(p1,gp); e.Graphics.DrawPath(p2,gp);
        };

        var shell=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Padding=new Padding(18),BackColor=Color.FromArgb(3,18,38)};
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,205));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,500));
        stage.Controls.Add(shell);

        Panel GlassPanel(Color baseColor)
        {
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
        }

        var tools=GlassPanel(Color.FromArgb(4,25,49));
        shell.Controls.Add(tools,0,0);
        var toolLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=7,Padding=new Padding(18,28,18,28),BackColor=Color.Transparent};
        toolLayout.RowStyles.Add(new RowStyle(SizeType.Percent,12));
        toolLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,132));
        toolLayout.RowStyles.Add(new RowStyle(SizeType.Percent,18));
        toolLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,132));
        toolLayout.RowStyles.Add(new RowStyle(SizeType.Percent,18));
        toolLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,132));
        toolLayout.RowStyles.Add(new RowStyle(SizeType.Percent,12));
        tools.Controls.Add(toolLayout);

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
        toolLayout.Controls.Add(banco,0,1); toolLayout.Controls.Add(corrigir,0,3); toolLayout.Controls.Add(temas,0,5);
        // Mantidos como atalhos visuais seguros no login; nao executam rotinas destrutivas sem fluxo existente.
        banco.Click+=(_,_)=>MessageBox.Show("A alteração do banco permanece protegida pelo fluxo administrativo do PDV.","Banco de Dados");
        corrigir.Click+=(_,_)=>MessageBox.Show("A correção do sistema permanece disponível no ambiente administrativo do PDV.","Corrigir Sistema");
        temas.Click+=(_,_)=>MessageBox.Show("Os temas permanecem disponíveis após o acesso ao PDV.","Temas");

        var center=GlassPanel(Color.FromArgb(5,35,70));
        shell.Controls.Add(center,1,0);
        var centerLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=7,Padding=new Padding(48),BackColor=Color.Transparent};
        centerLayout.RowStyles.Add(new RowStyle(SizeType.Percent,22));
        centerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,92));
        centerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,5));
        centerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
        centerLayout.RowStyles.Add(new RowStyle(SizeType.Percent,36));
        centerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,72));
        centerLayout.RowStyles.Add(new RowStyle(SizeType.Percent,12));
        center.Controls.Add(centerLayout);
        centerLayout.Controls.Add(new Label{Text="LEAL INFO PDV",Dock=DockStyle.Fill,ForeColor=Color.White,BackColor=Color.Transparent,
            Font=new Font("Segoe UI",36,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter},0,1);
        centerLayout.Controls.Add(new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(235,28,45),Margin=new Padding(55,0,55,0)},0,2);
        centerLayout.Controls.Add(new Label{Text="T E C N O L O G I A   Q U E   C O N E C T A",Dock=DockStyle.Fill,ForeColor=Color.FromArgb(220,235,247),BackColor=Color.Transparent,
            Font=new Font("Segoe UI",11,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter},0,3);
        centerLayout.Controls.Add(new Label{Text="SIMPLES E COMPLETO     •     SEGURO E CONFIÁVEL     •     AGILIDADE NO SEU DIA",Dock=DockStyle.Fill,
            ForeColor=Color.FromArgb(145,205,240),BackColor=Color.Transparent,Font=new Font("Segoe UI",10,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter},0,5);

        var card=GlassPanel(Color.FromArgb(4,31,62));
        shell.Controls.Add(card,2,0);
        var cardLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(34,28,34,26),BackColor=Color.Transparent};
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,105));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        card.Controls.Add(cardLayout);
        var title=new Label{Text=Auth.UserCount()==0?"PRIMEIRO ACESSO":"ACESSE O SISTEMA",Dock=DockStyle.Fill,ForeColor=Color.White,BackColor=Color.Transparent,
            Font=new Font("Segoe UI",21,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter};
        cardLayout.Controls.Add(title,0,0);
        var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,Padding=new Padding(16,4,16,4),BackColor=Color.Transparent};
        cardLayout.Controls.Add(p,0,1);
        cardLayout.Controls.Add(new Label{Text="LEAL INFO CONECTADO  •  ACESSO SEGURO",Dock=DockStyle.Fill,ForeColor=Color.FromArgb(145,190,220),
            BackColor=Color.Transparent,Font=new Font("Segoe UI",8.5f,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter},0,2);

        TextBox Box(bool password=false)=>new(){Dock=DockStyle.Fill,Font=new Font("Segoe UI",12,FontStyle.Bold),UseSystemPasswordChar=password,
            BackColor=Color.FromArgb(9,42,75),ForeColor=Color.White,BorderStyle=BorderStyle.FixedSingle};
        Label Lab(string x)=>new(){Text=x.ToUpperInvariant(),Dock=DockStyle.Fill,ForeColor=Color.White,BackColor=Color.Transparent,
            Font=new Font("Segoe UI",9.5f,FontStyle.Bold),TextAlign=ContentAlignment.BottomLeft};

        if(Auth.UserCount()==0) BuildFirstAdmin(p,Box,Lab); else BuildLogin(p,Box,Lab);

        Shown+=(_,_)=>{Opacity=1;stage.Invalidate();};
        Resize+=(_,_)=>stage.Invalidate();
    }

    static System.Drawing.Drawing2D.GraphicsPath RoundedPath(Rectangle r,int radius)
    {
        var gp=new System.Drawing.Drawing2D.GraphicsPath();
        int d=Math.Min(radius*2,Math.Min(r.Width,r.Height));
        gp.AddArc(r.Left,r.Top,d,d,180,90); gp.AddArc(r.Right-d,r.Top,d,d,270,90);
        gp.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); gp.AddArc(r.Left,r.Bottom-d,d,d,90,90); gp.CloseFigure();
        return gp;
    }

'@
$c=$c.Substring(0,$start)+$new+$c.Substring($end)
# Somente acabamento visual do login existente; autenticação e recuperação continuam nos mesmos handlers.
$c=$c.Replace('var emergency=new LinkLabel{Text="Usar código de recuperação de emergência"','var emergency=new LinkLabel{Text="Acesso Mestre"')
$c=$c.Replace('var enter=new Button{Text="ENTRAR",Dock=DockStyle.Fill,BackColor=Color.FromArgb(0,163,224)','var enter=new Button{Text="ENTRAR",Dock=DockStyle.Fill,BackColor=Color.FromArgb(0,110,245)')
Set-Content $path $c -Encoding UTF8
Write-Host 'V10.259: novo visual aplicado exclusivamente ao LoginForm; autenticacao e recuperacao preservadas.'
