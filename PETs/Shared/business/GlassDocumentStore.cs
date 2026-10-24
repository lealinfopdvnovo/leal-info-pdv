using LealInfoPDV.Network;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LealInfoPDV;
internal sealed record GlassDocumentParty(long Id,string Name,string Document,string Phone,string Email,string Address,string CityState);
internal sealed record GlassDocumentContext(GlassDocumentParty Company,GlassDocumentParty Customer,string ProjectId,long ProjectRevision,GlassSavedProject Project,decimal CommercialValue,string FinancialCondition,DateTime CapturedAt);
internal sealed record GlassReceiptSource(long MovementId,DateTime ReceivedAt,decimal Amount,string Description,string PaymentMethod,GlassDocumentContext Context);
internal sealed record GlassReceiptListItem(long MovementId,DateTime ReceivedAt,decimal Amount,string Description);
internal sealed record GlassContractClauses(string Object,string Value,string Payment,string Deadline,string Installation,string Responsibilities,string Notes)
{
    public string Warranty { get; init; } = "";
    public string Signatures { get; init; } = "";
}
internal sealed record GlassContractContent(GlassDocumentContext Context,GlassContractClauses Clauses);
internal sealed record GlassStoredContract(string ProjectId,long Revision,long ProjectRevision,long CustomerId,string State,string CreatedUtc,string ContentHash,GlassContractContent Content);

