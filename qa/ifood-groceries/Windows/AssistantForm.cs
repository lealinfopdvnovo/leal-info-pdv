namespace LealInfo.IFood.Assistant
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using LealInfo.IFood.Groceries;

    public sealed class AssistantForm : Form
    {
        private readonly Guid merchant = new Guid("84a8976b-0559-48c6-84ce-fc2502f1364a");
        private readonly List<AuthenticationEvidence> logs = new List<AuthenticationEvidence>();
        private readonly TextBox id = new TextBox(), secret = new TextBox();
        private readonly Label status = new Label(), counter = new Label();
        private readonly Button connect = new Button(), save = new Button(), toggle = new Button();
        private readonly CheckBox testApp = new CheckBox();
        private readonly DataGridView grid = new DataGridView();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private AuthenticationClient auth;
        private EventsClient events;
        private EventJournal journal;
        private EventReceiver receiver;
        private CancellationTokenSource operation;
        private bool busy, running, authenticated, merchantVerified, closing;
        private int polls, received, duplicate, skippedLogs;
        private string safeError = "Nenhuma falha.", folder;
        public AssistantForm()
        {
            Text = "LEAL INFO - iFood | ETAPA 2 - RECEBIMENTO R5";
            ClientSize = new Size(1000, 720); MinimumSize = new Size(760, 610);
            StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(24,26,30); ForeColor = Color.White;
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 5 };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); Controls.Add(panel);
            var login = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 5 };
            login.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150)); login.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            var title = new Label { Text="RECEBIMENTO DE PEDIDOS | LOJA DE TESTES", AutoSize=true, Font=new Font(Font,FontStyle.Bold), ForeColor=Color.FromArgb(224,191,113), Margin=new Padding(0,0,0,12) };
            login.Controls.Add(title,0,0); login.SetColumnSpan(title,2);
            login.Controls.Add(new Label {Text="Client ID - Teste (C)",AutoSize=true},0,1); id.Dock=DockStyle.Fill;login.Controls.Add(id,1,1);
            login.Controls.Add(new Label {Text="Client Secret",AutoSize=true},0,2);secret.Dock=DockStyle.Fill;secret.UseSystemPasswordChar=true;login.Controls.Add(secret,1,2);
            testApp.Text="Credenciais do Teste (C). Receber somente na minha loja de testes.";testApp.AutoSize=true;
            login.Controls.Add(testApp,0,3);login.SetColumnSpan(testApp,2);
            connect.Text="Conectar e iniciar recebimento";connect.AutoSize=true;
            toggle.Text="Pausar recebimento";toggle.AutoSize=true;toggle.Enabled=false;
            var buttons=new FlowLayoutPanel {AutoSize=true,Dock=DockStyle.Fill};buttons.Controls.Add(connect);buttons.Controls.Add(toggle);
            login.Controls.Add(buttons,0,4);login.SetColumnSpan(buttons,2);panel.Controls.Add(login,0,0);
            status.Text="Pronto para receber. As credenciais ficam somente nesta janela.";status.AutoSize=true;status.MaximumSize=new Size(940,0);status.Margin=new Padding(0,10,0,10);panel.Controls.Add(status,0,1);
            grid.Dock=DockStyle.Fill;grid.ReadOnly=true;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.RowHeadersVisible=false;
            grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;grid.BackgroundColor=Color.FromArgb(32,34,38);
            grid.DefaultCellStyle.BackColor=Color.FromArgb(32,34,38);grid.DefaultCellStyle.ForeColor=Color.White;grid.EnableHeadersVisualStyles=false;
            grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(55,50,35);grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.FromArgb(224,191,113);
            grid.Columns.Add("date","Evento (horario local)");grid.Columns.Add("order","Pedido (ID)");grid.Columns.Add("event","Situacao recebida");grid.Columns.Add("ack","Recebimento");
            grid.Columns[1].FillWeight=170;panel.Controls.Add(grid,0,2);
            counter.AutoSize=true;counter.Margin=new Padding(0,8,0,8);panel.Controls.Add(counter,0,3);
            var footer=new FlowLayoutPanel {AutoSize=true,Dock=DockStyle.Fill};save.Text="Salvar resultado para o Work";save.AutoSize=true;footer.Controls.Add(save);
            footer.Controls.Add(new Label {AutoSize=true,Text="Somente eventos. Nao confirma, separa ou cancela pedidos.",Margin=new Padding(8,8,0,0)});panel.Controls.Add(footer,0,4);
            connect.Click += async (s,e) => await ConnectAsync();
            toggle.Click += async (s,e) => {
                if(running){running=false;timer.Stop();if(operation!=null)operation.Cancel();toggle.Text="Retomar recebimento";status.Text="Recebimento pausado. Historico preservado.";}
                else if(!busy && receiver!=null && !receiver.Stopped){running=true;toggle.Text="Pausar recebimento";status.Text="Recebimento retomado. Aguardando o proximo intervalo de consulta.";timer.Start();await PollAsync();}
            };
            timer.Interval=1000;timer.Tick += async (s,e)=>{if(running && !busy)await PollAsync();};
            save.Click += (s,e)=>SaveReport();
            FormClosing += (s,e)=>{
                running=false;timer.Stop();if(busy){closing=true;e.Cancel=true;if(operation!=null)operation.Cancel();status.Text="Encerrando operacao e preservando a fila...";}
            };
            FormClosed += (s,e)=>{timer.Dispose();if(events!=null)events.Dispose();if(auth!=null)auth.Dispose();if(journal!=null)journal.Dispose();if(operation!=null)operation.Dispose();secret.Clear();};
            RefreshGrid();
        }
        private void Log(AuthenticationEvidence item)
        {lock(logs){if(logs.Count==10000){logs.RemoveAt(0);skippedLogs++;}logs.Add(item);}}
        private async Task ConnectAsync()
        {
            if(busy)return;
            if(!testApp.Checked || string.IsNullOrWhiteSpace(id.Text) || string.IsNullOrWhiteSpace(secret.Text))
            {status.Text="Use os campos do Teste (C) e marque a confirmacao.";return;}
            SetBusy(true);connect.Enabled=false;operation=new CancellationTokenSource();
            try
            {
                // Per-app directory prevents ACK bookkeeping being shared between different devices.
                string appHash;using(var sha=SHA256.Create())appHash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(id.Text.Trim()))).Replace("-", "").Substring(0,24);
                folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LEAL_INFO_IFOOD_TESTES","ETAPA2",merchant.ToString("D"),appHash);
                journal=new EventJournal(folder,merchant);
                auth=new AuthenticationClient(id.Text.Trim(),secret.Text.Trim(),Log,null,null,AuthenticationMode.Centralized);
                secret.Clear();id.ReadOnly=true;secret.Enabled=false;testApp.Enabled=false;
                status.Text="Conectando a sessao para receber pedidos...";
                await auth.ConnectCentralizedAsync(operation.Token);authenticated=true;
                await auth.VerifyMerchantAsync(merchant,operation.Token);merchantVerified=true;
                events=new EventsClient(merchant,auth.GetAccessTokenAsync,Log);receiver=new EventReceiver(events,journal);
                running=true;toggle.Enabled=true;timer.Start();status.Text="Conectado. Iniciando recebimento a cada 30 segundos.";RefreshGrid();
            }
            catch(Exception ex){Fail(ex);}
            finally{FinishOperation();}
            if(running && !closing)await PollAsync();
        }
        private async Task PollAsync()
        {
            if(busy || !running || receiver==null || DateTimeOffset.UtcNow<journal.NextAttemptUtc)return;
            SetBusy(true);operation=new CancellationTokenSource();
            try
            {
                var result=await receiver.RunCycleAsync(operation.Token);
                if(result.Polled){polls++;received+=result.Received;duplicate+=result.Received-result.Added;}
                status.Text=result.Message;RefreshGrid();
            }
            catch(OperationCanceledException){status.Text="Recebimento pausado. Eventos gravados permanecem na fila.";}
            catch(Exception ex){Fail(ex);RefreshGrid();}
            finally{FinishOperation();}
        }
        private void Fail(Exception ex)
        {
            running=false;timer.Stop();toggle.Enabled=false;
            var reception=ex as ReceptionFailure;var authentication=ex as AuthenticationFailure;
            safeError=reception!=null?reception.Message:authentication!=null?authentication.Message:"Falha local na fila ou na comunicacao. Preserve a pasta de testes para analise.";
            status.Text="Recebimento interrompido. "+safeError+" Salve o resultado.";
        }
        private void SetBusy(bool value){busy=value;save.Enabled=!value;UseWaitCursor=value;}
        private void FinishOperation()
        {
            SetBusy(false);if(operation!=null){operation.Dispose();operation=null;}
            if(closing)BeginInvoke(new Action(Close));
        }
        private void RefreshGrid()
        {
            grid.Rows.Clear();var rows=journal==null?new List<ReceivedEvent>():journal.Snapshot();
            foreach(var row in rows.Reverse().Take(2000))grid.Rows.Add(DateTimeOffset.Parse(row.CreatedAt).ToLocalTime().ToString("dd/MM HH:mm:ss"),row.OrderId,
                row.FullCode+(row.AwaitingFinalBag?" (sacola pendente)":""),row.AckAccepted?"Gravado / ACK aceito":"Gravado / ACK pendente");
            counter.Text="Pedidos no historico: "+rows.Select(r=>r.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count()+" | Eventos: "+rows.Count+" | ACK pendentes: "+rows.Count(r=>!r.AckAccepted)+" | Consultas nesta sessao: "+polls;
        }
        private void SaveReport()
        {
            if(busy)return;
            using(var dialog=new SaveFileDialog {Filter="Resultado (*.txt)|*.txt",FileName="RESULTADO_IFOOD_ETAPA2.txt",InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                try{File.WriteAllText(dialog.FileName,Report(),new UTF8Encoding(true));status.Text="Resultado salvo. Envie RESULTADO_IFOOD_ETAPA2.txt ao Work.";}
                catch{status.Text="Nao foi possivel salvar. Escolha uma pasta com permissao.";}
            }
        }
        private string Report()
        {
            var text=new StringBuilder("LEAL INFO - IFOOD ETAPA 2 - RECEBIMENTO R5\r\n");
            text.AppendLine("UTC: "+DateTimeOffset.UtcNow.ToString("O"));text.AppendLine("Loja de testes: "+merchant);
            text.AppendLine("Autenticacao nesta sessao: "+(authenticated?"CONFIRMADA":"NAO CONFIRMADA"));
            text.AppendLine("Acesso a loja nesta sessao: "+(merchantVerified?"CONFIRMADO":"NAO CONFIRMADO"));
            text.AppendLine("Consultas de eventos bem-sucedidas nesta sessao: "+polls);
            text.AppendLine("Eventos em ciclos concluidos nesta sessao: "+received+"; repetidos: "+duplicate);
            text.AppendLine("Ultima falha fatal: "+safeError);text.AppendLine("Recepcao ativa: "+running);
            var rows=journal==null?new List<ReceivedEvent>():journal.Snapshot();
            text.AppendLine("Eventos tecnicos persistidos: "+rows.Count+"; ACK pendentes: "+rows.Count(r=>!r.AckAccepted));
            text.AppendLine("Historico pode incluir sessoes anteriores. HTTP 204 confirma comunicacao, nao recebimento de pedido.");
            text.AppendLine("ACK confirma apenas transporte do evento; NAO confirma o pedido. Sem separacao, importacao da sacola ou efeitos no PDV.");
            text.AppendLine("Videos, logs oficiais e homologacao permanecem pendentes.");
            text.AppendLine("EVENTOS (IDs tecnicos; sem cliente, endereco, itens ou credenciais):");
            foreach(var row in rows)text.AppendLine(row.CreatedAt+" | pedido "+row.OrderId+" | evento "+row.Id+" | "+row.Code+" / "+row.FullCode+" | ACK "+row.AckAccepted+" | processamento comercial "+row.BusinessProcessed);
            text.AppendLine("HTTP (metadados; sem corpos, cabecalhos, chaves ou tokens):");
            lock(logs){foreach(var row in logs)text.AppendLine(row.AtUtc.ToString("O")+" | "+row.Method+" | "+row.Route+" | HTTP "+(row.Status.HasValue?row.Status.Value.ToString():"sem resposta")+" | "+row.ElapsedMs+" ms | "+row.Outcome);text.AppendLine("Registros antigos omitidos por limite: "+skippedLogs);}
            return text.ToString();
        }
        [STAThread] public static void Run(){Application.EnableVisualStyles();Application.Run(new AssistantForm());}
    }
}
