using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using LealInfoPDV.Network;

namespace LealInfoVendedor;
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--self-test")) { try { SelfTest.Run(); Environment.ExitCode=0; } catch(Exception ex) { File.WriteAllText("seller-test-error.txt",ex.ToString()); Environment.ExitCode=1; } return; }
        using var window = new SellerForm();
        if (args.Contains("--capture"))
        {
            var timer = new System.Windows.Forms.Timer { Interval=1200 };
            timer.Tick += (_,_) => { timer.Stop(); using var bitmap = new Bitmap(window.Width,window.Height); window.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height)); bitmap.Save("seller-preview.png"); timer.Dispose(); window.Close(); };
            window.Shown += (_,_) => timer.Start();
        }
        Application.Run(window);
    }
}
internal static class Issuer
{
    internal static string Issue(string serial, int computers, string mode, DateTimeOffset? expires,
        string contact, string privatePem, string? publicPem = null, string plan = "plus")
    {
        serial=serial.Trim().ToUpperInvariant();
        if (!Regex.IsMatch(serial,@"^LI-(?:[0-9A-F]{4}-){3}[0-9A-F]{4}$")) throw new ArgumentException("Copie o serial completo do PDV servidor. Exemplo: LI-1234-5678-ABCD-EF90.");
        if(plan is not ("standard" or "plus" or "pro")) throw new ArgumentException("Escolha Standard, Plus ou Pro.");
        if(computers < (plan=="standard"?1:2) || computers >1000) throw new ArgumentException("Confira a quantidade de computadores do plano, incluindo o servidor.");
        if(mode is not ("unico" or "mensal") || mode=="unico" && expires!=null || mode=="mensal" && (expires==null || expires<=DateTimeOffset.UtcNow)) throw new ArgumentException("Informe uma validade futura para a mensalidade.");
        using var key=RSA.Create(); key.ImportFromPem(privatePem);
        using var expected=RSA.Create(); expected.ImportFromPem(publicPem??SellerPublicKey.Pem);
        if(!CryptographicOperations.FixedTimeEquals(key.ExportSubjectPublicKeyInfo(),expected.ExportSubjectPublicKeyInfo())) throw new InvalidDataException("Esta chave não corresponde ao seu PDV. Use a chave original do kit do vendedor.");
        var terms = new LicenseTerms(serial,computers,mode,expires?.ToUniversalTime(),contact.Trim(),DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),Guid.NewGuid().ToString(),plan);
        var payload=JsonSerializer.SerializeToUtf8Bytes(terms);
        return JsonSerializer.Serialize(new SignedLicense(Convert.ToBase64String(payload),Convert.ToBase64String(key.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))));
    }
}
internal sealed class SellerForm : Form
{
    private readonly TextBox serial = new() { CharacterCasing=CharacterCasing.Upper };
    private readonly NumericUpDown computers = new() { Minimum=2,Maximum=1000,Value=2 };
    private readonly ComboBox plan = new() { DropDownStyle=ComboBoxStyle.DropDownList };
    private readonly ComboBox mode = new() { DropDownStyle=ComboBoxStyle.DropDownList };
    private readonly DateTimePicker expires = new() { Format=DateTimePickerFormat.Custom,CustomFormat="dd/MM/yyyy",Value=DateTime.Today.AddMonths(1),Enabled=false };
    private readonly TextBox contact = new();
    private readonly Label status = new() { Text="Preencha os dados e gere a licença para o cliente.",AutoSize=false };
    private readonly Color gold = Color.FromArgb(225,183,74);
    internal SellerForm()
    {
        Text="LEAL INFO • GERADOR DE PLANOS"; StartPosition=FormStartPosition.CenterScreen;
        ClientSize=new Size(700,680); MinimumSize=new Size(680,719); BackColor=Color.FromArgb(18,20,25);
        ForeColor=Color.White; Font=new Font("Segoe UI",11); AutoScaleMode=AutoScaleMode.Dpi;
        var layout = new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(28),ColumnCount=2,RowCount=11 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,190));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        int[] heights={66,48,54,54,54,54,54,54,52,62,66};
        foreach(var h in heights)layout.RowStyles.Add(new RowStyle(SizeType.Absolute,h)); Controls.Add(layout);
        var title=new Label { Text="LEAL INFO • PLANOS E LICENÇAS",Font=new Font("Segoe UI",19,FontStyle.Bold),ForeColor=gold,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft };
        layout.Controls.Add(title,0,0);layout.SetColumnSpan(title,2);
        var intro=new Label { Text="Emita licenças e renovações para seus clientes.",Dock=DockStyle.Fill,ForeColor=Color.LightGray };
        layout.Controls.Add(intro,0,1);layout.SetColumnSpan(intro,2);
        void Field(string text,Control control,int row)
        {
            layout.Controls.Add(new Label { Text=text,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft },0,row);
            control.Dock=DockStyle.Fill;control.Margin=new Padding(0,10,0,10);control.Font=Font;layout.Controls.Add(control,1,row);
        }
        serial.PlaceholderText="LI-1234-5678-ABCD-EF90";serial.MaxLength=22;
        plan.Items.AddRange(new object[]{"Standard","Plus","Pro"});
        plan.SelectedIndexChanged+=(_,_)=> { computers.Minimum=plan.SelectedIndex==0?1:2; computers.Value=computers.Minimum; };
        plan.SelectedIndex=1;
        Field("Plano",plan,2);Field("Serial do PDV",serial,3);Field("Computadores totais",computers,4);
        mode.Items.AddRange(new object[]{"Pagamento único","Mensalidade"});mode.SelectedIndex=0;
        mode.SelectedIndexChanged+=(_,_)=>expires.Enabled=mode.SelectedIndex==1;
        Field("Modalidade",mode,5);Field("Válida até",expires,6);
        contact.PlaceholderText="Seu WhatsApp ou telefone";Field("Contato do vendedor",contact,7);
        var note=new Label { Text="A quantidade inclui o servidor. Exemplo: 3 = servidor + 2 terminais.\nMensalidade válida até 23:59:59 da data escolhida, no horário deste PC.",Dock=DockStyle.Fill,Font=new Font("Segoe UI",9),ForeColor=Color.LightGray };
        layout.Controls.Add(note,0,8);layout.SetColumnSpan(note,2);
        var generate=new Button { Text="GERAR E SALVAR LICENÇA",Dock=DockStyle.Fill,BackColor=gold,ForeColor=Color.FromArgb(18,20,25),FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",12,FontStyle.Bold),Margin=new Padding(0,5,0,10) };
        generate.FlatAppearance.BorderSize=0;generate.Click+=(_,_)=>Generate();AcceptButton=generate;
        layout.Controls.Add(generate,0,9);layout.SetColumnSpan(generate,2);
        status.Dock=DockStyle.Fill;status.ForeColor=gold;status.Font=new Font("Segoe UI",9);
        layout.Controls.Add(status,0,10);layout.SetColumnSpan(status,2);
    }
    private void Generate()
    {
        try
        {
            var keyPath=Path.Combine(AppContext.BaseDirectory,"chave-vendedor.pem");
            if(!File.Exists(keyPath))throw new FileNotFoundException("Extraia todos os arquivos do kit na mesma pasta. A chave do vendedor precisa ficar junto do aplicativo.");
            DateTimeOffset? validity=null;
            if(mode.SelectedIndex==1) { var end=DateTime.SpecifyKind(expires.Value.Date.AddDays(1).AddSeconds(-1),DateTimeKind.Unspecified);validity=new DateTimeOffset(end,TimeZoneInfo.Local.GetUtcOffset(end)); }
            var text=Issuer.Issue(serial.Text,(int)computers.Value,mode.SelectedIndex==0?"unico":"mensal",validity,contact.Text,File.ReadAllText(keyPath),plan:new[]{"standard","plus","pro"}[plan.SelectedIndex]);
            using var dialog=new SaveFileDialog { Title="Salvar licença para o cliente",Filter="Licença LEAL INFO|*.leallicenca",DefaultExt="leallicenca",AddExtension=true,FileName="Licenca_"+plan.Text+"_"+serial.Text.Trim()+".leallicenca",InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) };
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            File.WriteAllText(dialog.FileName,text);
            status.Text="Licença salva. Envie somente o arquivo .leallicenca ao cliente.";
            MessageBox.Show(this,"Licença criada!\n\nNo PDV servidor do cliente, abra Configurações > Rede e licença > Importar licença do vendedor / Renovação.","Licença pronta",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
        catch(Exception ex) { status.Text=ex.Message;MessageBox.Show(this,ex.Message,"Confira os dados",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
    }
}
internal static class SelfTest
{
    internal static void Run()
    {
        var folder=Path.Combine(Path.GetTempPath(),"LealSellerTest-"+Guid.NewGuid());Directory.CreateDirectory(folder);
        try
        {
            using var rsa=RSA.Create(2048);var pem=rsa.ExportPkcs8PrivateKeyPem();var pub=rsa.ExportSubjectPublicKeyInfoPem();const string serial="LI-1234-5678-ABCD-EF90";
            var perpetual=Issuer.Issue(serial,3,"unico",null,"WhatsApp",pem,pub);
            var license=new NetworkLicense(Path.Combine(folder,"license"),serial,pub);license.Import(perpetual);
            if(license.Terms.ComputerLimit!=3||license.Terms.BillingMode!="unico")throw new Exception("Licença única incompatível com PDV.");
            // Outra instância permite validar a assinatura mensal sem conflito de revisões.
            var monthly=Issuer.Issue(serial,4,"mensal",DateTimeOffset.UtcNow.AddDays(30),"WhatsApp",pem,pub);
            var monthlyLicense=new NetworkLicense(Path.Combine(folder,"monthly"),serial,pub);monthlyLicense.Import(monthly);monthlyLicense.CheckAccess();
            if(monthlyLicense.Terms.BillingMode!="mensal" || monthlyLicense.Terms.Plan!="plus")throw new Exception("Licença mensal incompatível com PDV.");
            foreach(var tier in new[]{"standard","plus","pro"}) {
                var issued=Issuer.Issue(serial,tier=="standard"?1:2,"unico",null,"",pem,pub,tier);
                var check=new NetworkLicense(Path.Combine(folder,tier),serial,pub);check.Import(issued);
                if(check.Terms.Plan!=tier)throw new Exception("Plano perdido na assinatura.");
            }
            void Reject(Action action) {try{action();}catch(ArgumentException){return;}catch(InvalidDataException){return;}throw new Exception("Dados inválidos foram aceitos.");}
            Reject(()=>Issuer.Issue("errado",3,"unico",null,"",pem,pub));Reject(()=>Issuer.Issue(serial,1,"unico",null,"",pem,pub));
            Reject(()=>Issuer.Issue(serial,3,"mensal",DateTimeOffset.UtcNow.AddDays(-1),"",pem,pub));
            using var wrong=RSA.Create(2048);Reject(()=>Issuer.Issue(serial,3,"unico",null,"",wrong.ExportPkcs8PrivateKeyPem(),pub));
            File.WriteAllText("seller-test-success.txt","PASS: pagamento único, mensalidade e assinatura aceitos pelo validador real do PDV; serial, limite, vencimento e chave incorretos rejeitados.");
        }
        finally {Directory.Delete(folder,true);}
    }
}