// Documentos nunca gravam movimentos, vendas, pagamentos ou estoque.
internal static class GlassDocumentStore
{
    private static string Setting(PdvConnection db,PdvTransaction? tx,string key)
    {using var q=db.CreateCommand();q.Transaction=tx;q.CommandText="SELECT value FROM settings WHERE key=$key";q.Parameters.AddWithValue("$key",key);return Convert.ToString(q.ExecuteScalar(),CultureInfo.InvariantCulture)??"";}
    internal static GlassDocumentContext Context(GlassStoredProject saved)
    {using var db=Database.Open();using var tx=db.BeginTransaction();var context=Context(db,tx,saved);tx.Commit();return context;}
    private static GlassDocumentContext Context(PdvConnection db,PdvTransaction? tx,GlassStoredProject saved,long? customerId=null)
    {
        var p=GlassOperationsStore.Project(db,tx,saved.Id,saved.Revision);var quote=GlassOperationsStore.Quote(p);
        if(quote.ValidationError!=null)throw new InvalidOperationException(quote.ValidationError);
        long id=customerId??p.Quote.CustomerId;if(id<=0)throw new InvalidOperationException("Selecione e salve o cliente do projeto.");
        GlassDocumentParty customer;
        using(var q=db.CreateCommand()){q.Transaction=tx;q.CommandText="SELECT id,name,COALESCE(document,''),COALESCE(phone,''),COALESCE(email,''),COALESCE(address,'') FROM customers WHERE id=$id";q.Parameters.AddWithValue("$id",id);using var r=q.ExecuteReader();if(!r.Read())throw new InvalidOperationException("Cliente não localizado no cadastro do PDV.");customer=new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),"");}
        string name=Setting(db,tx,"company_trade_name");if(string.IsNullOrWhiteSpace(name))name=Setting(db,tx,"company_name");
        var company=new GlassDocumentParty(0,name,Setting(db,tx,"company_document"),Setting(db,tx,"company_phone"),Setting(db,tx,"company_email"),Setting(db,tx,"company_address"),Setting(db,tx,"company_city_state"));
        var financial=GlassOperationsStore.Read(saved);
        return new(company,customer,saved.Id,saved.Revision,p,quote.Total,financial.Display,DateTime.Now);
    }
    internal static IReadOnlyList<GlassReceiptListItem> Receipts(GlassStoredProject saved)
    {
        using var db=Database.Open();GlassOperationsStore.Project(db,null,saved.Id,saved.Revision);var result=new List<GlassReceiptListItem>();if(!GlassOperationsStore.Ready(db))return result;
        using var q=db.CreateCommand();q.CommandText="SELECT m.id,m.occurred_at,m.amount,m.description FROM cash_movements m JOIN glass_cash_links_v1 l ON l.cash_movement_id=m.id WHERE l.project_id=$id AND m.type='ENTRADA' AND m.amount>0 ORDER BY m.id DESC";q.Parameters.AddWithValue("$id",saved.Id);using var r=q.ExecuteReader();while(r.Read())result.Add(new(r.GetInt64(0),DateTime.ParseExact(r.GetString(1),"yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture),decimal.Round(Convert.ToDecimal(r.GetValue(2),CultureInfo.InvariantCulture),2),r.GetString(3)));return result;
    }
    internal static GlassReceiptSource Receipt(GlassStoredProject saved,long movement)
    {
        using var db=Database.Open();using var tx=db.BeginTransaction();if(!GlassOperationsStore.Ready(db,tx))throw new InvalidOperationException("Projeto sem recebimento financeiro registrado.");
        long customer;DateTime at;decimal amount;string description;long? sale;
        using(var q=db.CreateCommand()){q.Transaction=tx;q.CommandText="SELECT l.customer_id,m.occurred_at,m.amount,m.description,m.sale_id FROM cash_movements m JOIN glass_cash_links_v1 l ON l.cash_movement_id=m.id WHERE l.project_id=$project AND m.id=$id AND m.type='ENTRADA' AND m.amount>0";q.Parameters.AddWithValue("$project",saved.Id);q.Parameters.AddWithValue("$id",movement);using var r=q.ExecuteReader();if(!r.Read())throw new InvalidOperationException("Recibo exige uma ENTRADA real vinculada a este projeto. Saídas ou lançamentos excluídos não são recebimentos.");customer=r.GetInt64(0);at=DateTime.ParseExact(r.GetString(1),"yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);amount=decimal.Round(Convert.ToDecimal(r.GetValue(2),CultureInfo.InvariantCulture),2);description=r.GetString(3);sale=r.IsDBNull(4)?null:r.GetInt64(4);}
        string method="";
        if(sale.HasValue){using var q=db.CreateCommand();q.Transaction=tx;q.CommandText="SELECT method FROM sale_payments WHERE sale_id=$id ORDER BY id";q.Parameters.AddWithValue("$id",sale.Value);using var r=q.ExecuteReader();var methods=new List<string>();while(r.Read())methods.Add(r.GetString(0));if(methods.Count==1)method=methods[0];else if(methods.Count>1)method="Formas registradas na venda vinculada: "+string.Join(" / ",methods);}
        var result=new GlassReceiptSource(movement,at,amount,description,method,Context(db,tx,saved,customer));tx.Commit();return result;
    }
    private static bool ContractsReady(PdvConnection db)
    {using var q=db.CreateCommand();q.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='glass_contract_revisions_v1'";return Convert.ToInt32(q.ExecuteScalar())==1;}
    internal static void EnsureContracts()
    {
        GlassProjectStore.EnsureReady();using var db=Database.Open();if(ContractsReady(db))return;
        if(NetworkConfiguration.Current.Mode=="terminal")throw new InvalidOperationException("Abra Contratos primeiro no servidor atualizado para preparar a estrutura. Nenhum dado foi alterado.");
        Directory.CreateDirectory(Database.BackupFolder);
        using(var copy=new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(Database.BackupFolder,$"ANTES_VIDRA_CONTRATOS_V1_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.db")};Pooling=False")){copy.Open();db.BackupDatabase(copy);}
        using var tx=db.BeginTransaction();using var q=db.CreateCommand();q.Transaction=tx;q.CommandText="""
        CREATE TABLE IF NOT EXISTS glass_contract_revisions_v1(
          project_id TEXT NOT NULL REFERENCES glass_projects_v1(id) ON DELETE RESTRICT,
          revision INTEGER NOT NULL CHECK(revision>0),project_revision INTEGER NOT NULL,
          customer_id INTEGER NOT NULL REFERENCES customers(id) ON DELETE RESTRICT,
          state TEXT NOT NULL CHECK(state IN('RASCUNHO','FINALIZADO')),created_utc TEXT NOT NULL,
          content_hash TEXT NOT NULL,payload TEXT NOT NULL,PRIMARY KEY(project_id,revision));
        """;q.ExecuteNonQuery();tx.Commit();
    }
    internal static GlassContractContent NewContract(GlassStoredProject saved)
    {
        var c=Context(saved);var p=c.Project;
        return new(c,GlassDocumentTemplates.Contract(c));
    }
    internal static GlassStoredContract SaveContract(GlassStoredProject saved,GlassContractContent content,long expectedRevision,bool finalize)
    {
        if(content.Context.ProjectId!=saved.Id||content.Context.ProjectRevision!=saved.Revision||content.Context.Customer.Id!=saved.Data.Quote.CustomerId)throw new InvalidOperationException("Carregue os dados atuais do projeto/cliente antes de salvar nova revisão.");
        var clauses=content.Clauses;var texts=new[]{clauses.Object,clauses.Value,clauses.Payment,clauses.Deadline,clauses.Installation,clauses.Responsibilities,clauses.Notes,clauses.Warranty,clauses.Signatures};
        if(texts.Any(x=>x==null||x.Length>12000)||string.IsNullOrWhiteSpace(clauses.Object)||string.IsNullOrWhiteSpace(clauses.Value))throw new ArgumentException("Informe objeto e valor; cada texto aceita até 12.000 caracteres.");
        string payload=JsonSerializer.Serialize(content);if(payload.Length>150000)throw new InvalidDataException("Contrato excede o limite do documento.");
        EnsureContracts();using var db=Database.Open();using var tx=db.BeginTransaction();var project=GlassOperationsStore.Project(db,tx,saved.Id,saved.Revision);
        if(project.Quote.CustomerId!=content.Context.Customer.Id)throw new InvalidOperationException("Cliente do projeto mudou. Reabra o projeto.");
        long latest;using(var check=db.CreateCommand()){check.Transaction=tx;check.CommandText="SELECT COALESCE(MAX(revision),0) FROM glass_contract_revisions_v1 WHERE project_id=$id";check.Parameters.AddWithValue("$id",saved.Id);latest=Convert.ToInt64(check.ExecuteScalar());}
        if(latest!=expectedRevision)throw new InvalidOperationException("Contrato salvo por outra sessão. Reabra para consultar a revisão mais recente; nenhuma revisão foi sobrescrita.");
        string now=DateTime.UtcNow.ToString("O"),state=finalize?"FINALIZADO":"RASCUNHO",hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        using var q=db.CreateCommand();q.Transaction=tx;q.CommandText="INSERT INTO glass_contract_revisions_v1(project_id,revision,project_revision,customer_id,state,created_utc,content_hash,payload) VALUES($id,$revision,$projectRevision,$customer,$state,$now,$hash,$payload)";
        q.Parameters.AddWithValue("$id",saved.Id);q.Parameters.AddWithValue("$revision",latest+1);q.Parameters.AddWithValue("$projectRevision",saved.Revision);q.Parameters.AddWithValue("$customer",content.Context.Customer.Id);q.Parameters.AddWithValue("$state",state);q.Parameters.AddWithValue("$now",now);q.Parameters.AddWithValue("$hash",hash);q.Parameters.AddWithValue("$payload",payload);q.ExecuteNonQuery();tx.Commit();return new(saved.Id,latest+1,saved.Revision,content.Context.Customer.Id,state,now,hash,content);
    }
    internal static IReadOnlyList<GlassStoredContract> Contracts(string projectId)
    {
        EnsureContracts();using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="SELECT revision,project_revision,customer_id,state,created_utc,content_hash,payload FROM glass_contract_revisions_v1 WHERE project_id=$id ORDER BY revision DESC";q.Parameters.AddWithValue("$id",projectId);using var r=q.ExecuteReader();var list=new List<GlassStoredContract>();while(r.Read()){string payload=r.GetString(6),hash=r.GetString(5);if(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant()!=hash)throw new InvalidDataException("Conteúdo do contrato não corresponde ao hash salvo.");var content=JsonSerializer.Deserialize<GlassContractContent>(payload)??throw new InvalidDataException("Contrato inválido.");list.Add(new(projectId,r.GetInt64(0),r.GetInt64(1),r.GetInt64(2),r.GetString(3),r.GetString(4),hash,content));}return list;
    }
}
