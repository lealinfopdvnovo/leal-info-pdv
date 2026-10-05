using System.Reflection;
using System.Drawing.Printing;
using System.Globalization;
using LealInfoPDV;
internal static class Program
{
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] static extern int GetWindowText(IntPtr h,System.Text.StringBuilder text,int size);
    delegate bool ChildCallback(IntPtr h,IntPtr p);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr h,ChildCallback c,IntPtr p);
    static void Diagnostic(){var h=GetForegroundWindow();var b=new System.Text.StringBuilder(2048);GetWindowText(h,b,b.Capacity);Console.WriteLine("QA FOREGROUND: "+b);EnumChildWindows(h,(c,_)=>{var t=new System.Text.StringBuilder(2048);GetWindowText(c,t,t.Capacity);if(t.Length>0)Console.WriteLine("QA WINDOW: "+t);return true;},IntPtr.Zero);}
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static long Count(string table){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="SELECT COUNT(*) FROM "+table;return Convert.ToInt64(q.ExecuteScalar());}
    static string SideEffects()
    {using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="SELECT COALESCE(SUM(stock),0) FROM products";return string.Join("|",new[]{Count("cash_movements").ToString(),Count("sales").ToString(),Convert.ToString(q.ExecuteScalar(),CultureInfo.InvariantCulture)??"",Count("parts_withdrawals").ToString()});}
    static IEnumerable<Control> Children(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Children(x)));
    static T Control<T>(Form f,string name) where T:Control=>Children(f).OfType<T>().Single(c=>c.Name==name);
    static void Click(Form f,string name)=>f.BeginInvoke(new Action(()=>Control<Button>(f,name).PerformClick()));
    static void Money(TextBox box,string digits)
    {box.SelectAll();foreach(var digit in digits)typeof(Control).GetMethod("OnKeyPress",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(box,new object[]{new KeyPressEventArgs(digit)});}
    static void Snapshot(Form f,string path){using var image=new Bitmap(f.Width,f.Height);f.DrawToBitmap(image,new Rectangle(0,0,f.Width,f.Height));image.Save(path);}
    [STAThread] static void Main(string[] args)
    {
        Application.ThreadException+=(_,e)=>{Console.Error.WriteLine(e.Exception);Environment.Exit(1);};
        _=Task.Run(async()=>{await Task.Delay(TimeSpan.FromSeconds(10));Diagnostic();await Task.Delay(TimeSpan.FromSeconds(80));Environment.FailFast("Timeout da Nota de Serviço");});
        Database.Initialize();Application.EnableVisualStyles();Directory.CreateDirectory("nota-servico-qa");
        if(args.Contains("--verify-restart"))
        {
            var id=long.Parse(File.ReadAllText("service-note-id.txt"));var note=ServiceNote.Load(id);
            Check(note.TotalCents==14000&&note.Items.Count==2&&note.Status=="FINALIZADA","Reinício perdeu dados da nota");
            Check(SideEffects()==File.ReadAllText("service-side-effects.txt"),"Outro módulo alterado");Console.WriteLine("PASS: novo processo manteve nota e itens; vendas, caixa, estoque e retiradas intactos.");return;
        }
        File.WriteAllText("service-side-effects.txt",SideEffects());long customer;
        using(var db=Database.Open())using(var q=db.CreateCommand())
        {q.CommandText="INSERT INTO customers(name,document,phone,address) VALUES('QA CLIENTE NOTA','12345678000195','24988887777','Rua do Comércio, 125, Centro, Angra dos Reis - RJ'); SELECT last_insert_rowid();";customer=Convert.ToInt64(q.ExecuteScalar());}
        Check(ServiceNote.Customer(customer)["address"].Contains("125"),"Cliente sem endereço");
        foreach(var term in new[]{"QA CLIENTE NOTA","12345678000195","24988887777",customer.ToString()})Check(PartsWithdrawal.FindCustomers(term).Rows.Cast<System.Data.DataRow>().Any(r=>Convert.ToInt64(r["ID"])==customer),"Busca de cliente falhou");
        using var owner=new MainForm();var open=typeof(MainForm).GetMethod("OpenServiceNotes",BindingFlags.Instance|BindingFlags.NonPublic)!;
        Exception? failure=null;long savedId=0;var phase=0;var adding=0;var reopen=false;var started=DateTime.UtcNow;var ticks=0;
        using(var timer=new System.Windows.Forms.Timer{Interval=100})
        {
            timer.Tick+=(_,_)=>
            {
                var forms=Application.OpenForms.Cast<Form>().ToArray();var history=forms.FirstOrDefault(f=>f.Text=="NOTA DE SERVIÇO");if(history==null)return;
                if(++ticks%50==0)Console.WriteLine("QA phase="+phase+" forms="+string.Join(";",forms.Select(f=>f.Text)));
                var editor=forms.FirstOrDefault(f=>f.Text=="NOVA NOTA DE SERVIÇO"||f.Text.StartsWith("NOTA DE SERVIÇO Nº"));var search=forms.FirstOrDefault(f=>f.Text=="BUSCAR CLIENTE — NOTA DE SERVIÇO");var item=forms.FirstOrDefault(f=>f.Text=="ITEM / SERVIÇO DA NOTA");
                try
                {
                    if(DateTime.UtcNow-started>TimeSpan.FromSeconds(45))throw new Exception("Timeout fase "+phase);
                    if(phase==0){phase=1;Click(history,"newNote");return;}
                    if(phase==1&&editor!=null){phase=2;editor.BeginInvoke(new Action(()=>typeof(Control).GetMethod("OnKeyDown",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(editor,new object[]{new KeyEventArgs(Keys.F11)})));return;}
                    if(phase==2&&search!=null){Control<TextBox>(search,"noteCustomerSearch").Text="QA CLIENTE NOTA";Check(Convert.ToInt64(Control<DataGridView>(search,"noteCustomerGrid").CurrentRow!.Cells["ID"].Value)==customer,"Cliente errado");phase=3;Click(search,"selectNoteCustomer");return;}
                    if(phase==3&&editor!=null&&search==null)
                    {
                        Check(Control<TextBox>(editor,"name").Text=="QA CLIENTE NOTA"&&Control<TextBox>(editor,"document").Text=="12345678000195"&&Control<TextBox>(editor,"phone").Text=="24988887777"&&Control<TextBox>(editor,"address").Text.Contains("125"),"Autopreenchimento incompleto");
                        Control<TextBox>(editor,"equipment").Text="Notebook Dell Inspiron";Control<TextBox>(editor,"reference").Text="Portão azul";Control<TextBox>(editor,"address").Text="Rua do Comércio";Control<TextBox>(editor,"number").Text="125";Control<TextBox>(editor,"district").Text="Centro";Control<TextBox>(editor,"city").Text="Angra dos Reis";Control<TextBox>(editor,"state").Text="RJ";Control<TextBox>(editor,"zip").Text="23900-000";
                        Control<TabControl>(editor,"noteTabs").SelectedIndex=1;phase=4;Click(editor,"addNoteItem");return;
                    }
                    if(phase==4&&item!=null)
                    {
                        Control<TextBox>(item,"itemCode").Text=(adding+1).ToString("000");Control<TextBox>(item,"itemDescription").Text=new[]{"Troca de capacitor","Limpeza preventiva","Peça utilizada"}[adding];Control<TextBox>(item,"itemQuantity").Text=adding==2?"2":"1";Money(Control<TextBox>(item,"itemUnit"),new[]{"8000","5000","2500"}[adding]);
                        Check(Control<Label>(item,"itemTotal").Text.Contains(adding==0?"80,00":"50,00"),"Total do item incorreto");phase=5;Click(item,"confirmNoteItem");return;
                    }
                    if(phase==5&&editor!=null&&item==null)
                    {
                        if(++adding<3){phase=4;Click(editor,"addNoteItem");return;}
                        Check(Control<Label>(editor,"noteTotal").Text.Contains("180,00"),"Soma inicial incorreta");var grid=Control<DataGridView>(editor,"noteItems");grid.CurrentCell=grid.Rows[1].Cells[0];phase=6;Click(editor,"editNoteItem");return;
                    }
                    if(phase==6&&item!=null){Money(Control<TextBox>(item,"itemUnit"),"6000");phase=7;Click(item,"confirmNoteItem");return;}
                    if(phase==7&&editor!=null&&item==null)
                    {
                        Check(Control<Label>(editor,"noteTotal").Text.Contains("190,00"),"Edição não recalculou");var grid=Control<DataGridView>(editor,"noteItems");grid.CurrentCell=grid.Rows[2].Cells[0];Control<Button>(editor,"removeNoteItem").PerformClick();Check(Control<Label>(editor,"noteTotal").Text.Contains("140,00"),"Remoção não recalculou");
                        Control<TabControl>(editor,"noteTabs").SelectedIndex=2;Control<ComboBox>(editor,"noteStatus").SelectedItem="FINALIZADA";Control<TextBox>(editor,"warranty").Text="3 MESES";Money(Control<TextBox>(editor,"paidAmount"),"14000");Control<TextBox>(editor,"observations").Text="Teste funcional: componentes substituídos e equipamento revisado.";phase=8;Click(editor,"saveNote");return;
                    }
                    if(phase==8&&editor!=null&&Control<Label>(editor,"saveNoteResult").Text.Contains("salva"))
                    {
                        savedId=Convert.ToInt64(ServiceNote.History("QA CLIENTE NOTA").Rows[0]["ID"]);var note=ServiceNote.Load(savedId);Check(note.CustomerId==customer&&note.Items.Count==2&&note.TotalCents==14000&&note.PaidCents==14000&&note.CompletedAt.HasValue&&note.Status=="FINALIZADA","Dados salvos incorretos");Check(ServiceNote.Customer(customer)["address"].Contains("125"),"Cadastro original alterado");Snapshot(editor,"nota-servico-qa/editor-finalizacao.png");phase=9;Click(editor,"closeNoteEditor");return;
                    }
                    if(phase==9&&editor==null)
                    {Control<TextBox>(history,"noteSearch").Text=savedId.ToString("000000");Check(Control<DataGridView>(history,"noteHistory").Rows.Count==1,"Histórico não encontrou nota");Snapshot(history,"nota-servico-qa/historico.png");phase=10;history.BeginInvoke(new Action(()=>{var grid=Control<DataGridView>(history,"noteHistory");var button=Control<Button>(history,"openNote");Console.WriteLine("QA OPEN current="+grid.CurrentRow?.Cells["ID"].Value+" enabled="+button.Enabled+" visible="+button.Visible);button.PerformClick();Console.WriteLine("QA OPEN returned");}));return;}
                    if(phase==10&&editor!=null)
                    {Check(Control<TextBox>(editor,"name").Text=="QA CLIENTE NOTA"&&Control<TextBox>(editor,"warranty").Text=="3 MESES"&&Control<TextBox>(editor,"paidAmount").Text=="140,00","Reabertura perdeu valores");Control<TabControl>(editor,"noteTabs").SelectedIndex=1;Snapshot(editor,"nota-servico-qa/editor-itens.png");phase=11;Click(editor,"closeNoteEditor");return;}
                    if(phase==11&&editor==null){timer.Stop();history.Close();}
                }
                catch(Exception e){failure=e;timer.Stop();foreach(var f in forms.Reverse())if(f!=owner)f.Close();}
            };
            timer.Start();open.Invoke(owner,null);if(failure!=null)throw failure;
        }
        using(var timer=new System.Windows.Forms.Timer{Interval=100})
        {timer.Tick+=(_,_)=>{var f=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text=="NOTA DE SERVIÇO");if(f==null)return;try{Check(Control<DataGridView>(f,"noteHistory").Rows.Cast<DataGridViewRow>().Any(r=>Convert.ToInt64(r.Cells["ID"].Value)==savedId),"Tela fechada perdeu nota");reopen=true;}catch(Exception e){failure=e;}timer.Stop();f.Close();};timer.Start();open.Invoke(owner,null);if(failure!=null)throw failure;}Check(reopen,"Reabertura não validada");
        File.WriteAllText("service-note-id.txt",savedId.ToString());
        void Setting(string key,string value){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="INSERT INTO settings(key,value) VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value";q.Parameters.AddWithValue("$key",key);q.Parameters.AddWithValue("$value",value);q.ExecuteNonQuery();}
        Setting("company_name","QA EMPRESA DE SERVIÇOS LTDA");Setting("company_trade_name","QA ASSISTÊNCIA TÉCNICA");Setting("company_document","00.000.000/0001-00");Setting("company_phone","(24) 3000-0000");Setting("company_address","Rua da Empresa, 225, Centro");Setting("company_city_state","Angra dos Reis / RJ");Setting("company_activity","Instalação e manutenção • Dados fictícios de teste");Setting("pix_key","CHAVE-PIX-DE-TESTE");
        using(var logo=new Bitmap(300,180)){using var g=Graphics.FromImage(logo);g.Clear(Color.White);g.FillEllipse(Brushes.SteelBlue,80,20,140,140);using var font=new Font("Arial",50,FontStyle.Bold);g.DrawString("QA",font,Brushes.White,88,55);logo.Save("nota-servico-qa/logo-qa.png");}LealInfoPDV.Licensing.CompanyBranding.SetLogo("nota-servico-qa/logo-qa.png");
        var saved=ServiceNote.Load(savedId);using(var print=new ServiceNotePrint(saved))
        {Check(print.PageCount==1&&print.HasLogo,"Nota simples / logo inválido");print.RenderPages("nota-servico-qa/simples");var text=string.Join("\n",print.PageTexts);foreach(var value in new[]{"QA ASSISTÊNCIA TÉCNICA","QA CLIENTE NOTA","Portão azul","140,00","CHAVE-PIX-DE-TESTE","3 MESES"})Check(text.Contains(value),"Impressão perdeu "+value);using var document=print.CreateDocument();Check(document.DefaultPageSettings.PaperSize.Width==827&&document.DefaultPageSettings.PaperSize.Height==1169&&!document.DefaultPageSettings.Landscape,"Papel não é A4 retrato");}
        var created=saved.CreatedAt;var notesBefore=Count("service_notes");saved.Payment="PIX";ServiceNote.Save(saved);Check(saved.Id==savedId&&ServiceNote.Load(savedId).CreatedAt==created&&Count("service_notes")==notesBefore,"Edição alterou numeração/criação");
        var qaPassword=Guid.NewGuid().ToString();var operatorId=Auth.CreateUser("QA RESPONSÁVEL NOTA","qa-nota",qaPassword,"ADMINISTRADOR","","",false);Check(Auth.Login("qa-nota",qaPassword)?.Id==operatorId,"Sessão de teste inválida");
        var many=ServiceNote.Load(savedId);many.Id=0;many.Status="ABERTA";many.CompletedAt=null;many.Items=Enumerable.Range(1,85).Select(i=>new ServiceNoteItem{Code=i.ToString("000"),Description=$"ITEM{i:000} Serviço detalhado com descrição longa para validar quebra de linha sem invadir colunas: revisão preventiva e substituição de componente em equipamento do cliente.",Quantity=2,UnitCents=2500}).ToList();many.Items[40].Description+=" "+new string('X',2000);many.Observations="Nota com muitos itens. Paginação de teste.";var second=ServiceNote.Save(many);Check(second>savedId,"Numeração não sequencial");
        Check(ServiceNote.Load(second).UserId==operatorId&&ServiceNote.Load(second).Operator=="QA RESPONSÁVEL NOTA","Responsável da nota não foi registrado");Auth.Logout();
        using(var print=new ServiceNotePrint(ServiceNote.Load(second)))
        {Check(print.PageCount>1,"Não paginou");print.RenderPages("nota-servico-qa/muitas-paginas");var text=string.Join("\n",print.PageTexts);foreach(var i in Enumerable.Range(1,85))Check(text.Contains($"ITEM{i:000}"),"Item cortado: "+i);Check(print.PageTexts.All(t=>t.Contains("Nº "+second.ToString("000000"))&&t.Contains("Página")),"Página sem identificação");Check(text.Replace("\n","").Count(c=>c=='X')>=2000,"Descrição longa cortada");Check(text.Contains("4.250,00"),"Total de muitas páginas incorreto");Console.WriteLine("PASS: A4 nativo com logo e dados reais do cadastro; "+print.PageCount+" páginas, 85 itens, palavra de 2000 caracteres preservada e total 4250.00.");}
        Setting("company_logo_base64","");using(var withoutLogo=new ServiceNotePrint(saved)){Check(!withoutLogo.HasLogo,"Fallback indevido no logo");withoutLogo.RenderPages("nota-servico-qa/sem-logo");}
        foreach(var term in new[]{savedId.ToString("000000"),"QA CLIENTE NOTA","12345678000195","24988887777",saved.OrderDate.ToString("dd/MM/yyyy"),"Notebook Dell"})Check(ServiceNote.History(term).Rows.Cast<System.Data.DataRow>().Any(r=>Convert.ToInt64(r["ID"])==savedId),"Pesquisa do histórico falhou: "+term);
        var precision=new ServiceNoteItem{Quantity=1.005m,UnitCents=100};Check(precision.TotalCents==101,"Arredondamento monetário incorreto");
        foreach(var text in new[]{"-1,00","abc","0,001"}){try{ServiceNote.ParseMoney(text);throw new Exception("Valor inválido aceito");}catch(ArgumentException){}}
        var prior=Count("service_notes");var invalid=ServiceNote.Load(savedId);invalid.Id=0;invalid.Items[0].Quantity=-1;try{ServiceNote.Save(invalid);throw new Exception("Quantidade negativa aceita");}catch(ArgumentException){}Check(Count("service_notes")==prior,"Nota inválida salva");
        var printers=PrinterSettings.InstalledPrinters.Cast<string>().ToArray();Console.WriteLine("QA impressoras disponíveis: "+string.Join(", ",printers));
        if(printers.Length>0)
        {
            LealInfoPDV.Licensing.CompanyBranding.SetLogo("nota-servico-qa/logo-qa.png");
            using var print=new ServiceNotePrint(saved);using var doc=print.CreateDocument();doc.PrinterSettings.PrinterName=printers[0];var preview=new PreviewPrintController();doc.PrintController=preview;doc.Print();Check(preview.GetPreviewPageInfo().Length==1,"Preview nativo inválido");
            using var dialog=new PrintPreviewDialog{Document=doc,Width=1000,Height=800};using var timer=new System.Windows.Forms.Timer{Interval=1500};timer.Tick+=(_,_)=>{timer.Stop();dialog.Close();};timer.Start();dialog.ShowDialog();Console.WriteLine("PASS: PrintDocument e PrintPreviewDialog nativos executados com driver disponível (sem impressão física).");
            using var manyPrint=new ServiceNotePrint(ServiceNote.Load(second));using var manyDocument=manyPrint.CreateDocument();manyDocument.PrinterSettings.PrinterName=printers[0];var manyPreview=new PreviewPrintController();manyDocument.PrintController=manyPreview;manyDocument.Print();Check(manyPreview.GetPreviewPageInfo().Length==manyPrint.PageCount,"Driver perdeu páginas");
            if(printers.Contains("Microsoft Print to PDF"))
            {
                using var pdfPrint=new ServiceNotePrint(saved);using var pdfDocument=pdfPrint.CreateDocument();pdfDocument.PrinterSettings.PrinterName="Microsoft Print to PDF";pdfDocument.PrintController=new StandardPrintController();pdfDocument.PrinterSettings.PrintToFile=true;var path=Path.GetFullPath("nota-servico-qa/nota-a4-driver.pdf");pdfDocument.PrinterSettings.PrintFileName=path;pdfDocument.Print();
                var deadline=DateTime.UtcNow.AddSeconds(10);while(!File.Exists(path)&&DateTime.UtcNow<deadline)Thread.Sleep(100);Check(File.Exists(path)&&new FileInfo(path).Length>1000,"Driver PDF não gerou arquivo");Console.WriteLine("PASS: impressão virtual A4 gerada pelo Microsoft Print to PDF (sem impressão física).");
            }
        }
        else Console.WriteLine("LIMITAÇÃO: runner sem driver de impressora; layout A4 nativo renderizado; diálogo/saída ao driver dependem de teste no PDV físico.");
        Check(SideEffects()==File.ReadAllText("service-side-effects.txt"),"Módulo criou efeito no caixa/estoque/vendas/retiradas");
        using(var db=Database.Open())using(var q=db.CreateCommand()){q.CommandText="SELECT customer_id FROM service_notes WHERE id=$id";q.Parameters.AddWithValue("$id",savedId);Check(Convert.ToInt64(q.ExecuteScalar())==customer,"Vínculo não usa ID real");}
        Console.WriteLine("PASS: interface, F11, busca/ID e autopreenchimento; adicionar/editar/remover, total 180 -> 190 -> 140; salvar/reabrir/histórico/reimpressão; status/finalização/garantia/pago; máscara; persistência; nenhuma venda, caixa, estoque ou retirada alterados.");
    }
}
