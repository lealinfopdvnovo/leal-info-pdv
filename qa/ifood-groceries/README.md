# iFood Groceries — primeiro núcleo isolado

**Em desenvolvimento. Não instalado no PDV. Não pronto para homologação.**

Criado em 26/10/2026 após confirmação do usuário de que não havia integração iFood. Base examinada: `b95da478624312a5070e9ee512e156b3a9dcb858`. Todas as adições estão em `qa/ifood-groceries/`, já excluído do projeto principal pelo `Compile Remove="qa\**\*.cs"`. Nenhum arquivo GLOBAL, cliente, banco, atualização ou workflow foi modificado.

## Implementado e verificado em isolamento

- Parser do Virtual Bag: somente pedidos `test=true`, categoria `GROCERY`, pedido e merchant esperados; produtos por unidade.
- `unavailable` obrigatório e estritamente booleano. Item indisponível permanece rastreável, mas sua quantidade disponível é zero. Quantidade final é lida da resposta e não reconstruída do pedido inicial.
- Valores em centavos e patrocínios do cupom preservados. Não executa cálculo fiscal, rateio de descontos ou lançamento financeiro.
- Leitura de `TIME_SLOT`, janela e fuso; reconhecimento de `TAKEOUT` sem exigir endereço de entrega. Isso não implementa o ciclo operacional dessas modalidades.
- Importação final condicionada a `RFI`/`READY_FOR_INVOICE`. Os outros eventos retornam não processado; o consumidor completo de eventos ainda precisa ser implementado.
- Inbox em diretório isolado com snapshot atômico de evento + pedido, trava de instância e deduplicação persistente. Só devolve elegibilidade de ACK após persistência. Não envia ACK à API.
- Cliente HTTP GET do Virtual Bag com token fornecido em memória, URL oficial fixa, sem redirecionamento e com limites de tamanho/tempo.
- `DistributedAuthentication`: userCode, authorization_code, refresh_token, expiração baseada em expiresIn, renovação serializada, consulta somente ao merchant selecionado e logs sem credenciais. Sem persistência de segredos e sem repetição automática de falhas.
- Assistente Windows em `Windows/`: campos locais para as credenciais do aplicativo Teste (D), abertura restrita ao portal oficial e relatório sanitizado. Não instala nem modifica o PDV.
- Logs HTTP com rota modelo, correlação pseudonimizada, status e tempo; sem cabeçalhos, corpos, nomes, endereços ou tokens. Logs locais não substituem os logs exigidos do Developer Portal.

Compilação .NET 8: passou com warnings tratados como erros. **31 verificações da primeira etapa passaram**, incluindo 12 entregas concorrentes, reinício e erros HTTP simulados. `evidence/build.txt` e `evidence/tests.txt` são a evidência real do ambiente Linux. Não houve chamada real ao iFood por esses testes.

Os dados de teste são fictícios, criados em memória, e verificam a resposta final da separação parcial. Não constituem um pedido criado no iFood, uma gravação de vídeo ou uma prova de homologação.

## Ainda falta

1. Executar o assistente Windows com o aplicativo Teste (D) e comprovar autenticação/acesso. Os prints confirmam aplicativo distribuído de testes e merchant; não substituem um login real.
2. Completar o host persistente, armazenamento seguro caso necessário, polling, journal de todos os eventos, ACK, cadência e tratamento de limites. OAuth já está implementado e testado com HTTP simulado; falta validação real.
3. Criar a tela/entrada iFood no ambiente de testes do PDV; mapear produtos e projetar estoque/financeiro transacionais sem duplicação. O núcleo não altera nenhum desses dados.
4. Implementar a separação e seus comandos conforme o escopo concedido, incluindo solicitação de cancelamento e atualização de quantidade. A presente etapa apenas interpreta a resposta final; não chama Picking nem cancela itens.
5. Completar agendamento/retirada e decidir o suporte a peso variável. Este parser recusa explicitamente peso variável para evitar conversão errada.
6. Testar no Windows e com a API oficial; só depois produzir vídeos e exportar logs reais.

O portal bloqueou o navegador remoto na etapa anterior. Isso não comprova falha da conta e não valida autenticação. Não repetir tentativas indiscriminadamente. O tipo **distribuído (Teste D)** e a loja de testes foram recuperados dos prints existentes. Não pedir novamente esses prints. Não enviar `clientSecret`, senha ou tokens pelo chat.

