# iFood — continuidade da conversa de 05/11/2026

Registro dos passos, decisões e evidências disponíveis nesta conversa até o pedido de Adriano para guardar o progresso. Horários citados são os exibidos nas capturas do celular, em Brasília. Este documento é um resumo de continuidade, não uma transcrição literal nem prova de aprovação pelo iFood.

## Ponto exato de retomada

**A loja já aparece aberta no Separador e no app consumidor. O R8 foi aberto e conectado por Adriano. Na sacola estão 3 arrozes e 3 feijões; falta disponibilizar o açúcar.**

Última orientação operacional: no Portal do Parceiro, abrir **Produtos → Inativos** e procurar **Açúcar Refinado União 1kg — R$ 4,99**, para verificar/reativar o produto. Adriano ainda NÃO confirmou que encontrou ou reativou o açúcar. O caminho foi orientado; sua execução e o estado inativo não foram comprovados nesta conversa.

Adriano está fazendo um **ensaio passo a passo**. Depois fará **outro pedido, novo, gravando desde o começo**. Não tratar o ensaio como vídeo final. Não há confirmação de novo pedido concluído ou de gravação recebida nesta conversa.

## Decisões confirmadas por Adriano

- O suporte não está mais exigindo cupom, segundo relato explícito de Adriano nesta conversa. Prosseguir com essa orientação; não bloquear o trabalho cobrando cupom. A mensagem do suporte não foi anexada aqui.
- Objetivo imediato: terminar o ensaio, gravar os cenários solicitados e reunir o material para envio.
- Usar o **R8** que já foi entregue e utilizado. A instrução reconhecida pelo usuário é simplesmente **“abre o R8”**.
- Orientar com nome da tela, local e ação: um passo de cada vez, sem explicações longas, sem pedir novamente etapas já confirmadas.
- Não recomeçar a integração, não procurar botão iFood no PDV normal e não refazer a ferramenta já existente.
- Guardar esta continuidade no Baú de Ouro para evitar perda de contexto.
- Reunir **todas as exigências do suporte**, não apenas vídeos: gravações, logs, relatórios e identificação dos testes, organizados por cenário.
- Ao final, preparar **um ZIP completo** para Adriano baixar, descompactar no computador e encaminhar ao suporte. O envio ao suporte será feito por ele; não foi realizado pelo assistente.

## Passos desta conversa e evidências

| Etapa | O que aconteceu | O que foi comprovado |
| --- | --- | --- |
| Pedido antigo | Adriano tentou alterar quantidades na tela do pedido #4284, de 29/10/2026 | A captura 52421.png mostra pedido concluído às 14:02, feijão 3, açúcar 2, arroz 3 e total R$ 62,81. Essa tela é histórico, não sacola editável. |
| Tentativa de repetir compra | Foi orientado tocar “Adicionar à sacola” | A captura 52422.png mostrou bloqueio: loja abre amanhã às 00:00. |
| Identificação do programa | Houve confusão entre iFood Developer, PDV normal e programa de testes | Adriano confirmou o Portal do Desenvolvedor aberto e o PDV normal; depois reconheceu o R8. |
| R8 | Adriano abriu o R8, inseriu as credenciais localmente e informou que conectou | Relato do usuário. Viu registros concluídos, sacola final e cenário. Disse que a aba de produtos cadastrados estava vazia. Isso não prova que o catálogo remoto foi apagado. |
| Separador | Foi orientado abrir portal.ifood.com.br e “Abrir App Separador”, mantendo R8 conectado | Captura 52423.png às 11:17 mostra separador.ifood.com.br, loja correta e “Loja aberta” em verde; nenhum pedido em andamento. Não comprova qual ação causou a abertura. |
| App consumidor | Adriano voltou à loja | Captura 52424.png às 11:18 mostra modo de teste ativo, “Aberta até 23h59”, arroz R$ 5,99 e feijão R$ 7,99, ambos com quantidade 3. Total sem entrega R$ 41,94 / 6 itens. |
| Açúcar | Usuário percebeu ausência; foi orientada busca por açúcar no app | Adriano confirmou “Não tá disponível”. Não há diagnóstico confirmado entre estoque, inativação ou outro motivo. |
| Próximo passo | Orientado consultar Produtos → Inativos no Portal do Parceiro | Reativação ainda aguardando confirmação. |
| Organização | Criada pasta exclusiva no Baú e incluído acesso no índice principal | Pasta existente com README; nenhum vídeo armazenado nela ainda. |
| Entrega final | Adriano ampliou o combinado para todos os materiais exigidos | ZIP será preparado quando os materiais forem reunidos, antes do envio por Adriano. |

