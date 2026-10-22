using System.Runtime.CompilerServices;
namespace LealInfoPDV;
internal static class GlassHostExtensions
{
 static readonly ConditionalWeakTable<MainForm,GlassHost> Hosts=new();
 static GlassHost Host(MainForm m)=>Hosts.GetValue(m,x=>new GlassHost(x));
 internal static void OpenCustomersFromGlass(this MainForm m,Action<long>? select=null)=>m.PetCustomers(select);
 internal static void OpenFinanceFromGlass(this MainForm m,GlassStoredProject saved)=>Host(m).OpenFinanceFromGlass(saved);
 internal static string? OpenAgendaFromGlass(this MainForm m,GlassStoredProject? saved)=>Host(m).OpenAgendaFromGlass(saved);
 internal static void OpenReceiptsFromGlass(this MainForm m,GlassStoredProject saved)=>Host(m).OpenReceiptsFromGlass(saved);
 internal static void OpenContractsFromGlass(this MainForm m,GlassStoredProject saved)=>Host(m).OpenContractsFromGlass(saved);
}
internal sealed class GlassHost(MainForm main)
{
 void RunAllowed(string permission,Action action)=>main.PetRunAllowed(permission,action);
 void ApplyFloatingTheme(Form f)=>main.PetTheme(f);
 Form GridForm(string title,string sql,out DataGridView grid)=>main.PetGrid(title,sql,out grid);
 Form Editor(string title,string[] labels)=>main.PetEditor(title,labels);
 Button ActionButton(string text,Action action)=>main.PetAction(text,action);
 void ReloadGrid(DataGridView grid,string sql)=>main.PetReload(grid,sql);
 static long? SelectedId(DataGridView grid)=>MainForm.PetSelected(grid);
 static bool Confirm(string text)=>MainForm.PetConfirm(text);
 static void Info(string text)=>MainForm.PetInfo(text);
 static void Exec(string sql,params (string name,object value)[] pars)=>MainForm.PetExec(sql,pars);
    internal void OpenReceiptsFromGlass(GlassStoredProject saved)=>RunAllowed("cash",()=>{using var dialog=new GlassReceiptForm(saved,ApplyFloatingTheme);dialog.ShowDialog(main);});
    internal void OpenContractsFromGlass(GlassStoredProject saved)=>RunAllowed("quotes",()=>{using var dialog=new GlassContractForm(saved,ApplyFloatingTheme);dialog.ShowDialog(main);});
    internal void OpenFinanceFromGlass(GlassStoredProject saved)=>RunAllowed("cash",()=>ShowFinance(saved));
    internal string? OpenAgendaFromGlass(GlassStoredProject? saved)
    {
        string? selected=null;
        RunAllowed("orders",()=>{using var agenda=new GlassAgendaForm(saved,ApplyFloatingTheme);if(agenda.ShowDialog(main)==DialogResult.OK)selected=agenda.SelectedProjectId;});
        return selected;
    }
    private void OpenFinance()=>ShowFinance(null);
    private void ShowFinance(GlassStoredProject? saved)
    {
        if(saved!=null)GlassOperationsStore.EnsureReady();
        string sql=saved==null?CashFlow.HistorySql:GlassOperationsStore.HistorySql(saved.Id);
        using var f=GridForm(saved==null?"FLUXO DE CAIXA":"FLUXO DE CAIXA — VIDRAÇARIA",sql,out var grid);
        Label? overview=null;
        void RefreshOverview(){if(overview!=null)overview.Text=$"PROJETO: {saved!.Data.Description} • CLIENTE: {saved.Data.Quote.CustomerName}\r\n"+GlassOperationsStore.Read(saved).Display+"\r\nOrçamento não é pagamento. NOVO registra uma operação real, após confirmação.";}
        if(saved!=null){overview=new Label{Name="GlassFinanceOverview",Dock=DockStyle.Top,Height=105,Padding=new Padding(12)};f.Controls.Add(overview);overview.BringToFront();RefreshOverview();}
        var p=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=65,Padding=new Padding(15)};
        p.Controls.Add(ActionButton("NOVO",()=>{ShowCashMovement(saved);ReloadGrid(grid,sql);RefreshOverview();}));
        p.Controls.Add(ActionButton("EXCLUIR",()=>{var id=SelectedId(grid);if(id.HasValue&&Confirm("Excluir este lançamento?")){Exec("DELETE FROM cash_movements WHERE id=$id",("$id",id.Value));ReloadGrid(grid,sql);RefreshOverview();}}));
        p.Controls.Add(ActionButton("RESUMO DO CAIXA",()=>MessageBox.Show(f,CashFlow.ReadSummary(DateTime.Today,DateTime.Today.AddDays(1)).Display,"Resumo do caixa de hoje",MessageBoxButtons.OK,MessageBoxIcon.Information)));
        p.Controls.Add(ActionButton("FECHAR",f.Close));f.Controls.Add(p);
        // Dock processes siblings back-to-front: fill must follow header, summary and actions.
        // Otherwise the summary covers the first history rows despite all records being loaded.
        if(saved!=null)grid.BringToFront();
        ApplyFloatingTheme(f);f.ShowDialog(main);
    }

    private void RegisterCashMovement()=>ShowCashMovement(null);
    private void ShowCashMovement(GlassStoredProject? saved)
    {
        GlassCashOrigin? origin=saved==null?null:new(saved.Id,saved.Revision,saved.Data.Quote.CustomerId,Guid.NewGuid().ToString("N"));
        using var f=Editor("Nova movimentação manual",new[]{"Tipo","Descrição/Motivo","Valor"});
        var fields=(List<TextBox>)f.Tag!;
        if(saved!=null)fields[1].Text=$"VIDRAÇARIA — {saved.Data.Description} (projeto {saved.Id}; cliente ID {saved.Data.Quote.CustomerId})";
        GlassMoneyInput? glassAmount=null;
        if(saved!=null)
        {
            var oldAmount=fields[2];
            glassAmount=new GlassMoneyInput{Name="GlassCashAmount",Left=oldAmount.Left,Top=oldAmount.Top,Width=oldAmount.Width,Maximum=999999999m};
            f.Controls.Remove(oldAmount);oldAmount.Dispose();f.Controls.Add(glassAmount);
        }
        var oldType=fields[0];
        var type=new ComboBox{Left=oldType.Left,Top=oldType.Top,Width=oldType.Width,DropDownStyle=ComboBoxStyle.DropDownList};
        type.Items.AddRange(new[]{"ENTRADA","SAÍDA"});type.SelectedIndex=0;f.Controls.Remove(oldType);oldType.Dispose();f.Controls.Add(type);
        var oldSave=f.Controls.OfType<Button>().Single(b=>b.Text=="SALVAR");
        var save=ActionButton("SALVAR",()=>{});save.Left=oldSave.Left;save.Top=oldSave.Top;f.Controls.Remove(oldSave);oldSave.Dispose();f.Controls.Add(save);
        save.Click+=(_,_)=>
        {
            try
            {
                var amount=glassAmount==null?CashFlow.ParseAmount(fields[2].Text):CashFlow.ParseAmount(glassAmount.Text);
                if(string.IsNullOrWhiteSpace(fields[1].Text))throw new ArgumentException("Informe a descrição/motivo.");
                var selected=type.SelectedItem!.ToString()!;
                if(MessageBox.Show(f,$"Confirmar {selected} de {amount.ToString("C2",CashFlow.Brazilian)}?","Confirmar movimentação",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
                save.Enabled=false;
                if(origin==null)CashFlow.Register(selected,fields[1].Text,amount);
                else PetCash.RegisterForGlass(selected,fields[1].Text,amount,origin);
                f.DialogResult=DialogResult.OK;f.Close();
            }
            catch(Exception ex){save.Enabled=true;MessageBox.Show(f,ex.Message,"Movimentação não registrada",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        };
        ApplyFloatingTheme(f);f.ShowDialog(main);
    }


}
