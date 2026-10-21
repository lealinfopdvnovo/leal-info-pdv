using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace LealInfoPDV;

internal sealed record ReadyKitchenItem(string Code,string Name,string Category,string CommonsFile,string ImageFile,string ImageSha256,string SourceUrl,string Author,string License,string LicenseUrl,string Changes);
internal sealed record ReadyKitchenImportResult(int Added,int Skipped,string BackupPath);

internal static class ReadyKitchenCatalog
{
    private static readonly object Sync=new();
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    internal static ReadyKitchenItem[] Load()
    {
        using var stream=typeof(ReadyKitchenCatalog).Assembly.GetManifestResourceStream("ReadyKitchen.catalog.json")??throw new InvalidDataException("Catálogo pronto não encontrado.");
        var entries=JsonSerializer.Deserialize<ReadyKitchenItem[]>(stream)??throw new InvalidDataException("Catálogo inválido.");
        if(entries.Length!=18 || entries.Select(x=>x.Code).Distinct().Count()!=18)throw new InvalidDataException("Catálogo incompleto.");
        foreach(var e in entries)
            if(!Regex.IsMatch(e.Code,@"^LEALMIX[0-9]{3}$")||e.ImageFile!=e.Code+".jpg"||string.IsNullOrWhiteSpace(e.Author)||!e.SourceUrl.StartsWith("https://commons.wikimedia.org/wiki/")||!e.LicenseUrl.StartsWith("https://creativecommons.org/")&&!e.LicenseUrl.StartsWith("http://creativecommons.org/"))
                throw new InvalidDataException("Origem de foto inválida.");
        var expanded=entries.Concat(ReadyKitchenExpanded.Entries()).ToArray();
        if(expanded.Length<200||expanded.Select(x=>x.Code).Distinct().Count()!=expanded.Length||expanded.Select(x=>Key(x.Name)).Distinct().Count()!=expanded.Length)throw new InvalidDataException("Catálogo ampliado incompleto ou duplicado.");
        return expanded;
    }
    internal static byte[] Photo(ReadyKitchenItem item)
    {
        if(item.CommonsFile=="LEAL_CATEGORY_ART"){
            var illustrationBytes=ReadyKitchenExpanded.Illustration(item.Category);
            if(Convert.ToHexString(SHA256.HashData(illustrationBytes)).ToLowerInvariant()!=item.ImageSha256)throw new InvalidDataException("Integridade da ilustração inválida.");
            return illustrationBytes;
        }
        using var stream=typeof(ReadyKitchenCatalog).Assembly.GetManifestResourceStream("ReadyKitchen."+item.ImageFile)??throw new InvalidDataException("Foto não encontrada.");
        using var output=new MemoryStream();stream.CopyTo(output);var bytes=output.ToArray();
        if(bytes.Length>4_000_000||Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()!=item.ImageSha256)throw new InvalidDataException("Integridade da foto inválida.");
        using var input=new MemoryStream(bytes);using var image=Image.FromStream(input);if(image.Width<40||image.Height<40||image.Width>5000||image.Height>5000)throw new InvalidDataException("Dimensões da foto inválidas.");
        return bytes;
    }
    private static string Key(string text)
    {
        var decomposed=text.Trim().Normalize(NormalizationForm.FormD);
        return string.Concat(decomposed.Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)).Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }
    private static void WriteOnce(string path,byte[] bytes)
    {
        if(File.Exists(path))
        {
            if(!SHA256.HashData(File.ReadAllBytes(path)).SequenceEqual(SHA256.HashData(bytes)))throw new IOException("Arquivo de imagem existente diferente. Nenhum arquivo foi substituído.");
            return;
        }
        string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(bytes);f.Flush(true);}
            File.Move(temp,path,false);
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
    internal static ReadyKitchenImportResult Import(IReadOnlyDictionary<string,decimal> selected)
    {
        if(!Auth.IsAdmin)throw new InvalidOperationException("Somente ADMINISTRADOR pode importar produtos.");
        if(Network.NetworkConfiguration.Current.Mode=="terminal")throw new InvalidOperationException("Importe no computador servidor. Os produtos aparecerão no terminal ao atualizar o cardápio.");
        var source=Load().ToDictionary(x=>x.Code);
        if(selected.Count==0)throw new InvalidDataException("Selecione ao menos um produto.");
        foreach(var pair in selected)
            if(!source.ContainsKey(pair.Key)||pair.Value<=0||pair.Value>1_000_000||decimal.Round(pair.Value,2)!=pair.Value)
                throw new InvalidDataException("Informe um preço positivo, com até duas casas decimais, para cada produto selecionado.");
        lock(Sync)
        {
            // Decode and verify selected photos before any database mutation.
            var images=selected.Keys.ToDictionary(code=>code,code=>Photo(source[code]));
            Directory.CreateDirectory(Database.BackupFolder);
            string backup=Path.Combine(Database.BackupFolder,"ANTES_CATALOGO_PRONTO_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N")+".db");
            using var db=Database.Open();
            using(var snapshot=new SqliteConnection("Data Source="+backup+";Mode=ReadWriteCreate;Pooling=False"))
            {
                snapshot.Open();db.BackupDatabase(snapshot);using var verify=snapshot.CreateCommand();verify.CommandText="PRAGMA quick_check";
                if(Convert.ToString(verify.ExecuteScalar())!="ok")throw new InvalidDataException("Backup de proteção não validado. Importação cancelada.");
            }
            string folder=Path.Combine(Database.AppFolder,"ProductImages","ReadyKitchen");Directory.CreateDirectory(folder);
            using var tx=db.BeginTransaction();
            var names=new HashSet<string>();var codes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using(var existing=db.CreateCommand())
            {
                existing.Transaction=tx;existing.CommandText="SELECT name,COALESCE(barcode,'') FROM products";using var r=existing.ExecuteReader();
                while(r.Read()){names.Add(Key(r.GetString(0)));codes.Add(r.GetString(1));}
            }
            var columns=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using(var info=db.CreateCommand()){info.Transaction=tx;info.CommandText="PRAGMA table_info(products)";using var r=info.ExecuteReader();while(r.Read())columns.Add(r.GetString(1));}
            int added=0,skipped=0;
            foreach(var pair in selected)
            {
                var item=source[pair.Key];
                if(codes.Contains(item.Code)||names.Contains(Key(item.Name))){skipped++;continue;}
                string imagePath=Path.Combine(folder,item.Code+"-"+item.ImageSha256[..12]+".jpg");
                WriteOnce(imagePath,images[item.Code]);
                WriteOnce(imagePath+".credits.json",JsonSerializer.SerializeToUtf8Bytes(item,Json));
                using var insert=db.CreateCommand();insert.Transaction=tx;
                string extra=columns.Contains("kitchen_preparation")?",kitchen_preparation":"";
                string values=columns.Contains("kitchen_preparation")?",1":"";
                if(columns.Contains("kitchen_classified")){extra+=",kitchen_classified";values+=",1";}
                if(columns.Contains("track_stock")){extra+=",track_stock";values+=",0";}
                insert.CommandText="INSERT INTO products(barcode,name,category,price,cost,stock,min_stock,active,photo_path"+extra+") VALUES($code,$name,$category,$price,0,0,0,1,$photo"+values+")";
                insert.Parameters.AddWithValue("$code",item.Code);insert.Parameters.AddWithValue("$name",item.Name);insert.Parameters.AddWithValue("$category",item.Category);
                insert.Parameters.AddWithValue("$price",pair.Value);insert.Parameters.AddWithValue("$photo",imagePath);insert.ExecuteNonQuery();
                names.Add(Key(item.Name));codes.Add(item.Code);added++;
            }
            tx.Commit();return new(added,skipped,backup);
        }
    }
}
