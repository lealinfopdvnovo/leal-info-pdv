using LealInfoPDV.Network;
namespace LealInfoPDV.Licensing;
internal sealed class ActivationForm : Form
{
    internal ActivationForm()
    {
        Text="Ativação • LEAL INFO PDV"; StartPosition=FormStartPosition.CenterScreen;
        ClientSize=new Size(680,550); BackColor=Color.FromArgb(18,20,25); ForeColor=Color.White;
        Font=new Font("Segoe UI",11); AutoScaleMode=AutoScaleMode.Dpi;
        var layout=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(28),ColumnCount=1,RowCount=7 };
        foreach(var h in new[]{60,75,50,100,65,45,70})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));Controls.Add(layout);
        layout.Controls.Add(new Label {Text="ATIVAÇÃO DA LICENÇA",Dock=DockStyle.Fill,ForeColor=Color.Gold,Font=new Font("Segoe UI",20,FontStyle.Bold)},0,0);
        layout.Controls.Add(new Label {Text="Envie o serial deste computador ao vendedor.\nCole a chave recebida ou importe o arquivo de licença.",Dock=DockStyle.Fill},0,1);
        var serial=new TextBox {Text=Database.DeviceSerial(),ReadOnly=true,Dock=DockStyle.Fill};layout.Controls.Add(serial,0,2);
        var key=new TextBox {Multiline=true,Dock=DockStyle.Fill,PlaceholderText="Cole a chave de ativação recebida"};layout.Controls.Add(key,0,3);
        var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill};layout.Controls.Add(buttons,0,4);
        var status=new Label {Text="Os dados existentes da empresa serão preservados.",Dock=DockStyle.Fill,ForeColor=Color.Gold};layout.Controls.Add(status,0,6);
        void Activate(string text)
        {
            try {
                var json=text.Trim();
                if(json.StartsWith("LEAL1-"))json=System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(json[6..]));
                if(InstallationLicense.Store==null)InstallationLicense.LoadLocal();
                InstallationLicense.Store!.Import(json);InstallationLicense.RefreshLocal();
                if(!InstallationLicense.IsActivated(InstallationLicense.Current!))throw new InvalidDataException("Solicite ao vendedor uma licença com o código do cliente e o plano contratado.");
                DialogResult=DialogResult.OK;Close();
            } catch(Exception ex){status.Text=ex.Message;}
        }
        var import=new Button {Text="IMPORTAR LICENÇA",Width=235,Height=44};import.Click+=(_,_)=>{using var f=new OpenFileDialog {Filter="Licença LEAL INFO|*.leallicenca"};if(f.ShowDialog(this)==DialogResult.OK)Activate(File.ReadAllText(f.FileName));};buttons.Controls.Add(import);
        var activate=new Button {Text="ATIVAR CHAVE",Width=225,Height=44,BackColor=Color.Gold};activate.Click+=(_,_)=>Activate(key.Text);buttons.Controls.Add(activate);AcceptButton=activate;
        var terminal=new Button {Text="CONFIGURAR COMO TERMINAL",Dock=DockStyle.Fill};layout.Controls.Add(terminal,0,5);
        terminal.Click+=(_,_)=>{
            try {
                using(var settings=new NetworkSettingsForm())settings.ShowDialog(this);
                NetworkConfiguration.Load();
                if(NetworkConfiguration.Current.Mode=="terminal") { Database.Initialize();DialogResult=DialogResult.OK;Close(); }
            }catch(Exception ex){status.Text=ex.Message;}
        };
    }
}
