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
- Cliente HTTP GET do Virtual Bag com token fornecido em memória pelo futuro host, URL oficial fixa, sem redirecionamento e com limites de tamanho/tempo. Não realiza login nem contém credenciais.
- Logs HTTP com rota modelo, correlação pseudonimizada, status e tempo; sem cabeçalhos, corpos, nomes, endereços ou tokens. Logs locais não substituem os logs exigidos do Developer Portal.

Compilação .NET 8: passou com warnings tratados como erros. **31 verificações passaram**, incluindo 12 entregas concorrentes, reinício e erros HTTP simulados. `evidence/build.txt` e `evidence/tests.txt` são a evidência real do ambiente Linux. Não houve chamada real ao iFood por esses testes.

Os dados de teste são fictícios, criados em memória, e verificam a resposta final da separação parcial. Não constituem um pedido criado no iFood, uma gravação de vídeo ou uma prova de homologação.

## Ainda falta

1. Identificar tipo, escopos e loja vinculada do aplicativo de testes no portal. Autenticação centralizada e distribuída usam fluxos diferentes; nenhum foi presumido.
2. Implementar e verificar OAuth com armazenamento seguro, polling/webhook, journal de todos os eventos, ACK, cadência e tratamento de limites.
3. Criar a tela/entrada iFood no ambiente de testes do PDV; mapear produtos e projetar estoque/financeiro transacionais sem duplicação. O núcleo não altera nenhum desses dados.
4. Implementar a separação e seus comandos conforme o escopo concedido, incluindo solicitação de cancelamento e atualização de quantidade. A presente etapa apenas interpreta a resposta final; não chama Picking nem cancela itens.
5. Completar agendamento/retirada e decidir o suporte a peso variável. Este parser recusa explicitamente peso variável para evitar conversão errada.
6. Testar no Windows e com a API oficial; só depois produzir vídeos e exportar logs reais.

O portal bloqueou o navegador remoto na etapa anterior. Isso não comprova falha da conta e não valida autenticação. Não repetir tentativas indiscriminadamente. O próximo dado necessário é o **tipo do aplicativo de testes (centralizado ou distribuído)**; não enviar `clientSecret`, senha ou tokens pelo chat.

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
