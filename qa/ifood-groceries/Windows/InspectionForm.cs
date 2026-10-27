namespace LealInfo.IFood.Assistant
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Windows.Forms;
    using LealInfo.IFood.Groceries;

    public sealed partial class AssistantForm
    {
        private sealed class ProductChoice
        {
            internal Guid Id;internal string Caption;
            public override string ToString(){return Caption;}
        }
        private void BuildInspectionTab()
        {
            var page=new TabPage("Cenario 0/3/1 e evidencias") {BackColor=BackColor,ForeColor=ForeColor};
            var panel=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(8)};
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            reviewOrder.Name="pedidoConferencia";cancelChoice.Name="produtoCancelar";threeChoice.Name="produtoSepararTres";oneChoice.Name="produtoSepararUm";
            inspect.Name="conferirSelecao";summary.Name="salvarResumo";inspectionGrid.Name="criterios";
            reviewOrder.Dock=DockStyle.Top;reviewOrder.DropDownStyle=ComboBoxStyle.DropDownList;
            reviewOrder.SelectedIndexChanged+=(s,e)=>{if(!refreshingBags && reviewOrder.SelectedItem!=null && !Equals(bagPicker.SelectedItem,reviewOrder.SelectedItem))bagPicker.SelectedItem=reviewOrder.SelectedItem;};
            panel.Controls.Add(reviewOrder,0,0);
            var mapping=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,RowCount=4};
            mapping.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,205));mapping.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            var choices=new[]{cancelChoice,threeChoice,oneChoice};
            var labels=new[]{"Item 1 - solicitar cancelamento","Item 2 - separar 3 unidades","Item 3 - separar 1 unidade"};
            for(int n=0;n<3;n++)
            {
                mapping.Controls.Add(new Label {Text=labels[n],AutoSize=true,Margin=new Padding(0,6,4,4)},0,n);
                choices[n].Dock=DockStyle.Fill;choices[n].DropDownStyle=ComboBoxStyle.DropDownList;choices[n].DropDownWidth=720;
                choices[n].SelectedIndexChanged+=(s,e)=>{if(!refreshingReview)RenderInspection();};mapping.Controls.Add(choices[n],1,n);
            }
            inspect.Text="Conferir e guardar selecao";inspect.AutoSize=true;inspect.Enabled=false;
            summary.Text="Salvar resumo deste pedido";summary.AutoSize=true;summary.Enabled=false;
            var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true};buttons.Controls.Add(inspect);buttons.Controls.Add(summary);
            mapping.Controls.Add(buttons,0,3);mapping.SetColumnSpan(buttons,2);panel.Controls.Add(mapping,0,1);
            inspectionBanner.AutoSize=true;inspectionBanner.MaximumSize=new Size(900,0);inspectionBanner.Margin=new Padding(0,6,0,8);
            inspectionBanner.Text="Aguardando sacola final. Identifique os produtos por nome/ID; a posicao no array nao define a acao.";
            panel.Controls.Add(inspectionBanner,0,2);
            inspectionGrid.Dock=DockStyle.Fill;inspectionGrid.ReadOnly=true;inspectionGrid.AllowUserToAddRows=false;inspectionGrid.AllowUserToDeleteRows=false;inspectionGrid.RowHeadersVisible=false;
            inspectionGrid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;inspectionGrid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;
            inspectionGrid.BackgroundColor=grid.BackgroundColor;inspectionGrid.DefaultCellStyle=grid.DefaultCellStyle.Clone();inspectionGrid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;
            inspectionGrid.EnableHeadersVisualStyles=false;inspectionGrid.ColumnHeadersDefaultCellStyle=grid.ColumnHeadersDefaultCellStyle.Clone();
            inspectionGrid.Columns.Add("criterion","Criterio");inspectionGrid.Columns.Add("state","Resultado local");inspectionGrid.Columns.Add("detail","Detalhe / evidencia pendente");
            inspectionGrid.Columns[0].FillWeight=100;inspectionGrid.Columns[1].FillWeight=75;inspectionGrid.Columns[2].FillWeight=210;
            panel.Controls.Add(inspectionGrid,0,3);page.Controls.Add(panel);tabs.TabPages.Add(page);
            inspect.Click+=(s,e)=>{
                if(busy)return;
                try{
                    var bag=SelectedBag();if(bag==null)return;
                    var selection=CurrentSelection(bag);
                    if(selection==null){inspectionBanner.Text="Escolha tres produtos distintos. Nenhuma acao foi enviada ao iFood.";return;}
                    selections.Save(selection);RenderInspection();status.Text="Selecao guardada para este pedido. Conferencia local atualizada.";
                }catch(ReceptionFailure ex){status.Text=ex.Message;}catch{status.Text="Nao foi possivel guardar a selecao. Preserve a pasta para analise.";}
            };
            summary.Click+=(s,e)=>SaveOrderSummary();
            Resize+=(s,e)=>{inspectionBanner.MaximumSize=new Size(Math.Max(300,ClientSize.Width-70),0);};
        }
        private BagOrder SelectedBag()
        {
            if(bagStore==null || bagPicker.SelectedItem==null)return null;
            return bagStore.Snapshot().SingleOrDefault(b=>b.Id.ToString("D")==bagPicker.SelectedItem.ToString());
        }
        private static Guid ChoiceId(ComboBox combo){var choice=combo.SelectedItem as ProductChoice;return choice==null?Guid.Empty:choice.Id;}
        private ScenarioSelection CurrentSelection(BagOrder bag)
        {
            try{return ScenarioSelection.Create(bag.Id,bag.MerchantId,ChoiceId(cancelChoice),ChoiceId(threeChoice),ChoiceId(oneChoice));}
            catch(ReceptionFailure){return null;}
        }
        private void ShowInspection(BagOrder bag)
        {
            refreshingReview=true;
            try
            {
                var controls=new[]{cancelChoice,threeChoice,oneChoice};
                var previous=controls.Select(ChoiceId).ToArray();
                var plan=bag==null || selections==null?null:selections.Find(bag.Id);
                var wanted=bag!=null && displayedReviewOrder==bag.Id?previous:plan==null?new[]{Guid.Empty,Guid.Empty,Guid.Empty}:new[]{plan.CancelItem,plan.ThreeItem,plan.OneItem};
                for(int n=0;n<controls.Length;n++)
                {
                    var combo=controls[n];combo.Items.Clear();
                    if(bag!=null)
                    {
                        foreach(var item in bag.Items)combo.Items.Add(new ProductChoice {Id=item.Id,Caption=item.Name+" | "+item.Id});
                        if(wanted[n]!=Guid.Empty && !bag.Items.Any(i=>i.Id==wanted[n]))combo.Items.Add(new ProductChoice {Id=wanted[n],Caption="PRODUTO PREVISTO AUSENTE | "+wanted[n]});
                        combo.SelectedItem=combo.Items.Cast<ProductChoice>().SingleOrDefault(c=>c.Id==wanted[n]);
                    }
                    combo.Enabled=bag!=null;
                }
                displayedReviewOrder=bag==null?Guid.Empty:bag.Id;inspect.Enabled=bag!=null && !busy;summary.Enabled=bag!=null && !busy;
            }finally{refreshingReview=false;}
            RenderInspection();
        }
        private void RenderInspection()
        {
            var bag=SelectedBag();inspectionGrid.Rows.Clear();currentInspection=null;
            if(bag==null){inspectionBanner.Text="Aguardando sacola final e identificacao dos tres produtos.";return;}
            currentInspection=ScenarioInspection.Inspect(bag,CurrentSelection(bag));
            foreach(var row in currentInspection.Criteria)
            {
                int index=inspectionGrid.Rows.Add(row.Title,row.StatusText,row.Detail);
                inspectionGrid.Rows[index].DefaultCellStyle.ForeColor=row.State==InspectionState.Pass?Color.LightGreen:row.State==InspectionState.Fail?Color.Salmon:row.State==InspectionState.Pending?Color.FromArgb(224,191,113):Color.LightGray;
            }
            inspectionBanner.Text=(interfaceCheck?"SIMULACAO LOCAL - SEM API. ":"")+
                (currentInspection.QuantitiesMatch?"Quantidades 0/3/1 aprovadas na conferencia local. ":"Quantidades pendentes ou divergentes. ")+
                "Pedido original, cupom, solicitacao, video e logs continuam exigindo evidencia real. Nenhuma acao de separacao e enviada.";
        }
        private void SaveOrderSummary()
        {
            if(busy)return;var bag=SelectedBag();if(bag==null)return;
            using(var dialog=new SaveFileDialog {Filter="Resumo do pedido (*.txt)|*.txt",FileName="RESUMO_IFOOD_"+bag.Id.ToString("N").Substring(0,8)+".txt",InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                try{
                    var selection=CurrentSelection(bag);if(selection!=null)selections.Save(selection);
                    File.WriteAllText(dialog.FileName,ScenarioInspection.Inspect(bag,selection).Summary(bag,selection,interfaceCheck),new UTF8Encoding(true));
                    status.Text="Resumo local deste pedido salvo. Evidencias pendentes permanecem identificadas.";
                }catch{status.Text="Nao foi possivel salvar o resumo deste pedido. Escolha uma pasta com permissao.";}
            }
        }
        private void InitializeInterfaceCheck()
        {
            // This path creates no AuthenticationClient/EventsClient/BagProcessor and has no API calls.
            temporaryInterfaceFolder=Path.Combine(Path.GetTempPath(),"LEAL_INFO_IFOOD_INTERFACE_R7_"+Guid.NewGuid().ToString("N"));
            folder=temporaryInterfaceFolder;journal=new EventJournal(folder,merchant);bagStore=new BagStore(folder,merchant);selections=new ScenarioSelectionStore(folder,merchant);
            var orderId=new Guid("11111111-1111-4111-8111-111111111111");var eventId=new Guid("22222222-2222-4222-8222-222222222222");
            var productIds=new[]{new Guid("33333333-3333-4333-8333-333333333331"),new Guid("33333333-3333-4333-8333-333333333332"),new Guid("33333333-3333-4333-8333-333333333333")};
            var bag=new BagOrder {Id=orderId,MerchantId=merchant,Operation="DELIVERY",Scheduling="IMMEDIATE",BagGrossCents=4000,PaymentCents=3500,
                Items=productIds.Select((product,n)=>new BagItem {Id=product,Name="SIMULADO - Produto "+(n+1),ReportedQuantity=n==2?1:3,Unavailable=n==0,UnitCents=1000,GrossCents=n==2?1000:3000}).ToArray(),
                Benefits=new[]{new BagBenefit {Target="CART",Liability="OWN",Cents=500}}};
            journal.Stage(new[]{new ReceivedEvent {Id=eventId.ToString("D"),OrderId=orderId.ToString("D"),MerchantId=merchant.ToString("D"),Code="RFI",FullCode="READY_FOR_INVOICE",CreatedAt=DateTimeOffset.UtcNow.ToString("O")}});
            bagStore.Save(eventId,bag);journal.MarkBagPersisted(eventId.ToString("D"));
            selections.Save(ScenarioSelection.Create(orderId,merchant,productIds[0],productIds[1],productIds[2]));
            id.Clear();secret.Clear();id.Enabled=false;secret.Enabled=false;testApp.Enabled=false;connect.Enabled=false;toggle.Enabled=false;
            connectionPanel.Visible=false;showConnection.Visible=true;
            status.Text="SIMULACAO LOCAL DA INTERFACE - sem login, sem rede e sem pedido real.";
        }
        private void RunInterfaceCheck()
        {
            var report=new StringBuilder("LEAL INFO - VERIFICACAO DA INTERFACE R7\r\nSIMULACAO LOCAL - SEM API - NAO E HOMOLOGACAO\r\n");int pass=0,fail=0;
            Action<bool,string> check=(ok,name)=>{report.AppendLine((ok?"PASS":"FAIL")+" | "+name);if(ok)pass++;else fail++;};
            try
            {
                tabs.SelectedIndex=2;PerformLayout();tabs.PerformLayout();inspectionGrid.Refresh();
                check(interfaceCheck && auth==null && events==null && bagProcessor==null && !authenticated && !running,"Modo local sem clientes de rede ou autenticacao");
                check(!connect.Enabled && !id.Enabled && !secret.Enabled,"Conexao e entrada de credenciais desabilitadas");
                showConnection.PerformClick();check(connectionPanel.Visible && !id.Enabled && !secret.Enabled,"Painel de conexao expande sem habilitar credenciais no modo local");
                showConnection.PerformClick();PerformLayout();check(!connectionPanel.Visible,"Painel recolhido libera area para conferencia, como apos conectar");
                check(bagStore.Snapshot().Count==1 && inspectionGrid.Rows.Count==10,"Pedido ficticio e dez criterios renderizados");
                check(currentInspection!=null && currentInspection.QuantitiesMatch,"Cenario ficticio 0/3/1 aprovado localmente");
                check(currentInspection.Criteria.Count(c=>c.State==InspectionState.Pending)==5,"Cinco evidencias externas permanecem pendentes");
                var original=threeChoice.SelectedItem;
                threeChoice.SelectedItem=threeChoice.Items.Cast<ProductChoice>().Single(c=>c.Id==ChoiceId(cancelChoice));
                check(!currentInspection.QuantitiesMatch,"Selecao repetida nao gera aprovacao");
                threeChoice.SelectedItem=original;
                var originalCancel=cancelChoice.SelectedItem;
                Guid cancelId=ChoiceId(cancelChoice),threeId=ChoiceId(threeChoice);
                cancelChoice.SelectedItem=cancelChoice.Items.Cast<ProductChoice>().Single(c=>c.Id==threeId);
                threeChoice.SelectedItem=threeChoice.Items.Cast<ProductChoice>().Single(c=>c.Id==cancelId);
                inspect.PerformClick();
                check(selections.Find(SelectedBag().Id).CancelItem==threeId && !currentInspection.QuantitiesMatch,"Botao guarda alteracao por pedido e sinaliza divergencia");
                cancelChoice.SelectedItem=originalCancel;threeChoice.SelectedItem=original;inspect.PerformClick();
                check(selections.Find(SelectedBag().Id).CancelItem==cancelId && currentInspection.QuantitiesMatch,"Botao restaura plano correto e atualiza aprovacao local");
                string summaryText=currentInspection.Summary(SelectedBag(),CurrentSelection(SelectedBag()),true);
                check(summaryText.Contains("SIMULACAO LOCAL") && summaryText.Contains("PENDENTE") && !summaryText.Contains("SIMULADO - Produto"),"Resumo identifica simulacao e pendencias; usa IDs sem dados pessoais");
                foreach(var control in new Control[]{reviewOrder,cancelChoice,threeChoice,oneChoice,inspect,summary,showConnection,inspectionGrid})
                {
                    var rectangle=RectangleToClient(control.Parent.RectangleToScreen(control.Bounds));
                    check(control.Visible && control.Width>0 && control.Height>0 && ClientRectangle.Contains(rectangle),"Controle visivel dentro da janela: "+control.GetType().Name+" / "+control.Name);
                }
                using(var graphics=CreateGraphics())report.AppendLine("DPI atual: "+graphics.DpiX+" x "+graphics.DpiY);
                report.AppendLine("Resolucao/area util atual: "+Screen.FromControl(this).WorkingArea);report.AppendLine("Janela atual: "+Size+"; cliente: "+ClientSize);
                report.AppendLine("Formulario de login real, outras resolucoes/escalas e pedidos reais: NAO VALIDADOS por esta verificacao.");
            }
            catch(Exception ex){check(false,"Falha local na verificacao: "+ex.GetType().Name);}
            report.AppendLine("TOTAL: "+pass+" PASS; "+fail+" FAIL. Dados ficticios, nenhuma chamada HTTP.");
            string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"RESULTADO_IFOOD_INTERFACE_R7.txt");
            try{File.WriteAllText(path,report.ToString(),new UTF8Encoding(true));MessageBox.Show("Verificacao local concluida: "+pass+" passou; "+fail+" falhou.\nEnvie RESULTADO_IFOOD_INTERFACE_R7.txt salvo na Area de Trabalho.\nDados ficticios; sem acesso ao iFood.","LEAL INFO - Interface R7",MessageBoxButtons.OK,fail==0?MessageBoxIcon.Information:MessageBoxIcon.Warning);}
            catch{MessageBox.Show(report.ToString(),"LEAL INFO - Resultado local nao salvo",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }
    }
}
