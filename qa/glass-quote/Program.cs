using LealInfoPDV;
int count = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); count++; }
foreach (var item in new[] { (1200m,1000m,1,1.2m), (1800m,1000m,1,1.8m), (1000m,1500m,2,3m) })
{
    var q = new GlassQuote(new("Projeto",item.Item1,item.Item2,item.Item3),100m,50m,10m,5m);
    Check(q.ValidationError == null, "Medidas válidas " + item);
    Check(q.TotalArea == item.Item4, "Conversão mm → m² e quantidade " + item);
    Check(q.MaterialValue == item.Item4 * 100m, "Preço por m² " + item);
    Check(q.Total == item.Item4 * 100m + 45m, "Serviço total/desconto/acréscimo " + item);
}
var start = new GlassQuote(new("A",1200,1000,1),100,0,0,0);
var updated = start with { Project = start.Project with { WidthMm = 1800 } };
Check(updated.Total == 180 && updated.TotalArea == 1.8m, "Atualização de medidas recalcula orçamento");
Check(new GlassQuote(new("",0,1000,1),1,0,0,0).ValidationError != null, "Largura zero recusada");
Check(new GlassQuote(new("",1000,-1,1),1,0,0,0).ValidationError != null, "Altura negativa recusada");
Check(new GlassQuote(new("",1000,1000,0),1,0,0,0).ValidationError != null, "Quantidade zero recusada");
Check(start with { PricePerSquareMeter = -1 } is { ValidationError: not null }, "Preço negativo recusado");
Check(start with { Discount = 121 } is { ValidationError: not null }, "Total negativo recusado");
Check(start with { Discount = 121, Addition = 1 } is { ValidationError: null, Total: 0 }, "Desconto limitado ao total disponível");
Check(new GlassQuote(new("",1001,1001,1),1.5m,0,0,0).MaterialValue == 1.50m, "Arredondamento monetário após multiplicação");
Console.WriteLine($"TOTAL: {count} PASS");
