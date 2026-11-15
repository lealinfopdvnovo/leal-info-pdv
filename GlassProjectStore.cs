using System.Globalization;
using System.Text.Json;
using LealInfoPDV.Network;

namespace LealInfoPDV;

// Entradas editáveis; nenhuma imagem, total ou distribuição é fonte de verdade.
public sealed record GlassSavedQuote(long CustomerId, string CustomerName, string Service,
    decimal Price, decimal ServiceValue, decimal Discount, decimal Addition);
public sealed record GlassSavedPiece(string Id, string Description, decimal WidthMm, decimal HeightMm,
    string WidthUnit, string HeightUnit, int Quantity, string Material, bool Rotation);
public sealed record GlassSavedCut(decimal WidthMm, decimal HeightMm, string WidthUnit, string HeightUnit,
    bool AllowRotation, string MaterialSuggestion, int Sequence, GlassSavedPiece[] Pieces, bool WasCalculated);
public sealed record GlassSavedProject(int FormatVersion, string Model, string Description,
    decimal WidthMm, decimal HeightMm, string WidthUnit, string HeightUnit, int Quantity,
    GlassSavedQuote Quote, GlassMaterialLink[] Materials, GlassSavedCut? Cut, bool Show3DMeasurements, string? ServiceType = null, JsonElement? AppProject = null);
public sealed record GlassStoredProject(string Id, long Revision, string CreatedUtc, string UpdatedUtc, GlassSavedProject Data);
public sealed record GlassProjectListItem(string Id, long Revision, string Description, string Customer,
    string Model, string CreatedUtc, string UpdatedUtc);

