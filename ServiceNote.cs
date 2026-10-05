using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("LealInfoPDV.ServiceNoteTests")]
namespace LealInfoPDV;

internal sealed class ServiceNoteItem
{
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; } = 1;
    public long UnitCents { get; set; }
    public long TotalCents => checked((long)decimal.Round(Quantity * UnitCents, 0, MidpointRounding.AwayFromZero));
}
internal sealed class ServiceNoteRecord
{
    public long Id { get; set; }
    public long? CustomerId { get; set; }
    public Dictionary<string,string> Customer { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Equipment { get; set; } = "";
    public DateTime OrderDate { get; set; } = DateTime.Today;
    public string Payment { get; set; } = "DINHEIRO";
    public string Observations { get; set; } = "";
    public string Status { get; set; } = "ABERTA";
    public DateTime? CompletedAt { get; set; }
    public string Warranty { get; set; } = "";
    public long PaidCents { get; set; }
    public string CreatedAt { get; set; } = "";
    public long? UserId { get; set; }
    public string Operator { get; set; } = "";
    public List<ServiceNoteItem> Items { get; set; } = new();
    public long TotalCents => Items.Aggregate(0L, (total,item) => checked(total + item.TotalCents));
    public string Detail(string key) => Customer.GetValueOrDefault(key) ?? "";
    public string Address => string.Join(", ", new[] { Detail("address"), Detail("number"), Detail("complement"), Detail("district"), string.Join(" - ",new[]{Detail("city"),Detail("state")}.Where(s=>s.Length>0)), Detail("zip").Length>0?"CEP "+Detail("zip"):"" }.Where(s=>s.Length>0));
}
internal static class ServiceNote
{
    internal static readonly CultureInfo Brazilian = CultureInfo.GetCultureInfo("pt-BR");
    internal static string Money(long cents) => (cents / 100m).ToString("C2", Brazilian);
    internal static long ParseMoney(string text)
    {
        if(!decimal.TryParse(text,NumberStyles.Number,Brazilian,out var value) || value < 0 || value > 999999999m || decimal.Round(value,2)!=value)
            throw new ArgumentException("Informe um valor monetário válido e positivo ou zero.");
        return checked((long)(value*100m));
    }
    internal static Dictionary<string,string> Customer(long id)
    {
        using var db=Database.Open();using var cmd=db.CreateCommand();cmd.CommandText="SELECT * FROM customers WHERE id=$id";cmd.Parameters.AddWithValue("$id",id);
        using var reader=cmd.ExecuteReader();if(!reader.Read())throw new ArgumentException("Cliente não encontrado. Busque novamente.");
        var raw=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        for(var i=0;i<reader.FieldCount;i++)raw[reader.GetName(i)]=reader.IsDBNull(i)?"":Convert.ToString(reader.GetValue(i))??"";
        string Get(params string[] names)=>names.Select(n=>raw.GetValueOrDefault(n)??"").FirstOrDefault(v=>v.Length>0)??"";
        return new(StringComparer.OrdinalIgnoreCase) {
            ["name"]=Get("name"),["document"]=Get("document"),["phone"]=Get("phone"),
            ["address"]=Get("street","address"),["number"]=Get("number","address_number"),["complement"]=Get("complement","address_complement"),
            ["district"]=Get("district","neighborhood","bairro"),["city"]=Get("city","cidade"),["state"]=Get("state","uf"),
            ["zip"]=Get("zip","zip_code","postal_code","cep"),["reference"]=Get("reference","address_reference") };
    }
    internal static DataTable History(string term)
    {
        using var db=Database.Open();using var cmd=db.CreateCommand();
        cmd.CommandText="""
            SELECT id AS ID, printf('%06d',id) AS 'Nº NOTA', substr(order_date,9,2)||'/'||substr(order_date,6,2)||'/'||substr(order_date,1,4) AS DATA,
            customer_name AS CLIENTE,equipment AS 'PRODUTO/EQUIPAMENTO',total_cents/100.0 AS 'VALOR TOTAL',status AS STATUS
            FROM service_notes WHERE CAST(id AS TEXT) LIKE $q OR printf('%06d',id) LIKE $q OR customer_name LIKE $q
            OR customer_document LIKE $q OR customer_phone LIKE $q OR order_date LIKE $q
            OR substr(order_date,9,2)||'/'||substr(order_date,6,2)||'/'||substr(order_date,1,4) LIKE $q OR equipment LIKE $q
            ORDER BY id DESC
            """;
        cmd.Parameters.AddWithValue("$q","%"+term.Trim()+"%");using var reader=cmd.ExecuteReader();var table=new DataTable();table.Load(reader);return table;
    }
    internal static long Save(ServiceNoteRecord note)
    {
        if(string.IsNullOrWhiteSpace(note.Detail("name"))||string.IsNullOrWhiteSpace(note.Equipment))throw new ArgumentException("Informe o cliente e o produto/equipamento.");
        if(note.Items.Count==0)throw new ArgumentException("Adicione pelo menos um item/serviço.");
        if(note.Status is not ("ABERTA" or "FINALIZADA"))throw new ArgumentException("Status inválido.");
        if(note.Status=="FINALIZADA"&&!note.CompletedAt.HasValue)throw new ArgumentException("Informe a data e hora de finalização.");
        if(note.PaidCents<0||note.PaidCents>99999999900L)throw new ArgumentException("Valor pago inválido.");
        foreach(var item in note.Items)
            if(string.IsNullOrWhiteSpace(item.Description)||item.Quantity<=0||item.Quantity>999999m||item.UnitCents<0||item.UnitCents>99999999900L)
                throw new ArgumentException("Confira a descrição, quantidade positiva e valor de cada item.");
        var total=note.TotalCents;
        using var db=Database.Open();using var tx=db.BeginTransaction();
        if(note.CustomerId.HasValue){using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="SELECT id FROM customers WHERE id=$id";c.Parameters.AddWithValue("$id",note.CustomerId.Value);if(c.ExecuteScalar()==null)throw new ArgumentException("Cliente vinculado não existe mais. Busque novamente.");}
        using var cmd=db.CreateCommand();cmd.Transaction=tx;
        var columns="customer_id,customer_name,customer_document,customer_phone,customer_details,equipment,order_date,payment,observations,status,completed_at,warranty,paid_cents,total_cents";
        var values="$customer,$name,$document,$phone,$details,$equipment,$date,$payment,$observations,$status,$completed,$warranty,$paid,$total";
        if(note.Id==0)
        {
            note.CreatedAt=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");note.UserId=Auth.Current?.Id;note.Operator=Auth.OperatorName;
            cmd.CommandText=$"INSERT INTO service_notes({columns},created_at,user_id,operator) VALUES({values},$created,$user,$operator); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$created",note.CreatedAt);cmd.Parameters.AddWithValue("$user",(object?)note.UserId??DBNull.Value);cmd.Parameters.AddWithValue("$operator",note.Operator);
        }
        else
        {
            using var exists=db.CreateCommand();exists.Transaction=tx;exists.CommandText="SELECT id FROM service_notes WHERE id=$id";exists.Parameters.AddWithValue("$id",note.Id);if(exists.ExecuteScalar()==null)throw new ArgumentException("Nota não encontrada.");
            cmd.CommandText="UPDATE service_notes SET "+string.Join(",",columns.Split(',').Zip(values.Split(','),(c,v)=>c+"="+v))+" WHERE id=$id";cmd.Parameters.AddWithValue("$id",note.Id);
        }
        cmd.Parameters.AddWithValue("$customer",(object?)note.CustomerId??DBNull.Value);cmd.Parameters.AddWithValue("$name",note.Detail("name").Trim());cmd.Parameters.AddWithValue("$document",note.Detail("document"));cmd.Parameters.AddWithValue("$phone",note.Detail("phone"));cmd.Parameters.AddWithValue("$details",JsonSerializer.Serialize(note.Customer));
        cmd.Parameters.AddWithValue("$equipment",note.Equipment.Trim());cmd.Parameters.AddWithValue("$date",note.OrderDate.ToString("yyyy-MM-dd"));cmd.Parameters.AddWithValue("$payment",note.Payment);cmd.Parameters.AddWithValue("$observations",note.Observations.Trim());cmd.Parameters.AddWithValue("$status",note.Status);cmd.Parameters.AddWithValue("$completed",note.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss")??"");cmd.Parameters.AddWithValue("$warranty",note.Warranty.Trim());cmd.Parameters.AddWithValue("$paid",note.PaidCents);cmd.Parameters.AddWithValue("$total",total);
        var id=note.Id==0?Convert.ToInt64(cmd.ExecuteScalar()):note.Id;if(note.Id!=0)cmd.ExecuteNonQuery();
        using(var clear=db.CreateCommand()){clear.Transaction=tx;clear.CommandText="DELETE FROM service_note_items WHERE service_note_id=$id";clear.Parameters.AddWithValue("$id",id);clear.ExecuteNonQuery();}
        for(var position=0;position<note.Items.Count;position++)
        {
            var item=note.Items[position];using var insert=db.CreateCommand();insert.Transaction=tx;
            insert.CommandText="INSERT INTO service_note_items(service_note_id,position,code,description,quantity,unit_cents,total_cents) VALUES($id,$position,$code,$description,$qty,$unit,$total)";
            insert.Parameters.AddWithValue("$id",id);insert.Parameters.AddWithValue("$position",position);insert.Parameters.AddWithValue("$code",item.Code.Trim());insert.Parameters.AddWithValue("$description",item.Description.Trim());insert.Parameters.AddWithValue("$qty",item.Quantity.ToString(CultureInfo.InvariantCulture));insert.Parameters.AddWithValue("$unit",item.UnitCents);insert.Parameters.AddWithValue("$total",item.TotalCents);insert.ExecuteNonQuery();
        }
        tx.Commit();note.Id=id;return id;
    }
    internal static ServiceNoteRecord Load(long id)
    {
        using var db=Database.Open();using var cmd=db.CreateCommand();cmd.CommandText="SELECT * FROM service_notes WHERE id=$id";cmd.Parameters.AddWithValue("$id",id);
        var note=new ServiceNoteRecord();using(var r=cmd.ExecuteReader())
        {
            if(!r.Read())throw new ArgumentException("Nota não encontrada.");string Text(string key)=>Convert.ToString(r[key])??"";
            note.Id=id;note.CustomerId=r["customer_id"]==DBNull.Value?null:Convert.ToInt64(r["customer_id"]);
            note.Customer=JsonSerializer.Deserialize<Dictionary<string,string>>(Text("customer_details"))??new();note.Equipment=Text("equipment");note.OrderDate=DateTime.ParseExact(Text("order_date"),"yyyy-MM-dd",CultureInfo.InvariantCulture);note.Payment=Text("payment");note.Observations=Text("observations");note.Status=Text("status");note.CompletedAt=Text("completed_at").Length==0?null:DateTime.ParseExact(Text("completed_at"),"yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);note.Warranty=Text("warranty");note.PaidCents=Convert.ToInt64(r["paid_cents"]);note.CreatedAt=Text("created_at");note.UserId=r["user_id"]==DBNull.Value?null:Convert.ToInt64(r["user_id"]);note.Operator=Text("operator");
        }
        cmd.CommandText="SELECT code,description,quantity,unit_cents FROM service_note_items WHERE service_note_id=$id ORDER BY position,id";
        using var items=cmd.ExecuteReader();while(items.Read())note.Items.Add(new(){Code=items.GetString(0),Description=items.GetString(1),Quantity=decimal.Parse(items.GetString(2),CultureInfo.InvariantCulture),UnitCents=items.GetInt64(3)});return note;
    }
}