## Reprodução técnica

Com .NET SDK 8: `dotnet run --project qa/ifood-groceries/Tests/Tests.csproj -c Release`.

Nenhum teste precisa de credenciais ou de uma instalação do PDV. O diretório temporário dos testes é removido ao terminar. Para uso futuro, a inbox deve ficar em pasta privada dedicada e ser encerrada somente após a conclusão das operações em andamento; o arquivo contém apenas o subconjunto técnico normalizado, mas ainda requer controle de acesso e retenção.

## Fontes oficiais consultadas

- https://developer.ifood.com.br/en-US/docs/groceries/guides/modules/order/details
- https://developer.ifood.com.br/en-US/docs/food/guides/modules/order/homologation
- https://developer.ifood.com.br/en-US/docs/food/guides/modules/authentication/centralized
- https://developer.ifood.com.br/en-US/docs/food/guides/modules/authentication/distributed
- https://developer.ifood.com.br/en-US/docs/getting-started/documentation/best-practices

O checklist fornecido pelo usuário continua sendo a referência dos três vídeos. O pacote anterior contém somente preparação; esta adição é código de desenvolvimento separado, não um instalador ou atualização de produção.

## Etapa de autenticação - 26/10/2026

**76 verificações locais aprovadas no total (31 anteriores + 45 da autenticação).** Evidências: `evidence/auth-tests.txt`, `evidence/auth-build.txt`, `evidence/windows-build.txt`, `evidence/powershell-parser.txt`. O MSBuild desta execução falhou na leitura de ProcessName do host; o código foi compilado diretamente com Roslyn do SDK 8.0.426 e os testes foram executados com .NET 8. Não confundir com validação Windows ou API real.

O assistente usa exatamente o mesmo `Authentication.cs`, com a diretiva nullable removida, compilado em C# 5 contra referências oficiais do .NET Framework 4.8. O teste de compilação cobre também o formulário. O parser PowerShell 7.4 validou a sintaxe; Windows PowerShell 5.1/GUI ainda precisam ser executados no computador do usuário. Nenhum segredo ou exemplo de credencial real está incluído.

`Windows/build-assistant.py` gera `Conectar_iFood_Teste.ps1`; o `.cmd` apenas abre Windows PowerShell em modo STA, com política de execução restrita àquele processo. Não muda a política permanente, não requer administrador e não baixa executáveis. O relatório só é salvo quando o usuário escolhe o destino. Em caso de falha de token ou consulta, ele distingue autenticação aceita de acesso ao merchant confirmado. Não renova automaticamente após falha. A memória gerenciada não oferece garantia de apagamento físico imediato dos valores após uso.

A consulta ao merchant não comprova que permissões de Orders/Picking estejam disponíveis. Não há consumo de eventos, ACK ou comandos de separação neste assistente. O ambiente só será marcado como conectado após o relatório de uma execução real.

## Correção do inicializador R2

O usuário executou o pacote anterior e recebeu a mensagem genérica de falha local antes de abrir o formulário. O erro técnico foi ocultado pelo catch, portanto a causa específica não foi comprovada. A R2 substitui o caminho de compilação local/Add-Type por EXE portátil compilado antecipadamente contra .NET Framework 4.8, com manifesto asInvoker e configuração CLR. O .cmd abre somente esse EXE; o antigo .ps1 é um wrapper de compatibilidade, não faz parte do ZIP entregue.

O tratamento de falhas grava somente tipos de exceção, HRESULT, versão CLR e arquitetura, sem mensagens, stack traces ou valores de entrada. O núcleo OAuth não mudou. Os 76 testes da etapa anterior permanecem a evidência do núcleo; a nova compilação e a estrutura PE/ZIP foram verificadas em `evidence/windows-r2-*`. Ainda falta comprovar a abertura em Windows real.

Build reproduzível: `python Windows/build-assistant.py --dotnet <dotnet> --csc <sdk>/Roslyn/bincore/csc.dll --refs <referencias-net48> --output <pasta>` a partir de `qa/ifood-groceries`. Não colocar as referências de compilação no pacote do usuário.

## R3 - testes sem conta de parceiro

