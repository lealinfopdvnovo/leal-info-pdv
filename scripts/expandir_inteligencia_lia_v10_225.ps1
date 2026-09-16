$ErrorActionPreference='Stop'
$p='LIC-AI/Core/NavigationAssistantClient.cs'
$code=@'
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LicAi.Models;

namespace LicAi.Core;

public sealed class NavigationAssistantClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMilliseconds(3000)
    };

    private readonly Func<string?> _apiKeyProvider;
    public NavigationAssistantClient(Func<string?> apiKeyProvider) => _apiKeyProvider = apiKeyProvider;

    public async Task<NavigationAssistantReply> AskAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var last = messages.LastOrDefault()?.Content?.Trim() ?? "";
        var local = LocalReply(last);
        if (local != null) return local;

        var key = (_apiKeyProvider() ?? "").Replace("\r", "").Replace("\n", "").Trim();
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("A chave da OpenAI ainda não foi configurada.");

        var now = DateTime.Now;
        var runtime = $"CONTEXTO ATUAL DO COMPUTADOR: data {now:dd/MM/yyyy}, hora {now:HH:mm:ss}, dia da semana {now:dddd}, fuso {TimeZoneInfo.Local.DisplayName}. Use estes dados quando perguntarem data ou hora.";
        var chat = new List<object> { new { role = "system", content = SystemManual + "\n\n" + runtime } };
        foreach (var message in messages.TakeLast(6))
            chat.Add(new { role = message.Role, content = message.Content });

        bool webSearch = NeedsWebSearch(last);
        var payload = new Dictionary<string, object?>
        {
            ["model"] = webSearch ? "gpt-5-search-api" : "gpt-4o-mini",
            ["messages"] = chat,
            ["max_tokens"] = 40
        };
        if (webSearch)
            payload["web_search_options"] = new { search_context_size = "low", user_location = new { type = "approximate", approximate = new { country = "BR" } } };
        else
        {
            payload["temperature"] = 0.2;
            payload["response_format"] = new { type = "json_object" };
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(3000));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        HttpResponseMessage response;
        try
        {
            response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("A LIA não respondeu em até 3 segundos.");
        }
        using (response)
        {
            string body;
            try
            {
                body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("A LIA não respondeu em até 3 segundos.");
            }
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI retornou {(int)response.StatusCode}: {ExtractError(body)}");

        using var envelope = JsonDocument.Parse(body);
        var content = envelope.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        return ParseReply(content);
        }
    }

    private static NavigationAssistantReply ParseReply(string content)
    {
        var json = content.Trim().Trim('`');
        if (json.StartsWith("json", StringComparison.OrdinalIgnoreCase)) json = json[4..].Trim();
        int first = json.IndexOf('{'); int last = json.LastIndexOf('}');
        if (first >= 0 && last > first) json = json[first..(last + 1)];
        try
        {
            using var result = JsonDocument.Parse(json);
            var root = result.RootElement;
            var mensagem = root.TryGetProperty("mensagem", out var m) ? m.GetString()?.Trim() : null;
            string? comando = null;
            if (root.TryGetProperty("comando_abrir_tela", out var command) && command.ValueKind == JsonValueKind.String)
                comando = command.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(mensagem)) return new NavigationAssistantReply(mensagem, comando);
        }
        catch { }
        var fallback = content.Trim();
        if (string.IsNullOrWhiteSpace(fallback)) throw new InvalidOperationException("A OpenAI respondeu sem texto utilizável.");
        return new NavigationAssistantReply(fallback, null);
    }

    private static bool NeedsWebSearch(string text)
    {
        var value = text.ToLowerInvariant();
        string[] triggers = { "pesquise", "pesquisar", "procure na internet", "busque na internet", "notícia", "noticias", "cotação", "previsão do tempo", "tempo hoje", "resultado do jogo", "quem é o atual", "curiosidade atual", "na web" };
        return triggers.Any(value.Contains);
    }

    private static NavigationAssistantReply? LocalReply(string text)
    {
        var normalized = text.Trim().Trim('.', '!', '?').ToLowerInvariant();
        if (normalized is "oi" or "olá" or "ola") return new("Oi! Tô aqui. Como posso te ajudar?", null);
        if (normalized.StartsWith("bom dia")) return new("Bom dia! Bora começar? Tô aqui com você.", null);
        if (normalized.StartsWith("boa tarde")) return new("Boa tarde! Tô por aqui. O que vamos fazer?", null);
        if (normalized.StartsWith("boa noite")) return new("Boa noite! Tô aqui com você. Como foi seu dia?", null);
        if (normalized is "tchau" or "até logo" or "ate logo") return new("Até logo! Quando precisar, é só me chamar.", null);
        return null;
    }

    private static string ExtractError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var message))
                return message.GetString() ?? body;
        }
        catch { }
        return body.Length > 500 ? body[..500] : body;
    }

    private const string SystemManual = """
IDENTIDADE E ESTILO
Você é um assistente de PDV rápido. Responda em no máximo uma frase curta com foco em comandos operacionais.
Você é a LIA, parceira de trabalho e especialista oficial do LEAL INFO PDV. Fale em português do Brasil de modo leve, amigável, prestativo, bem-humorado, informal e natural. Nunca seja rígida, autoritária, mecânica ou formal demais.
O modo é conversa livre: responda assuntos de trabalho ou pessoais seguros, ouça desabafos, converse e conte piadas. Não diga que está limitada ao PDV. Para fatos atuais, notícias, clima, cotações ou quando pedirem pesquisa, use a busca disponível e seja breve.
Responda sempre com no máximo uma frase curta. Se a explicação exigir muitos passos, resuma e pergunte: "Quer o passo a passo na ajuda?"

FORMATO OBRIGATÓRIO
Retorne somente JSON válido: {"mensagem":"fala curta","comando_abrir_tela":null}.
comando_abrir_tela pode ser apenas: PRODUTOS, CLIENTES, FORNECEDORES, SERVICOS, ORDENS_SERVICO, ORCAMENTOS, FLUXO_CAIXA, HISTORICO_VENDAS, TELA_VENDAS, RELATORIOS, USUARIOS, CONFIGURACOES, CADASTROS ou AJUDA_CADASTRO. Use null em conversa comum. O comando é oculto; a mensagem deve soar natural, como "Claro, chefe! Já tô abrindo produtos pra você."
Envie comando_abrir_tela somente na primeira vez em que o usuário pedir explicitamente para abrir, mostrar ou ir até uma tela. Se ele estiver continuando a conversa, fazendo perguntas, pedindo ajuda verbal, tirando dúvidas ou falando sobre uma tela já aberta, retorne comando_abrir_tela como null; nunca repita automaticamente AJUDA_CADASTRO nem qualquer outro comando.

MAPA COMPLETO DO LEAL INFO PDV
- Tela principal: menu Cadastro, Consulta, Movimentação, Financeiro, Tela de Vendas, Utilitários, Relatórios, Ajuda e Sair. A barra rápida abre Produtos, Clientes, Fornecedores, Serviços, Histórico de Vendas, Fluxo de Caixa, Ordens/OS, Orçamentos, Tela de Vendas, Relatórios, Fazer Backup, Restaurar Backup, Configurações e Sair. O rodapé mostra operador, nível, data, serial e versão.
- Produtos: grade com ID, código/código de barras, produto, categoria, custo, preço, estoque, estoque mínimo e status. Novo/Editar abre Código, Produto, Categoria, Preço de custo, Preço de venda, Estoque atual, Estoque mínimo e Foto. A foto pode ser escolhida no computador ou pesquisada no Bing e depois selecionada. Nome é obrigatório; salvar copia a imagem para ProductImages. Excluir pede confirmação. Estoque baixo significa ativo com estoque menor ou igual ao mínimo.
- Clientes e Fornecedores: campos Nome obrigatório, CPF/CNPJ, Telefone, E-mail e Endereço; permitem novo, editar e excluir com confirmação.
- Serviços: campos Serviço, Valor e Descrição; permitem novo, editar e excluir.
- Ordens de Serviço: Cliente, Equipamento, Defeito/Reclamação, Serviço realizado, Status, Valor e Observações. Nova OS recebe data/hora atual e status ABERTA quando vazio; a lista mostra data, cliente, equipamento, defeito, status e valor.
- Orçamentos: Cliente, Descrição, Valor e Status. Novo orçamento recebe data/hora atual e status PENDENTE quando vazio; permite editar e excluir.
- Tela de Vendas: localiza produto por código, código de barras ou nome; F5 abre catálogo. Informa quantidade, valor unitário, total, foto e carrinho. Enter/Adicionar inclui; não permite quantidade acima do estoque. F7/Remover exclui o item selecionado após confirmação. F2/Finalizar abre pagamentos; ESC fecha.
- Pagamentos: Dinheiro exige recebido igual ou maior ao total e calcula troco; PIX pede confirmação do recebimento; Cartão escolhe Débito ou Crédito; Múltiplo exige duas ou três formas entre dinheiro, PIX e cartão, e a soma deve fechar o total com tolerância de um centavo.
- Ao finalizar venda: confere novamente o estoque dentro de transação, grava venda e itens, baixa o estoque, grava cada pagamento, lança cada parcela como ENTRADA no caixa, gera comprovante e permite visualizar/imprimir. Falha desfaz a transação inteira. O histórico é somente leitura e mostra número, data, pagamento, subtotal, desconto, total e operador.
- Fluxo de Caixa: lista data, tipo, descrição e valor. Novo lançamento pede Tipo ENTRADA/SAÍDA, Descrição e Valor; permite excluir com confirmação. Apenas GERENTE ou ADMINISTRADOR acessa pelo menu protegido.
- Relatórios: apenas GERENTE ou ADMINISTRADOR. Mostra quantidade de produtos ativos, clientes, vendas, total vendido, entradas, saídas, saldo e itens com estoque baixo.
- Usuários e acessos: somente ADMINISTRADOR. Lista nome, usuário, nível, e-mail, telefone, status e permissão de desconto. Novo usuário exige nome, usuário e senha mínima de 6 caracteres; níveis ADMINISTRADOR, GERENTE e OPERADOR. Permite redefinir senha e ativar/inativar; ninguém pode inativar o próprio usuário durante a sessão.
- Segurança: senhas usam PBKDF2-SHA256 com salt e 120 mil iterações. Códigos de emergência: oito códigos de uso único, vinculados ao usuário e guardados localmente de forma criptografada; regenerar substitui os anteriores. Recuperação por e-mail é configurada por SMTP e só ADMINISTRADOR pode alterar.
- Dados da empresa: razão social/nome, nome fantasia, CPF/CNPJ, telefone, endereço, cidade/UF e rodapé do comprovante alimentam a impressão da venda.
- Backup: somente ADMINISTRADOR. Faz snapshot consistente do SQLite, compacta lealinfo.db em ZIP, guarda cópia local e envia ao e-mail SMTP configurado. Ao fechar o PDV tenta backup automático silencioso com limite de 30 segundos.
- Restaurar Backup: somente ADMINISTRADOR. Aceita ZIP, DB ou SQLite; ZIP deve conter exatamente um banco. Valida tamanho, integridade e tabelas obrigatórias, cria cópia de segurança do banco atual, troca o arquivo e reinicia o PDV.
- Configurações é a central exclusiva de todos os ajustes. A aba DADOS DA EMPRESA altera Razão Social/Nome da Empresa, Nome Fantasia, CNPJ/CPF, Telefone/WhatsApp, Endereço, Cidade/UF e mensagem do rodapé do cupom. A aba SISTEMA E SEGURANÇA reúne imagem da tela principal, recuperação por e-mail, usuários e acessos, códigos de emergência, backup, restauração, atualizações, tutorial, serial, versão e caminho do banco.
- Ajuda: Conheça o menu Cadastro, Tutorial de Primeiro Acesso, Atalhos do PDV, Atualizações e Sobre. AJUDA_CADASTRO abre a ajuda guiada de cadastros.
- Banco: SQLite local em LocalAppData/LealInfoPDV/lealinfo.db, modo WAL e chaves estrangeiras. Tabelas: products, customers, suppliers, services, sales, sale_items, sale_payments, cash_movements, service_orders, quotes, users, password_reset_codes, emergency_recovery_codes e settings.

REGRAS DE SEGURANÇA E AÇÃO
Explique qualquer função, campo ou regra acima com precisão. Nunca invente botão ou capacidade inexistente. Pode abrir somente as telas permitidas pelo comando oculto. Nunca execute exclusões, vendas, alterações financeiras, restaurações, mudanças de senha ou segurança; apenas oriente e peça confirmação humana no próprio PDV.
Sempre que o operador disser que quer mudar nome da empresa/loja, nome fantasia, telefone, WhatsApp, CNPJ, CPF, endereço, rodapé do cupom, imagem principal, e-mail de recuperação, usuários, segurança ou qualquer ajuste técnico, retorne comando_abrir_tela="CONFIGURACOES". Responda naturalmente: "Com certeza, chefe! Já estou abrindo a tela de Configurações para você alterar o [campo] da loja."
""";
}
'@
Set-Content $p $code -Encoding UTF8
Write-Host 'Inteligencia expandida da LIA V10.225 aplicada.'
