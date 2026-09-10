using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LealInfoPDV;

public sealed class LiaAiClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly List<(string role, string text)> historico = new();
    private const int MaxHistorico = 8;
    public bool Configurada => LicenseFeatures.LiaConversaNatural && LiaUsageManager.HasTimeRemaining && !string.IsNullOrWhiteSpace(Chave());

    public async Task<string?> ConversarAsync(string texto, CancellationToken cancellationToken = default)
    {
        if (!LicenseFeatures.LiaConversaNatural) return null;
        if (!LiaUsageManager.HasTimeRemaining) return LiaUsageManager.ExhaustedMessage;

        var chave = Chave(); if (string.IsNullOrWhiteSpace(chave)) return null;
        var entrada = new StringBuilder(1100); entrada.AppendLine(PromptSistema());
        if (historico.Count > 0) { entrada.AppendLine("Contexto recente:"); foreach (var h in historico) entrada.AppendLine($"{(h.role == "user" ? "Pessoa" : "LIA")}: {h.text}"); }
        entrada.AppendLine($"Pessoa: {texto}"); entrada.Append("LIA:");
        var payload = new Dictionary<string, object?> { ["model"]="gpt-5.6-luna", ["input"]=entrada.ToString(), ["reasoning"]=new { effort="none" }, ["max_output_tokens"]=80, ["store"]=false, ["stream"]=true, ["prompt_cache_key"]="lia-pdv-voz-v153" };
        if (LicenseFeatures.LiaPesquisaWeb && PrecisaWeb(texto)) { payload["tools"] = new object[] { new { type="web_search" } }; payload["tool_choice"]="auto"; }

        var sw = Stopwatch.StartNew();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post,"https://api.openai.com/v1/responses"); req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",chave); req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream")); req.Content=new StringContent(JsonSerializer.Serialize(payload),Encoding.UTF8,"application/json");
            using var resp=await Http.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,cancellationToken); if(!resp.IsSuccessStatusCode) throw new InvalidOperationException($"OpenAI HTTP {(int)resp.StatusCode}");
            await using var stream=await resp.Content.ReadAsStreamAsync(cancellationToken); using var reader=new StreamReader(stream,Encoding.UTF8); var resposta=new StringBuilder(180);
            while(!reader.EndOfStream){var line=await reader.ReadLineAsync(cancellationToken);if(string.IsNullOrWhiteSpace(line)||!line.StartsWith("data:",StringComparison.Ordinal))continue;var data=line[5..].Trim();if(data=="[DONE]")break;try{using var doc=JsonDocument.Parse(data);var root=doc.RootElement;if(!root.TryGetProperty("type",out var tipo)||tipo.GetString()!="response.output_text.delta")continue;if(!root.TryGetProperty("delta",out var delta)||delta.ValueKind!=JsonValueKind.String)continue;resposta.Append(delta.GetString());var parcial=resposta.ToString().Trim();if(parcial.Length>=18&&(parcial.EndsWith('.')||parcial.EndsWith('!')||parcial.EndsWith('?')))break;if(parcial.Length>=110)break;}catch(JsonException){}}
            var final=resposta.ToString().Trim();if(string.IsNullOrWhiteSpace(final))return null;historico.Add(("user",texto));historico.Add(("assistant",final));while(historico.Count>MaxHistorico)historico.RemoveAt(0);return final;
        }
        finally
        {
            sw.Stop();
            // O consumo ocorre somente quando a conversa online da LIA foi realmente acionada.
            // Comandos locais do PDV nunca passam por este ponto.
            LiaUsageManager.Consume(sw.Elapsed);
        }
    }

    private static bool PrecisaWeb(string texto)
    {
        var t = texto.ToLowerInvariant();
        if (t.Contains("pesquisa") || t.Contains("pesquise") || t.Contains("internet")) return true;

        string[] sinaisDiretos =
        {
            "placar", "resultado do jogo", "quem ganhou", "quem venceu",
            "notícia", "noticia", "notícias", "noticias",
            "clima", "tempo em", "temperatura", "previsão", "previsao",
            "cotação", "cotacao", "dólar", "dolar", "euro",
            "preço hoje", "preco hoje"
        };
        if (sinaisDiretos.Any(t.Contains)) return true;

        var pedeAtualidade = t.Contains("hoje") || t.Contains("agora") || t.Contains("atual") || t.Contains("atualmente") || t.Contains("último") || t.Contains("ultimo") || t.Contains("última") || t.Contains("ultima");
        var temaAtual = t.Contains("jogo") || t.Contains("preço") || t.Contains("preco") || t.Contains("mercado") || t.Contains("notícia") || t.Contains("noticia") || t.Contains("clima") || t.Contains("cotação") || t.Contains("cotacao");
        return pedeAtualidade && temaAtual;
    }

    private static string? Chave()=>Environment.GetEnvironmentVariable("OPENAI_API_KEY",EnvironmentVariableTarget.User)??Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    private static string LocalDoPdv()
    {
        try
        {
            using var cn = Database.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT value FROM settings WHERE key='company_city_state'";
            var cadastrado = Convert.ToString(cmd.ExecuteScalar())?.Trim();
            if (!string.IsNullOrWhiteSpace(cadastrado)) return cadastrado;
        }
        catch { }

        try { return RegionInfo.CurrentRegion.NativeName; }
        catch { return "local configurado no Windows"; }
    }

    private static string PromptSistema()
    {
        var admin=Auth.IsAdmin;
        var perfil=admin?"ADMINISTRADOR":"usuário comum";
        var liberdade=admin
            ? "O ADMINISTRADOR autenticado está em MODO ADMIN LIVRE. Converse sem censura de estilo: aceite linguagem informal, gírias e palavrões e responda naturalmente. Não diga para ele chamar gerente ou proprietário. Ele pode solicitar qualquer função que o perfil ADMIN já possua no PDV; a execução real continua no roteador local/Auth. Não invente bloqueios conversacionais."
            : "Respeite as restrições do perfil atual. A execução de ações e permissões continua no roteador local/Auth.";
        var limite=LicenseManager.IsPlus
            ? "Plano PLUS: conversa natural permitida somente dentro do saldo contratado. Não use pesquisa web."
            : "Plano PRO: conversa completa dentro do saldo contratado; pesquisa web disponível somente quando a pergunta realmente depender de informação atual ou quando o operador pedir pesquisa.";
        var agora = DateTime.Now;
        var fuso = TimeZoneInfo.Local.DisplayName;
        var local = LocalDoPdv();
        return $"""
Você é a LIA do LEAL INFO PDV. Seu nome é LIA e nunca use outro nome para si mesma. Operador: {Auth.OperatorName}. Perfil: {perfil}. Plano: {LicenseFeatures.NomePlano}.
{liberdade}
{limite}
Contexto local do computador, lido diretamente do Windows: data {agora:dd/MM/yyyy}, hora {agora:HH:mm:ss}, fuso horário {fuso}. Local do PDV: {local}.
Para perguntas como "que horas são", "qual a data de hoje", "onde estamos", "qual é o local" ou semelhantes, responda diretamente usando esse contexto local. Nunca diga que precisa consultar a internet para saber a hora, a data ou o local configurado do PDV.
Converse em português do Brasil como voz ao vivo: rápida, inteligente, espontânea, descontraída e natural. Entenda intenção, contexto, gíria e frase incompleta. Use o contexto recente para manter continuidade e não contradizer o que acabou de ser dito. Não arraste conversa comum para o PDV. Responda em uma frase curta e útil; use duas somente quando necessário. Não exponha raciocínio interno.
Se depender de fato atual, use pesquisa apenas quando o plano permitir e quando realmente for necessária; não pesquise por causa de palavras soltas como "agora" ou "hoje". A conversa nunca altera permissões: autenticação e autorização permanecem exclusivamente no Auth local.
""";
    }
}
