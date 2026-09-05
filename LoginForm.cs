using System.Drawing;

namespace LealInfoPDV;

public sealed class LoginForm : Form
{
    public bool EmbeddedMode { get; set; }

    public LoginForm()
    {
        Text="LEAL INFO PDV • ACESSO";
        StartPosition=FormStartPosition.CenterScreen;
        FormBorderStyle=FormBorderStyle.None;
        WindowState=FormWindowState.Maximized;
        MaximizeBox=false; MinimizeBox=false;
        BackColor=Color.FromArgb(2,10,22);
        Font=new Font("Segoe UI",10);
        Opacity=0;

        // V10.47: ambiente de login redesenhado. A tela inteira agora faz parte
        // da identidade do sistema; o formulário antigo não fica mais "solto".
        var stage=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(2,10,22)};
        Controls.Add(stage);

        // Identidade à esquerda — limpa, sem usar o logotipo que não foi aprovado.
        var identity=new Panel{BackColor=Color.FromArgb(3,18,36)};
        stage.Controls.Add(identity);

        var accent=new Panel{BackColor=Color.FromArgb(0,163,224),Height=5};
        identity.Controls.Add(accent);
        var brand=new Label{Text="LEAL INFO",AutoSize=false,ForeColor=Color.White,BackColor=Color.Transparent,
            Font=new Font("Segoe UI",26,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft};
        var connected=new Label{Text="CONECTADO",AutoSize=false,ForeColor=Color.FromArgb(55,205,255),BackColor=Color.Transparent,
            Font=new Font("Segoe UI",26,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft};
        var product=new Label{Text="PDV PRO",AutoSize=false,ForeColor=Color.FromArgb(175,194,211),BackColor=Color.Transparent,
            Font=new Font("Segoe UI",16,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft};
        var tagline=new Label{Text="TECNOLOGIA QUE CONECTA",AutoSize=false,ForeColor=Color.FromArgb(104,151,181),BackColor=Color.Transparent,
            Font=new Font("Segoe UI",10.5f,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft};
        identity.Controls.Add(brand); identity.Controls.Add(connected); identity.Controls.Add(product); identity.Controls.Add(tagline);

        // Cartão integrado ao ambiente fullscreen.
        var card=new TableLayoutPanel{ColumnCount=1,RowCount=3,BackColor=Color.FromArgb(236,245,250),Padding=new Padding(0)};
        card.RowStyles.Add(new RowStyle(SizeType.Absolute,104));
        card.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
        stage.Controls.Add(card);

        var header=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(4,70,112)};
        var headerTitle=new Label{Text=Auth.UserCount()==0?"PRIMEIRO ACESSO":"BEM-VINDO DE VOLTA",Dock=DockStyle.Fill,
            ForeColor=Color.White,Font=new Font("Segoe UI",22,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter};
        header.Controls.Add(headerTitle); card.Controls.Add(header,0,0);

        var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,Padding=new Padding(62,22,62,18),BackColor=Color.FromArgb(236,245,250)};
        card.Controls.Add(p,0,1);
        card.Controls.Add(new Label{Text="LEAL INFO CONECTADO  •  ACESSO SEGURO  •  V10.116",Dock=DockStyle.Fill,
            ForeColor=Color.FromArgb(82,111,133),Font=new Font("Segoe UI",8.5f,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter},0,2);

        TextBox Box(bool password=false)=>new(){Dock=DockStyle.Fill,Font=new Font("Segoe UI",12,FontStyle.Bold),
            UseSystemPasswordChar=password,BackColor=Color.White,ForeColor=Color.FromArgb(8,38,68),BorderStyle=BorderStyle.FixedSingle};
        Label Lab(string x)=>new(){Text=x.ToUpperInvariant(),Dock=DockStyle.Fill,ForeColor=Color.FromArgb(4,55,94),
            Font=new Font("Segoe UI",9.5f,FontStyle.Bold),TextAlign=ContentAlignment.BottomLeft};

        if(Auth.UserCount()==0) BuildFirstAdmin(p,Box,Lab);
        else BuildLogin(p,Box,Lab);

        const int finalW=650, finalH=650;
        int anim=0;
        var cinematic=new System.Windows.Forms.Timer{Interval=16};

        void LayoutScene(double scale=1.0, double progress=1.0)
        {
            int sw=ClientSize.Width, sh=ClientSize.Height;

            // V10.61: uma única composição fullscreen. Nada de tela dividida.
            identity.Bounds=new Rectangle(0,0,sw,sh); identity.SendToBack();

            int w=(int)(finalW*scale), h=(int)(finalH*scale);
            int x=(sw-w)/2;
            int targetY=Math.Max(190,(sh-finalH)/2+70);
            int y=(int)(targetY+(1-progress)*70+(finalH-h)/2);
            card.Bounds=new Rectangle(x,y,w,h);

            // Marca central, acima do login.
            // V10.63: identidade com caixas altas o bastante para não cortar fonte.
            // Mantém o conjunto centralizado e deixa espaço real antes do cartão.
            int brandW=Math.Min(900,Math.Max(520,sw-120));
            int brandX=(sw-brandW)/2;
            int brandTop=Math.Max(24,targetY-230);

            accent.SetBounds((sw-120)/2,brandTop,120,5);
            brand.SetBounds(brandX,brandTop+16,brandW,62);
            connected.SetBounds(brandX,brandTop+76,brandW,62);
            product.SetBounds(brandX,brandTop+138,brandW,42);
            tagline.SetBounds(brandX,brandTop+180,brandW,34);

            brand.TextAlign=ContentAlignment.MiddleCenter;
            connected.TextAlign=ContentAlignment.MiddleCenter;
            product.TextAlign=ContentAlignment.MiddleCenter;
            tagline.TextAlign=ContentAlignment.MiddleCenter;

            // O painel fullscreen de identidade não pode cobrir o cartão.
            card.BringToFront();
            brand.BringToFront();
            connected.BringToFront();
            product.BringToFront();
            tagline.BringToFront();
            accent.BringToFront();
        }

        Resize+=(_,_)=>LayoutScene(1,1);
        Shown+=(_,_)=>
        {
            if(EmbeddedMode)
            {
                Opacity=1;
                LayoutScene(.84,0);
            }
            else
            {
                LayoutScene(.72,0);
            }
            cinematic.Start();
        };
        cinematic.Tick+=(_,_)=>
        {
            anim++;
            // ~2,15 s. Ease-out cúbico: chega suave, sem "PÁ" na tela.
            double t=Math.Min(1.0,anim/125.0);
            double eased=1-Math.Pow(1-t,3);
            if(!EmbeddedMode) Opacity=Math.Min(1.0,Math.Max(0,(t-0.10)/0.32));
            double scale=(EmbeddedMode ? .88 : .80)+((EmbeddedMode ? .12 : .20)*eased);
            LayoutScene(scale,eased);
            if(t>=1.0){
                LayoutScene(1.0,1.0);cinematic.Stop();Opacity=1;LayoutScene(1,1);}
        };
    }

    void BuildLogin(TableLayoutPanel p,Func<bool,TextBox> Box,Func<string,Label> Lab)
    {
        p.RowCount=10;
        p.RowStyles.Add(new RowStyle(SizeType.Absolute,30)); p.RowStyles.Add(new RowStyle(SizeType.Absolute,50));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute,30)); p.RowStyles.Add(new RowStyle(SizeType.Absolute,50));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute,28)); p.RowStyles.Add(new RowStyle(SizeType.Absolute,28));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute,32)); p.RowStyles.Add(new RowStyle(SizeType.Absolute,54));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute,46)); p.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var user=Box(false); var pass=Box(true);

        // V10.105: acabamento visual dos campos de login.
        // A autenticação permanece intacta; somente a apresentação foi modernizada.
        Panel ModernField(TextBox box, bool password=false)
        {
            box.BorderStyle=BorderStyle.None;
            box.BackColor=Color.White;
            box.ForeColor=Color.FromArgb(8,38,68);
            box.Margin=new Padding(0);
            box.Dock=DockStyle.Fill;

            var host=new Panel
            {
                Dock=DockStyle.Fill,
                BackColor=Color.White,
                Padding=password ? new Padding(16,13,0,10) : new Padding(16,13,16,10),
                Margin=new Padding(0,2,0,2)
            };

            bool focused=false;
            void ApplyRound()
            {
                if(host.Width<4 || host.Height<4) return;
                int radius=Math.Min(16,Math.Max(4,host.Height/2-2));
                int d=radius*2;
                var r=new Rectangle(0,0,host.Width-1,host.Height-1);
                using var gp=new System.Drawing.Drawing2D.GraphicsPath();
                gp.AddArc(r.Left,r.Top,d,d,180,90);
                gp.AddArc(r.Right-d,r.Top,d,d,270,90);
                gp.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);
                gp.AddArc(r.Left,r.Bottom-d,d,d,90,90);
                gp.CloseFigure();
                host.Region?.Dispose();
                host.Region=new Region(gp);
            }

            host.Paint+=(_,e)=>
            {
                if(host.Width<4 || host.Height<4) return;
                e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
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
            };
            host.Resize+=(_,_)=>ApplyRound();
            host.HandleCreated+=(_,_)=>ApplyRound();
            box.Enter+=(_,_)=>{focused=true;host.Invalidate();};
            box.Leave+=(_,_)=>{focused=false;host.Invalidate();};

            host.Controls.Add(box);
            if(password)
            {
                var eye=new Button
                {
                    Text="👁",Dock=DockStyle.Right,Width=52,FlatStyle=FlatStyle.Flat,
                    BackColor=Color.White,ForeColor=Color.FromArgb(4,70,112),
                    Font=new Font("Segoe UI Emoji",12),Cursor=Cursors.Hand
                };
                eye.FlatAppearance.BorderSize=0;
                eye.FlatAppearance.MouseOverBackColor=Color.FromArgb(232,247,252);
                eye.Click+=(_,_)=>
                {
                    box.UseSystemPasswordChar=!box.UseSystemPasswordChar;
                    eye.Text=box.UseSystemPasswordChar?"👁":"🙈";
                    box.Focus();
                };
                host.Controls.Add(eye);
                eye.BringToFront();
            }
            return host;
        }

        p.Controls.Add(Lab("Usuário"),0,0); p.Controls.Add(ModernField(user),0,1);
        p.Controls.Add(Lab("Senha"),0,2);
        p.Controls.Add(ModernField(pass,true),0,3);

        var forgot=new LinkLabel{Text="Esqueci minha senha",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight};
        p.Controls.Add(forgot,0,4);

        var emergency=new LinkLabel{Text="Usar código de recuperação de emergência",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight};
        p.Controls.Add(emergency,0,5);

        var recoveryStatus=new Label
        {
            Text = EmailRecovery.IsConfigured()
                ? "Recuperação por e-mail: ATIVA"
                : "Recuperação por e-mail: NÃO CONFIGURADA",
            Dock=DockStyle.Fill,
            ForeColor = EmailRecovery.IsConfigured() ? Color.DarkGreen : Color.DarkRed,
            Font=new Font("Segoe UI",9,FontStyle.Bold),
            TextAlign=ContentAlignment.MiddleRight
        };
        p.Controls.Add(recoveryStatus,0,6);

        var enter=new Button{Text="ENTRAR",Dock=DockStyle.Fill,BackColor=Color.FromArgb(0,163,224),ForeColor=Color.White,
            FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",12,FontStyle.Bold)};
        enter.FlatAppearance.BorderSize=0; p.Controls.Add(enter,0,7);

        var help=new Button{Text="▶  AJUDA PARA RECUPERAR SENHA",Dock=DockStyle.Fill,BackColor=Color.FromArgb(4,70,112),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",10,FontStyle.Bold)};
        help.FlatAppearance.BorderSize=0;
        p.Controls.Add(help,0,8);
        help.Click+=(_,_)=>OpenRecoveryHelp();

        AcceptButton=enter;

        void Go()
        {
            if(Auth.Login(user.Text,pass.Text)!=null){DialogResult=DialogResult.OK;Close();return;}
            MessageBox.Show("Usuário ou senha inválidos, ou usuário inativo.","Acesso",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            pass.Clear(); pass.Focus();
        }
        enter.Click+=(_,_)=>Go();

        forgot.Click+=(_,_)=>OpenPasswordRecovery();

        emergency.Click+=(_,_)=>OpenEmergencyRecovery();

        void OpenRecoveryHelp()
        {
            using var f=new Form
            {
                Text="Ajuda • Recuperação de Senha",
                StartPosition=FormStartPosition.CenterParent,
                Width=720,Height=520,
                FormBorderStyle=FormBorderStyle.FixedDialog,
                MaximizeBox=false,MinimizeBox=false,
                BackColor=Color.FromArgb(236,245,250),
                Font=new Font("Segoe UI",10)
            };

            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(34,26,34,26)};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,72));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
            f.Controls.Add(root);

            root.Controls.Add(new Label
            {
                Text="PERDEU A SENHA? SEM PÂNICO.",
                Dock=DockStyle.Fill,
                ForeColor=Color.FromArgb(4,70,112),
                Font=new Font("Segoe UI",20,FontStyle.Bold),
                TextAlign=ContentAlignment.MiddleCenter
            },0,0);

            var guide=new Label
            {
                Dock=DockStyle.Fill,
                BackColor=Color.White,
                ForeColor=Color.FromArgb(8,38,68),
                Padding=new Padding(28),
                Font=new Font("Segoe UI",11,FontStyle.Bold),
                TextAlign=ContentAlignment.MiddleLeft,
                Text="▶ GUIA RÁPIDO DE RECUPERAÇÃO\\r\\n\\r\\n"+
                     "1. Se o e-mail estiver ativo, use “Esqueci minha senha”.\\r\\n\\r\\n"+
                     "2. Sem acesso ao e-mail? Use “Recuperação de emergência”.\\r\\n\\r\\n"+
                     "3. O sistema pode procurar automaticamente a chave recuperacao.leal neste computador.\\r\\n\\r\\n"+
                     "4. Se não houver chave local, use um dos códigos de emergência guardados.\\r\\n\\r\\n"+
                     "5. Após validar, cadastre uma nova senha."
            };
            root.Controls.Add(guide,0,1);

            var video=new Button
            {
                Text="▶  ASSISTIR VÍDEO EXPLICATIVO",
                Dock=DockStyle.Fill,
                BackColor=Color.FromArgb(0,163,224),
                ForeColor=Color.White,
                FlatStyle=FlatStyle.Flat,
                Font=new Font("Segoe UI",11,FontStyle.Bold)
            };
            video.FlatAppearance.BorderSize=0;
            root.Controls.Add(video,0,2);

            var options=new Button
            {
                Text="NÃO CONSIGO RECUPERAR MINHA SENHA",
                Dock=DockStyle.Fill,
                BackColor=Color.FromArgb(185,22,38),
                ForeColor=Color.White,
                FlatStyle=FlatStyle.Flat,
                Font=new Font("Segoe UI",10.5f,FontStyle.Bold)
            };
            options.FlatAppearance.BorderSize=0;
            root.Controls.Add(options,0,3);

            video.Click+=(_,_)=>{
                var candidates=new[]{
                    Path.Combine(AppContext.BaseDirectory,"ajuda_recuperar_senha.mp4"),
                    Path.Combine(AppContext.BaseDirectory,"Ajuda","recuperar_senha.mp4")
                };
                var file=candidates.FirstOrDefault(File.Exists);
                if(file==null)
                {
                    MessageBox.Show("O vídeo explicativo ainda não foi adicionado.\\r\\n\\r\\nO guia desta tela já mostra todas as opções de recuperação.",
                        "Ajuda",MessageBoxButtons.OK,MessageBoxIcon.Information);
                    return;
                }
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file){UseShellExecute=true});
                }
                catch(Exception ex){MessageBox.Show("Não foi possível abrir o vídeo:\\r\\n"+ex.Message);}
            };

            options.Click+=(_,_)=>{
                string email=EmailRecovery.IsConfigured()?"ATIVA":"NÃO CONFIGURADA";
                string local=Auth.HasLocalRecoveryKey()?"ENCONTRADA":"NÃO ENCONTRADA";
                MessageBox.Show(
                    "DIAGNÓSTICO DE RECUPERAÇÃO\\r\\n\\r\\n"+
                    "Recuperação por e-mail: "+email+"\\r\\n"+
                    "Chave local neste computador: "+local+"\\r\\n\\r\\n"+
                    (Auth.HasLocalRecoveryKey()
                        ?"Recomendação: tente primeiro a CHAVE LOCAL."
                        : EmailRecovery.IsConfigured()
                            ?"Recomendação: tente primeiro a RECUPERAÇÃO POR E-MAIL."
                            :"Use um CÓDIGO DE EMERGÊNCIA que tenha sido guardado."),
                    "LEAL INFO PDV",MessageBoxButtons.OK,MessageBoxIcon.Information);
            };

            f.ShowDialog(this);
        }

        void OpenEmergencyRecovery()
        {
            using var f=new Form
            {
                Text="Recuperação de Emergência",
                StartPosition=FormStartPosition.CenterParent,
                Width=560,
                Height=390,
                FormBorderStyle=FormBorderStyle.FixedDialog,
                MaximizeBox=false,
                MinimizeBox=false,
                BackColor=Color.FromArgb(224,239,248),
                Font=new Font("Segoe UI",10)
            };

            var p2=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=7,Padding=new Padding(36,24,36,24)};
            f.Controls.Add(p2);
            Label L2(string s)=>new(){Text=s,Dock=DockStyle.Fill,ForeColor=Color.FromArgb(4,55,94),Font=new Font("Segoe UI",10.5f,FontStyle.Bold),TextAlign=ContentAlignment.BottomLeft};

            var identity=new TextBox{Dock=DockStyle.Fill,Font=new Font("Segoe UI",12,FontStyle.Bold)};
            var code=new TextBox{Dock=DockStyle.Fill,Font=new Font("Segoe UI",12,FontStyle.Bold),TextAlign=HorizontalAlignment.Center};
            var validate=new Button{Text="VALIDAR CÓDIGO DE EMERGÊNCIA",Dock=DockStyle.Fill,BackColor=Color.FromArgb(185,22,38),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",10.5f,FontStyle.Bold)};
            validate.FlatAppearance.BorderSize=0;
            var local=new Button{Text="PROCURAR CHAVE NESTE COMPUTADOR",Dock=DockStyle.Fill,BackColor=Color.FromArgb(8,72,120),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",10,FontStyle.Bold)};
            local.FlatAppearance.BorderSize=0;
            var status2=new Label{Dock=DockStyle.Fill,ForeColor=Color.FromArgb(4,70,112),Font=new Font("Segoe UI",9.5f,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter};

            p2.RowStyles.Add(new RowStyle(SizeType.Absolute,32));
            p2.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            p2.RowStyles.Add(new RowStyle(SizeType.Absolute,32));
            p2.RowStyles.Add(new RowStyle(SizeType.Absolute,50));
            p2.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
            p2.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
            p2.RowStyles.Add(new RowStyle(SizeType.Percent,100));

            p2.Controls.Add(L2("Usuário ou e-mail"),0,0);
            p2.Controls.Add(identity,0,1);
            p2.Controls.Add(L2("Código de emergência (opcional se houver chave local)"),0,2);
            p2.Controls.Add(code,0,3);
            p2.Controls.Add(validate,0,4);
            p2.Controls.Add(local,0,5);
            p2.Controls.Add(status2,0,6);

            local.Click+=(_,_)=>{
                if(string.IsNullOrWhiteSpace(identity.Text)){status2.Text="Informe primeiro o usuário ou e-mail.";return;}
                status2.Text="Procurando chave de recuperação local...";
                var localResult=Auth.TryLocalRecovery(identity.Text);
                status2.Text=localResult.message;
                if(localResult.ok) OpenNewPassword(localResult.userId);
            };

            void OpenNewPassword(long recoveredUserId)
            {
                using var nf=new Form{Text="Criar nova senha",StartPosition=FormStartPosition.CenterParent,Width=500,Height=310,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Color.FromArgb(224,239,248)};
                var np=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,Padding=new Padding(36,26,36,26)};
                nf.Controls.Add(np);
                var p1=new TextBox{Dock=DockStyle.Top,UseSystemPasswordChar=true,Font=new Font("Segoe UI",12)};
                var p2c=new TextBox{Dock=DockStyle.Top,UseSystemPasswordChar=true,Font=new Font("Segoe UI",12)};
                var save=new Button{Text="SALVAR NOVA SENHA",Dock=DockStyle.Top,Height=46,BackColor=Color.FromArgb(0,163,224),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
                np.Controls.Add(new Label{Text="Nova senha (mínimo 6 caracteres)",Height=28,Dock=DockStyle.Top}); np.Controls.Add(p1);
                np.Controls.Add(new Label{Text="Confirmar nova senha",Height=28,Dock=DockStyle.Top}); np.Controls.Add(p2c); np.Controls.Add(save);
                save.Click+=(_,_)=>{
                    if(p1.Text.Length<6){MessageBox.Show("Use pelo menos 6 caracteres.");return;}
                    if(p1.Text!=p2c.Text){MessageBox.Show("As senhas não conferem.");return;}
                    Auth.ResetPassword(recoveredUserId,p1.Text);
                    MessageBox.Show("Senha alterada com sucesso.");
                    nf.DialogResult=DialogResult.OK; nf.Close();
                };
                if(nf.ShowDialog(f)==DialogResult.OK){f.Close();pass.Focus();}
            }

            validate.Click+=(_,_)=>{
                if(string.IsNullOrWhiteSpace(identity.Text)||string.IsNullOrWhiteSpace(code.Text))
                { status2.Text="Preencha usuário/e-mail e código."; return; }
                var r=Auth.ValidateEmergencyCode(identity.Text,code.Text);
                status2.Text=r.message;
                if(r.ok) OpenNewPassword(r.userId);
            };

            f.ShowDialog(this);
        }

        void OpenPasswordRecovery()
        {
            using var f=new Form{Text="Recuperar senha",StartPosition=FormStartPosition.CenterParent,Width=560,Height=430,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Color.FromArgb(224,239,248),Font=new Font("Segoe UI",10)};
            var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=7,Padding=new Padding(36,24,36,24)};f.Controls.Add(p);
            Label L(string x)=>new(){Text=x,Dock=DockStyle.Fill,ForeColor=Color.FromArgb(4,55,94),Font=new Font("Segoe UI",10.5f,FontStyle.Bold),TextAlign=ContentAlignment.BottomLeft};
            var identity=new TextBox{Dock=DockStyle.Fill,Font=new Font("Segoe UI",12,FontStyle.Bold)};
            var code=new TextBox{Dock=DockStyle.Fill,Font=new Font("Segoe UI",16,FontStyle.Bold),TextAlign=HorizontalAlignment.Center,Enabled=false};
            var send=new Button{Text="ENVIAR CÓDIGO POR E-MAIL",Dock=DockStyle.Fill,BackColor=Color.FromArgb(0,145,210),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
            var validate=new Button{Text="VALIDAR CÓDIGO",Dock=DockStyle.Fill,BackColor=Color.FromArgb(0,163,224),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Enabled=false};
            send.FlatAppearance.BorderSize=0;validate.FlatAppearance.BorderSize=0;
            p.RowStyles.Add(new RowStyle(SizeType.Absolute,32));p.RowStyles.Add(new RowStyle(SizeType.Absolute,48));p.RowStyles.Add(new RowStyle(SizeType.Absolute,58));p.RowStyles.Add(new RowStyle(SizeType.Absolute,32));p.RowStyles.Add(new RowStyle(SizeType.Absolute,55));p.RowStyles.Add(new RowStyle(SizeType.Absolute,58));p.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            p.Controls.Add(L("Usuário ou e-mail cadastrado"),0,0);p.Controls.Add(identity,0,1);p.Controls.Add(send,0,2);p.Controls.Add(L("Código recebido"),0,3);p.Controls.Add(code,0,4);p.Controls.Add(validate,0,5);
            var status=new Label{Dock=DockStyle.Fill,ForeColor=Color.FromArgb(4,70,112),Font=new Font("Segoe UI",9.5f,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter};p.Controls.Add(status,0,6);
            send.Click+=(_,_)=>{if(string.IsNullOrWhiteSpace(identity.Text)){status.Text="Informe o usuário ou e-mail.";return;}send.Enabled=false;Cursor.Current=Cursors.WaitCursor;var r=EmailRecovery.SendResetCode(identity.Text);Cursor.Current=Cursors.Default;send.Enabled=true;status.Text=r.message;if(r.ok){code.Enabled=true;validate.Enabled=true;code.Focus();}};
            validate.Click+=(_,_)=>{if(string.IsNullOrWhiteSpace(code.Text)){status.Text="Digite o código recebido.";return;}var r=EmailRecovery.ValidateCode(identity.Text,code.Text);status.Text=r.message;if(!r.ok)return;
                using var nf=new Form{Text="Criar nova senha",StartPosition=FormStartPosition.CenterParent,Width=500,Height=310,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Color.FromArgb(224,239,248)};
                var np=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,Padding=new Padding(36,26,36,26)};nf.Controls.Add(np);
                var p1=new TextBox{Dock=DockStyle.Top,UseSystemPasswordChar=true,Font=new Font("Segoe UI",12)};var p2=new TextBox{Dock=DockStyle.Top,UseSystemPasswordChar=true,Font=new Font("Segoe UI",12)};
                var save=new Button{Text="SALVAR NOVA SENHA",Dock=DockStyle.Top,Height=46,BackColor=Color.FromArgb(0,163,224),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
                np.Controls.Add(new Label{Text="Nova senha (mínimo 6 caracteres)",Height=28,Dock=DockStyle.Top});np.Controls.Add(p1);np.Controls.Add(new Label{Text="Confirmar nova senha",Height=28,Dock=DockStyle.Top});np.Controls.Add(p2);np.Controls.Add(save);
                save.Click+=(_,_)=>{if(p1.Text.Length<6){MessageBox.Show("Use pelo menos 6 caracteres.");return;}if(p1.Text!=p2.Text){MessageBox.Show("As senhas não conferem.");return;}Auth.ResetPassword(r.userId,p1.Text);MessageBox.Show("Senha alterada com sucesso.");nf.DialogResult=DialogResult.OK;nf.Close();};
                if(nf.ShowDialog(f)==DialogResult.OK){f.Close();pass.Focus();}
            };
            f.ShowDialog(this);
        }
    }

    void BuildFirstAdmin(TableLayoutPanel p,Func<bool,TextBox> Box,Func<string,Label> Lab)
    {
        p.RowCount=11;
        for(int i=0;i<10;i++) p.RowStyles.Add(new RowStyle(SizeType.Absolute,i%2==0?28:44));
        p.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var name=Box(false); var user=Box(false); var email=Box(false); var pass=Box(true); var confirm=Box(true);
        var arr=new[]{("Nome completo",name),("Usuário",user),("E-mail para recuperação",email),("Senha",pass),("Confirmar senha",confirm)};
        int r=0; foreach(var x in arr){p.Controls.Add(Lab(x.Item1),0,r++);p.Controls.Add(x.Item2,0,r++);}
        var save=new Button{Text="CRIAR ADMINISTRADOR E ENTRAR",Dock=DockStyle.Fill,Height=48,BackColor=Color.FromArgb(0,163,224),
            ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",11,FontStyle.Bold)};
        save.FlatAppearance.BorderSize=0;p.Controls.Add(save,0,10);
        AcceptButton=save;
        save.Click+=(_,_)=>{
            if(string.IsNullOrWhiteSpace(name.Text)||string.IsNullOrWhiteSpace(user.Text)||pass.Text.Length<6){
                MessageBox.Show("Preencha nome, usuário e uma senha com pelo menos 6 caracteres.");return;}
            if(pass.Text!=confirm.Text){MessageBox.Show("As senhas não conferem.");return;}
            try{
                Auth.CreateUser(name.Text,user.Text,pass.Text,"ADMINISTRADOR",email.Text,"",true);
                Auth.Login(user.Text,pass.Text); DialogResult=DialogResult.OK; Close();
            }catch(Exception ex){MessageBox.Show("Não foi possível criar o administrador:\\n"+ex.Message);}
        };
    }
}
