using LealInfoPDV.Network;
using System.Globalization;
using System.Text.Json;
namespace LealInfoPDV;
internal sealed record GlassCashOrigin(string ProjectId,long Revision,long CustomerId,string OperationId);
internal sealed record GlassFinancialState(decimal ProjectValue,decimal Received,decimal Expenses)
{
    internal decimal Remaining=>Math.Max(0,ProjectValue-Received);
    internal decimal Excess=>Math.Max(0,Received-ProjectValue);
    internal string Status=>Received<=0?"PENDENTE":Remaining>0?"PARCIALMENTE PAGO":"PAGO";
    internal string Display=>$"Valor do projeto: {GlassBrazilianMoney.Format(ProjectValue)} • Recebido/sinal: {GlassBrazilianMoney.Format(Received)} • A receber: {GlassBrazilianMoney.Format(Remaining)} • Saídas: {GlassBrazilianMoney.Format(Expenses)} • {Status}"+(Excess>0?$" • Excedente: {GlassBrazilianMoney.Format(Excess)}":"");
}
internal sealed record GlassAppointment(string Id,string ProjectId,long? CustomerId,string Customer,string Phone,string Address,string Project,string Kind,DateTime At,string Notes);
internal static class GlassOperationsStore
{
    internal static bool Ready(PdvConnection db,PdvTransaction? tx=null)
    {using var q=db.CreateCommand();q.Transaction=tx;q.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN('glass_cash_links_v1','glass_appointments_v1')";return Convert.ToInt32(q.ExecuteScalar())==2;}
    internal static void EnsureReady()
    {
        GlassProjectStore.EnsureReady();using var db=Database.Open();if(Ready(db))return;
        if(NetworkConfiguration.Current.Mode=="terminal")throw new InvalidOperationException("Abra Financeiro/Agenda da Vidraçaria primeiro no servidor atualizado. Nenhum dado foi alterado.");
        Directory.CreateDirectory(Database.BackupFolder);
        using(var copy=new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(Database.BackupFolder,$"ANTES_VIDRA_FIN_AGENDA_V1_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.db")};Pooling=False")){copy.Open();db.BackupDatabase(copy);}
        using var tx=db.BeginTransaction();using var q=db.CreateCommand();q.Transaction=tx;
        q.CommandText="""
        CREATE TABLE IF NOT EXISTS glass_cash_links_v1(
          cash_movement_id INTEGER PRIMARY KEY REFERENCES cash_movements(id) ON DELETE CASCADE,
          project_id TEXT NOT NULL REFERENCES glass_projects_v1(id) ON DELETE RESTRICT,
          customer_id INTEGER NOT NULL REFERENCES customers(id) ON DELETE RESTRICT,
          operation_id TEXT NOT NULL UNIQUE,created_utc TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS glass_cash_project_idx ON glass_cash_links_v1(project_id);
        CREATE TABLE IF NOT EXISTS glass_appointments_v1(
          id TEXT PRIMARY KEY,project_id TEXT NOT NULL REFERENCES glass_projects_v1(id) ON DELETE RESTRICT,
          customer_id INTEGER REFERENCES customers(id) ON DELETE RESTRICT,
          kind TEXT NOT NULL CHECK(kind IN('MEDIÇÃO','INSTALAÇÃO')),scheduled_local TEXT NOT NULL,
          notes TEXT NOT NULL DEFAULT '',created_utc TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS glass_appointment_at_idx ON glass_appointments_v1(scheduled_local);
        """;q.ExecuteNonQuery();tx.Commit();
    }
    internal static GlassSavedProject Project(PdvConnection db,PdvTransaction? tx,string id,long revision)
    {
        using var q=db.CreateCommand();q.Transaction=tx;q.CommandText="SELECT revision,payload FROM glass_projects_v1 WHERE id=$id";q.Parameters.AddWithValue("$id",id);using var r=q.ExecuteReader();
        if(!r.Read()||r.GetInt64(0)!=revision)throw new InvalidOperationException("Projeto alterado ou excluído por outra sessão. Reabra o projeto antes de continuar.");
        return JsonSerializer.Deserialize<GlassSavedProject>(r.GetString(1))??throw new InvalidDataException("Projeto inválido.");
    }
    internal static GlassQuote Quote(GlassSavedProject p)=>new(new GlassProject(p.Description,p.WidthMm,p.HeightMm,p.Quantity),p.Quote.Price,p.Quote.ServiceValue,p.Quote.Discount,p.Quote.Addition);
    internal static GlassFinancialState Read(GlassStoredProject saved)
    {using var db=Database.Open();var p=Project(db,null,saved.Id,saved.Revision);return Read(db,null,saved.Id,Quote(p).Total);}
    private static GlassFinancialState Read(PdvConnection db,PdvTransaction? tx,string id,decimal total)
    {
        if(!Ready(db,tx))return new(total,0,0);
        using var q=db.CreateCommand();q.Transaction=tx;
        q.CommandText="SELECT COALESCE(SUM(CASE WHEN m.type='ENTRADA' THEN m.amount ELSE 0 END),0),COALESCE(SUM(CASE WHEN m.type='SAÍDA' THEN m.amount ELSE 0 END),0) FROM glass_cash_links_v1 l JOIN cash_movements m ON m.id=l.cash_movement_id WHERE l.project_id=$id";q.Parameters.AddWithValue("$id",id);using var r=q.ExecuteReader();r.Read();return new(total,decimal.Round(Convert.ToDecimal(r.GetValue(0),CultureInfo.InvariantCulture),2),decimal.Round(Convert.ToDecimal(r.GetValue(1),CultureInfo.InvariantCulture),2));
    }
    internal static long? ValidateCash(PdvConnection db,PdvTransaction tx,GlassCashOrigin origin,string type,decimal amount)
    {
        using(var q=db.CreateCommand())
        {q.Transaction=tx;q.CommandText="SELECT m.id,l.project_id,l.customer_id,m.type,m.amount FROM glass_cash_links_v1 l JOIN cash_movements m ON m.id=l.cash_movement_id WHERE l.operation_id=$op";q.Parameters.AddWithValue("$op",origin.OperationId);using var r=q.ExecuteReader();if(r.Read()){if(r.GetString(1)!=origin.ProjectId||r.GetInt64(2)!=origin.CustomerId||r.GetString(3)!=type||Convert.ToDecimal(r.GetValue(4),CultureInfo.InvariantCulture)!=amount)throw new InvalidOperationException("Operação financeira já utilizada com outros dados.");return r.GetInt64(0);}}
        var p=Project(db,tx,origin.ProjectId,origin.Revision);
        if(p.Quote.CustomerId<=0||p.Quote.CustomerId!=origin.CustomerId)throw new InvalidOperationException("Selecione e salve o cliente vinculado antes de registrar movimentação.");
        using(var q=db.CreateCommand()){q.Transaction=tx;q.CommandText="SELECT COUNT(*) FROM customers WHERE id=$id";q.Parameters.AddWithValue("$id",origin.CustomerId);if(Convert.ToInt32(q.ExecuteScalar())!=1)throw new InvalidOperationException("Cliente não existe mais no cadastro.");}
        var quote=Quote(p);if(quote.ValidationError!=null)throw new InvalidOperationException(quote.ValidationError);
        if(type=="ENTRADA"&&amount>Read(db,tx,origin.ProjectId,quote.Total).Remaining)throw new InvalidOperationException("Recebimento maior que o saldo restante do projeto.");
        if(!Guid.TryParseExact(origin.OperationId,"N",out _))throw new InvalidOperationException("Identificador de operação inválido.");return null;
    }
    internal static void LinkCash(PdvConnection db,PdvTransaction tx,GlassCashOrigin origin,long movement)
    {using var q=db.CreateCommand();q.Transaction=tx;q.CommandText="INSERT INTO glass_cash_links_v1(cash_movement_id,project_id,customer_id,operation_id,created_utc) VALUES($cash,$project,$customer,$op,$now)";q.Parameters.AddWithValue("$cash",movement);q.Parameters.AddWithValue("$project",origin.ProjectId);q.Parameters.AddWithValue("$customer",origin.CustomerId);q.Parameters.AddWithValue("$op",origin.OperationId);q.Parameters.AddWithValue("$now",DateTime.UtcNow.ToString("O"));q.ExecuteNonQuery();}
    internal static void CheckCustomer(PdvConnection db,PdvTransaction tx,string id,long customer)
    {
        if(!Ready(db,tx))return;using var q=db.CreateCommand();q.Transaction=tx;q.CommandText="SELECT COUNT(*) FROM glass_cash_links_v1 WHERE project_id=$id AND customer_id<>$customer";q.Parameters.AddWithValue("$id",id);q.Parameters.AddWithValue("$customer",customer);if(Convert.ToInt32(q.ExecuteScalar())>0)throw new InvalidOperationException("Projeto com movimentação financeira: preserve o cliente dos lançamentos ou crie outro projeto.");
    }
    internal static string HistorySql(string id)=>CashFlow.HistorySql.Replace("FROM cash_movements ORDER BY",$"FROM cash_movements WHERE id IN(SELECT cash_movement_id FROM glass_cash_links_v1 WHERE project_id='{id.Replace("'","''")}') ORDER BY");
    internal static string Schedule(GlassStoredProject saved,string kind,DateTime at,string notes)
    {
        if(kind is not ("MEDIÇÃO" or "INSTALAÇÃO"))throw new ArgumentException("Escolha MEDIÇÃO ou INSTALAÇÃO.");
        if(at<DateTime.Now.AddMinutes(-1))throw new ArgumentException("Informe uma data/hora futura.");if(notes.Length>2000)throw new ArgumentException("Observação muito longa.");
        EnsureReady();using var db=Database.Open();using var tx=db.BeginTransaction();var p=Project(db,tx,saved.Id,saved.Revision);long? client=p.Quote.CustomerId>0?p.Quote.CustomerId:null;
        if(client.HasValue){using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="SELECT COUNT(*) FROM customers WHERE id=$id";c.Parameters.AddWithValue("$id",client.Value);if(Convert.ToInt32(c.ExecuteScalar())!=1)throw new InvalidOperationException("Cliente não existe mais no cadastro.");}
        string id=Guid.NewGuid().ToString("N");using var q=db.CreateCommand();q.Transaction=tx;q.CommandText="INSERT INTO glass_appointments_v1(id,project_id,customer_id,kind,scheduled_local,notes,created_utc) VALUES($id,$project,$customer,$kind,$at,$notes,$now)";
        q.Parameters.AddWithValue("$id",id);q.Parameters.AddWithValue("$project",saved.Id);q.Parameters.AddWithValue("$customer",(object?)client??DBNull.Value);q.Parameters.AddWithValue("$kind",kind);q.Parameters.AddWithValue("$at",at.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture));q.Parameters.AddWithValue("$notes",notes.Trim());q.Parameters.AddWithValue("$now",DateTime.UtcNow.ToString("O"));q.ExecuteNonQuery();tx.Commit();return id;
    }
    internal static IReadOnlyList<GlassAppointment> Appointments(bool future=true)
    {
        EnsureReady();var items=new List<GlassAppointment>();using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="SELECT a.id,a.project_id,a.customer_id,COALESCE(c.name,''),COALESCE(c.phone,''),COALESCE(c.address,''),p.description,a.kind,a.scheduled_local,a.notes FROM glass_appointments_v1 a JOIN glass_projects_v1 p ON p.id=a.project_id LEFT JOIN customers c ON c.id=a.customer_id WHERE ($all=1 OR a.scheduled_local>=$from) ORDER BY a.scheduled_local,a.id";q.Parameters.AddWithValue("$all",future?0:1);q.Parameters.AddWithValue("$from",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture));using var r=q.ExecuteReader();while(r.Read())items.Add(new(r.GetString(0),r.GetString(1),r.IsDBNull(2)?null:r.GetInt64(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetString(6),r.GetString(7),DateTime.ParseExact(r.GetString(8),"yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture),r.GetString(9)));return items;
    }
}
