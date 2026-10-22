using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LealInfoPDV.Network;

namespace LealInfoPDV;

internal sealed record KitchenRouteProduct(long Id,string Name,string Category,int Mode,bool Reviewed);
internal static class KitchenProductRouting
{
    internal const int Direct=0, Preparation=1;
    internal static string Label(int mode)=>mode==Preparation?"EXIGE PREPARO":"ENTREGA DIRETA";
    private static readonly object Sync=new();
    // Individual decisions for the reviewed 258-item catalog, including exceptions
    // inside beverages, starters, side dishes, desserts and cafeteria categories.
    private static readonly HashSet<int> DirectCatalog=new(new[]{3,16,17,18,48,115,116,119,120,121,153,154,155,156,157,158,159,160,161,162,163,164,220,221,227,229,230,231,232,233,234,235,238,251}.Concat(Enumerable.Range(19,29)).Concat(Enumerable.Range(75,17)));
    private static string Key(string name)=>string.Concat(name.Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)).ToUpperInvariant();
    internal static int Suggest(string name,string code="")
    {
        var known=ReadyKitchenCatalog.Load().FirstOrDefault(p=>Key(p.Name)==Key(name)&&(string.IsNullOrWhiteSpace(code)||p.Code==code));
        if(known!=null)return DirectCatalog.Contains(int.Parse(known.Code[7..],CultureInfo.InvariantCulture))?Direct:Preparation;
        string n=Key(name);
        if(Regex.IsMatch(n,@"\b(COMBO|SUCO NATURAL|VITAMINA|CAIPIRINHA|MILK.?SHAKE|FRITO|FRITA|GRELHADO|GRELHADA|QUENTE)\b"))return Preparation;
        if(Regex.IsMatch(n,@"\b(REFRIGERANTE|CERVEJA|AGUA MINERAL|ENERGETICO|ISOTONICO|SUCO (DE CAIXINHA|ENGARRAFADO|PRONTO)|AMENDOIM|BRIGADEIRO|PUDIM)\b"))return Direct;
        return Preparation; // Unknown names require review; never infer from category alone.
    }
    internal static void Ensure()
    {
        if(NetworkConfiguration.Current.Mode=="terminal")return;
        lock(Sync)
        {
            using var db=Database.Open();var columns=new HashSet<string>();
            using(var q=db.CreateCommand()){q.CommandText="PRAGMA table_info(products)";using var r=q.ExecuteReader();while(r.Read())columns.Add(r.GetString(1));}
            bool migrate=!columns.Contains("kitchen_route");
            bool pending=false;if(!migrate){using var q=db.CreateCommand();q.CommandText="SELECT EXISTS(SELECT 1 FROM products WHERE id>0 AND kitchen_preparation=1 AND kitchen_route IS NULL)";pending=Convert.ToInt32(q.ExecuteScalar())==1;}
            if(!migrate&&!pending)return;
            Directory.CreateDirectory(Database.BackupFolder);
            string backup=Path.Combine(Database.BackupFolder,"ANTES_CLASSIFICACAO_COZINHA_"+Guid.NewGuid().ToString("N")+".db");
            using(var snapshot=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+backup+";Pooling=False")){snapshot.Open();db.BackupDatabase(snapshot);using var q=snapshot.CreateCommand();q.CommandText="PRAGMA quick_check";if(Convert.ToString(q.ExecuteScalar())!="ok")throw new InvalidDataException("Backup não validado. Classificação não aplicada.");}
            using var tx=db.BeginTransaction();
            void Exec(string sql){using var q=db.CreateCommand();q.Transaction=tx;q.CommandText=sql;q.ExecuteNonQuery();}
            foreach(string definition in new[]{"kitchen_route INTEGER CHECK(kitchen_route IN (0,1))","kitchen_route_reviewed INTEGER NOT NULL DEFAULT 0","kitchen_route_actor TEXT NOT NULL DEFAULT ''"})if(!columns.Contains(definition.Split(' ')[0]))Exec("ALTER TABLE products ADD COLUMN "+definition);
            Exec("""
                CREATE TABLE IF NOT EXISTS kitchen_route_history_v1(id INTEGER PRIMARY KEY AUTOINCREMENT,product_id INTEGER NOT NULL,old_mode INTEGER,new_mode INTEGER,reviewed INTEGER NOT NULL,actor TEXT NOT NULL,occurred_at TEXT NOT NULL);
                CREATE TRIGGER IF NOT EXISTS kitchen_route_update_v1 AFTER UPDATE OF kitchen_route,kitchen_route_reviewed ON products WHEN OLD.kitchen_route IS NOT NEW.kitchen_route OR OLD.kitchen_route_reviewed IS NOT NEW.kitchen_route_reviewed BEGIN
                  INSERT INTO kitchen_route_history_v1(product_id,old_mode,new_mode,reviewed,actor,occurred_at) VALUES(NEW.id,OLD.kitchen_route,NEW.kitchen_route,NEW.kitchen_route_reviewed,NEW.kitchen_route_actor,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                END;
                CREATE TRIGGER IF NOT EXISTS kitchen_route_insert_v1 AFTER INSERT ON products WHEN NEW.kitchen_route IS NOT NULL BEGIN
                  INSERT INTO kitchen_route_history_v1(product_id,old_mode,new_mode,reviewed,actor,occurred_at) VALUES(NEW.id,NULL,NEW.kitchen_route,NEW.kitchen_route_reviewed,NEW.kitchen_route_actor,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                END;
                """);
            var pendingRows=new List<(long Id,string Name,string Code)>();
            using(var q=db.CreateCommand()){q.Transaction=tx;q.CommandText="SELECT id,name,COALESCE(barcode,'') FROM products WHERE id>0 AND kitchen_preparation=1 AND kitchen_route IS NULL";using var r=q.ExecuteReader();while(r.Read())pendingRows.Add((r.GetInt64(0),r.GetString(1),r.GetString(2)));}
            foreach(var row in pendingRows){using var q=db.CreateCommand();q.Transaction=tx;q.CommandText="UPDATE products SET kitchen_route=$mode,kitchen_route_reviewed=0,kitchen_route_actor='SUGESTÃO AUTOMÁTICA — REVISAR' WHERE id=$id AND kitchen_route IS NULL";q.Parameters.AddWithValue("$mode",Suggest(row.Name,row.Code));q.Parameters.AddWithValue("$id",row.Id);q.ExecuteNonQuery();}
            tx.Commit();
        }
    }
    internal static KitchenRouteProduct[] List()
    {
        Ensure();using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="SELECT id,name,COALESCE(category,''),COALESCE(kitchen_route,1),kitchen_route_reviewed FROM products WHERE id>0 AND active=1 AND kitchen_preparation=1 ORDER BY name,id";using var r=q.ExecuteReader();var rows=new List<KitchenRouteProduct>();while(r.Read())rows.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetInt32(3),r.GetInt32(4)==1));return rows.ToArray();
    }
    internal static void Review(IReadOnlyDictionary<long,int> changes)
    {
        if(!Auth.CanManageCatalog)throw new InvalidOperationException("Sem permissão para alterar o cadastro.");Ensure();
        if(changes.Any(p=>p.Value is not (Direct or Preparation)))throw new InvalidOperationException("Escolha EXIGE PREPARO ou ENTREGA DIRETA.");
        using var db=Database.Open();using var tx=db.BeginTransaction();
        foreach(var p in changes){using var q=db.CreateCommand();q.Transaction=tx;q.CommandText="UPDATE products SET kitchen_route=$mode,kitchen_route_reviewed=1,kitchen_route_actor=$actor WHERE id=$id AND kitchen_preparation=1";q.Parameters.AddWithValue("$mode",p.Value);q.Parameters.AddWithValue("$actor",Auth.OperatorName);q.Parameters.AddWithValue("$id",p.Key);if(q.ExecuteNonQuery()!=1)throw new InvalidOperationException("Produto alterado ou removido. Reabra a revisão.");}tx.Commit();
    }
    internal static string[] Conflicts(IEnumerable<KitchenItem> items,bool tableOnly)
    {
        var products=List().ToDictionary(p=>p.Id);return items.Where(i=>i.ProductId is not long id||!products.TryGetValue(id,out var p)||p.Mode!=(tableOnly?Direct:Preparation)).Select(i=>i.Description).Distinct().ToArray();
    }
    internal static bool Confirm(IWin32Window owner,IEnumerable<KitchenItem> items,bool tableOnly)
    {
        var conflicts=Conflicts(items,tableOnly);if(conflicts.Length==0)return true;
        string action=tableOnly?"ADICIONAR À MESA sem preparo":"ENVIAR À COZINHA com preparo";
        return MessageBox.Show(owner,"A ação escolhida contraria a classificação ou inclui itens sem classificação:\n\n"+string.Join("\n",conflicts.Take(12))+"\n\nConfirma "+action+" para estes itens? A classificação do cadastro será mantida.","Conferir destino dos produtos",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)==DialogResult.Yes;
    }
}
