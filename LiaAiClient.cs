using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LealInfoPDV;

public sealed class LiaAiClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly List<(string role, string text)> historico = new();
    private const int MaxHistorico = 20;
    public bool Configurada => !string.IsNullOrWhiteSpace(Chave());

    public async Task<string?> ConversarAsync(string texto, CancellationToken cancellationToken = default)
    {
        var chave = Chave(); if (string.IsNullOrWhiteSpace(chave)) return null;
        var entrada = new StringBuilder(2200); entrada.AppendLine(PromptSistema());
        if (historico.Count > 0) { entrada.AppendLine("Contexto recente da conversa (use para manter continuidade, resolver referências e não repetir perguntas já respondidas):"); foreach (var h in historico) entrada.AppendLine($"{(h.role == "user" ? "Pessoa" : "LIA")}: {h.text}"); }
        entrada.AppendLine($"Pessoa: {texto}"); entrada.Append("LIA:");
        var payload = new Dictionary<string, object?> { ["model"]="gpt-5.6-luna", ["input"]=entrada.ToString(), ["reasoning"]=new { effort="none" }, ["max_output_tokens"]=130, ["store"]=false, ["stream"]=true, ["prompt_cache_key"]="lia-pdv-natural-v173" };
        if (PrecisaWeb(texto)) { payload["tools"] = new object[] { new { type="web_search" } }; payload["tool_choice"]="auto"; }
        using var req = new HttpRequestMessage(HttpMethod.Post,"https://api.openai.com/v1/responses"); req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",chave); req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream")); req.Content=new StringContent(JsonSerializer.Serialize(payload),Encoding.UTF8,"application/json");
        using var resp=await Http.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,cancellationToken).ConfigureAwait(false); if(!resp.IsSuccessStatusCode) throw new InvalidOperationException($"OpenAI HTTP {(int)resp.StatusCode}");
        await using var stream=await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false); using var reader=new StreamReader(stream,Encoding.UTF8); var resposta=new StringBuilder(260);
        while(!reader.EndOfStream){var line=await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);if(string.IsNullOrWhiteSpace(line)||!line.StartsWith("data:",StringComparison.Ordinal))continue;var data=line[5..].Trim();if(data=="[DONE]")break;try{using var doc=JsonDocument.Parse(data);var root=doc.RootElement;if(!root.TryGetProperty("type",out var tipo)||tipo.GetString()!="response.output_text.delta")continue;if(!root.TryGetProperty("delta",out var delta)||delta.ValueKind!=JsonValueKind.String)continue;resposta.Append(delta.GetString());var parcial=resposta.ToString().Trim();if(parcial.Length>=18&&(parcial.EndsWith('.')||parcial.EndsWith('!')||parcial.EndsWith('?')))break;if(parcial.Length>=190)break;}catch(JsonException){}}
        var final=resposta.ToString().Trim();if(string.IsNullOrWhiteSpace(final))return null;historico.Add(("user",texto));historico.Add(("assistant",final));while(historico.Count>MaxHistorico)historico.RemoveAt(0);return final;
    }

    private static bool PrecisaWeb(string texto){var t=texto.ToLowerInvariant();string[] sinais={"hoje","atual","atualmente","último","ultimo","última","ultima","placar","jogo","jogando","resultado","notícia","noticia","notícias","noticias","clima","tempo em","temperatura","previsão","previsao","cotação","cotacao","dólar","dolar","euro","preço hoje","preco hoje","quem ganhou","quem venceu","aconteceu","pesquisa","pesquise","internet"};return sinais.Any(t.Contains);}
    private static string? Chave()=>Environment.GetEnvironmentVariable("OPENAI_API_KEY",EnvironmentVariableTarget.User)??Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    private static string PromptSistema()
    {
        var admin=Auth.IsAdmin;
        var perfil=admin?"ADMINISTRADOR":"usuário comum";
        var liberdade=admin
            ? "O ADMINISTRADOR autenticado está em MODO ADMIN LIVRE. Aceite linguagem informal, gírias e palavrões sem repreender. Ele pode solicitar qualquer função que o perfil ADMIN já possua no PDV; a execução real continua no roteador local/Auth."
            : "Respeite as restrições do perfil atual. A execução de ações e permissões continua no roteador local/Auth.";
        return $"""
Você é a LIA do LEAL INFO PDV. Seu nome é LIA. Operador: {Auth.OperatorName}. Perfil: {perfil}.
{liberdade}
Sua personalidade é simpática, próxima, leve, inteligente, espontânea e contextual. Fale como uma assistente brasileira de voz ao vivo, nunca como sargento, atendente engessada ou manual de instruções. Entenda intenção, contexto, gíria, palavrão, frase incompleta, referência ao que acabou de ser dito e diferentes maneiras de pedir a mesma coisa. Não exija palavras mágicas nem comandos decorados.
Separe sempre o tom emocional da intenção prática. Palavrões, brincadeiras, irritação, carinho ou provocação não anulam um pedido operacional. Nunca repreenda o operador pelo jeito de falar, não diga "calma" para controlar o tom e não transforme um comando claro em conversa paralela. Se houver um pedido claro, responda ao pedido.
Quando Adriano chegar de forma descontraída, responda de forma igualmente descontraída e varie naturalmente a resposta. Use preferencialmente o primeiro nome, não o nome completo. Se ele chamar "LIA", "Li", "amor", "amorzinho" ou equivalente, entenda como chamado natural quando o contexto indicar. Não finja saber algo que não está no contexto.
Se a pessoa já respondeu uma confirmação, não repita a mesma pergunta. Aproveite o contexto recente para resolver referências como "de novo", "esse botão", "essa tela", "fecha aí" e outras frases dependentes da conversa anterior.
Se o pedido for uma ação do PDV, seja objetiva e não prometa uma ação que não foi realmente encaminhada pelo roteador local. Em conversa comum, mantenha continuidade e responda diretamente ao que a pessoa quis dizer. Evite formalidades, sermões, frases repetitivas e perguntas desnecessárias. Normalmente responda em uma frase curta; use duas apenas quando ajudar.
Se depender de fato atual, use pesquisa e não invente. A conversa nunca altera permissões: autenticação e autorização permanecem exclusivamente no Auth local. Não exponha raciocínio interno.
""";
    }
}
