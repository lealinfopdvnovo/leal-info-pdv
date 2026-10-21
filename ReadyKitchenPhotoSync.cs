using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
namespace LealInfoPDV;
internal static partial class ReadyKitchenCatalog
{
    internal static bool IsCatalogPhotoReference(string path,string code)
    {
        if(string.IsNullOrWhiteSpace(path)||!Regex.IsMatch(code,@"^LEALMIX[0-9]{3}$"))return false;
        try{return Path.GetDirectoryName(path)?.EndsWith(Path.Combine("ProductImages","ReadyKitchen"),StringComparison.OrdinalIgnoreCase)==true&&Regex.IsMatch(Path.GetFileName(path),"^"+Regex.Escape(code)+@"-[a-fA-F0-9]{12}\.jpg$",RegexOptions.IgnoreCase);}
        catch(ArgumentException){return false;}
    }
    // Only immutable, proven catalog-owned files are eligible. Customer-selected photos are never replaced.
    private static bool OwnedPhoto(string path,ReadyKitchenItem item,string folder)
    {
        if(!IsCatalogPhotoReference(path,item.Code)||!Path.GetFullPath(path).StartsWith(Path.GetFullPath(folder)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!File.Exists(path+".credits.json"))return false;
        try{
            var prior=JsonSerializer.Deserialize<ReadyKitchenItem>(File.ReadAllBytes(path+".credits.json"));
            if(prior==null||prior.Code!=item.Code||prior.ImageSha256 is not {Length:64}||Path.GetFileName(path)!=prior.Code+"-"+prior.ImageSha256[..12]+".jpg"||prior.CommonsFile!="LEAL_CATEGORY_ART"&&prior.SourceUrl?.StartsWith("https://commons.wikimedia.org/wiki/")!=true)return false;
            return !File.Exists(path)||Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).Equals(prior.ImageSha256,StringComparison.OrdinalIgnoreCase);
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException){return false;}
    }
    internal static int RepairPhotos()
    {
        if(Network.NetworkConfiguration.Current.Mode=="terminal")return 0;
        lock(Sync){
            var pack=Load().ToDictionary(e=>e.Code,StringComparer.OrdinalIgnoreCase);
            string folder=Path.Combine(Database.AppFolder,"ProductImages","ReadyKitchen");
            var candidates=new List<(long Id,string Previous,string Target,ReadyKitchenItem Item)>();
            using var db=Database.Open();
            using(var read=db.CreateCommand()){
                read.CommandText="SELECT id,barcode,name,COALESCE(photo_path,'') FROM products WHERE id>0 AND barcode LIKE 'LEALMIX%'";
                using var r=read.ExecuteReader();while(r.Read()){
                    if(!pack.TryGetValue(r.GetString(1),out var item)||!item.Name.Equals(r.GetString(2),StringComparison.OrdinalIgnoreCase))continue;
                    string previous=r.GetString(3),target=Path.Combine(folder,item.Code+"-"+item.ImageSha256[..12]+".jpg");
                    if(previous.Equals(target,StringComparison.OrdinalIgnoreCase)&&File.Exists(target))continue;
                    if(OwnedPhoto(previous,item,folder))candidates.Add((r.GetInt64(0),previous,target,item));
                }
            }
            if(candidates.Count==0)return 0;
            Directory.CreateDirectory(Database.BackupFolder);
            string backup=Path.Combine(Database.BackupFolder,"ANTES_FOTOS_CATALOGO_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N")+".db");
            using(var snapshot=new SqliteConnection("Data Source="+backup+";Pooling=False")){
                snapshot.Open();db.BackupDatabase(snapshot);using var verify=snapshot.CreateCommand();verify.CommandText="PRAGMA quick_check";if(Convert.ToString(verify.ExecuteScalar())!="ok")throw new InvalidDataException("Backup das fotos não validado.");
            }
            Directory.CreateDirectory(folder);
            foreach(var c in candidates){WriteOnce(c.Target,Photo(c.Item));WriteOnce(c.Target+".credits.json",JsonSerializer.SerializeToUtf8Bytes(c.Item,Json));}
            using var tx=db.BeginTransaction();int changed=0;
            foreach(var c in candidates){
                if(!OwnedPhoto(c.Previous,c.Item,folder))continue;
                using var update=db.CreateCommand();update.Transaction=tx;update.CommandText="UPDATE products SET photo_path=$new WHERE id=$id AND photo_path=$previous AND name=$name COLLATE NOCASE";
                update.Parameters.AddWithValue("$new",c.Target);update.Parameters.AddWithValue("$id",c.Id);update.Parameters.AddWithValue("$previous",c.Previous);update.Parameters.AddWithValue("$name",c.Item.Name);changed+=update.ExecuteNonQuery();
            }
            tx.Commit();return changed;
        }
    }
}
