namespace LealInfo.IFood.Assistant
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Windows.Forms;
    using LealInfo.IFood.Groceries;

    public sealed class AssistantForm : Form
    {
        private readonly Guid merchant = new Guid("84a8976b-0559-48c6-84ce-fc2502f1364a");
        private readonly List<AuthenticationEvidence> logs = new List<AuthenticationEvidence>();
        private readonly TextBox id = new TextBox(), secret = new TextBox();
        private readonly Label status = new Label();
        private readonly Button connect = new Button(), save = new Button();
        private readonly CheckBox testApp = new CheckBox();
        private AuthenticationClient auth;
        private bool busy, authenticated, merchantVerified;
        private string safeError = "Nenhuma tentativa realizada.";
        public AssistantForm()
        {
            Text = "LEAL INFO - iFood TESTE (C) - R4";
            ClientSize = new Size(730, 490); MinimumSize = new Size(650, 490);
            StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(24,26,30); ForeColor = Color.White;
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 10, AutoScroll = true };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); Controls.Add(panel);
            Add(panel, new Label { Text="iFood | Aplicativo de testes centralizado (C)", AutoSize=true, Font=new Font(Font,FontStyle.Bold), ForeColor=Color.FromArgb(224,191,113) });
            Add(panel, new Label { Text="No Developer Portal, abra o aplicativo terminado em Teste (C).\nA conexao usa as credenciais desse aplicativo, sem login de parceiro.", AutoSize=true });
            Add(panel, new Label { Text="Client ID do aplicativo Teste (C)", AutoSize=true }); Add(panel,id);
            Add(panel, new Label { Text="Client Secret do Teste (C) - somente na memoria desta janela", AutoSize=true }); secret.UseSystemPasswordChar=true; Add(panel,secret);
            testApp.Text="Estou usando as credenciais do Teste (C).";testApp.AutoSize=true;Add(panel,testApp);
            connect.Text="Conectar e verificar minha loja de testes";connect.AutoSize=true;Add(panel,connect);
            status.Text="Aguardando. Nenhuma chamada foi feita.";status.AutoSize=true;status.MaximumSize=new Size(660,0);Add(panel,status);
            save.Text="Salvar resultado para enviar ao Work";save.AutoSize=true;Add(panel,save);
            connect.Click += async (s,e) => {
                if(busy)return;
                if(!testApp.Checked || string.IsNullOrWhiteSpace(id.Text) || string.IsNullOrWhiteSpace(secret.Text))
                {status.Text="Preencha os campos do Teste (C) e marque a confirmacao.";return;}
                SetBusy(true);connect.Enabled=false;
                try {
                    auth=new AuthenticationClient(id.Text.Trim(),secret.Text.Trim(),entry=>{lock(logs){logs.Add(entry);}},null,null,AuthenticationMode.Centralized);
                    secret.Clear();id.ReadOnly=true;secret.Enabled=false;testApp.Enabled=false;
                    status.Text="Autenticando o aplicativo de testes...";
                    await auth.ConnectCentralizedAsync(CancellationToken.None);authenticated=true;
                    status.Text="Autenticacao aceita. Consultando sua loja de testes...";
                    await auth.VerifyMerchantAsync(merchant,CancellationToken.None);merchantVerified=true;
                    safeError="Nenhum.";status.Text="AUTENTICADO. Acesso a loja de testes confirmado.\nSalve o resultado para continuarmos com os pedidos.";
                } catch(Exception ex){
                    var known=ex as AuthenticationFailure;
                    safeError=known!=null?known.Message:"Falha local inesperada; nenhum detalhe privado foi registrado.";
                    status.Text=(authenticated?"Autenticacao aceita; acesso a loja NAO confirmado. ":"BLOQUEADO. ")+safeError+"\nSalve o resultado. Nao ha repeticao automatica.";
                }
                finally{SetBusy(false);}
            };
            save.Click += (s,e)=> {
                if(busy)return;
                using(var dialog=new SaveFileDialog { Filter="Resultado de teste (*.txt)|*.txt",FileName="RESULTADO_IFOOD_TESTE.txt",InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) }){
                    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                    try{File.WriteAllText(dialog.FileName,Report(),new UTF8Encoding(true));status.Text="Resultado salvo. Envie somente esse arquivo ao Work.";}
                    catch{status.Text="Nao foi possivel salvar. Escolha uma pasta com permissao de gravacao.";}
                }
            };
            FormClosing += (s,e)=>{if(busy){e.Cancel=true;status.Text="Aguarde a operacao terminar para fechar.";}};
            FormClosed += (s,e)=>{if(auth!=null)auth.Dispose();secret.Clear();};
        }
        private static void Add(TableLayoutPanel panel,Control control)
        {control.Dock=DockStyle.Top;control.Margin=new Padding(0,0,0,10);panel.Controls.Add(control);}
        private void SetBusy(bool value){busy=value;save.Enabled=!value;UseWaitCursor=value;}
        private string Report()
        {
            var text=new StringBuilder();text.AppendLine("LEAL INFO - DIAGNOSTICO IFOOD TESTE (C) - R4");
            text.AppendLine("UTC: "+DateTimeOffset.UtcNow.ToString("O"));
            text.AppendLine("Fluxo: client_credentials (aplicativo de testes centralizado)");
            text.AppendLine("Loja de testes (Merchant UUID): " + merchant.ToString("D"));
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
