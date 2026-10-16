using LealInfoPDV;
using System.Reflection;
using System.Globalization;
using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using System.IO.Compression;

static class Program
{
    const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static int checks;
    static void Check(bool b,string name){if(!b)throw new Exception("FAIL "+name);checks++;Console.WriteLine("PASS "+name);}
    static IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
    static T Find<T>(Control c,string name) where T:Control=>All(c).OfType<T>().Single(x=>x.Name==name);
    static object? Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Flags)!.Invoke(target,args);
    static void Pump(){Application.DoEvents();Thread.Sleep(40);Application.DoEvents();}
    static string Json(object o)=>JsonSerializer.Serialize(o);
    static GlassSavedProject Snapshot(GlassWorkshopForm f)=>(GlassSavedProject)Call(f,"CaptureSavedProject")!;
    static GlassStoredProject Save(GlassWorkshopForm f)=>(GlassStoredProject)Call(f,"SaveCurrentProject")!;
    static void Load(GlassWorkshopForm f,string id)=>Call(f,"LoadSavedProject",id);
    static void Sql(string s){using var c=Database.Open();using var q=c.CreateCommand();q.CommandText=s;q.ExecuteNonQuery();}
    static string Data()
    {
        using var c=Database.Open();using var list=c.CreateCommand();list.CommandText="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'glass_projects%' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        using var r=list.ExecuteReader();var tables=new List<string>();while(r.Read())tables.Add(r.GetString(0));r.Close();
        var text=new System.Text.StringBuilder();
        foreach(var table in tables){using var q=c.CreateCommand();q.CommandText="SELECT * FROM \""+table.Replace("\"","\"\"")+"\" ORDER BY rowid";using var rows=q.ExecuteReader();while(rows.Read()){text.Append(table);for(int i=0;i<rows.FieldCount;i++)text.Append(Json(rows.IsDBNull(i)?null!:rows.GetValue(i)));}}
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.ToString())));
    }
    static void Child(string command,string id)
    {
        var p=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        p.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);p.ArgumentList.Add(command);p.ArgumentList.Add(id);
        using var process=Process.Start(p)!;var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        Check(process.WaitForExit(60000),"Processo separado terminou");
        Console.Write(output.GetAwaiter().GetResult());Console.Write(error.GetAwaiter().GetResult());Check(process.ExitCode==0,"Reinicio real de processo "+command);
    }
    [STAThread]static void Main(string[] args)
    {
        Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","Somente Windows descartável");
        CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("pt-BR");
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();
        Database.Initialize();
        if(args.Length>0)
        {
            if(args[0]=="--verify-process") CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("en-US");
            using var reopened=new GlassWorkshopForm();reopened.Show();Pump();Load(reopened,args[1]);
            var snapshot=Snapshot(reopened);
            Check(snapshot.WidthMm==1200 && snapshot.WidthUnit=="m" && snapshot.HeightMm==1900 && snapshot.HeightUnit=="cm","Dimensão física e representação após novo processo");
            Check(snapshot.Cut!.Pieces[0].WidthMm==800 && snapshot.Cut.Pieces[0].WidthUnit=="cm","Peça e unidade após novo processo");
            Check(snapshot.Cut.Pieces[1].HeightMm==1500 && snapshot.Cut.Pieces[1].HeightUnit=="m","Outra peça independente");
            if(args[0]=="--edit-process")
            {
                Find<TextBox>(reopened,"Description").Text="PROJETO A EDITADO";
                Find<NumericUpDown>(reopened,"QuotePrice").Value=72;
                Call(reopened,"EnsureCutPlan");
                var cut=(GlassCutPlanForm)typeof(GlassWorkshopForm).GetField("cutPlan",Flags)!.GetValue(reopened)!;
                cut.Show(reopened);Pump();
                var grid=Find<DataGridView>(cut,"CutPieces");
                ((GlassCutPlanForm.PieceRow)grid.Rows[0].DataBoundItem).Description="PEÇA ALTERADA";
                Check(cut.Recalculate(),"Recálculo depois de edição");
                Save(reopened);
            }
            else {Check(snapshot.Description=="PROJETO A EDITADO" && snapshot.Quote.Price==72 && snapshot.Cut.Pieces[0].Description=="PEÇA ALTERADA","Última gravação recuperada em outro processo");}
            reopened.Close();return;
        }
        Directory.CreateDirectory("glass-persistence-evidencias");
        Sql("INSERT INTO customers(id,name) VALUES(998,'CLIENTE FICTICIO PERSISTENCIA'); INSERT INTO products(id,name,price,stock,active) VALUES(998,'MATERIAL FICTICIO PERSISTENCIA',20,13,1)");
        string before=Data();
        GlassStoredProject a;
        using(var f=new GlassWorkshopForm())
        {
            f.Show();Pump();
            Find<TextBox>(f,"Description").Text="PROJETO A";
            Find<NumericUpDown>(f,"WidthMm").Value=1200;Find<ComboBox>(f,"WidthMmUnit").SelectedItem="m";
            Find<NumericUpDown>(f,"HeightMm").Value=1900;Find<ComboBox>(f,"HeightMmUnit").SelectedItem="cm";
            Find<NumericUpDown>(f,"Quantity").Value=2;
            Find<Button>(f,"LoadQuoteCustomers").PerformClick();Find<ComboBox>(f,"QuoteCustomer").SelectedIndex=1;
            Find<TextBox>(f,"QuoteService").Text="SERVIÇO FICTÍCIO";
            Find<NumericUpDown>(f,"QuotePrice").Value=50;Find<NumericUpDown>(f,"QuoteServiceValue").Value=30;
            Find<NumericUpDown>(f,"QuoteDiscount").Value=10;Find<NumericUpDown>(f,"QuoteAddition").Value=7;
            var tabs=Find<TabControl>(f,"GlassSections");tabs.SelectedIndex=1;Pump();
            Find<TextBox>(f,"MaterialQuery").Text="MATERIAL FICTICIO";Find<Button>(f,"FindMaterial").PerformClick();Find<Button>(f,"LinkMaterial").PerformClick();
            Call(f,"EnsureCutPlan");var cut=(GlassCutPlanForm)typeof(GlassWorkshopForm).GetField("cutPlan",Flags)!.GetValue(f)!;
            cut.Show(f);Pump();
            Find<NumericUpDown>(cut,"SheetWidth").Value=3000;Find<ComboBox>(cut,"SheetWidthUnit").SelectedItem="m";
            Find<NumericUpDown>(cut,"SheetHeight").Value=2000;Find<ComboBox>(cut,"SheetHeightUnit").SelectedItem="cm";
            Find<Button>(cut,"CutAllowRotation").PerformClick();
            var grid=Find<DataGridView>(cut,"CutPieces");
            var first=(GlassCutPlanForm.PieceRow)grid.Rows[0].DataBoundItem;
            first.Width="800";first.WidthUnit="cm";first.Height="1000";first.HeightUnit="m";first.Quantity="2";first.Material="VIDRO FICTICIO";first.Rotation=false;
            Find<Button>(cut,"AddCutPiece").PerformClick();
            var second=(GlassCutPlanForm.PieceRow)grid.Rows[1].DataBoundItem;second.Description="PEÇA INDEPENDENTE";second.Width="600";second.Height="1500";second.HeightUnit="m";second.Quantity="1";second.Material="VIDRO FICTICIO";
            Check(cut.Recalculate(),"Packing inicial existente");decimal used=cut.Result!.UsedArea;
            cut.Hide();tabs.SelectedIndex=5;Pump();Find<Button>(f,"Toggle3DMeasurements").PerformClick();
            a=Save(f);Check(a.Data.Show3DMeasurements,"Preferência de cotas 3D incluída");
            Check(a.Revision==1 && GlassProjectStore.List().Count==1,"Primeiro salvar cria um projeto");
            Check(a.Data.Materials.Single().ProductId==998 && a.Data.Quote.CustomerId==998,"Cliente/material existentes sem duplicar cadastro");
            Check(a.Data.Cut!.Pieces[0].WidthMm==800 && a.Data.Cut.Pieces[1].HeightMm==1500,"Peças canônicas e unidades preservadas");
            Check(a.Data.Quote.Price==50 && a.Data.Quote.Discount==10 && a.Data.Quote.Addition==7,"Entradas comerciais persistidas");
            f.Close();
            using var reopened=new GlassWorkshopForm();reopened.Show();Pump();Load(reopened,a.Id);
            Check(Json(Snapshot(reopened))==Json(a.Data),"Fechar Central/reabrir restaura todos os campos");
            var restoredCut=(GlassCutPlanForm)typeof(GlassWorkshopForm).GetField("cutPlan",Flags)!.GetValue(reopened)!;
            Check(restoredCut.Result!.UsedArea==used,"Packing reconstruído equivalente");
            Check(Find<TextBox>(reopened,"QuoteSummary").Text.Contains("255,00"),"Orçamento reconstruído por fórmula existente");
            reopened.Close();
        }
        Check(Directory.GetFiles(Database.BackupFolder,"ANTES_VIDRACARIA_V1_*.db").Length==1,"Backup pré-migração único");
        using(var b=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+Directory.GetFiles(Database.BackupFolder,"ANTES_VIDRACARIA_V1_*.db").Single()))
        {b.Open();using var q=b.CreateCommand();q.CommandText="SELECT COUNT(*) FROM products WHERE id=998 AND stock=13";Check(Convert.ToInt32(q.ExecuteScalar())==1,"Backup pré-migração contém dados anteriores");}
        using(var f=new GlassWorkshopForm())
        {
            f.Show();Load(f,a.Id);var stale=Save(f);Check(stale.Id==a.Id && stale.Revision==2 && GlassProjectStore.List().Count==1,"Salvar atualiza sem duplicar");
            bool conflict=false;try{GlassProjectStore.Save(a.Data,a);}catch(InvalidOperationException){conflict=true;}Check(conflict,"Sessão antiga não sobrescreve silenciosamente");
            Call(f,"ResetSavedProject");Find<TextBox>(f,"Description").Text="PROJETO B";var b=Save(f);
            Check(b.Id!=a.Id && GlassProjectStore.List().Count==2,"Projetos independentes");
            GlassProjectStore.Delete(b.Id,b.Revision);Check(GlassProjectStore.List().Count==1 && GlassProjectStore.Load(a.Id).Data.Description=="PROJETO A","Excluir só selecionado");
            f.Close();
        }
        Child("--edit-process",a.Id);Child("--verify-process",a.Id);
        Check(GlassProjectStore.List().Count==1,"Processos não duplicam projeto");
        Check(Data()==before,"Todas as tabelas operacionais intactas");
        string taskName="CreateZipAsync";
        string zip=Task.Run(async () => await (Task<string>)typeof(DatabaseBackupService).GetMethod(taskName,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{CancellationToken.None})!).GetAwaiter().GetResult();
        using(var archive=ZipFile.OpenRead(zip)){archive.GetEntry("lealinfo.db")!.ExtractToFile("glass-persistence-evidencias/backup.db",true);}
        using(var b=new Microsoft.Data.Sqlite.SqliteConnection("Data Source=glass-persistence-evidencias/backup.db"))
        {b.Open();using var q=b.CreateCommand();q.CommandText="SELECT COUNT(*) FROM glass_projects_v1 WHERE id='"+a.Id+"'";Check(Convert.ToInt32(q.ExecuteScalar())==1,"Backup normal inclui projeto");}
        using(var db=Database.Open()){using var q=db.CreateCommand();q.CommandText="PRAGMA integrity_check";Check(Convert.ToString(q.ExecuteScalar())=="ok","Integridade SQLite");}
        var final=GlassProjectStore.Load(a.Id);
        var invalid=final.Data with {WidthUnit="km"};
        bool refused=false;try{GlassProjectStore.Save(invalid,final);}catch(InvalidDataException){refused=true;}Check(refused && GlassProjectStore.Load(a.Id).Revision==final.Revision,"Entrada inválida não altera versão salva");
        Sql("CREATE TRIGGER qa_fail_glass BEFORE UPDATE ON glass_projects_v1 BEGIN SELECT RAISE(ABORT,'falha controlada'); END");
        bool atomic=false;try{GlassProjectStore.Save(final.Data with {Description="NÃO DEVE GRAVAR"},final);}catch(Microsoft.Data.Sqlite.SqliteException){atomic=true;}
        Sql("DROP TRIGGER qa_fail_glass");
        Check(atomic && Json(GlassProjectStore.Load(a.Id))==Json(final),"Falha transacional preserva registro completo");
        foreach(var size in new[]{new Size(1366,768),new Size(1600,900),new Size(1920,1080)})
        foreach(float factor in new[]{1f,1.25f,1.5f})
        {
            using var f=new GlassWorkshopForm();f.Show();f.Scale(new SizeF(factor,factor));f.Size=new(Math.Min(size.Width,f.Width),Math.Min(size.Height-60,f.Height));Load(f,a.Id);Pump();
            foreach(var name in new[]{"SaveGlassProject","OpenGlassProject","NewSavedGlassProject"})
            {var button=Find<Button>(f,name);var scroll=All(f).OfType<Panel>().Single(x=>x.Name=="FieldsProjetos");scroll.ScrollControlIntoView(button);Pump();Check(button.Visible && scroll.RectangleToScreen(scroll.ClientRectangle).IntersectsWith(button.RectangleToScreen(button.ClientRectangle)),"Acesso "+name+" "+size+" "+factor);}
            using var image=new Bitmap(f.Width,f.Height);f.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save("glass-persistence-evidencias/central-"+size.Width+"-"+factor+".png");
            f.Close();
        }
        using(var f=new GlassWorkshopForm())
        {
            f.Show();Load(f,a.Id);Pump();
            long revision=GlassProjectStore.Load(a.Id).Revision;
            Find<Button>(f,"SaveGlassProject").PerformClick();
            Check(GlassProjectStore.Load(a.Id).Revision==revision+1,"Botão SALVAR PROJETO real grava");
            bool opened=false;
            using var timer=new System.Windows.Forms.Timer {Interval=60};
            timer.Tick+=(_,_)=>{
                var chooser=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text.StartsWith("ABRIR PROJETO"));
                if(chooser==null)return;
                timer.Stop();opened=true;
                Find<Button>(chooser,"ConfirmOpenGlassProject").PerformClick();
            };
            timer.Start();Find<Button>(f,"OpenGlassProject").PerformClick();
            Check(opened && Snapshot(f).Description=="PROJETO A EDITADO","Botão ABRIR/lista/modal restaura projeto");
            f.Close();
        }
        File.WriteAllText("glass-persistence-evidencias/projeto-final.json",Json(GlassProjectStore.Load(a.Id)));
        Console.WriteLine("PASS PERSISTENCIA "+checks+" verificações; fechamento/reabertura e novos processos Windows; nenhum cadastro/estoque/venda/pagamento alterado.");
    }
}
