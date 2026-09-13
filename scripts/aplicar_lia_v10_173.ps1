$ErrorActionPreference = 'Stop'
$path = 'LiaVoiceController.cs'
$c = Get-Content $path -Raw

function Replace-Required([string]$old,[string]$new,[string]$label) {
    if (-not $script:c.Contains($old)) { throw "Anchor nao encontrado: $label" }
    $script:c = $script:c.Replace($old,$new)
}

# Fecha/recolhe a LIA somente depois que ela terminar de falar a confirmação.
Replace-Required '    private bool webPronto;`r`n    private TaskCompletionSource<bool>? falaTerminou;' '    private bool webPronto;`r`n    private bool encerrarAposFala;`r`n    private TaskCompletionSource<bool>? falaTerminou;' 'campo encerrarAposFala'
if (-not $c.Contains('private bool encerrarAposFala;')) {
    Replace-Required "    private bool webPronto;`n    private TaskCompletionSource<bool>? falaTerminou;" "    private bool webPronto;`n    private bool encerrarAposFala;`n    private TaskCompletionSource<bool>? falaTerminou;" 'campo encerrarAposFala LF'
}

# Voz mais próxima do perfil Viola disponível na API pública: leve, espontânea e curiosa.
$oldVoice='            var payload=new{model="gpt-4o-mini-tts",voice="marin",input=texto,instructions="Fale em português do Brasil, com voz feminina natural, calorosa, clara e profissional. Ritmo de conversa normal, sem soar robótica.",response_format="mp3",speed=1.04};'
$newVoice='            var payload=new{model="gpt-4o-mini-tts",voice="shimmer",input=texto,instructions="Fale em português do Brasil com voz feminina leve, espontânea, curiosa, humana e expressiva. Tom próximo e natural de conversa ao vivo, com suavidade e personalidade, sem soar robótica, formal ou infantil. Use variação natural de ritmo e emoção.",response_format="mp3",speed=1.0};'
Replace-Required $oldVoice $newVoice 'voz TTS'

# Após a fala 'Fechando', encerra a sessão e recolhe o botão/orbe de verdade.
Replace-Required 'RegistrarLog("RESPOSTA",resposta);await FalarAsync(resposta);' 'RegistrarLog("RESPOSTA",resposta);await FalarAsync(resposta);if(encerrarAposFala){encerrarAposFala=false;Encerrar();return;}' 'execucao apos fala'

# Hora local deve ser instantânea/local, sem chamada de IA/web.
$processAnchor='        var n=Normalizar(texto);'
$processInsert=@'
        var n=Normalizar(texto);

        if(Tem(n,"que horas","que horas tem","que horario","qual horario","qual o horario","horario agora","hora agora","horas agora"))
            return $"Agora são {DateTime.Now:HH:mm}.";

        if((Tem(n,"fecha","fechar","feche","recolhe","recolher","esconde","esconder","oculta","ocultar","some") && Tem(n,"botao","lia")) || Tem(n,"vai dormir lia","fecha essa porra desse botao","recolhe essa porra desse botao"))
            return RecolherLia();
'@
Replace-Required $processAnchor $processInsert 'atalhos locais'

# Nome curto nos cumprimentos em vez do nome civil completo.
$oldGreeting='        if(Tem(n,"oi lia","ola lia","oi","ola","bom dia","boa tarde","boa noite","bom dia lia","boa tarde lia","boa noite lia","lia bom dia","lia boa tarde","lia boa noite")){if(n.Contains("bom dia"))return $"Bom dia, {Auth.OperatorName}. Como posso ajudar?";if(n.Contains("boa tarde"))return $"Boa tarde, {Auth.OperatorName}. Como posso ajudar?";if(n.Contains("boa noite"))return $"Boa noite, {Auth.OperatorName}. Como posso ajudar?";return $"Oi, {Auth.OperatorName}. Como posso ajudar?";}'
$newGreeting='        if(Tem(n,"oi lia","ola lia","oi","ola","bom dia","boa tarde","boa noite","bom dia lia","boa tarde lia","boa noite lia","lia bom dia","lia boa tarde","lia boa noite")){var nome=NomeCurtoOperador();if(n.Contains("bom dia"))return $"Bom dia, {nome}. Como posso ajudar?";if(n.Contains("boa tarde"))return $"Boa tarde, {nome}. Como posso ajudar?";if(n.Contains("boa noite"))return $"Boa noite, {nome}. Como posso ajudar?";return $"Oi, {nome}. Como posso ajudar?";}'
Replace-Required $oldGreeting $newGreeting 'cumprimento nome curto'
Replace-Required '        if(Tem(n,"ta me ouvindo","esta me ouvindo","voce me ouve","consegue me ouvir"))return $"Sim, {Auth.OperatorName}. Estou ouvindo você.";' '        if(Tem(n,"ta me ouvindo","esta me ouvindo","voce me ouve","consegue me ouvir"))return $"Sim, {NomeCurtoOperador()}. Estou ouvindo você.";' 'nome curto ouvindo'

