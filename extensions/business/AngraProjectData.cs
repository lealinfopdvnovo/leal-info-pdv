using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace LealInfoPDV;

internal static class AngraProjectData
{
 internal static string Text(JsonNode? n)=>n?.ToString()??"";
 internal static string Summary(string s)=>s.Length<=240?s:s[..237]+"...";
 internal static string Decimal(decimal n)=>n.ToString("G29",CultureInfo.InvariantCulture);
 internal static decimal Number(string text,bool positive=false){if(!Regex.IsMatch(text,@"^[0-9]+(?:[.,][0-9]+)?$",RegexOptions.CultureInvariant)||!decimal.TryParse(text.Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var n)||n<0||n>1_000_000_000m||(positive&&n==0))throw new InvalidDataException("Número de integração inválido.");return n;}
 internal static decimal Money(JsonNode? n){string raw=Text(n);if(raw=="")return 0;if(!Regex.IsMatch(raw,@"^[0-9]+(?:\.[0-9]{1,2})?$",RegexOptions.CultureInvariant)||!GlassBrazilianMoney.TryParse(raw.Replace('.',','),out var amount)||amount<0||amount>1_000_000_000m)throw new InvalidDataException("Valor monetário canônico inválido.");return amount;}
 internal static void Validate(JsonElement raw){var p=JsonNode.Parse(raw.GetRawText())!.AsObject();if(string.IsNullOrWhiteSpace(Text(p["title"]))||Text(p["title"]).Length>160||string.IsNullOrWhiteSpace(Text(p["type"]))||Text(p["type"]).Length>100||p["measures"] is not JsonArray measures||measures.Count is <1 or >24)throw new InvalidDataException("Projeto do app inválido.");var labels=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var m in measures){string label=Text(m?["label"]);if(label.Trim()==""||!labels.Add(label))throw new InvalidDataException("Dimensão sem nome único.");decimal mm=GlassDimensionUnits.ToMillimetres(Number(Text(m?["value"]),true),Text(m?["unit"]));if(mm!=Number(Text(m?["mm"]),true)||mm>1_000_000m)throw new InvalidDataException("Dimensão física inconsistente.");}if(p["budget"] is JsonObject b)foreach(string k in new[]{"price","service","discount","extra"})Money(b[k]);}
 internal static GlassSavedProject Import(JsonObject p,Func<string,string,long> official)
 {
  var raw=JsonSerializer.SerializeToElement(p);Validate(raw);var measures=(JsonArray)p["measures"]!;
  (decimal Mm,string Unit) Dimension(string label){var m=measures.FirstOrDefault(x=>string.Equals(Text(x?["label"]),label,StringComparison.OrdinalIgnoreCase));return m==null?(0m,"mm"):(Number(Text(m["mm"]),true),Text(m["unit"]));}
  var w=Dimension("Largura");var h=Dimension("Altura");var budget=p["budget"] as JsonObject??new();int quantity=1;if(budget["quantity"]!=null&&(!int.TryParse(Text(budget["quantity"]),out quantity)||quantity is <1 or >1000))throw new InvalidDataException("Quantidade comercial inválida.");
  string client=Text(p["clientLocalId"]);long customer=client==""?0:official("clients",client);string customerName="";if(customer>0){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="SELECT name FROM customers WHERE id=$id";q.Parameters.AddWithValue("$id",customer);customerName=Convert.ToString(q.ExecuteScalar())??throw new InvalidDataException("Cliente vinculado não encontrado.");}
  var links=new List<GlassMaterialLink>();foreach(var id in p["materialLocalIds"] as JsonArray??new()){long product=official("materials",Text(id));if(product==0)continue;using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="SELECT name,COALESCE(barcode,''),COALESCE(category,''),price FROM products WHERE id=$id";q.Parameters.AddWithValue("$id",product);using var r=q.ExecuteReader();if(r.Read())links.Add(new(product,r.GetString(0),r.GetString(1),r.GetString(2),Convert.ToDecimal(r.GetValue(3),CultureInfo.InvariantCulture)));}
  GlassSavedCut? cut=null;if(p["cut"] is JsonObject c&&!string.IsNullOrWhiteSpace(Text(c["width"]))&&!string.IsNullOrWhiteSpace(Text(c["height"]))){var pieces=new List<GlassSavedPiece>();foreach(var x in c["pieces"] as JsonArray??new()){string wu=Text(x?["widthUnit"]),hu=Text(x?["heightUnit"]);pieces.Add(new(Text(x?["id"]),Text(x?["description"]),GlassDimensionUnits.ToMillimetres(Number(Text(x?["width"]),true),wu),GlassDimensionUnits.ToMillimetres(Number(Text(x?["height"]),true),hu),wu,hu,int.Parse(Text(x?["quantity"]),CultureInfo.InvariantCulture),Text(x?["material"]),x?["allowRotation"]?.GetValue<bool>()??true));}string cu=Text(c["widthUnit"]),ch=Text(c["heightUnit"]);cut=new(GlassDimensionUnits.ToMillimetres(Number(Text(c["width"]),true),cu),GlassDimensionUnits.ToMillimetres(Number(Text(c["height"]),true),ch),cu,ch,c["allowRotation"]?.GetValue<bool>()??true,"",0,pieces.ToArray(),false);}
  string type=Text(p["type"]),model=Text(p["model"]);return new(2,model==""?type:model,Text(p["title"]),w.Mm,h.Mm,w.Unit,h.Unit,quantity,new(customer,customerName,Summary(Text(p["configuration"])),Money(budget["price"]),Money(budget["service"]),Money(budget["discount"]),Money(budget["extra"])),links.ToArray(),cut,true,type,raw);
 }
 internal static bool LegacyCompatible(GlassSavedProject p)=>p.AppProject==null||(p.ServiceType=="Janela"&&p.Model==GlassProject.ModelName);
 internal static GlassSavedProject PreserveNative(GlassSavedProject native,GlassSavedProject? previous)
 {
  if(previous?.AppProject is not JsonElement raw||!LegacyCompatible(previous))return native;
  var p=JsonNode.Parse(raw.GetRawText())!.AsObject();var mapping=AngraSyncService.Current?.Store??throw new InvalidOperationException("Aguarde a inicialização do vínculo do app antes de salvar este projeto.");var fresh=Export(new GlassStoredProject("",0,"","",native),mapping.Uuid);
  foreach(string key in new[]{"title","configuration","clientLocalId"})p[key]=fresh[key]?.DeepClone();
  var oldMeasures=p["measures"]!.AsArray();foreach(var item in fresh["measures"]!.AsArray()){var old=oldMeasures.FirstOrDefault(m=>Text(m?["label"])==Text(item?["label"]));if(old==null)oldMeasures.Add(item!.DeepClone());else if(Number(Text(old["mm"]))!=Number(Text(item?["mm"]))||Text(old["unit"])!=Text(item?["unit"])){old["value"]=item?["value"]?.DeepClone();old["unit"]=item?["unit"]?.DeepClone();old["mm"]=item?["mm"]?.DeepClone();}}
  var b=p["budget"] as JsonObject??new JsonObject();foreach(string k in new[]{"quantity","price","service","discount","extra"})b[k]=fresh["budget"]?[k]?.DeepClone();p["budget"]=b;
  if(JsonSerializer.Serialize(native.Cut)!=JsonSerializer.Serialize(previous.Cut))p["cut"]=fresh["cut"]?.DeepClone();
  if(JsonSerializer.Serialize(native.Materials)!=JsonSerializer.Serialize(previous.Materials)){var selected=fresh["materialLocalIds"]!.AsArray();foreach(var id in p["materialLocalIds"] as JsonArray??new())if(mapping.Official("materials",Text(id))==0)selected.Add(id?.DeepClone());p["materialLocalIds"]=selected.DeepClone();}
  return native with{FormatVersion=2,ServiceType=previous.ServiceType,AppProject=JsonSerializer.SerializeToElement(p)};
 }
 internal static JsonObject Export(GlassStoredProject saved,Func<string,string,string> uuid)
 {
  var d=saved.Data;if(d.AppProject is JsonElement raw)return JsonNode.Parse(raw.GetRawText())!.AsObject();
  JsonObject Measure(string label,decimal mm,string u)=>new(){["label"]=label,["value"]=Decimal(mm/GlassDimensionUnits.Factor(u)),["unit"]=u,["mm"]=Decimal(mm)};
  var measures=new JsonArray(Measure("Largura",d.WidthMm,d.WidthUnit),Measure("Altura",d.HeightMm,d.HeightUnit));var materialIds=new JsonArray();foreach(var m in d.Materials)materialIds.Add(uuid("materials",m.ProductId.ToString(CultureInfo.InvariantCulture)));
  var p=new JsonObject{["title"]=d.Description,["type"]="Janela",["model"]=d.Model,["configuration"]=d.Quote.Service,["clientLocalId"]=d.Quote.CustomerId>0?uuid("clients",d.Quote.CustomerId.ToString(CultureInfo.InvariantCulture)):"",["measures"]=measures,["materialLocalIds"]=materialIds,["budget"]=new JsonObject{["quantity"]=d.Quantity.ToString(),["price"]=Decimal(d.Quote.Price),["service"]=Decimal(d.Quote.ServiceValue),["discount"]=Decimal(d.Quote.Discount),["extra"]=Decimal(d.Quote.Addition)}};
  if(d.Cut is{} c){var pieces=new JsonArray();foreach(var x in c.Pieces)pieces.Add(new JsonObject{["id"]=x.Id,["description"]=x.Description,["width"]=Decimal(x.WidthMm/GlassDimensionUnits.Factor(x.WidthUnit)),["height"]=Decimal(x.HeightMm/GlassDimensionUnits.Factor(x.HeightUnit)),["widthUnit"]=x.WidthUnit,["heightUnit"]=x.HeightUnit,["quantity"]=x.Quantity,["material"]=x.Material,["allowRotation"]=x.Rotation});p["cut"]=new JsonObject{["width"]=Decimal(c.WidthMm/GlassDimensionUnits.Factor(c.WidthUnit)),["height"]=Decimal(c.HeightMm/GlassDimensionUnits.Factor(c.HeightUnit)),["widthUnit"]=c.WidthUnit,["heightUnit"]=c.HeightUnit,["allowRotation"]=c.AllowRotation,["pieces"]=pieces};}return p;
 }
}
