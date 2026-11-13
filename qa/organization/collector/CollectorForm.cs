using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LealInfo.Inventory;
namespace LealInfo.Collector
{
 public interface ICollectorDialogs
 {
  string ChooseFolder(Form owner);string ChooseOutput(Form owner);void Result(Form owner,string message,bool error);
 }
 public sealed class CollectorDialogs:ICollectorDialogs
 {
  public string ChooseFolder(Form owner){using(var d=new FolderBrowserDialog{Description="Selecione a pasta do LEAL INFO PDV",SelectedPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"LEAL INFO PDV")})return d.ShowDialog(owner)==DialogResult.OK?d.SelectedPath:null;}
  public string ChooseOutput(Form owner){using(var d=new SaveFileDialog{Filter="Relatório (*.json)|*.json",DefaultExt="json",AddExtension=true,FileName="LEAL_INFO_CONFERENCIA_INSTALACAO.json",CheckPathExists=true,RestoreDirectory=true})return d.ShowDialog(owner)==DialogResult.OK?d.FileName:null;}
  public void Result(Form owner,string message,bool error){MessageBox.Show(owner,message,error?"Não foi possível conferir":"Conferência",MessageBoxButtons.OK,error?MessageBoxIcon.Warning:MessageBoxIcon.Information);}
 }
 public sealed class CollectorForm:Form
 {
  readonly ICollectorDialogs dialogs;readonly Button start,cancel;readonly Label status;CancellationTokenSource operation;
  public bool Busy {get;private set;}
  public CollectorForm(ICollectorDialogs dialogs)
  {
   this.dialogs=dialogs;Text="LEAL INFO — Conferir instalação";Width=680;Height=330;StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.Dpi;
   var text=new Label{Dock=DockStyle.Top,Height=145,Padding=new Padding(18),Text="Conferência somente de leitura.\n\nSelecione a pasta do PDV e salve o relatório de versões e hashes.\nNão copia banco, licença, senhas nem conteúdo de arquivos.\nNão altera o PDV e não envia nada pela internet."};
   status=new Label{Name="status",Dock=DockStyle.Fill,Padding=new Padding(18),Text="Pronto para conferir."};
   start=new Button{Name="collect",Text="SELECIONAR PASTA E SALVAR RELATÓRIO",Dock=DockStyle.Bottom,Height=50};
   cancel=new Button{Name="cancel",Text="CANCELAR",Dock=DockStyle.Bottom,Height=35,Enabled=false};
   Controls.Add(status);Controls.Add(text);Controls.Add(cancel);Controls.Add(start);
   start.Click+=async(_,__)=>await RunAsync();cancel.Click+=(_,__)=>{if(operation!=null)operation.Cancel();};
   FormClosing+=(_,e)=>{if(Busy){if(operation!=null)operation.Cancel();e.Cancel=true;}};
  }
  async Task RunAsync()
  {
   if(Busy)return;var folder=dialogs.ChooseFolder(this);if(folder==null)return;
   Busy=true;start.Enabled=false;cancel.Enabled=true;operation=new CancellationTokenSource();status.Text="Conferindo arquivos...";
   try{
    var report=await Task.Run(()=>InstallationInventory.Collect(folder,operation.Token));operation.Token.ThrowIfCancellationRequested();
    cancel.Enabled=false;var output=dialogs.ChooseOutput(this);if(output==null){status.Text="Operação cancelada. Nenhum relatório salvo.";return;}
    InstallationInventory.SaveReport(folder,output,report,operation.Token);
    status.Text="Relatório salvo.";dialogs.Result(this,"Relatório salvo. Envie somente esse JSON para análise.",false);
   }catch(OperationCanceledException){status.Text="Operação cancelada. Nenhum relatório salvo.";}
   catch(UnauthorizedAccessException){status.Text="Sem permissão de leitura.";dialogs.Result(this,"Não foi possível ler a pasta. Escolha a pasta do PDV à qual você tem acesso; não é necessário abrir como administrador.",true);}
   catch(Exception e){status.Text="Conferência não concluída.";dialogs.Result(this,e is IOException?e.Message:"Não foi possível concluir. Selecione a pasta do PDV e tente novamente.",true);}
   finally{operation.Dispose();operation=null;Busy=false;start.Enabled=true;cancel.Enabled=false;}
  }
 }
}
