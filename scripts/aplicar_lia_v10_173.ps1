$ErrorActionPreference = 'Stop'
$path = 'LiaVoiceController.cs'
$c = Get-Content $path -Raw

function Replace-Required([string]$old,[string]$new,[string]$label) {
    if (-not $script:c.Contains($old)) { throw "Anchor nao encontrado: $label" }
    $script:c = $script:c.Replace($old,$new)
}

if (-not $c.Contains('private bool encerrarAposFala;')) {
    $novo = [regex]::Replace($c,'(?m)^(\s*private bool webPronto;\r?$)','$1' + [Environment]::NewLine + '    private bool encerrarAposFala;',1)
    if ($novo -eq $c) { throw 'Anchor nao encontrado: campo encerrarAposFala' }
    $c = $novo
}

$oldVoice='            var payload=new{model="gpt-4o-mini-tts",voice="marin",input=texto,instructions="Fale em português do Brasil, com voz feminina natural, calorosa, clara e profissional. Ritmo de conversa normal, sem soar robótica.",response_format="mp3",speed=1.04};'
$newVoice='            var payload=new{model="gpt-4o-mini-tts",voice="shimmer",input=texto,instructions="Fale em português do Brasil com voz feminina leve, espontânea, curiosa, humana e expressiva. Tom próximo e natural de conversa ao vivo, com suavidade e personalidade, sem soar robótica, formal ou infantil. Use variação natural de ritmo e emoção.",response_format="mp3",speed=1.0};'
Replace-Required $oldVoice $newVoice 'voz TTS'

Replace-Required 'RegistrarLog("RESPOSTA",resposta);await FalarAsync(resposta);' 'RegistrarLog("RESPOSTA",resposta);await FalarAsync(resposta);if(encerrarAposFala){encerrarAposFala=false;Encerrar();return;}' 'execucao apos fala'

$processAnchor='        var n=Normalizar(texto);'
$processInsert=@'
        var n=Normalizar(texto);

        if(Tem(n,"que horas","que horas tem","que horario","qual horario","qual o horario","horario agora","hora agora","horas agora"))
            return $"Agora são {DateTime.Now:HH:mm}.";

        if((Tem(n,"fecha","fechar","feche","recolhe","recolher","esconde","esconder","oculta","ocultar","some") && Tem(n,"botao","lia")) || Tem(n,"vai dormir lia","fecha essa porra desse botao","recolhe essa porra desse botao"))
            return RecolherLia();
'@
Replace-Required $processAnchor $processInsert 'atalhos locais'

$oldGreeting='        if(Tem(n,"oi lia","ola lia","oi","ola","bom dia","boa tarde","boa noite","bom dia lia","boa tarde lia","boa noite lia","lia bom dia","lia boa tarde","lia boa noite")){if(n.Contains("bom dia"))return $"Bom dia, {Auth.OperatorName}. Como posso ajudar?";if(n.Contains("boa tarde"))return $"Boa tarde, {Auth.OperatorName}. Como posso ajudar?";if(n.Contains("boa noite"))return $"Boa noite, {Auth.OperatorName}. Como posso ajudar?";return $"Oi, {Auth.OperatorName}. Como posso ajudar?";}'
$newGreeting='        if(Tem(n,"oi lia","ola lia","oi","ola","bom dia","boa tarde","boa noite","bom dia lia","boa tarde lia","boa noite lia","lia bom dia","lia boa tarde","lia boa noite")){var nome=NomeCurtoOperador();if(n.Contains("bom dia"))return $"Bom dia, {nome}. Como posso ajudar?";if(n.Contains("boa tarde"))return $"Boa tarde, {nome}. Como posso ajudar?";if(n.Contains("boa noite"))return $"Boa noite, {nome}. Como posso ajudar?";return $"Oi, {nome}. Como posso ajudar?";}'
Replace-Required $oldGreeting $newGreeting 'cumprimento nome curto'
Replace-Required '        if(Tem(n,"ta me ouvindo","esta me ouvindo","voce me ouve","consegue me ouvir"))return $"Sim, {Auth.OperatorName}. Estou ouvindo você.";' '        if(Tem(n,"ta me ouvindo","esta me ouvindo","voce me ouve","consegue me ouvir"))return $"Sim, {NomeCurtoOperador()}. Estou ouvindo você.";' 'nome curto ouvindo'

$oldSwitch='                "FECHAR_TELA"=>PrepararFechamento(n),'
$newSwitch='                "RECOLHER_LIA"=>RecolherLia(),' + [Environment]::NewLine + '                "FECHAR_TELA"=>PrepararFechamento(n),'
Replace-Required $oldSwitch $newSwitch 'switch recolher'

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

$oldYes='        if(!Tem(n,"sim","pode","pode fechar","fecha","feche","confirmo","isso","isso mesmo","pode sim"))return "Só confirma pra mim: sim ou não?";'
$newYes='        if(!Tem(n,"sim","pode","pode fechar","fecha","feche","confirmo","isso","isso mesmo","pode sim","tenho","tenho certeza","certeza","claro","com certeza","confirmado","confirmada","pode ir","manda","manda ver","vai","bora"))return "Só confirma pra mim: sim ou não?";'
Replace-Required $oldYes $newYes 'confirmacoes naturais'

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