# Nova intenção local do roteador semântico.
Replace-Required '                "FECHAR_TELA"=>PrepararFechamento(n),' '                "RECOLHER_LIA"=>RecolherLia(),`r`n                "FECHAR_TELA"=>PrepararFechamento(n),' 'switch recolher'
if (-not $c.Contains('"RECOLHER_LIA"=>RecolherLia()')) {
    Replace-Required "                \"FECHAR_TELA\"=>PrepararFechamento(n)," "                \"RECOLHER_LIA\"=>RecolherLia(),`n                \"FECHAR_TELA\"=>PrepararFechamento(n)," 'switch recolher LF'
}

# Métodos auxiliares antes do abridor genérico de telas.
$helperAnchor='    private string AbrirTelaMain(string metodo,string nome)'
$helpers=@'
    private string RecolherLia()
    {
        encerrarAposFala=true;
        return "Fechando.";
    }

    private static string NomeCurtoOperador()
    {
        var nome=(Auth.OperatorName??"").Trim();
        if(string.IsNullOrWhiteSpace(nome))return "Adriano";
        return nome.Split(' ',StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??nome;
    }

    private string AbrirTelaMain(string metodo,string nome)
'@
Replace-Required $helperAnchor $helpers 'helpers'

# Confirmações naturais: aceita 'tenho', 'certeza', 'claro', etc., mesmo com palavrões junto.
$oldYes='        if(!Tem(n,"sim","pode","pode fechar","fecha","feche","confirmo","isso","isso mesmo","pode sim"))return "Só confirma pra mim: sim ou não?";'
$newYes='        if(!Tem(n,"sim","pode","pode fechar","fecha","feche","confirmo","isso","isso mesmo","pode sim","tenho","tenho certeza","certeza","claro","com certeza","confirmado","confirmada","pode ir","manda","manda ver","vai","bora"))return "Só confirma pra mim: sim ou não?";'
Replace-Required $oldYes $newYes 'confirmacoes naturais'

# Nome amigável da tela: remove versão/título técnico e chama PDV de 'tela de vendas'.
$oldNome=@'
    private static string NomeTela(Form f)
    {
        var t=(f.Text??"").Trim();
        if(string.IsNullOrWhiteSpace(t))return "atual";
        return t.Length>45?t[..45]:t;
    }
'@
$newNome=@'
    private static string NomeTela(Form f)
    {
        var t=(f.Text??"").Trim();
        var n=Normalizar(t);
        if(n.Contains("venda"))return "de vendas";
        if(n.Contains("produto"))return "de produtos";
        if(n.Contains("cliente"))return "de clientes";
        if(n.Contains("fornecedor"))return "de fornecedores";
        if(n.Contains("servico"))return "de serviços";
        if(string.IsNullOrWhiteSpace(t))return "atual";
        t=Regex.Replace(t,@"\s+V?\d+(\.\d+)+.*$","",RegexOptions.IgnoreCase).Trim();
        return t.Length>32?t[..32]:t;
    }
'@
Replace-Required $oldNome $newNome 'nome de tela limpo'

Set-Content $path $c -Encoding UTF8
Write-Host 'V10.173 aplicada: contexto ampliado, recolhimento real da LIA, confirmacoes naturais, hora local e voz estilo Viola.'