public static class GlassProjectStore
{
    private const int MaximumPayload = 2_000_000;
    public static void EnsureReady()
    {
        using var db = Database.Open();
        using var check = db.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='glass_projects_v1'";
        if (Convert.ToInt32(check.ExecuteScalar()) == 1) return;
        if (NetworkConfiguration.Current.Mode == "terminal")
            throw new InvalidOperationException("Abra primeiro a Vidraçaria no servidor atualizado para preparar a persistência. Nenhum dado foi alterado.");
        // Snapshot SQLite consistente (inclui WAL), antes da extensão aditiva de schema.
        Directory.CreateDirectory(Database.BackupFolder);
        string backup = Path.Combine(Database.BackupFolder, $"ANTES_VIDRACARIA_V1_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.db");
        using (var copy = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={backup};Pooling=False"))
        { copy.Open(); db.BackupDatabase(copy); }
        using var tx = db.BeginTransaction();
        using var create = db.CreateCommand(); create.Transaction = tx;
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS glass_projects_v1(
                id TEXT PRIMARY KEY,
                revision INTEGER NOT NULL,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                description TEXT NOT NULL,
                customer_name TEXT NOT NULL,
                model TEXT NOT NULL,
                format_version INTEGER NOT NULL,
                payload TEXT NOT NULL
            );
            """;
        create.ExecuteNonQuery(); tx.Commit();
    }
    public static GlassStoredProject Save(GlassSavedProject data, GlassStoredProject? previous = null) => SaveCore(data, previous, null);
    internal static GlassStoredProject SaveImported(GlassSavedProject data, GlassStoredProject? previous, Action<PdvConnection,PdvTransaction,string> link) => SaveCore(data,previous,link);
    private static GlassStoredProject SaveCore(GlassSavedProject data, GlassStoredProject? previous, Action<PdvConnection,PdvTransaction,string>? link)
    {
        Validate(data);
        string payload = JsonSerializer.Serialize(data);
        if (payload.Length > MaximumPayload) throw new InvalidDataException("Projeto excede o limite de armazenamento.");
        EnsureReady();
        string now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        string id = previous?.Id ?? Guid.NewGuid().ToString("N");
        long revision = checked((previous?.Revision ?? 0) + 1);
        using var db = Database.Open(); using var tx = db.BeginTransaction();
        if(previous!=null)GlassOperationsStore.CheckCustomer(db,tx,id,data.Quote.CustomerId);
        using var q = db.CreateCommand(); q.Transaction = tx;
        q.CommandText = previous == null
            ? "INSERT INTO glass_projects_v1(id,revision,created_utc,updated_utc,description,customer_name,model,format_version,payload) VALUES($id,$r,$created,$now,$description,$customer,$model,$format,$payload)"
            : "UPDATE glass_projects_v1 SET revision=$r,updated_utc=$now,description=$description,customer_name=$customer,model=$model,format_version=$format,payload=$payload WHERE id=$id AND revision=$old";
        q.Parameters.AddWithValue("$format",data.FormatVersion);q.Parameters.AddWithValue("$id", id); q.Parameters.AddWithValue("$r", revision);
        q.Parameters.AddWithValue("$created", previous?.CreatedUtc ?? now); q.Parameters.AddWithValue("$now", now);
        q.Parameters.AddWithValue("$description", data.Description); q.Parameters.AddWithValue("$customer", data.Quote.CustomerName);
        q.Parameters.AddWithValue("$model", data.Model); q.Parameters.AddWithValue("$payload", payload);
        q.Parameters.AddWithValue("$old", previous?.Revision ?? 0);
        if (q.ExecuteNonQuery() != 1) throw new InvalidOperationException("Projeto alterado ou excluído por outra sessão. Reabra antes de salvar; a versão existente foi preservada.");
        link?.Invoke(db,tx,id);tx.Commit();
        return new(id, revision, previous?.CreatedUtc ?? now, now, data);
    }
    public static GlassStoredProject Load(string id)
    {
        EnsureReady(); using var db = Database.Open(); using var q = db.CreateCommand();
        q.CommandText = "SELECT revision,created_utc,updated_utc,format_version,payload FROM glass_projects_v1 WHERE id=$id";
        q.Parameters.AddWithValue("$id", id); using var r = q.ExecuteReader();
        if (!r.Read()) throw new InvalidOperationException("Projeto não encontrado.");
        string payload = r.GetString(4);
        if (r.GetInt32(3) is not (1 or 2) || payload.Length > MaximumPayload) throw new InvalidDataException("Formato de projeto não suportado. O registro original foi preservado.");
        var data = JsonSerializer.Deserialize<GlassSavedProject>(payload) ?? throw new InvalidDataException("Projeto inválido.");
        Validate(data); return new(id, r.GetInt64(0), r.GetString(1), r.GetString(2), data);
    }
    public static IReadOnlyList<GlassProjectListItem> List()
    {
        EnsureReady(); using var db = Database.Open(); using var q = db.CreateCommand();
        q.CommandText = "SELECT id,revision,description,customer_name,model,created_utc,updated_utc FROM glass_projects_v1 ORDER BY updated_utc DESC,id";
        using var r = q.ExecuteReader(); var list = new List<GlassProjectListItem>();
        while (r.Read()) list.Add(new(r.GetString(0),r.GetInt64(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetString(6)));
        return list;
    }
    public static void Delete(string id, long revision)
    {
        EnsureReady(); using var db = Database.Open(); using var tx = db.BeginTransaction(); using var q = db.CreateCommand(); q.Transaction = tx;
        q.CommandText = "DELETE FROM glass_projects_v1 WHERE id=$id AND revision=$r";
        q.Parameters.AddWithValue("$id",id); q.Parameters.AddWithValue("$r",revision);
        if (q.ExecuteNonQuery()!=1) throw new InvalidOperationException("Projeto mudou em outra sessão. Atualize a lista antes de excluir.");
        tx.Commit();
    }
    public static void Validate(GlassSavedProject p)
    {
        if (p == null || p.FormatVersion is not (1 or 2) || (p.FormatVersion==1 && p.Model != GlassProject.ModelName) || p.Quote == null || p.Materials == null)
            throw new InvalidDataException("Formato/modelo inválido.");
        if(p.FormatVersion==2){if(p.AppProject is not JsonElement raw || p.ServiceType==null)throw new InvalidDataException("Projeto genérico sem dados estruturados.");AngraProjectData.Validate(raw);Text(p.ServiceType,100);Text(p.Model,200);}
        Text(p.Description,160); Dimension(p.WidthMm,p.WidthUnit); Dimension(p.HeightMm,p.HeightUnit);
        if (p.Quantity<1 || p.Quantity>9999) throw new InvalidDataException("Quantidade inválida.");
        Text(p.Quote.CustomerName,500); Text(p.Quote.Service,240);
        if(p.Quote.CustomerId<0) throw new InvalidDataException("Cliente inválido.");
        foreach(var v in new[]{p.Quote.Price,p.Quote.ServiceValue,p.Quote.Discount,p.Quote.Addition})
            if(v<0 || v>1000000000m) throw new InvalidDataException("Valor comercial inválido.");
        if(p.Materials.Length>1000 || p.Materials.Any(x=>x==null || x.ProductId<=0)) throw new InvalidDataException("Materiais inválidos.");
        foreach(var m in p.Materials){Text(m.Name,1000);Text(m.Barcode,500);Text(m.Category,500);}
        if(p.Cut is not { } c) return;
        Dimension(c.WidthMm,c.WidthUnit);Dimension(c.HeightMm,c.HeightUnit);Text(c.MaterialSuggestion,1000);
        if(c.Sequence<0 || c.Pieces==null || c.Pieces.Length>1000) throw new InvalidDataException("Plano de corte inválido.");
        var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var x in c.Pieces)
        {
            if(x==null || string.IsNullOrWhiteSpace(x.Id) || !ids.Add(x.Id.Trim()) || x.Quantity<1 || x.Quantity>1000) throw new InvalidDataException("Peças inválidas.");
            Text(x.Id,80);Text(x.Description,1000);Text(x.Material,1000);Dimension(x.WidthMm,x.WidthUnit);Dimension(x.HeightMm,x.HeightUnit);
        }
        if(c.WasCalculated)
            GlassCutPacking.Calculate(c.WidthMm,c.HeightMm,c.Pieces.Select(x=>new GlassCutPiece(x.Id,x.Description,x.WidthMm,x.HeightMm,x.Quantity,x.Material,x.Rotation)).ToArray(),c.AllowRotation);
    }
    private static void Text(string value,int maximum){if(value==null || value.Length>maximum)throw new InvalidDataException("Texto inválido no projeto.");}
    private static void Dimension(decimal value,string unit)
    {if(!GlassDimensionUnits.Symbols.Contains(unit) || value<0 || value>1000000m)throw new InvalidDataException("Dimensão/unidade inválida.");}
}

internal static class GlassSavedControls
{
    internal static IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
    internal static string Unit(NumericUpDown n)=>All(n.Parent!).OfType<ComboBox>().Single(c=>c.Name==n.Name+"Unit").Text;
    internal static void Set(NumericUpDown n,decimal millimetres,string unit)
    {
        All(n.Parent!).OfType<ComboBox>().Single(c=>c.Name==n.Name+"Unit").SelectedItem=unit;
        n.Value=millimetres;
    }
}
