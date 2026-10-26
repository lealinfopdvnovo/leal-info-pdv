namespace LealInfo.IFood.Assistant
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Windows.Forms;
    using LealInfo.IFood.Groceries;

    public sealed class AssistantForm : Form
    {
        private readonly Guid merchant = new Guid("84a8976b-0559-48c6-84ce-fe2502f1364a");
        private readonly List<AuthenticationEvidence> logs = new List<AuthenticationEvidence>();
        private readonly TextBox id = new TextBox(), secret = new TextBox(), authorization = new TextBox();
        private readonly Label status = new Label(), link = new Label();
        private readonly Button begin = new Button(), open = new Button(), finish = new Button(), save = new Button();
        private readonly CheckBox testApp = new CheckBox();
        private DistributedAuthentication auth;
        private LinkingCode code;
        private bool busy, authenticated, merchantVerified;
        private string safeError = "Nenhuma tentativa realizada.";
        public AssistantForm()
        {
            Text = "LEAL INFO - Conectar iFood de TESTES";
            ClientSize = new Size(730, 620); MinimumSize = new Size(650, 610);
            StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(24,26,30); ForeColor = Color.White;
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 15, AutoScroll = true };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); Controls.Add(panel);
            Add(panel, new Label { Text="iFood | Aplicativo de testes distribuido (D)", AutoSize=true, Font=new Font(Font,FontStyle.Bold), ForeColor=Color.FromArgb(224,191,113) });
            Add(panel, new Label { Text="Use as credenciais do aplicativo com nome terminado em Teste (D).\nEste assistente verifica o login e a loja de testes. Nao altera o PDV.", AutoSize=true });
            Add(panel, new Label { Text="Client ID do aplicativo Teste (D)", AutoSize=true }); Add(panel,id);
            Add(panel, new Label { Text="Client Secret (fica somente na memoria desta janela)", AutoSize=true }); secret.UseSystemPasswordChar=true; Add(panel,secret);
            testApp.Text="Confirmo que estou usando o aplicativo Teste (D).";testApp.AutoSize=true;Add(panel,testApp);
            begin.Text="1. Gerar codigo de autorizacao";begin.AutoSize=true;Add(panel,begin);
            link.Text="O codigo temporario aparecera aqui.";link.AutoSize=true;Add(panel,link);
            open.Text="2. Abrir autorizacao no navegador";open.AutoSize=true;open.Enabled=false;Add(panel,open);
            Add(panel,new Label { Text="Depois de autorizar no portal, cole o codigo recebido abaixo:",AutoSize=true });
            authorization.UseSystemPasswordChar=true;authorization.Enabled=false;Add(panel,authorization);
            finish.Text="3. Validar conexao com a loja de testes";finish.AutoSize=true;finish.Enabled=false;Add(panel,finish);
            status.Text="Aguardando. Nenhuma chamada foi feita.";status.AutoSize=true;status.MaximumSize=new Size(660,0);Add(panel,status);
            save.Text="Salvar resultado para enviar ao Work";save.AutoSize=true;Add(panel,save);
            begin.Click += async (s,e) => {
                if(busy)return;
                if(!testApp.Checked || string.IsNullOrWhiteSpace(id.Text) || string.IsNullOrWhiteSpace(secret.Text))
                {status.Text="Preencha os campos e confirme o uso do aplicativo de testes.";return;}
                SetBusy(true);begin.Enabled=false;
                try {
                    auth=new DistributedAuthentication(id.Text.Trim(),secret.Text.Trim(),entry=>{lock(logs){logs.Add(entry);}});
                    secret.Clear();id.ReadOnly=true;secret.Enabled=false;testApp.Enabled=false;
                    code=await auth.BeginAsync(CancellationToken.None);
                    link.Text="Codigo: "+code.UserCode+" | Valido ate "+code.ExpiresAt.ToLocalTime().ToString("HH:mm:ss");
                    open.Enabled=true;authorization.Enabled=true;finish.Enabled=true;
                    safeError="Aguardando autorizacao no portal.";status.Text="Codigo gerado. Clique no passo 2 e autorize o aplicativo de testes.";
                } catch(Exception ex){Fail(ex);}
                finally{SetBusy(false);}
            };
            open.Click += (s,e) => {
                if(code==null || busy)return;
                if(DateTimeOffset.UtcNow>=code.ExpiresAt){status.Text="Codigo expirado. Salve o resultado e feche a janela.";return;}
                try{Process.Start(new ProcessStartInfo(code.VerificationUrl.AbsoluteUri){UseShellExecute=true});}
                catch{status.Text="Nao foi possivel abrir o navegador. Acesse https://portal.ifood.com.br/apps/code e use o codigo exibido.";}
            };
            finish.Click += async (s,e) => {
                if(busy || auth==null || code==null)return;
                if(string.IsNullOrWhiteSpace(authorization.Text)){status.Text="Cole o codigo retornado pelo portal no campo acima.";return;}
                SetBusy(true);finish.Enabled=false;open.Enabled=false;
                var value=authorization.Text.Trim();authorization.Clear();authorization.Enabled=false;
                try{
                    await auth.CompleteAsync(code,value,CancellationToken.None);authenticated=true;value=null;
                    status.Text="Autenticacao aceita. Consultando apenas a loja de testes...";
                    await auth.VerifyMerchantAsync(merchant,CancellationToken.None);merchantVerified=true;
                    safeError="Nenhum.";status.Text="AUTENTICADO. Acesso a loja de testes confirmado. Salve o resultado.\nIsso ainda nao valida pedidos, separacao ou homologacao.";
                }catch(Exception ex){Fail(ex);}
                finally{value=null;SetBusy(false);}
            };
            save.Click += (s,e)=> {
                if(busy)return;
                using(var dialog=new SaveFileDialog { Filter="Resultado de teste (*.txt)|*.txt",FileName="RESULTADO_IFOOD_TESTE.txt",InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) }){
                    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                    try{File.WriteAllText(dialog.FileName,Report(),new UTF8Encoding(true));status.Text="Resultado salvo. Envie somente esse arquivo ao Work.";}
                    catch{status.Text="Nao foi possivel salvar. Escolha uma pasta com permissao de gravacao.";}
                }
            };
            FormClosing += (s,e)=>{if(busy){e.Cancel=true;status.Text="Aguarde a operacao terminar para fechar (limite de tempo de rede).";}};
            FormClosed += (s,e)=>{if(auth!=null)auth.Dispose();secret.Clear();authorization.Clear();};
        }
        private static void Add(TableLayoutPanel panel,Control control)
        {control.Dock=DockStyle.Top;control.Margin=new Padding(0,0,0,8);panel.Controls.Add(control);}
        private void SetBusy(bool value){busy=value;save.Enabled=!value;UseWaitCursor=value;}
        private void Fail(Exception ex)
        {
            var known=ex as AuthenticationFailure;
            safeError=known!=null?known.Message:"Falha local inesperada; nenhum detalhe privado foi registrado.";
            status.Text=(authenticated?"Autenticacao aceita; acesso a loja NAO confirmado. ":"BLOQUEADO. ")+safeError+"\nSalve o resultado. Nao ha repeticao automatica.";
        }
        private string Report()
        {
            var text=new StringBuilder();text.AppendLine("LEAL INFO - DIAGNOSTICO IFOOD TESTE (D)");
            text.AppendLine("UTC: "+DateTimeOffset.UtcNow.ToString("O"));
            text.AppendLine("Autenticacao: "+(authenticated?"CONFIRMADA":"NAO CONFIRMADA"));
            text.AppendLine("Acesso a loja de testes: "+(merchantVerified?"CONFIRMADO":"NAO CONFIRMADO"));
            text.AppendLine("Resultado: "+safeError);
            text.AppendLine("Pedidos, separacao, videos e homologacao: NAO VALIDADOS por este assistente.");
            text.AppendLine("HTTP (metadados; sem corpos, cabecalhos, chaves ou tokens):");
            lock(logs){foreach(var row in logs)text.AppendLine(row.AtUtc.ToString("O")+" | "+row.Method+" | "+row.Route+" | HTTP "+(row.Status.HasValue?row.Status.Value.ToString():"sem resposta")+" | "+row.ElapsedMs+" ms | "+row.Outcome);}
            return text.ToString();
        }
        [STAThread] public static void Run(){Application.EnableVisualStyles();Application.Run(new AssistantForm());}
    }
}
