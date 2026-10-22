using System.Net;
using System.Text;
using System.Text.Json;
namespace Leal.Diagnostics;
internal static class Report
{
    internal static readonly JsonSerializerOptions JsonOptions=new(){WriteIndented=true};
    internal static IEnumerable<string> Findings(Session session)
    {
        var valid=session.Samples.Where(s=>s.ProcessAvailable).ToArray();var cpu=valid.Where(s=>s.PdvCpu.HasValue).Select(s=>s.PdvCpu!.Value).ToArray();
        if(valid.Length<10)yield return "Coleta do PDV insuficiente para avaliar tendência: acompanhe o programa aberto durante uma sessão maior.";
        if(valid.Length>=10&&cpu.Length>0&&cpu.Average()>=65)yield return "CPU média do PDV elevada nesta coleta. Revisar redesenho de telas, imagens e tarefas repetidas; o relatório não identifica a função responsável.";
        if(session.Samples.Count>=10&&session.Samples.Count(s=>s.AvailableRam<session.Computer.TotalRam*.15)>=session.Samples.Count*.5)yield return "Pouca RAM livre em pelo menos metade das amostras. Verificar também os demais programas; não atribuir toda a pressão ao PDV.";
        var memory=valid.Where(s=>s.PrivateBytes.HasValue).ToArray();
        if(memory.Length>=60&&memory[^1].Seconds-memory[0].Seconds>=120)
        {
            double first=memory.Take(10).Average(s=>(double)s.PrivateBytes!.Value),last=memory.TakeLast(10).Average(s=>(double)s.PrivateBytes!.Value);
            if(last-first>50*1048576&&last>first*1.2)yield return "Memória privada cresceu mais de 50 MB e 20% entre as médias inicial/final. É uma tendência observada, não comprovação de vazamento; repetir o mesmo fluxo e inspecionar o código.";
        }
        if(session.Files.Groups.Where(g=>new[]{".jpg",".png",".jpeg",".webp",".bmp"}.Contains(g.Extension)).Sum(g=>g.Bytes)>20*1048576)yield return "Imagens têm participação relevante na pasta observada. Avaliar miniaturas, carregamento sob demanda e cache; preservar as fotos originais e o acabamento visual.";
        if(session.Files.SameSizeImages.Count>0)yield return "Há imagens com mesmo tamanho e extensão. Isso não confirma duplicação: comparar o conteúdo antes de qualquer decisão.";
        if(valid.Length>=10&&cpu.Length>0&&cpu.Average()<65)yield return "A CPU média do PDV ficou abaixo do limiar de triagem nesta coleta. Isso não comprova ausência de travamentos ou lentidão pontual.";
        yield return "Tamanho no disco não mede desempenho. Bibliotecas do .NET e DLLs não devem ser removidas com base apenas neste inventário.";
        yield return "Otimizações de banco, sincronização Firebase e tempo de resposta das telas exigem análise do código ou instrumentação complementar. Nenhuma limpeza foi executada.";
    }
    internal static string Html(Session d)
    {
        string E(object? value)=>WebUtility.HtmlEncode(Convert.ToString(value)??"indisponível");
        var b=new StringBuilder("<!doctype html><html lang='pt-BR'><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>Diagnóstico LEAL INFO PDV</title><style>body{background:#0c1118;color:#ecedf1;font:16px system-ui;margin:32px;max-width:1250px}h1,h2{color:#efca71}section{background:#151e2a;padding:20px;margin:18px 0;border:1px solid #4e4531;border-radius:12px}table{border-collapse:collapse;width:100%;font-size:14px}td,th{padding:9px;text-align:left;border-bottom:1px solid #344252;overflow-wrap:anywhere}small{color:#bac3ce}li{margin:10px 0}a{color:#f5d77e}@media print{body,section{background:white;color:black}h1,h2{color:#664715}table{font-size:10px}section{break-inside:avoid}}</style><body><h1>LEAL INFO · Diagnóstico do PDV</h1>");
        b.Append($"<p>Coleta local · {E(d.Started.ToString("g"))} · ferramenta {E(d.ToolVersion)} · {(d.Interrupted?"interrompida pelo usuário":"finalizada")}</p><p>Somente análise. Sem alterações no PDV, banco, licença ou configurações. Este HTML funciona sem internet e pode ser impresso pelo navegador.</p>");
        b.Append($"<section><h2>Computador e escopo</h2><p>{E(d.Computer.Windows)} · {E(d.Computer.Architecture)}<br>{E(d.Computer.Processor)} · {d.Computer.LogicalProcessors} processadores lógicos<br>RAM física: {E(Units.Size(d.Computer.TotalRam))}<br>Discos: {E(d.Computer.Drives)}<br>RAM da ferramenta ao iniciar: {E(Units.Size(d.Computer.ToolWorkingSet))}<br>Pasta analisada: {E(d.Files.Folder)} · versão do arquivo: {E(d.Files.Version)}</p><p>{E(d.Samples.Count)} amostras; {E(d.Samples.Count(s=>s.ProcessAvailable))} com processo acessível; duração observada: {E(d.Samples.LastOrDefault()?.Seconds.ToString("N0"))} s.</p></section>");
        b.Append("<section><h2>Medições por etapa marcada</h2><small>As etapas são informadas pelo operador; não são tempos automáticos de transação.</small><table><tr><th>Etapa</th><th>Amostras PDV</th><th>CPU média / pico PDV</th><th>CPU média PC</th><th>RAM física mínima livre</th><th>Pico RAM PDV / privada</th><th>Pico TCP PDV</th></tr>");
        foreach(var g in d.Samples.GroupBy(s=>s.Stage))
        {
            var cpu=g.Where(s=>s.PdvCpu.HasValue).Select(s=>s.PdvCpu!.Value).ToArray();var system=g.Where(s=>s.SystemCpu.HasValue).Select(s=>s.SystemCpu!.Value).ToArray();var working=g.Where(s=>s.WorkingSet.HasValue).Select(s=>(double)s.WorkingSet!.Value).ToArray();var priv=g.Where(s=>s.PrivateBytes.HasValue).Select(s=>(double)s.PrivateBytes!.Value).ToArray();var tcp=g.Where(s=>s.TcpConnections.HasValue).Select(s=>s.TcpConnections!.Value).ToArray();
            b.Append($"<tr><td>{E(g.Key)}</td><td>{g.Count(s=>s.ProcessAvailable)}</td><td>{E(Units.Metric(cpu.Length>0?cpu.Average():null))} / {E(Units.Metric(cpu.Length>0?cpu.Max():null))}</td><td>{E(Units.Metric(system.Length>0?system.Average():null))}</td><td>{E(Units.Size(g.Min(s=>s.AvailableRam)))}</td><td>{E(working.Length>0?Units.Size(working.Max()):null)} / {E(priv.Length>0?Units.Size(priv.Max()):null)}</td><td>{E(tcp.Length>0?tcp.Max().ToString():null)}</td></tr>");
        }
        b.Append("</table></section><section><h2>Rede e E/S</h2>");
        long received=0,sent=0;ulong read=0,write=0;int ioPairs=0;
        for(int i=1;i<d.Samples.Count;i++){var a=d.Samples[i-1];var z=d.Samples[i];received+=Math.Max(0,z.NetworkReceived-a.NetworkReceived);sent+=Math.Max(0,z.NetworkSent-a.NetworkSent);if(a.Pid==z.Pid&&a.Pid.HasValue&&z.PdvCpu.HasValue&&a.ReadBytes.HasValue&&z.ReadBytes.HasValue&&a.WriteBytes.HasValue&&z.WriteBytes.HasValue){ioPairs++;if(z.ReadBytes>=a.ReadBytes)read+=z.ReadBytes!.Value-a.ReadBytes!.Value;if(z.WriteBytes>=a.WriteBytes)write+=z.WriteBytes!.Value-a.WriteBytes!.Value;}}
        b.Append($"<p>Adaptadores do computador: recebido {E(Units.Size(received))}; enviado {E(Units.Size(sent))}. Não atribuído ao PDV/Firebase.</p>");
        if(ioPairs>0)b.Append($"<p>E/S lógica observada do processo: leitura {E(Units.Size(read))}; escrita {E(Units.Size(write))}. {ioPairs} intervalos válidos; não representa disco físico. Reinícios e contadores indisponíveis são excluídos.</p>");
        else b.Append("<p>E/S do processo: indisponível, sem pares de contadores válidos.</p>");
        var ownCpu=d.Samples.Where(s=>s.AnalyzerCpu.HasValue).Select(s=>s.AnalyzerCpu!.Value).ToArray();b.Append($"<p>Impacto do analisador: CPU média {E(Units.Metric(ownCpu.Length>0?ownCpu.Average():null))}; pico de RAM {E(Units.Size(d.Samples.Count>0?d.Samples.Max(s=>s.AnalyzerWorkingSet):d.Computer.ToolWorkingSet))}. Incluído no consumo total do computador.</p></section>");
        b.Append($"<section><h2>Arquivos: {E(Units.Size(d.Files.Bytes))} observados</h2><p>{d.Files.Files} arquivos · {d.Files.Skipped} entradas ignoradas · {E(d.Files.Note)}</p><table><tr><th>Extensão</th><th>Arquivos</th><th>Tamanho</th></tr>");
        foreach(var g in d.Files.Groups.Take(25))b.Append($"<tr><td>{E(g.Extension)}</td><td>{g.Files}</td><td>{E(Units.Size(g.Bytes))}</td></tr>");
        b.Append("</table><h3>Maiores arquivos observados</h3><table><tr><th>Caminho relativo</th><th>Tamanho</th></tr>");foreach(var f in d.Files.Largest)b.Append($"<tr><td>{E(f.RelativeName)}</td><td>{E(Units.Size(f.Bytes))}</td></tr>");
        b.Append("</table><h3>Imagens com mesmo tamanho/extensão — não confirmadas como duplicadas</h3>");foreach(var g in d.Files.SameSizeImages)b.Append($"<p>{E(Units.Size(g.Bytes))} cada: {E(string.Join(" · ",g.Files))}</p>");
        b.Append("</section><section><h2>Triagem e próximos passos</h2><ul>");foreach(var f in Findings(d))b.Append($"<li>{E(f)}</li>");b.Append("</ul></section><section><h2>Limites da medição</h2><ul>");foreach(var l in d.Limitations)b.Append($"<li>{E(l)}</li>");
        foreach(var note in d.Samples.Where(s=>s.Note!=null).Select(s=>s.Note).Distinct())b.Append($"<li>{E(note)}</li>");
        b.Append("</ul><p>O relatório contém nomes de arquivos relativos e características do PC. Revise antes de compartilhar. Nenhum dado é enviado automaticamente.</p></section></body></html>");return b.ToString();
    }
    internal static void Save(Session d,string htmlPath,string? monitoredFolder)
    {
        string full=Path.GetFullPath(htmlPath);
        if(monitoredFolder!=null){string root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(monitoredFolder))+Path.DirectorySeparatorChar;if(full.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new IOException("Escolha uma pasta de destino fora da instalação do PDV.");}
        string jsonPath=Path.ChangeExtension(full,"json");
        if(File.Exists(jsonPath))throw new IOException("Já existe um JSON com esse nome. Escolha outro nome para preservar o relatório anterior.");
        string html=Html(d),json=JsonSerializer.Serialize(d,JsonOptions);
        File.WriteAllText(full,html,new UTF8Encoding(false));
        try{File.WriteAllText(jsonPath,json,new UTF8Encoding(false));}catch{throw new IOException("HTML salvo, mas não foi possível salvar o JSON. Confira o destino antes de repetir.");}
    }
}
