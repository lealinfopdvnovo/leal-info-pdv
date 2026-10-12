using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Drawing;
using LealInfoPDV.Licensing;
using LealInfoPDV.Network;
internal static class LicenseFieldTest
{
    [STAThread] internal static void Run(bool smoke = false)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        var identity=MachineLicenseIdentity.Current;
        var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LealInfoPDV_LICENCIAMENTO_TESTE");
        Directory.CreateDirectory(root);
        var production=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LealInfoPDV");
        var path=Path.Combine(root,"network.license");
        var log=Path.Combine(root,"TESTE_WINDOWS.txt");
        void Log(string value)=>File.AppendAllText(log,DateTimeOffset.Now.ToString("o")+" "+value+Environment.NewLine);
        // Cópia inicial somente de arquivos de licenciamento; produção é exclusivamente lida.
        if(!File.Exists(path) && File.Exists(Path.Combine(production,"network.license"))) {
            foreach(var name in new[]{"network.license","network.license.signed","network.license.migration"})
                if(File.Exists(Path.Combine(production,name)))File.Copy(Path.Combine(production,name),Path.Combine(root,name),false);
        }
        using var form=new Form{Text="TESTE ISOLADO — LICENCIAMENTO LEAL INFO PDV",ClientSize=new Size(730,500),MinimumSize=new Size(620,450),StartPosition=FormStartPosition.CenterScreen,AutoScaleMode=AutoScaleMode.Dpi};
        var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(16)};form.Controls.Add(panel);
        panel.Controls.Add(new Label{Text="SOMENTE TESTE DE LICENCIAMENTO. Não altera banco nem instalação do PDV.\nSerial hardware: "+identity.Serial+"\nFeche/abra, reinicie e desligue o Windows; reabra este mesmo teste.",Width=680,Height=72});
        var status=new Label{Width=680,Height=90};panel.Controls.Add(status);
        var input=new TextBox{Multiline=true,Width=680,Height=100,ScrollBars=ScrollBars.Vertical};panel.Controls.Add(input);
        NetworkLicense? store=null;
        void Reload(){store=new NetworkLicense(path,identity.Serial,legacySerial:identity.LegacySerial);store.CheckAccess();var t=store.Terms;if(!InstallationLicense.IsActivated(t))throw new InvalidOperationException("NÃO ATIVADA — importe a licença ou autorização válida.");status.Text="ATIVADA — Cliente "+t.ClientCode+" — "+t.Plan.ToUpperInvariant()+"\n"+(store.NeedsStateRecovery?"TERMINAIS: BLOQUEADOS — checkpoint indisponível.":"Computadores cadastrados: "+store.RegisteredCount)+"\nIdentidade: "+identity.Serial;Log("ATIVADA hardware="+identity.Serial+" revisao="+t.Revision+" recuperacaoPendente="+store.NeedsStateRecovery);}
        void Action(Action action){try{action();}catch(Exception e){status.Text=e.Message;Log("BLOQUEADO tipo="+e.GetType().Name);}}
        void Apply(string text){store??=new NetworkLicense(path,identity.Serial,legacySerial:identity.LegacySerial);if(LicenseMigration.IsAuthorization(text))store.ApplyMigration(text);else store.Activate(text);Reload();}
        var buttons=new FlowLayoutPanel{Width=680,Height=105,AutoSize=false};panel.Controls.Add(buttons);
        var file=new Button{Text="IMPORTAR ARQUIVO",Width=215,Height=40};file.Click+=(_,_)=>{using var dialog=new OpenFileDialog{Filter="Licença/autorização|*.leallicenca;*.lealmigracao;*.txt"};if(dialog.ShowDialog(form)==DialogResult.OK)Action(()=>Apply(File.ReadAllText(dialog.FileName)));};buttons.Controls.Add(file);
        var paste=new Button{Text="ATIVAR TEXTO",Width=215,Height=40};paste.Click+=(_,_)=>Action(()=>Apply(input.Text));buttons.Controls.Add(paste);
        var check=new Button{Text="REABRIR / VALIDAR",Width=215,Height=40};check.Click+=(_,_)=>Action(Reload);buttons.Controls.Add(check);
        var export=new Button{Text="SOLICITAR MIGRAÇÃO",Width=215,Height=40};export.Click+=(_,_)=>{
            using var dialog=new OpenFileDialog{Filter="Licença original|*.leallicenca;*.txt"};if(dialog.ShowDialog(form)!=DialogResult.OK)return;
            Action(()=>{var request=LicenseMigration.CreateRequest(File.ReadAllText(dialog.FileName),path,identity.Serial,identity.LegacySerial,SellerPublicKey.Pem);var dest=Path.Combine(root,"SOLICITACAO_MIGRACAO.json");File.WriteAllText(dest,JsonSerializer.Serialize(request,new JsonSerializerOptions{WriteIndented=true}));status.Text="Solicitação criada. O vendedor deve verificar e assinar; não é uma licença.\n"+dest;Log("SOLICITACAO state="+request.StateAvailability);});
        };buttons.Controls.Add(export);
        var open=new Button{Text="ABRIR RELATÓRIO",Width=215,Height=40};open.Click+=(_,_)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe",root){UseShellExecute=true});buttons.Controls.Add(open);
        form.Shown+=(_,_)=>Action(Reload);
        Log("ABERTURA TESTE hardware="+identity.Serial+" hardwareBacked="+identity.HardwareBacked+" bootUtc="+DateTimeOffset.UtcNow.AddMilliseconds(-Environment.TickCount64).ToString("o"));
        using var smokeTimer=new System.Windows.Forms.Timer{Interval=600};
        if(smoke){smokeTimer.Tick+=(_,_)=>{smokeTimer.Stop();form.Close();};form.Shown+=(_,_)=>smokeTimer.Start();}
        Application.Run(form);
    }
}
