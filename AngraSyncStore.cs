using System.Globalization;
using System.Text.Json.Nodes;
using LealInfoPDV.Network;
namespace LealInfoPDV;

internal sealed record AngraLink(string Entity,string Id,string? LocalId,long Revision,JsonObject? Baseline,JsonObject? Pending,JsonObject? Conflict,string Error);
internal sealed class AngraSyncStore
{
 internal string Tenant{get;}
 internal AngraSyncStore(string tenant){if(!System.Text.RegularExpressions.Regex.IsMatch(tenant,@"^(?:[a-f0-9]{64}|test-[0-9]+(?:-other)?)$"))throw new InvalidDataException("Empresa de integração inválida.");Tenant=tenant;EnsureReady();}
 internal void EnsureReady()
 {
  if(NetworkConfiguration.Current.Mode=="terminal")throw new InvalidOperationException("Configure a integração no servidor do PDV.");
  using var db=Database.Open();using(var check=db.CreateCommand()){check.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE name='angra_sync_links_v1' AND type='table'";if(Convert.ToInt32(check.ExecuteScalar())==1){using var scope=db.CreateCommand();scope.CommandText="SELECT COUNT(*) FROM angra_sync_links_v1 WHERE tenant<>$t";scope.Parameters.AddWithValue("$t",Tenant);if(Convert.ToInt64(scope.ExecuteScalar())>0)throw new InvalidOperationException("Este banco já pertence a outro vínculo. Nenhum dado foi misturado.");return;}}
  Directory.CreateDirectory(Database.BackupFolder);string backup=Path.Combine(Database.BackupFolder,"ANTES_ANGRA_SYNC_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N")+".db");using(var copy=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+backup+";Pooling=False")){copy.Open();db.BackupDatabase(copy);}
  using var tx=db.BeginTransaction();using var q=db.CreateCommand();q.Transaction=tx;q.CommandText="""
   CREATE TABLE IF NOT EXISTS angra_sync_links_v1(
    tenant TEXT NOT NULL,entity TEXT NOT NULL,id TEXT NOT NULL,local_id TEXT,
    revision INTEGER NOT NULL DEFAULT 0,baseline TEXT,pending TEXT,conflict TEXT,error TEXT NOT NULL DEFAULT '',
    PRIMARY KEY(tenant,entity,id),UNIQUE(tenant,entity,local_id));
   """;q.ExecuteNonQuery();tx.Commit();
 }
 internal IReadOnlyList<AngraLink> Links(){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="SELECT entity,id,local_id,revision,baseline,pending,conflict,error FROM angra_sync_links_v1 WHERE tenant=$t ORDER BY entity,id";q.Parameters.AddWithValue("$t",Tenant);using var r=q.ExecuteReader();var rows=new List<AngraLink>();while(r.Read())rows.Add(new(r.GetString(0),r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),r.GetInt64(3),Parse(r.IsDBNull(4)?null:r.GetString(4)),Parse(r.IsDBNull(5)?null:r.GetString(5)),Parse(r.IsDBNull(6)?null:r.GetString(6)),r.GetString(7)));return rows;}
 static JsonObject? Parse(string? s)=>s==null?null:JsonNode.Parse(s)!.AsObject();
 internal AngraLink Link(string entity,string id)=>Links().FirstOrDefault(x=>x.Entity==entity&&x.Id==id)??new(entity,id,null,0,null,null,null,"");
 internal void Save(AngraLink l){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="""
  INSERT INTO angra_sync_links_v1(tenant,entity,id,local_id,revision,baseline,pending,conflict,error) VALUES($t,$e,$id,$local,$r,$b,$p,$c,$error)
  ON CONFLICT(tenant,entity,id) DO UPDATE SET local_id=excluded.local_id,revision=excluded.revision,baseline=excluded.baseline,pending=excluded.pending,conflict=excluded.conflict,error=excluded.error
  """;q.Parameters.AddWithValue("$t",Tenant);q.Parameters.AddWithValue("$e",l.Entity);q.Parameters.AddWithValue("$id",l.Id);q.Parameters.AddWithValue("$local",(object?)l.LocalId??DBNull.Value);q.Parameters.AddWithValue("$r",l.Revision);q.Parameters.AddWithValue("$b",(object?)l.Baseline?.ToJsonString()??DBNull.Value);q.Parameters.AddWithValue("$p",(object?)l.Pending?.ToJsonString()??DBNull.Value);q.Parameters.AddWithValue("$c",(object?)l.Conflict?.ToJsonString()??DBNull.Value);q.Parameters.AddWithValue("$error",l.Error);q.ExecuteNonQuery();}
 internal string Uuid(string entity,string local){var l=Links().FirstOrDefault(x=>x.Entity==entity&&x.LocalId==local);if(l!=null)return l.Id;string id=Guid.NewGuid().ToString();Save(new(entity,id,local,0,null,null,null,""));return id;}
 internal static string? LocalNumber(string? id)=>id!=null&&id.StartsWith("alias:",StringComparison.Ordinal)?id.Split(':')[1]:id;
 internal long Official(string entity,string id){var l=Link(entity,id);return long.TryParse(LocalNumber(l.LocalId),out var n)?n:0;}
 internal JsonObject? ReadLocal(AngraLink l)
 {
  if(l.LocalId==null)return null;
  if(l.Entity=="projects"){try{return AngraProjectData.Export(GlassProjectStore.Load(l.LocalId),Uuid);}catch(InvalidOperationException){return null;}}
  using var db=Database.Open();using var q=db.CreateCommand();string table=l.Entity switch{"clients"=>"customers","suppliers"=>"suppliers","materials"=>"products",_=>throw new InvalidDataException("Entidade inválida.")};q.CommandText=l.Entity=="materials"?"SELECT name,COALESCE(barcode,''),COALESCE(category,''),price,stock,active FROM products WHERE id=$id":"SELECT name,COALESCE(document,''),COALESCE(phone,''),COALESCE(email,''),COALESCE(address,'') FROM "+table+" WHERE id=$id";q.Parameters.AddWithValue("$id",long.Parse(LocalNumber(l.LocalId)!,CultureInfo.InvariantCulture));using var r=q.ExecuteReader();if(!r.Read())return null;
  if(l.Entity=="materials")return new(){["name"]=r.GetString(0),["barcode"]=r.GetString(1),["category"]=r.GetString(2),["price"]=AngraProjectData.Decimal(Convert.ToDecimal(r.GetValue(3),CultureInfo.InvariantCulture)),["stock"]=AngraProjectData.Decimal(Convert.ToDecimal(r.GetValue(4),CultureInfo.InvariantCulture)),["active"]=r.GetInt32(5)==1,["specification"]="Cadastro oficial do PDV"};
  var p=new JsonObject{["name"]=r.GetString(0),["document"]=r.GetString(1),["phone"]=r.GetString(2),["email"]=r.GetString(3),["address"]=r.GetString(4)};
  if(l.Baseline!=null&&AngraProjectData.Text(l.Baseline["address"])==r.GetString(4))foreach(string k in new[]{"cep","street","number","complement","district","city","uf"})if(l.Baseline[k]!=null)p[k]=l.Baseline[k]!.DeepClone();
  return p;
 }
 internal void Discover()
 {
  foreach(var (entity,table) in new[]{("clients","customers"),("suppliers","suppliers"),("materials","products")}){var ids=new List<long>();using(var db=Database.Open()){using var q=db.CreateCommand();q.CommandText="SELECT id FROM "+table+" WHERE id>0";using var r=q.ExecuteReader();while(r.Read())ids.Add(r.GetInt64(0));}foreach(long id in ids)Uuid(entity,id.ToString(CultureInfo.InvariantCulture));}
  GlassProjectStore.EnsureReady();foreach(var p in GlassProjectStore.List())Uuid("projects",p.Id);
 }
 internal JsonArray Prepare()
 {
  Discover();var changes=new JsonArray();int bytes=0;foreach(var l in Links()){
   if(l.Conflict!=null)continue;var payload=ReadLocal(l);if(payload==null){if(l.Baseline!=null)Save(l with{Error="Registro excluído no PDV. Exclusão remota exige revisão; não foi recriado."});continue;}
   JsonObject? pending=l.Pending;if(pending==null&&!JsonNode.DeepEquals(payload,l.Baseline)){pending=new(){["entity"]=l.Entity,["id"]=l.Id,["baseRevision"]=l.Revision,["operationId"]=Guid.NewGuid().ToString(),["payload"]=payload,["officialId"]=LocalNumber(l.LocalId)};Save(l with{Pending=pending,Error=""});}
   if(pending!=null&&changes.Count<20&&bytes+pending.ToJsonString().Length<3_000_000){changes.Add(pending.DeepClone());bytes+=pending.ToJsonString().Length;}
  }return changes;
 }
 internal void Apply(JsonObject response)
 {
  if(AngraProjectData.Text(response["tenant"])!=Tenant)throw new InvalidOperationException("Resposta de outro PDV recusada.");
  foreach(var item in response["results"] as JsonArray??new()){
   var r=item!.AsObject();var l=Link(AngraProjectData.Text(r["entity"]),AngraProjectData.Text(r["id"]));if(r["ok"]?.GetValue<bool>()==true){var rec=r["record"]!.AsObject();Save(l with{Revision=rec["revision"]!.GetValue<long>(),Baseline=rec["payload"]!.DeepClone().AsObject(),Pending=null,Error=""});}else if(r["conflict"]?.GetValue<bool>()==true)Save(l with{Conflict=r["record"]?.DeepClone().AsObject(),Error="Alterações concorrentes preservadas. Revise as duas versões."});else Save(l with{Error=AngraProjectData.Text(r["error"])});
  }
  var records=(response["records"] as JsonArray??new()).OrderBy(x=>AngraProjectData.Text(x?["entity"])=="projects"?1:0);
  foreach(var item in records){var r=item!.AsObject();string entity=AngraProjectData.Text(r["entity"]),id=AngraProjectData.Text(r["id"]);if(!new[]{"clients","suppliers","materials","projects"}.Contains(entity))continue;var l=Link(entity,id);if(r["revision"]!.GetValue<long>()<=l.Revision)continue;var remote=r["payload"]!.AsObject();var current=ReadLocal(l);if(l.Pending!=null||current!=null&&!JsonNode.DeepEquals(current,l.Baseline)&&!JsonNode.DeepEquals(current,remote)){Save(l with{Conflict=r.DeepClone().AsObject(),Error="Conflito: versão local e remota preservadas."});continue;}
   try{string local=ApplyRecord(l,remote,current);Save(l with{LocalId=local,Revision=r["revision"]!.GetValue<long>(),Baseline=remote.DeepClone().AsObject(),Pending=null,Conflict=null,Error=""});}
   catch(Exception e){Save(l with{Conflict=r.DeepClone().AsObject(),Error=e.Message});}
  }
 }
 internal string ApplyRecord(AngraLink l,JsonObject p,JsonObject? expected)
 {
  if(l.Entity=="materials")throw new InvalidOperationException("Produtos só podem ser publicados por este PDV.");
  if(l.LocalId!=null&&expected==null)throw new InvalidOperationException("Registro excluído localmente. Não será recriado silenciosamente.");
  if(l.Entity=="projects"){
   GlassStoredProject? previous=l.LocalId==null?null:GlassProjectStore.Load(l.LocalId);
   if(expected!=null&&previous!=null&&!JsonNode.DeepEquals(AngraProjectData.Export(previous,Uuid),expected))throw new InvalidOperationException("Projeto mudou durante a sincronização.");
   var data=AngraProjectData.Import(p,(entity,id)=>{long official=Official(entity,id);if(official==0&&entity=="clients")throw new InvalidOperationException("Cliente do projeto ainda pendente de vínculo no PDV.");return official;});if(previous!=null&&JsonNode.DeepEquals(AngraProjectData.Export(previous,Uuid),p))return previous.Id;
   return GlassProjectStore.SaveImported(data,previous,(db,tx,id)=>{using var map=db.CreateCommand();map.Transaction=tx;map.CommandText="INSERT INTO angra_sync_links_v1(tenant,entity,id,local_id) VALUES($t,'projects',$id,$local) ON CONFLICT(tenant,entity,id) DO UPDATE SET local_id=excluded.local_id";map.Parameters.AddWithValue("$t",Tenant);map.Parameters.AddWithValue("$id",l.Id);map.Parameters.AddWithValue("$local",id);map.ExecuteNonQuery();}).Id;
  }
  string table=l.Entity=="clients"?"customers":"suppliers";using var db=Database.Open();using var tx=db.BeginTransaction();
  // Compare the exact database columns inside the same transaction before changing anything.
  if(l.LocalId!=null&&expected!=null){using var check=db.CreateCommand();check.Transaction=tx;check.CommandText="SELECT name,COALESCE(document,''),COALESCE(phone,''),COALESCE(email,''),COALESCE(address,'') FROM "+table+" WHERE id=$id";check.Parameters.AddWithValue("$id",long.Parse(LocalNumber(l.LocalId)!));using var r=check.ExecuteReader();if(!r.Read())throw new InvalidOperationException("Cadastro excluído durante a sincronização.");string[] keys={"name","document","phone","email","address"};for(int i=0;i<keys.Length;i++)if(r.GetString(i)!=AngraProjectData.Text(expected[keys[i]]))throw new InvalidOperationException("Cadastro mudou durante a sincronização. Dados preservados.");}
  string document=AngraProjectData.Text(p["document"]),normalized=System.Text.RegularExpressions.Regex.Replace(document,"[^0-9A-Za-z]","");if(normalized!=""){using var duplicate=db.CreateCommand();duplicate.Transaction=tx;duplicate.CommandText="SELECT id,COALESCE(document,'') FROM "+table;using var r=duplicate.ExecuteReader();while(r.Read())if(r.GetInt64(0).ToString()!=LocalNumber(l.LocalId)&&System.Text.RegularExpressions.Regex.Replace(r.GetString(1),"[^0-9A-Za-z]","").Equals(normalized,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Documento já pertence ao cadastro PDV "+r.GetInt64(0)+". Vínculo deve ser conciliado; nenhum duplicado foi criado.");}
  using var q=db.CreateCommand();q.Transaction=tx;q.CommandText=l.LocalId==null?"INSERT INTO "+table+"(name,document,phone,email,address) VALUES($n,$d,$p,$e,$a);SELECT last_insert_rowid();":"UPDATE "+table+" SET name=$n,document=$d,phone=$p,email=$e,address=$a WHERE id=$id;SELECT $id;";q.Parameters.AddWithValue("$n",AngraProjectData.Text(p["name"]));q.Parameters.AddWithValue("$d",document);q.Parameters.AddWithValue("$p",AngraProjectData.Text(p["phone"]));q.Parameters.AddWithValue("$e",AngraProjectData.Text(p["email"]));q.Parameters.AddWithValue("$a",AngraProjectData.Text(p["address"]));q.Parameters.AddWithValue("$id",l.LocalId==null?0:long.Parse(LocalNumber(l.LocalId)!));string resultId=Convert.ToString(q.ExecuteScalar(),CultureInfo.InvariantCulture)!;string local=l.LocalId??resultId;
  // Persist identity in the same transaction as the actual customer/supplier insertion (crash-safe retry).
  using var identity=db.CreateCommand();identity.Transaction=tx;identity.CommandText="INSERT INTO angra_sync_links_v1(tenant,entity,id,local_id,revision,baseline) VALUES($t,$e,$id,$local,0,NULL) ON CONFLICT(tenant,entity,id) DO UPDATE SET local_id=excluded.local_id";identity.Parameters.AddWithValue("$t",Tenant);identity.Parameters.AddWithValue("$e",l.Entity);identity.Parameters.AddWithValue("$id",l.Id);identity.Parameters.AddWithValue("$local",local);identity.ExecuteNonQuery();tx.Commit();return local;
 }
 internal void LinkExisting(string entity,string id,long localId)
 {
  if(entity is not ("clients" or "suppliers"))throw new InvalidOperationException("Somente cadastros correspondentes podem ser vinculados.");var l=Link(entity,id);if(l.Conflict==null)throw new InvalidOperationException("Não há conflito para conciliar.");using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="SELECT COUNT(*) FROM "+(entity=="clients"?"customers":"suppliers")+" WHERE id=$id";q.Parameters.AddWithValue("$id",localId);if(Convert.ToInt32(q.ExecuteScalar())!=1)throw new InvalidOperationException("Cadastro não encontrado.");Directory.CreateDirectory(Database.BackupFolder);File.WriteAllText(Path.Combine(Database.BackupFolder,"ANGRA_VINCULO_"+Guid.NewGuid().ToString("N")+".json"),l.Conflict.ToJsonString());Save(l with{LocalId="alias:"+localId+":"+id,Revision=l.Conflict["revision"]!.GetValue<long>(),Baseline=l.Conflict["payload"]!.DeepClone().AsObject(),Pending=null,Conflict=null,Error=""});
 }
 internal void Resolve(string entity,string id,bool remote)
 {
  var l=Link(entity,id);if(l.Conflict==null)return;string backup=Path.Combine(Database.BackupFolder,"ANGRA_CONFLITO_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N")+".json");Directory.CreateDirectory(Database.BackupFolder);File.WriteAllText(backup,new JsonObject{["local"]=ReadLocal(l),["remote"]=l.Conflict.DeepClone()}.ToJsonString());
  var rec=l.Conflict;string? local=l.LocalId;if(remote)local=ApplyRecord(l,rec["payload"]!.AsObject(),ReadLocal(l));Save(l with{LocalId=local,Revision=rec["revision"]!.GetValue<long>(),Baseline=rec["payload"]!.DeepClone().AsObject(),Pending=null,Conflict=null,Error=""});
 }
}