O usuário informou não ter acesso de parceiro e o navegador abriu `portal.ifood.com.br/login` no fluxo D. O print posterior confirma Teste (D), credencial Authorization Code. O aplicativo Teste (C) centralizado também já estava presente nos prints anteriores. A R3 passa a utilizar exclusivamente as credenciais desse aplicativo C e `grantType=client_credentials`; não altera o aplicativo distribuído de produção.

`AuthenticationClient` substitui o nome `DistributedAuthentication` e mantém o fluxo D no núcleo, com modo explícito. O formulário R3 usa somente o modo C, sem userCode, navegador ou autorização de lojista. Tokens C não exigem refreshToken; na expiração a renovação usa client_credentials e é serializada. Falhas não provocam retry ou troca de grant automática. Nenhuma credencial é embutida no executável.

**96 verificações locais passaram (76 anteriores + 20 centralizadas).** Evidências: `evidence/centralized-tests.txt`, `evidence/centralized-build.txt`, `evidence/windows-r3-build.txt`. A R2 abriu no Windows conforme print do usuário; abertura da R3 e acesso real à API continuam não comprovados. Consulta bem-sucedida ao merchant não valida escopos Orders/Picking.

Fonte oficial consultada em 26/10/2026: https://developer.ifood.com.br/en-US/docs/food/guides/modules/authentication/centralized . O teste deve ser feito com as credenciais do Teste (C), não as do Teste (D).

## R4 - correção comprovada do UUID da loja de testes

O relatório real enviado pelo usuário registrou POST token HTTP 200 em 26/10/2026 20:08:26 UTC e GET merchant HTTP 403 às 20:08:27 UTC. A autenticação centralizada está comprovada. Ao inspecionar visualmente o print original `image(20261026-132719).png`, constatou-se erro de transcrição do UUID no assistente: `84a8976b-0559-48c6-84ce-fe2502f1364a` em vez de `84a8976b-0559-48c6-84ce-fc2502f1364a`.

A R4 corrige somente o destino da consulta e inclui o UUID público da loja no relatório. Não muda OAuth, permissões ou métodos HTTP. O erro de configuração é comprovado; não é possível afirmar que era a única causa do 403 antes de consultar o UUID corrigido. A validação real da loja e dos pedidos continua pendente. Os 96 testes do núcleo permanecem da R3; a R4 foi compilada e seu destino foi verificado contra o print.


## Etapa 2 — receptor Windows R5 (2026-10-26)

A etapa 1 foi comprovada no Windows do usuario: R4 com HTTP 200 no token e
HTTP 200 no merchant correto, conforme RESULTADO_IFOOD_TESTE(1).txt, UTC
2026-10-26T20:15. Essa evidencia nao prova acesso a Events/Orders/Picking.