## Arquivos de evidência desta conversa

As imagens foram vistas nesta conversa. As referências abaixo identificam os originais existentes; **este registro não copia os arquivos de imagem para o GitHub**.

| Arquivo | Conteúdo | Referência |
| --- | --- | --- |
| 52421.png | Pedido #4284 concluído | [Original](https://chatgpt.com/library?file_id=libfile_e172db7a76ec8191bf502dba9b22ffd4&account_id=personal) |
| 52422.png | Aviso de loja fechada até 00:00 | [Original](https://chatgpt.com/library?file_id=libfile_97ad6a0ebcfc819193f7104ee010aa74&account_id=personal) |
| 52423.png | Separador com loja aberta às 11:17 | [Original](https://chatgpt.com/library?file_id=libfile_241772a9501c8191aae8baa862492a21&account_id=personal) |
| 52424.png | Loja aberta e sacola com arroz 3 / feijão 3 | [Original](https://chatgpt.com/library?file_id=libfile_f580c013c8048191b0233e5dcc564ff8&account_id=personal) |

## Identificação e continuidade técnica recuperada

- Loja: **Teste - 69.426.025 ADRIANO LEAL DOS SANTOS**.
- Chamado de homologação: **35288437**.
- Programa de testes: **R8**, separado do PDV normal. Pacote registrado no histórico: **IFOOD_CADASTRO_R8_1.zip**.
- Nome genérico “ferramenta de conferência” confundiu o usuário. Não usá-lo sozinho como instrução; dizer R8.
- O App Separador é onde ocorre a separação. O R8 recebe e confere os dados; não presumir que a aba cenário efetue cancelamento/picking no iFood.
- Histórico consultado nesta sessão: BAU_IFOOD_ATUALIZADO.md, versão 11, de 31/10/2026. Ele registra pedido #6074 e conferência local açúcar cancelado / arroz 3 / feijão 1 já executados.
- Esse histórico também registra limitação importante: recuperação complementar resolveu a sacola #6074, mas não comprova correção permanente do parser do binário R8 para novas sacolas. Não tratar o novo ensaio ou vídeo como aprovado antecipadamente; se houver falha real, usar a evidência do erro sem repetir pedidos às cegas.
- R8 conectado é relato atual; a abertura da loja está comprovada nas capturas. Não presumir sessão permanente após fechar o programa.
- Não guardar credenciais no Baú. Não alterar PDV, GLOBAL/PET, Firebase, APK, licenças ou feeds por causa desta organização.

## Pasta e pacote para suporte

[Pasta de materiais para homologação](../../../VIDEOS-HOMOLOGACAO-IFOOD/README.md).

Combinado:
1. Receber os arquivos que Adriano enviar nesta conversa.
2. Preservar os originais, identificar ensaio versus gravação final e organizar por cenário/data.
3. Conferir com o checklist efetivamente solicitado pelo suporte; registrar vídeo, data/hora/fuso, pedido e client_id do aplicativo usado quando disponíveis, sem client_secret.
4. Reunir logs oficiais de requests, relatórios e demais evidências pertinentes. Relatório local não substitui logs oficiais.
5. Manter inventário de recebido, conferido e pendente. Não declarar “tudo completo” sem conferir o conjunto.
6. Criar e verificar um ZIP com os materiais finais, fornecer download e deixar Adriano fazer o envio.
7. O checklist anterior solicita links de vídeos separados por cenário. O ZIP é a forma de entrega combinada com Adriano; não afirmar que o suporte aceita um ZIP como substituto desses links. Conferir a orientação vigente ao preparar o envio.

## Como continuar sem repetir a confusão

Retomar pela confirmação da disponibilidade do açúcar, que é o ponto ainda pendente. Depois, adicionar 3 unidades à sacola, seguindo o ensaio já combinado. Quando chegar a gravação final, iniciar antes do novo pedido e acompanhar o cenário solicitado.

Não perguntar novamente qual programa de teste existe, não mandar abrir o PDV normal para procurar menus e não tratar a loja como ainda fechada com base na captura antiga. Não exigir novos prints por hábito: pedir apenas informação que falte para a próxima ação concreta.

A preferência de comunicação foi confirmada expressamente por Adriano: **direto, concreto, um passo por vez**.
