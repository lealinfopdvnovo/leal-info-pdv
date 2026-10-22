using System.Diagnostics;
namespace Leal.Diagnostics;
internal sealed record ProcessChoice(int? Pid,string Label){public override string ToString()=>Label;}
internal sealed class MainForm : Form
{
    readonly ComboBox processes=new(){DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
    readonly ComboBox duration=new(){DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
    readonly ComboBox stages=new(){DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
    readonly TextBox folder=new(){ReadOnly=true,Dock=DockStyle.Fill,PlaceholderText="Opcional: escolha a pasta do PDV para inventariar arquivos"};
    readonly Button start=new(){Text="INICIAR ANÁLISE",AutoSize=true};readonly Button stop=new(){Text="ENCERRAR COLETA",Enabled=false,AutoSize=true};readonly Button save=new(){Text="SALVAR RELATÓRIO",Enabled=false,AutoSize=true};readonly Button refresh=new(){Text="LOCALIZAR PDV",AutoSize=true};readonly Button browse=new(){Text="ESCOLHER PASTA",AutoSize=true};
    readonly Label state=new(){Text="Pronto para analisar. O PDV pode continuar aberto.",AutoSize=true,Dock=DockStyle.Fill};
    readonly TextBox log=new(){Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical};
    readonly Label live=new(){Text="CPU · RAM · E/S · REDE",Dock=DockStyle.Fill,Font=new Font("Segoe UI",13,FontStyle.Bold)};
    Session? session;string? sessionFolder;CancellationTokenSource? cancellation;string stage="Repouso";bool running;
    internal MainForm()
    {
        Text="LEAL INFO · DIAGNÓSTICO DO PDV 1.0";StartPosition=FormStartPosition.CenterScreen;Size=new Size(960,760);MinimumSize=new Size(760,620);BackColor=Color.FromArgb(10,15,22);ForeColor=Color.White;Font=new Font("Segoe UI",10);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=1,RowCount=8};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        foreach(var h in new[]{72,75,48,85,62,62})root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        root.Controls.Add(new Label{Text="LEAL INFO  |  DIAGNÓSTICO DO PDV\nMedição local para otimizar desempenho e preservar o visual",Dock=DockStyle.Fill,ForeColor=Color.FromArgb(245,205,105),Font=new Font("Segoe UI",15,FontStyle.Bold)},0,0);
        var selection=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=2};selection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));selection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,170));selection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,160));selection.Controls.Add(new Label{Text="Processo a acompanhar",AutoSize=true},0,0);selection.Controls.Add(new Label{Text="Tempo (segundos)",AutoSize=true},2,0);selection.Controls.Add(processes,0,1);selection.Controls.Add(refresh,1,1);selection.Controls.Add(duration,2,1);root.Controls.Add(selection,0,1);
        var folders=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};folders.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));folders.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,170));folders.Controls.Add(folder,0,0);folders.Controls.Add(browse,1,0);root.Controls.Add(folders,0,2);
        root.Controls.Add(new Label{Text="1. Selecione o PDV e, se desejar, sua pasta.  2. Clique em INICIAR.\n3. Use o PDV normalmente e marque a etapa abaixo.  4. Encerre e salve o relatório.\nNão executa vendas, limpeza, ajustes ou envio de dados.",Dock=DockStyle.Fill},0,3);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};actions.Controls.AddRange(new Control[]{start,stop,save});root.Controls.Add(actions,0,4);
        var marker=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3};marker.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,180));marker.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,160));marker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));var mark=new Button{Text="MARCAR ETAPA",AutoSize=true};marker.Controls.Add(stages,0,0);marker.Controls.Add(mark,1,0);marker.Controls.Add(live,2,0);root.Controls.Add(marker,0,5);root.Controls.Add(log,0,6);root.Controls.Add(state,0,7);Controls.Add(root);
        foreach(int seconds in new[]{60,180,300,600})duration.Items.Add(seconds);duration.SelectedIndex=1;stages.Items.AddRange(new object[]{"Repouso","Venda","Produtos","Cozinha","Outro"});stages.SelectedIndex=0;
        foreach(Control c in Descendants(this))if(c is Button b){b.FlatStyle=FlatStyle.Flat;b.BackColor=Color.FromArgb(29,43,56);b.ForeColor=Color.FromArgb(250,213,124);b.Padding=new Padding(8,4,8,4);}else if(c is TextBox){c.BackColor=Color.FromArgb(18,27,37);c.ForeColor=Color.White;}
        refresh.Click+=(_,_)=>RefreshProcesses();browse.Click+=(_,_)=>{using var dialog=new FolderBrowserDialog{Description="Pasta da instalação do PDV (somente leitura)",UseDescriptionForTitle=true};if(dialog.ShowDialog(this)==DialogResult.OK)folder.Text=dialog.SelectedPath;};
        start.Click+=async(_,_)=>await Run();stop.Click+=(_,_)=>cancellation?.Cancel();save.Click+=(_,_)=>Save();mark.Click+=(_,_)=>{stage=Convert.ToString(stages.SelectedItem)??"Outro";if(session!=null&&running){double at=session.Samples.LastOrDefault()?.Seconds??0;session.Stages.Add(new(at,stage));log.AppendText($"Etapa: {stage} · {at:N0} s{Environment.NewLine}");}};
        Shown+=(_,_)=>RefreshProcesses();FormClosing+=(_,e)=>{if(running){e.Cancel=true;cancellation?.Cancel();state.Text="Encerrando coleta. Aguarde para salvar ou fechar.";}};
    }
    static IEnumerable<Control> Descendants(Control root){foreach(Control c in root.Controls){yield return c;foreach(var child in Descendants(c))yield return child;}}
    void RefreshProcesses()
    {
        processes.Items.Clear();processes.Items.Add(new ProcessChoice(null,"Automático: aguardar LealInfoPDV"));
        var found=Collector.FindPdv();foreach(var p in found)using(p){processes.Items.Add(new ProcessChoice(p.Id,$"{p.ProcessName} · PID {p.Id}"));try{string? path=p.MainModule?.FileName;if(string.IsNullOrEmpty(folder.Text)&&path!=null)folder.Text=Path.GetDirectoryName(path);}catch(Exception e)when(e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException){}}
        processes.SelectedIndex=processes.Items.Count==2?1:0;
        if(found.Length==0&&string.IsNullOrEmpty(folder.Text)){var guess=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"LEAL INFO PDV");if(Directory.Exists(guess))folder.Text=guess;}
    }
    async Task Run()
    {
        if(running)return;running=true;start.Enabled=save.Enabled=refresh.Enabled=browse.Enabled=processes.Enabled=duration.Enabled=false;stop.Enabled=true;log.Clear();stage=Convert.ToString(stages.SelectedItem)??"Repouso";cancellation=new();var token=cancellation.Token;
        try
        {
            session=null;session=new(){Computer=Collector.Computer()};session.Stages.Add(new(0,stage));int seconds=(int)duration.SelectedItem!;string? selectedFolder=string.IsNullOrWhiteSpace(folder.Text)?null:folder.Text;sessionFolder=selectedFolder;var collector=new Collector((processes.SelectedItem as ProcessChoice)?.Pid);
            var inventory=Task.Run(()=>FileInventory.Scan(selectedFolder,token));var progress=new Progress<Sample>(s=>{session.Samples.Add(s);live.Text=$"PDV CPU {Units.Metric(s.PdvCpu)}\nRAM {(s.WorkingSet.HasValue?Units.Size(s.WorkingSet.Value):"indisponível")}";state.Text=$"{s.Seconds:N0} / {seconds} s · {s.Stage} · {session.Samples.Count} amostras";if(session.Samples.Count%10==0)log.AppendText($"{s.Seconds:N0}s · {s.Stage} · CPU PC {Units.Metric(s.SystemCpu)} · RAM livre {Units.Size(s.AvailableRam)}{Environment.NewLine}");});
            var watch=Stopwatch.StartNew();
            while(watch.Elapsed.TotalSeconds<seconds&&!token.IsCancellationRequested){string currentStage=stage;var sample=await Task.Run(()=>collector.Take(currentStage));((IProgress<Sample>)progress).Report(sample);try{await Task.Delay(1000,token);}catch(OperationCanceledException){break;}}
            session.Interrupted=token.IsCancellationRequested;session.Files=await Task.WhenAny(inventory,Task.Delay(22000))==inventory?await inventory:new(null,null,0,0,new(),new(),new(),0,false,"Inventário indisponível: leitura excedeu o limite de espera.");await Task.Yield();
            log.AppendText(Environment.NewLine+string.Join(Environment.NewLine,Report.Findings(session)));state.Text="Coleta encerrada. Salve o relatório HTML + JSON para análise.";save.Enabled=true;
        }
        catch(Exception e){state.Text="Não foi possível concluir: "+e.Message;if(session?.Samples.Count>0){session.Interrupted=true;save.Enabled=true;session.Limitations.Add("Coleta parcial devido a erro: "+e.GetType().Name);}}
        finally{running=false;stop.Enabled=false;start.Enabled=refresh.Enabled=browse.Enabled=processes.Enabled=duration.Enabled=true;cancellation.Dispose();cancellation=null;}
    }
    void Save()
    {
        if(session==null)return;using var dialog=new SaveFileDialog{Title="Salvar relatório fora da pasta do PDV",Filter="Relatório HTML|*.html",FileName="Diagnostico-PDV-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".html",InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try{Report.Save(session,dialog.FileName,sessionFolder);MessageBox.Show(this,"Relatório HTML e dados JSON salvos. Nenhum arquivo do PDV foi alterado.","Relatório salvo");}catch(Exception e){MessageBox.Show(this,e.Message,"Não foi possível salvar",MessageBoxButtons.OK,MessageBoxIcon.Information);}
    }
}
