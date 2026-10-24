namespace LicAi.Core;

/// <summary>Resposta local imediata para o PDV, sem internet, chave ou modelo externo.</summary>
public static class LocalPdvAssistant
{
    public static string Answer(string input)
    {
        var text = Normalize(input);
        if (string.IsNullOrWhiteSpace(text)) return "Pode escrever sua dúvida sobre o PDV.";

        var navigation = Navigation(text);
        if (navigation != null) return "COMANDO: " + navigation;

        if (Has(text, "bom dia")) return "Bom dia, chefe! Estou pronta para ajudar com o PDV.";
        if (Has(text, "boa tarde")) return "Boa tarde, chefe! O que você precisa fazer no PDV?";
        if (Has(text, "boa noite")) return "Boa noite, chefe! Pode me perguntar sobre qualquer tela do PDV.";
        if (Has(text, "hora", "horas")) return $"Agora são {DateTime.Now:HH:mm}.";
        if (Has(text, "data", "dia de hoje")) return $"Hoje é {DateTime.Now:dd/MM/yyyy}.";

        if (Has(text, "impressora", "bobina", "imprimir"))
            return "Abra Configurações, entre em Equipamentos e escolha Impressora Térmica. Selecione a impressora instalada, a bobina de 58, 76 ou 80 milímetros e use Imprimir teste. Com duas ou mais impressoras, o PDV pergunta onde imprimir.";
        if (Has(text, "carga", "plu") && Has(text, "balanca", "produto"))
            return "Em Configurações, abra Equipamentos e depois Carga de Produtos. O PDV reúne os itens marcados como Usar balança ou unidade KG e envia PLU, descrição, preço, unidade e validade por arquivo, COM ou TCP.";
        if (Has(text, "balanca", "peso", "pesar"))
            return "Na Central de Equipamentos, abra a aba Balança. Escolha COM, TCP/IP, teclado HID ou etiqueta, selecione o perfil da marca e clique em Testar leitura. Balança USB pode aparecer como porta COM ou teclado.";
        if (Has(text, "pix", "qr code"))
            return "Em Configurações, abra a aba PIX. No PIX simples, informe a chave de qualquer banco e o PDV gera QR Code e Copia e Cola; o recebimento é confirmado manualmente. Mercado Pago automático é opcional e exige Access Token.";
        if (Has(text, "backup", "restaurar", "restauracao"))
            return "Em Configurações, na aba Sistema e Segurança, use Fazer backup ou Restaurar backup. A restauração pede confirmação e guarda uma cópia do banco atual antes da troca.";
        if (Has(text, "produto", "estoque", "categoria", "marca", "subgrupo"))
            return "No Cadastro de Produtos você informa código, descrição, fornecedor, unidade, preços, estoque, marca, categoria, grupo, subgrupo, validade e foto. Os botões de mais cadastram as classificações sem sair da tela. Para item pesado, marque Usar balança e Venda fracionada.";
        if (Has(text, "venda", "vender", "caixa", "pagamento", "troco", "desconto"))
            return "Na Tela de Vendas, F5 busca o produto, Enter confirma a quantidade, F7 remove um item e F4 abre a finalização. Na janela de pagamento, F3 aciona o PIX. O pagamento pode ser dinheiro, PIX, cartão ou múltiplo; confira total, desconto e troco antes de concluir.";
        if (Has(text, "cliente", "fornecedor"))
            return "Clientes e fornecedores possuem nome, documento, telefone, e-mail e endereço. Antes de cadastrar, pesquise pelo nome ou documento para evitar duplicidade.";
        if (Has(text, "servico", "ordem de servico", "os ", "equipamento", "defeito"))
            return "Serviços guardam nome, preço e descrição. A Ordem de Serviço registra cliente, equipamento, defeito, serviço realizado, status, valor e observações.";
        if (Has(text, "orcamento"))
            return "Em Orçamentos você registra cliente, descrição, valor e status. O orçamento não vira venda concluída até o operador confirmar o processo correto.";
        if (Has(text, "historico", "vendas anteriores"))
            return "Abra Histórico de Vendas para localizar vendas anteriores e consultar seus detalhes.";
        if (Has(text, "relatorio", "faturamento", "lucro", "financeiro"))
            return "Use Relatórios para os resumos e Fluxo de Caixa para entradas e saídas. A LIA não inventa valores: abra a tela correspondente para consultar os números reais.";
        if (Has(text, "usuario", "senha", "permissao", "acesso"))
            return "Usuários, senhas e níveis de acesso ficam em Configurações, na aba Sistema e Segurança. Somente um administrador pode alterar permissões e recursos protegidos.";
        if (Has(text, "empresa", "cnpj", "telefone", "rodape"))
            return "Em Configurações, abra Dados da Empresa para alterar nome, nome fantasia, CNPJ ou CPF, telefone, endereço, cidade e mensagem do rodapé do comprovante.";
        if (Has(text, "atalho", "tecla", "esc"))
            return "Os principais atalhos são F5 para buscar produto, F4 para finalizar, F3 para acionar o PIX no fechamento, F7 para remover item e ESC para fechar a tela ativa.";
        if (Has(text, "ajuda", "o que voce sabe", "o que sabe", "funcao"))
            return "Posso explicar produtos, vendas, clientes, fornecedores, serviços, ordens, orçamentos, caixa, relatórios, PIX, backup, impressora, balança, carga de produtos, usuários e configurações. Diga a tela ou tarefa que deseja aprender.";

        return "Estou funcionando no modo local. Diga o nome da tela ou da tarefa, por exemplo: como cadastrar produto, configurar balança, imprimir cupom ou finalizar uma venda.";
    }

    private static string? Navigation(string text)
    {
        var open = Has(text, "abra", "abrir", "mostre", "mostrar", "va para", "ir para", "entre em");
        if (Has(text, "fechar tela", "fechar janela")) return "FECHAR_TELA";
        if (!open) return null;
        if (Has(text, "produto")) return "PRODUTOS";
        if (Has(text, "cliente")) return "CLIENTES";
        if (Has(text, "fornecedor")) return "FORNECEDORES";
        if (Has(text, "ordem de servico", " os")) return "ORDENS_SERVICO";
        if (Has(text, "servico")) return "SERVICOS";
        if (Has(text, "orcamento")) return "ORCAMENTOS";
        if (Has(text, "fluxo", "caixa")) return "FLUXO_CAIXA";
        if (Has(text, "historico")) return "HISTORICO_VENDAS";
        if (Has(text, "motoboy", "entrega", "delivery")) return "ENTREGAS";
        if (Has(text, "venda", "pdv")) return "TELA_VENDAS";
        if (Has(text, "relatorio")) return "RELATORIOS";
        if (Has(text, "usuario")) return "USUARIOS";
        if (Has(text, "configuracao", "configurar", "empresa", "pix", "balanca", "impressora")) return "CONFIGURACOES";
        if (Has(text, "cadastro")) return "CADASTROS";
        if (Has(text, "ajuda")) return "AJUDA_CADASTRO";
        return null;
    }

    private static bool Has(string text, params string[] terms) => terms.Any(text.Contains);
    private static string Normalize(string value)
    {
        var normalized = value.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        return new string(normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(System.Text.NormalizationForm.FormC);
    }
}
