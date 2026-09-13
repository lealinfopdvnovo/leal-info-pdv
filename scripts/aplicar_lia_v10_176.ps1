$ErrorActionPreference='Stop'
$path='LiaVoiceController.cs'
$c=Get-Content $path -Raw

# V10.173 e aplicada antes desta etapa. Aqui garantimos que abrir/fechar PDV
# sejam decididos localmente ANTES de LiaCore/IA, sem disputa semantica.
$anchor='        var n=Normalizar(texto);'
if(-not $c.Contains($anchor)){throw 'Anchor ProcessarAsync nao encontrado'}
$insert=@'
        var n=Normalizar(texto);

        // V10.176 - comandos essenciais do PDV sao deterministas e locais.
        // FECHAR tem precedencia absoluta sobre qualquer conceito de venda/PDV.
        bool alvoPdv = Tem(n,"pdv","tela pdv","tela de pdv","tela venda","tela de venda","tela de vendas","vendas");
        bool verboFechar = Tem(n,"fecha","fechar","feche","encerra","encerrar","saia","sair");
        bool verboAbrir = Tem(n,"abre","abrir","abra","abri","entra","entrar");
        if(alvoPdv && verboFechar)
            return PrepararFechamentoPdv();
        if(alvoPdv && verboAbrir)
            return Acao("Abrindo o PDV.",main.LiaAbrirVendas);
'@
$c=$c.Replace($anchor,$insert)

$helper='    private string PrepararFechamento(string n)'
if(-not $c.Contains($helper)){throw 'Anchor PrepararFechamento nao encontrado'}
$newHelper=@'
    private string PrepararFechamentoPdv()
    {
        var alvo=Application.OpenForms.Cast<Form>()
            .Where(f=>!ReferenceEquals(f,main) && !ReferenceEquals(f,orbe) && f.Visible && !f.IsDisposed)
            .OrderByDescending(f=>f.Focused || f.ContainsFocus)
            .ThenByDescending(f=>f.Handle==Form.ActiveForm?.Handle)
            .FirstOrDefault(f=>Normalizar(f.Text??"").Contains("venda"));
        if(alvo is null)
        {
            // Se houver uma unica tela operacional aberta, usa a mesma logica segura existente.
            var atual=ObterTelaParaFechar();
            if(atual is not null && Normalizar(atual.Text??"").Contains("venda")) alvo=atual;
        }
        if(alvo is null)return "A tela do PDV já está fechada.";
        fechamentoPendente=alvo;
        fechamentoSistemaPendente=false;
        fechamentoPendenteEm=DateTime.Now;
        return TelaTemDadosEmEdicao(alvo)
            ? "Tem informações em edição. Tem certeza que quer fechar a tela do PDV?"
            : "Tem certeza que quer fechar a tela do PDV?";
    }

    private string PrepararFechamento(string n)
'@
$c=$c.Replace($helper,$newHelper)

# Nunca fale titulo/versao da janela em confirmacao do PDV; helper acima usa frase fixa.
Set-Content $path $c -Encoding UTF8
Write-Host 'V10.176 aplicada: abrir/fechar PDV deterministico, fechamento com precedencia e confirmacao limpa.'
