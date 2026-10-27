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

    public sealed partial class AssistantForm : Form
    {
        private readonly Guid merchant = new Guid("84a8976b-0559-48c6-84ce-fc2502f1364a");
        private readonly List<AuthenticationEvidence> logs = new List<AuthenticationEvidence>();
        private readonly TextBox id = new TextBox(), secret = new TextBox();
        private readonly Label status = new Label(), counter = new Label();
        private readonly Button connect = new Button(), save = new Button(), toggle = new Button(), showConnection = new Button();
        private Control connectionPanel;
        private readonly CheckBox testApp = new CheckBox();
        private readonly DataGridView grid = new DataGridView(), bagGrid = new DataGridView();
        private readonly ComboBox bagPicker = new ComboBox();
        private readonly Label bagTotals = new Label();
        private readonly TabControl tabs=new TabControl();
        private readonly ComboBox reviewOrder=new ComboBox(), cancelChoice=new ComboBox(), threeChoice=new ComboBox(), oneChoice=new ComboBox();
        private readonly DataGridView inspectionGrid=new DataGridView();
        private readonly Button inspect=new Button(), summary=new Button();
        private readonly Label inspectionBanner=new Label();
        private ScenarioSelectionStore selections;
        private ScenarioInspection currentInspection;
        private Guid displayedReviewOrder;
        private bool refreshingBags,refreshingReview;
        private readonly bool interfaceCheck;
        private string temporaryInterfaceFolder;
        private BagStore bagStore;
        private BagProcessor bagProcessor;
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private AuthenticationClient auth;
        private EventsClient events;
        private EventJournal journal;
        private EventReceiver receiver;
        private CancellationTokenSource operation;
        private bool busy, running, authenticated, merchantVerified, closing;
        private int polls, received, duplicate, skippedLogs;
        private string safeError = "Nenhuma falha.", folder;
        public AssistantForm(bool localInterfaceCheck=false)
        {
            interfaceCheck=localInterfaceCheck;
            Text = "LEAL INFO - iFood | CONFERENCIA 0/3/1 R7"+(interfaceCheck?" | SIMULACAO LOCAL":"");
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
            connectionPanel=login;
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
            grid.Columns[1].FillWeight=170;
            tabs.Dock=DockStyle.Fill;var eventsTab=new TabPage("Eventos");eventsTab.Controls.Add(grid);tabs.TabPages.Add(eventsTab);
            var bagTab=new TabPage("Sacola final - conferencia");bagTab.BackColor=BackColor;bagTab.ForeColor=ForeColor;
            var bagPanel=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(8)};
            bagPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));bagPanel.RowStyles.Add(new RowStyle(SizeType.Percent,100));bagPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bagPicker.Dock=DockStyle.Top;bagPicker.DropDownStyle=ComboBoxStyle.DropDownList;bagPicker.SelectedIndexChanged+=(s,e)=>ShowBag();bagPanel.Controls.Add(bagPicker,0,0);
            bagGrid.Dock=DockStyle.Fill;bagGrid.ReadOnly=true;bagGrid.AllowUserToAddRows=false;bagGrid.AllowUserToDeleteRows=false;bagGrid.RowHeadersVisible=false;bagGrid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
            bagGrid.BackgroundColor=grid.BackgroundColor;bagGrid.DefaultCellStyle=grid.DefaultCellStyle.Clone();
            bagGrid.Columns.Add("product","Produto");bagGrid.Columns.Add("quantity","Qtd. informada");bagGrid.Columns.Add("unavailable","Indisponivel");bagGrid.Columns.Add("available","Qtd. disponivel");bagGrid.Columns.Add("gross","Valor informado");
            bagGrid.Columns[0].FillWeight=200;bagPanel.Controls.Add(bagGrid,0,1);
            bagTotals.AutoSize=true;bagTotals.MaximumSize=new Size(900,0);bagTotals.Margin=new Padding(0,8,0,0);bagTotals.Text="Aguardando RFI e sacola final. Esta tela nao executa a separacao.";bagPanel.Controls.Add(bagTotals,0,2);
            bagTab.Controls.Add(bagPanel);tabs.TabPages.Add(bagTab);BuildInspectionTab();panel.Controls.Add(tabs,0,2);
            counter.AutoSize=true;counter.Margin=new Padding(0,8,0,8);panel.Controls.Add(counter,0,3);
            var footer=new FlowLayoutPanel {AutoSize=true,Dock=DockStyle.Fill};save.Text="Salvar resultado para o Work";save.AutoSize=true;footer.Controls.Add(save);footer.Controls.Add(toggle);
            showConnection.Text="Dados da conexao";showConnection.AutoSize=true;showConnection.Visible=false;showConnection.Name="dadosConexao";
            showConnection.Click+=(s,e)=>{connectionPanel.Visible=!connectionPanel.Visible;};footer.Controls.Add(showConnection);
            footer.Controls.Add(new Label {AutoSize=true,Text="Eventos e sacola final de testes. Separacao ainda externa.",Margin=new Padding(8,8,0,0)});panel.Controls.Add(footer,0,4);
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
            FormClosed += (s,e)=>{timer.Dispose();if(bagProcessor!=null)bagProcessor.Dispose();if(bagStore!=null)bagStore.Dispose();if(events!=null)events.Dispose();if(auth!=null)auth.Dispose();if(journal!=null)journal.Dispose();if(operation!=null)operation.Dispose();secret.Clear();if(temporaryInterfaceFolder!=null){try{Directory.Delete(temporaryInterfaceFolder,true);}catch{}}};
            Shown+=(s,e)=>{
                var area=Screen.FromControl(this).WorkingArea;
                MinimumSize=new Size(Math.Min(MinimumSize.Width,area.Width-16),Math.Min(MinimumSize.Height,area.Height-16));
                Size=new Size(Math.Min(Width,area.Width-16),Math.Min(Height,area.Height-16));
                Location=new Point(area.Left+(area.Width-Width)/2,area.Top+(area.Height-Height)/2);
                if(interfaceCheck)BeginInvoke(new Action(RunInterfaceCheck));
            };
            Resize+=(s,e)=>{status.MaximumSize=new Size(Math.Max(300,ClientSize.Width-50),0);bagTotals.MaximumSize=new Size(Math.Max(300,ClientSize.Width-70),0);};
            if(interfaceCheck)InitializeInterfaceCheck();
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
                journal=new EventJournal(folder,merchant);bagStore=new BagStore(folder,merchant);selections=new ScenarioSelectionStore(folder,merchant);
                auth=new AuthenticationClient(id.Text.Trim(),secret.Text.Trim(),Log,null,null,AuthenticationMode.Centralized);
                secret.Clear();id.ReadOnly=true;secret.Enabled=false;testApp.Enabled=false;
                status.Text="Conectando a sessao para receber pedidos...";
                await auth.ConnectCentralizedAsync(operation.Token);authenticated=true;
                await auth.VerifyMerchantAsync(merchant,operation.Token);merchantVerified=true;
                events=new EventsClient(merchant,auth.GetAccessTokenAsync,Log);receiver=new EventReceiver(events,journal);
                bagProcessor=new BagProcessor(journal,bagStore,auth.GetAccessTokenAsync,Log);
                connectionPanel.Visible=false;showConnection.Visible=true;running=true;toggle.Enabled=true;timer.Start();status.Text="Conectado. Iniciando recebimento a cada 30 segundos.";RefreshGrid();
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
                int imported=0;
                if(result.Polled){polls++;received+=result.Received;duplicate+=result.Received-result.Added;imported=await bagProcessor.ProcessAsync(operation.Token);}
                if(imported>0)result.Message+=" Sacola final validada e gravada na aba de conferencia.";
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
        private void SetBusy(bool value){busy=value;save.Enabled=!value;inspect.Enabled=!value && bagPicker.SelectedItem!=null;summary.Enabled=!value && bagPicker.SelectedItem!=null;UseWaitCursor=value;}
        private void FinishOperation()
        {
            SetBusy(false);if(operation!=null){operation.Dispose();operation=null;}
            if(closing)BeginInvoke(new Action(Close));
        }
        private void RefreshGrid()
        {
            grid.Rows.Clear();var rows=journal==null?new List<ReceivedEvent>():journal.Snapshot();
            foreach(var row in rows.Reverse().Take(2000))grid.Rows.Add(DateTimeOffset.Parse(row.CreatedAt).ToLocalTime().ToString("dd/MM HH:mm:ss"),row.OrderId,
                row.FullCode+(row.AwaitingFinalBag?(row.BusinessProcessed?" (sacola gravada)":" (sacola pendente)"):""),row.AckAccepted?"Gravado / ACK aceito":"Gravado / ACK pendente");
            RefreshBags();
            counter.Text="Pedidos no historico: "+rows.Select(r=>r.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count()+" | Eventos: "+rows.Count+" | ACK pendentes: "+rows.Count(r=>!r.AckAccepted)+" | Consultas nesta sessao: "+polls;
        }
        private void RefreshBags()
        {
            string selected=bagPicker.SelectedItem as string;
            var bags=bagStore==null?new List<BagOrder>():bagStore.Snapshot();refreshingBags=true;
            try{
                bagPicker.Items.Clear();reviewOrder.Items.Clear();
                foreach(var bag in bags){string key=bag.Id.ToString("D");bagPicker.Items.Add(key);reviewOrder.Items.Add(key);}
                if(selected!=null && bagPicker.Items.Contains(selected))bagPicker.SelectedItem=selected;
                else if(bagPicker.Items.Count>0)bagPicker.SelectedIndex=bagPicker.Items.Count-1;
                reviewOrder.SelectedItem=bagPicker.SelectedItem;
            }finally{refreshingBags=false;}
            ShowBag();
        }
        private void ShowBag()
        {
            if(refreshingBags)return;
            bagGrid.Rows.Clear();if(bagStore==null || bagPicker.SelectedItem==null){ShowInspection(null);return;}
            var bag=bagStore.Snapshot().Single(b=>b.Id.ToString("D")==bagPicker.SelectedItem.ToString());
            if(!Equals(reviewOrder.SelectedItem,bagPicker.SelectedItem))reviewOrder.SelectedItem=bagPicker.SelectedItem;
            ShowInspection(bag);
            foreach(var item in bag.Items){int index=bagGrid.Rows.Add(item.Name,item.ReportedQuantity,item.Unavailable?"SIM":"NAO",item.AvailableQuantity,Money(item.GrossCents));
                if(item.Unavailable)bagGrid.Rows[index].DefaultCellStyle.ForeColor=Color.Salmon;}
            bagTotals.Text="Modalidade: "+bag.Operation+" | Agenda: "+bag.Scheduling+"\nJanela: "+bag.WindowFrom+" / "+bag.WindowTo+
                "\nTotal da sacola: "+Money(bag.BagGrossCents)+" | Pagamento informado: "+Money(bag.PaymentCents)+
                "\nBeneficios informados: "+bag.Benefits.Length+" | "+string.Join("; ",bag.Benefits.Take(4).Select(b=>b.Target+" / "+b.Liability+": "+Money(b.Cents)).ToArray())+
                (bag.Benefits.Length>4?" (demais no relatorio)":"")+
                "\nBeneficio nao comprova cupom. Linha indisponivel mantem dados informados; quantidade disponivel e zero. Sem baixa de estoque ou financeiro.";
        }
        private static string Money(decimal cents){return (cents/100m).ToString("C2",System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));}
        private void SaveReport()
        {
            if(busy)return;
            using(var dialog=new SaveFileDialog {Filter="Resultado (*.txt)|*.txt",FileName="RESULTADO_IFOOD_CONFERENCIA_R7.txt",InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                try{File.WriteAllText(dialog.FileName,Report(),new UTF8Encoding(true));status.Text="Resultado salvo. Envie RESULTADO_IFOOD_CONFERENCIA_R7.txt ao Work.";}
                catch{status.Text="Nao foi possivel salvar. Escolha uma pasta com permissao.";}
            }
        }
        private string Report()
        {
            var text=new StringBuilder("LEAL INFO - IFOOD CONFERENCIA R7\r\n");
            text.AppendLine("Origem: "+(interfaceCheck?"SIMULACAO LOCAL - SEM API, SEM HOMOLOGACAO":"CONEXAO DE TESTES / CONFERENCIA LOCAL"));
            text.AppendLine("UTC: "+DateTimeOffset.UtcNow.ToString("O"));text.AppendLine("Loja de testes: "+merchant);
            text.AppendLine("Autenticacao nesta sessao: "+(authenticated?"CONFIRMADA":"NAO CONFIRMADA"));
            text.AppendLine("Acesso a loja nesta sessao: "+(merchantVerified?"CONFIRMADO":"NAO CONFIRMADO"));
            text.AppendLine("Consultas de eventos bem-sucedidas nesta sessao: "+polls);
            text.AppendLine("Eventos em ciclos concluidos nesta sessao: "+received+"; repetidos: "+duplicate);
            text.AppendLine("Ultima falha fatal: "+safeError);text.AppendLine("Recepcao ativa: "+running);
            var rows=journal==null?new List<ReceivedEvent>():journal.Snapshot();
            text.AppendLine("Eventos tecnicos persistidos: "+rows.Count+"; ACK pendentes: "+rows.Count(r=>!r.AckAccepted));
            text.AppendLine("Historico pode incluir sessoes anteriores. HTTP 204 confirma comunicacao, nao recebimento de pedido.");
            text.AppendLine("ACK confirma apenas transporte; NAO confirma o pedido. A sacola final e gravada somente em RFI, em pasta isolada, sem efeitos no PDV.");
            text.AppendLine("Sacolas finais persistidas: "+(bagStore==null?0:bagStore.Snapshot().Count));
            text.AppendLine("SACOLAS (IDs, quantidades e valores tecnicos; sem dados do consumidor):");
            if(bagStore!=null)foreach(var bag in bagStore.Snapshot()){
                text.AppendLine("Pedido "+bag.Id+" | "+bag.Operation+" | "+bag.Scheduling+" | janela "+bag.WindowFrom+" / "+bag.WindowTo+" | total centavos "+bag.BagGrossCents+" | pagamento centavos "+bag.PaymentCents);
                foreach(var item in bag.Items)text.AppendLine("Item "+item.Id+" | quantidade informada "+item.ReportedQuantity+" | unavailable "+item.Unavailable+" | disponivel "+item.AvailableQuantity+" | valor informado centavos "+item.GrossCents);
                foreach(var benefit in bag.Benefits)text.AppendLine("Beneficio "+benefit.Target+" | "+benefit.Liability+" | centavos "+benefit.Cents);
            }
            text.AppendLine("Beneficio nao comprova cupom; unavailable nao comprova solicitacao de cancelamento. Evidencias reais permanecem pendentes.");
            text.AppendLine("Videos, logs oficiais e homologacao permanecem pendentes.");
            text.AppendLine("EVENTOS (IDs tecnicos; sem cliente, endereco, itens ou credenciais):");
            foreach(var row in rows)text.AppendLine(row.CreatedAt+" | pedido "+row.OrderId+" | evento "+row.Id+" | "+row.Code+" / "+row.FullCode+" | ACK "+row.AckAccepted+" | sacola persistida "+row.BusinessProcessed);
            text.AppendLine("HTTP (metadados; sem corpos, cabecalhos, chaves ou tokens):");
            lock(logs){foreach(var row in logs)text.AppendLine(row.AtUtc.ToString("O")+" | "+row.Method+" | "+row.Route+" | HTTP "+(row.Status.HasValue?row.Status.Value.ToString():"sem resposta")+" | "+row.ElapsedMs+" ms | "+row.Outcome);text.AppendLine("Registros antigos omitidos por limite: "+skippedLogs);}
            return text.ToString();
        }
        [STAThread] public static void Run(bool localInterfaceCheck=false){Application.EnableVisualStyles();Application.Run(new AssistantForm(localInterfaceCheck));}
    }
}