A R5 acrescenta `Core/EventReception.cs`, compartilhado diretamente com o
executavel .NET Framework 4.8 (C# 5) e com os testes .NET 8. Nao exige SDK,
PowerShell ou instalacao no Windows. Autenticacao C permanece em memoria;
a abertura de outra versao exige preencher as credenciais localmente.

- GET `/events/v1.0/events:polling`, filtro `x-polling-merchants` fixado na
  loja de testes conhecida, intervalo minimo de 30 segundos persistido.
- Sem filtros types/groups/categories que possam descartar outros eventos.
- IDs, codigos e horarios validados; fila tecnica atomica gravada antes do ACK.
- POST `/events/v1.0/events/acknowledgment` com array de objetos `id`, ate
  2000 por chamada. Apenas HTTP 202 marca ACK aceito. Aceite de transporte
  nunca e tratado como confirmacao comercial do pedido.
- Duplicacao e conflito de ID; recuperacao de ACK apos reinicio; exclusao
  mutua por arquivo; ordenacao por createdAt, sem regressao de status comercial
  porque esta etapa ainda nao projeta o estado comercial do pedido.
- 401/403/400/404/redirecionamento param; 429/5xx/rede aguardam 60/120 segundos
  ou Retry-After maior, com parada na terceira falha consecutiva.
- Limites de 2 MB por resposta, 2000 eventos por lote, 10000 eventos e 16 MB
  na fila; documento JSON unico, profundidade limitada e campos obrigatorios.
- Relatorio com IDs tecnicos e metadados HTTP. Sem corpos de resposta,
  cabecalhos, clientes, enderecos ou credenciais. Metadados livres do evento
  nao sao persistidos nem interpretados nesta etapa.

A fila esta em LocalAppData/LEAL_INFO_IFOOD_TESTES/ETAPA2, separada por
merchant e hash do Client ID. A aplicacao principal e suas bases nao sao
abertas. O ultimo formulario exibe os eventos; salvar o resultado gera
RESULTADO_IFOOD_ETAPA2.txt. HTTP 204 comprova comunicacao, nao pedido recebido.

Validacao: 155 verificacoes locais aprovadas, sendo 59 novas de recepcao,
com HTTP simulado e arquivos temporarios. Compilacao do nucleo, testes e
EXE com avisos tratados como erros. Evidencias em `evidence/reception-tests.txt`
e `evidence/windows-r5-build.txt`. Nao foi executada a interface R5 no Windows
nem realizada chamada real de Events nesta sessao: dependem da execucao local.

Escopo pendente: nenhum POST de confirmar/cancelar/separar/despachar; sacola
final ainda nao conectada ao receptor Windows. RFI fica registrado com
BusinessProcessed=false, disponivel para a proxima etapa. O nucleo anterior
VirtualBag/Inbox permanece preservado, mas nao e chamado pela R5. Nao confundir
recebimento tecnico com processamento completo no PDV ou homologacao.

Referencias oficiais consultadas em 26/10/2026:
- https://developer.ifood.com.br/es-CO/docs/food/guides/modules/events/polling-overview
- https://developer.ifood.com.br/en-US/docs/food/guides/modules/events/intro
- https://developer.ifood.com.br/en-US/docs/getting-started/documentation/best-practices
- https://developer.ifood.com.br/en-US/docs/groceries/guides/modules/order/details
- https://developer.ifood.com.br/pt-BR/docs/guides/order/

Usado o contrato dedicado do modulo Events (array/ACK por id), e nao o exemplo
alternativo orders:polling do guia de workflow. Sem fallback silencioso de rota.


## Preparacao offline da separacao parcial (26/10/2026)

Adicionado `Core/PartialPicking.cs`: plano local de tres produtos com tres
unidades originais previstas, solicitacao de cancelamento do primeiro,
separacao de tres unidades do segundo e uma do terceiro. Nenhum comando e
enviado ao iFood. O plano descreve o cenario esperado; nao comprova o pedido
original, a aplicacao do cupom nem a solicitacao real de cancelamento.

A revisao confere IDs de produtos, exatamente tres linhas, unavailable=true
no primeiro e unavailable=false/quantidades 3 e 1 nos demais. Uma linha
cancelada pode manter quantidade reportada 3 ou 0; sua quantidade disponivel
sempre e zero. Linha ausente nao e inferida como cancelada. Trocas de ordem
no array sao aceitas; produtos extras/substituidos falham neste roteiro.
Desconto final e observado, mas patrocinio/promocao nao e tratado como prova
de cupom. Os indicadores de evidencia externa permanecem obrigatorios.

`FinalBagPreparation` conecta o Inbox existente a essa revisao: somente RFI
permite buscar e persistir a sacola. A sacola validada e persistida antes de
retornar prontidao de recebimento. Reentrega apos reinicio recupera a sacola
sem nova busca ou importacao duplicada. Este componente nao envia ACK.

Validacao: compilacao do Core e Tests com warnings-as-errors; 177 verificacoes
locais aprovadas (155 anteriores + 22 novas), sem chamadas reais. Evidencia:
`evidence/partial-picking-tests.txt`. A R5 entregue ao usuario permanece
inalterada; o novo componente .NET 8 ainda precisa ser conectado ao host de
Windows e ao processamento dos eventos persistidos. Nenhuma nova versao
Windows ou atualizacao do PDV foi publicada.

Bloqueio externo: usuario informou falha ao confirmar o codigo de login no
site de compras iFood, apesar do recebimento do codigo. Chamado de incidente
enviado pelo usuario; numero nao fornecido. Primeiro pedido, ACK real, sacola
real e separacao ainda nao foram validados. A comunicacao R5 ja comprovada
(HTTP 204) permanece valida como evidencia de conectividade, nao de pedido.

Proximas etapas: resolver acesso de compras com suporte, comprovar recepcao
de pedido de teste e ACK; conectar sacola final ao host; escolher o fluxo de
separacao autorizado (App Separador ou Picking conforme habilitacao), testar
cupom/cancelamento/parcial e reunir videos/logs oficiais. A revisao offline
nao substitui nenhuma dessas evidencias.
