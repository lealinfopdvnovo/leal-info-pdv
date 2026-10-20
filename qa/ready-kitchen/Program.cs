using LealInfoPDV;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

static class Program
{
    static readonly List<string> checks=new();
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks.Add(why);Console.WriteLine("PASS READY "+why);}
    static void Reject(Action action,string why){try{action();}catch(Exception e)when(e is InvalidOperationException or InvalidDataException or Microsoft.Data.Sqlite.SqliteException){Check(true,why);return;}throw new Exception("Expected rejection: "+why);}
    static object? Sql(string sql){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText=sql;return q.ExecuteScalar();}
    static string Snapshot()
    {
        using var db=Database.Open();var names=new List<string>();
        using(var q=db.CreateCommand()){q.CommandText="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE 'kitchen_%' ORDER BY name";using var r=q.ExecuteReader();while(r.Read())names.Add(r.GetString(0));}
        var result=new List<object>();
        foreach(var name in names)
        {
            using var q=db.CreateCommand();q.CommandText="SELECT * FROM \""+name.Replace("\"","<invalid>")+"\""+(name=="products"?" WHERE COALESCE(barcode,'') NOT LIKE 'LEALMIX%'":"")+" ORDER BY rowid";
            using var r=q.ExecuteReader();var rows=new List<string[]>();
            while(r.Read())rows.Add(Enumerable.Range(0,r.FieldCount).Select(i=>r.IsDBNull(i)?"<NULL>":Convert.ToString(r.GetValue(i),System.Globalization.CultureInfo.InvariantCulture)!).ToArray());
            result.Add(new{name,rows});
        }
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(result)));
    }
    static int BackupCount()=>Directory.Exists(Database.BackupFolder)?Directory.GetFiles(Database.BackupFolder,"ANTES_CATALOGO_PRONTO_*.db").Length:0;
    [STAThread]static int Main()
    {
        if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||!OperatingSystem.IsWindows())throw new Exception("Disposable Windows runner only");
        using var watchdog=new System.Threading.Timer(_=>Environment.Exit(93),null,60000,Timeout.Infinite);
        Directory.CreateDirectory("new-order-evidence");
        try
        {
            Check(File.Exists(Database.DbPath),"reuse isolated QA database only");
            Auth.Login("neworderqa","QA-isolated-123!");Check(Auth.IsAdmin,"fixture administrator authenticated");
            var entries=ReadyKitchenCatalog.Load();Check(entries.Length==18&&entries.Select(e=>e.Category).Distinct().Count()==6,"mixed pack has 18 items across six categories");
            foreach(var item in entries)
            {
                using var input=new MemoryStream(ReadyKitchenCatalog.Photo(item));using var image=Image.FromStream(input);
                Check(image.Width>=40&&image.Height>=40&&!string.IsNullOrWhiteSpace(item.Author)&&item.SourceUrl.StartsWith("https://commons.wikimedia.org/wiki/"),"licensed real photo and author verified: "+item.Code);
            }
            Sql("INSERT INTO products(barcode,name,category,price,stock,active,photo_path) VALUES('PRESERVAR_EXISTENTE','cHeEsEbUrGeR','CLIENTE',19.90,17,1,'foto-cliente-preservada.jpg'); SELECT 1");
            string before=Snapshot();int backups=BackupCount();
            Reject(()=>ReadyKitchenCatalog.Import(new Dictionary<string,decimal>()),"empty selection rejected");
            Reject(()=>ReadyKitchenCatalog.Import(new Dictionary<string,decimal>{{entries[1].Code,0}}),"unpriced product rejected");
            Reject(()=>ReadyKitchenCatalog.Import(new Dictionary<string,decimal>{{entries[1].Code,-1}}),"negative price rejected");
            Reject(()=>ReadyKitchenCatalog.Import(new Dictionary<string,decimal>{{entries[1].Code,1.001m}}),"fractional cent rejected");
            Reject(()=>ReadyKitchenCatalog.Import(new Dictionary<string,decimal>{{"UNKNOWN",10}}),"unknown external product rejected");
            Auth.Logout();Reject(()=>ReadyKitchenCatalog.Import(new Dictionary<string,decimal>{{entries[1].Code,10}}),"unauthorized import rejected");
            Auth.Login("neworderqa","QA-isolated-123!");
            Check(Snapshot()==before&&BackupCount()==backups,"validation and permission failures change no data or backups");
            Sql("CREATE TRIGGER qa_ready_abort BEFORE INSERT ON products WHEN NEW.barcode='"+entries[2].Code+"' BEGIN SELECT RAISE(ABORT,'QA_ROLLBACK'); END; SELECT 1");
            Reject(()=>ReadyKitchenCatalog.Import(new Dictionary<string,decimal>{{entries[1].Code,10},{entries[2].Code,11}}),"second insert failure rolls back the whole batch");
            Check(Convert.ToInt32(Sql("SELECT COUNT(*) FROM products WHERE barcode LIKE 'LEALMIX%'"))==0&&Snapshot()==before,"failed batch leaves no partial products and preserves original rows");
            Sql("DROP TRIGGER qa_ready_abort; SELECT 1");
            var prices=entries.Select((item,i)=>(item.Code,Price:10m+i+.25m)).ToDictionary(x=>x.Code,x=>x.Price);
            var imported=ReadyKitchenCatalog.Import(prices);
            Check(imported.Added==17&&imported.Skipped==1,"import adds selected new items and preserves same-name existing product");
            Check(Snapshot()==before,"existing products stock prices users settings sales payments and finances unchanged");
            using(var backup=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+imported.BackupPath+";Mode=ReadOnly;Pooling=False"))
            {
                backup.Open();using var q=backup.CreateCommand();q.CommandText="PRAGMA quick_check";Check(Convert.ToString(q.ExecuteScalar())=="ok","online pre-import backup passes integrity check");
                q.CommandText="SELECT COUNT(*) FROM products WHERE barcode LIKE 'LEALMIX%'";Check(Convert.ToInt32(q.ExecuteScalar())==0,"backup contains actual pre-import product state");
            }
            foreach(var item in entries.Skip(1))
            {
                using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="SELECT price,stock,photo_path FROM products WHERE barcode=$code";q.Parameters.AddWithValue("$code",item.Code);using var r=q.ExecuteReader();
                Check(r.Read()&&Convert.ToDecimal(r.GetDouble(0))==prices[item.Code]&&r.GetDouble(1)==0,"operator price and zero initial stock: "+item.Code);
                string photo=r.GetString(2);Check(File.Exists(photo)&&File.Exists(photo+".credits.json")&&Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(photo))).ToLowerInvariant()==item.ImageSha256,"persistent photo hash and credits: "+item.Code);
            }
            var repeated=ReadyKitchenCatalog.Import(prices);Check(repeated.Added==0&&repeated.Skipped==18,"repeated import cannot duplicate or replace products");
            Parallel.For(0,2,_=>{var result=ReadyKitchenCatalog.Import(prices);if(result.Added!=0)throw new Exception("Concurrent duplicate");});
            Check(Convert.ToInt32(Sql("SELECT COUNT(*) FROM products WHERE barcode LIKE 'LEALMIX%'"))==17,"concurrent reimports preserve exactly one copy");
            Application.EnableVisualStyles();using var form=new ReadyKitchenImportForm();form.Show();Application.DoEvents();
            var grid=form.Controls.Find("ReadyKitchenProducts",true).OfType<DataGridView>().Single();
            Check(grid.Rows.Count==18&&grid.Rows.Cast<DataGridViewRow>().All(r=>!Convert.ToBoolean(r.Cells["Selected"].Value)&&string.IsNullOrWhiteSpace(Convert.ToString(r.Cells["Price"].Value))),"preview requires explicit selection and operator prices");
            using(var shot=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save("new-order-evidence/catalogo-pronto-importacao.png");}
            form.Close();Check(Snapshot()==before,"opening and canceling preview preserves existing data");
            File.WriteAllText("new-order-evidence/ready-import.json",JsonSerializer.Serialize(new{success=true,checks},new JsonSerializerOptions{WriteIndented=true}));
            return 0;
        }
        catch(Exception e){File.WriteAllText("new-order-evidence/ready-import-failure.txt",e.ToString());Console.Error.WriteLine(e);return 1;}
    }
}
