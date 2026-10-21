using LealInfoPDV;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
internal static class KitchenPhotoTests
{
    const BindingFlags P=BindingFlags.NonPublic|BindingFlags.Instance;
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,P)!.GetValue(o)!;
    static string Value(string code,string field){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="SELECT "+field+" FROM products WHERE barcode=$c";q.Parameters.AddWithValue("$c",code);return Convert.ToString(q.ExecuteScalar())??"";}
    static void Set(string code,string field,string value){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="UPDATE products SET "+field+"=$v WHERE barcode=$c";q.Parameters.AddWithValue("$v",value);q.Parameters.AddWithValue("$c",code);q.ExecuteNonQuery();}
    static string Legacy(string code,bool remove=false,bool modify=false){
        var entry=ReadyKitchenExpanded.Entries().Single(e=>e.Code==code);
        string path=Path.Combine(Database.AppFolder,"ProductImages","ReadyKitchen",code+"-"+entry.ImageSha256[..12]+".jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path,ReadyKitchenExpanded.Illustration(entry.Category));File.WriteAllBytes(path+".credits.json",JsonSerializer.SerializeToUtf8Bytes(entry));
        if(modify)File.WriteAllBytes(path,ReadyKitchenCatalog.Photo(ReadyKitchenCatalog.Load().Single(e=>e.Code==code)));
        if(remove)File.Delete(path);Set(code,"photo_path",path);return path;
    }
    internal static void Run(KitchenForm form,Action<bool,string> check,Action<Func<bool>> pump){
        var pack=ReadyKitchenCatalog.Load();check(pack.Length==258&&pack.All(e=>e.CommonsFile.StartsWith("File:")&&e.SourceUrl.StartsWith("https://commons.wikimedia.org/wiki/")&&e.Author.Length>0),"all 258 products have attributed photographic assets instead of category artwork");
        string before=Value("LEALMIX019","name||'|'||price||'|'||stock||'|'||category||'|'||active");
        string legacy=Legacy("LEALMIX019");string missing=Legacy("LEALMIX022",remove:true);
        string modified=Legacy("LEALMIX023",modify:true);string modifiedHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(modified)));
        string renamed=Legacy("LEALMIX021");Set("LEALMIX021","name","BEBIDA PERSONALIZADA DO CLIENTE");
        string custom=Path.Combine(Database.AppFolder,"FotosCliente","personalizada.jpg");Directory.CreateDirectory(Path.GetDirectoryName(custom)!);File.WriteAllBytes(custom,ReadyKitchenCatalog.Photo(pack[0]));Set("LEALMIX020","photo_path",custom);
        int BackupCount()=>Directory.GetFiles(Database.BackupFolder,"ANTES_FOTOS_CATALOGO_*.db").Length;int backups=BackupCount();
        var menu=Field<KitchenCatalogView>(form,"mainCatalog");var task=menu.ReloadAsync();pump(()=>task.IsCompleted);task.GetAwaiter().GetResult();
        string repaired=Value("LEALMIX019","photo_path");var target=pack.Single(e=>e.Code=="LEALMIX019");
        check(repaired!=legacy&&File.Exists(repaired)&&Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(repaired))).ToLowerInvariant()==target.ImageSha256,"opening catalog replaces proven legacy artwork with correct packaged photo");
        check(before==Value("LEALMIX019","name||'|'||price||'|'||stock||'|'||category||'|'||active"),"automatic photo repair preserves names, prices, stock, categories and activation");
        check(Value("LEALMIX020","photo_path")==custom&&File.Exists(custom),"customer-selected photo is never replaced");
        check(Value("LEALMIX021","photo_path")==renamed&&Value("LEALMIX021","name")=="BEBIDA PERSONALIZADA DO CLIENTE","renamed customized products are never automatically rewritten");
        check(Value("LEALMIX023","photo_path")==modified&&Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(modified)))==modifiedHash,"customer modification of an existing catalog photo is preserved by hash check");
        check(Value("LEALMIX022","photo_path")!=missing&&File.Exists(Value("LEALMIX022","photo_path")),"missing owned catalog photo is restored without reimporting products");
        check(File.Exists(legacy)&&File.Exists(legacy+".credits.json")&&BackupCount()==backups+1,"photo repair creates verified database backup and retains old images for rollback");
        task=menu.ReloadAsync();pump(()=>task.IsCompleted);task.GetAwaiter().GetResult();check(BackupCount()==backups+1,"reopening repaired catalog does not repeatedly mutate data or create backups");
        string remote=Path.Combine(Path.GetTempPath(),"QA-NOT-PRESENT-REMOTE","ProductImages","ReadyKitchen",target.Code+"-"+target.ImageSha256[..12]+".jpg");
        var model=new KitchenCatalogProduct(99001,target.Name,target.Category,10m,remote,target.Code);
        using var card=(Control)typeof(KitchenCatalogView).GetMethod("Card",P)!.Invoke(menu,new object[]{model})!;
        IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
        check(All(card).OfType<PictureBox>().Any(p=>p.Image!=null),"terminal renders embedded catalog photo when server-local image path is unavailable");
        Set("LEALMIX021","name",pack.Single(e=>e.Code=="LEALMIX021").Name);
    }
}
