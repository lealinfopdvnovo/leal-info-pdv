# REGISTRO DA CONVERSA E CHECKLIST OFICIAL — 13/11/2026
**Projeto:** LEAL INFO PDV / integração iFood Groceries / módulo Order (Orders Virtual Bag)
**Chamado atual:** 36296851
**Chamado anterior encerrado:** 35288437
**Natureza:** transcrição dos requisitos compartilhados pelo proprietário e registro das decisões. Não equivale à aprovação técnica.

## Resposta iFood — fluxo de homologação (13/11, 17:24)
O iFood informou que a integradora deve solicitar homologação somente após concluir o desenvolvimento. O analista validará front-end, requests de API, aplicativo e loja de teste; poderá encerrar o chamado se a integração estiver incompleta. Groceries e Marketplace devem ter chamados distintos; Financial também separado. Solicitou confirmação explícita de ciência. Proprietário confirmou ciência; iFood registrou confirmação.

## Resposta iFood — etapa de evidências (13/11, 17:43)
O iFood informou que o checklist Orders Virtual Bag foi enviado e solicitou links completos dos vídeos iniciados por http://, https:// ou www., acessíveis ao analista. A análise técnica ocorrerá após o recebimento.

## CHECKLIST OFICIAL, texto fornecido pelo proprietário (13/11, 17:42)

> Homologação - Checklist de vídeos do módulo Orders Virtual Bag
>
> Módulo Order Virtual Bag
>
> Cenário 1 — Separação Parcial de Itens
>
> Gravar um vídeo realizando um pedido contendo:
> - 3 produtos;
> - 3 unidades de cada produto;
> - Cupom de desconto aplicado.
>
> Durante a separação, executar o seguinte fluxo:
>
> Item 1 — Solicitar cancelamento
> Item 2 — Separar as 3 unidades sem alterações
> Item 3 — Separar apenas 1 unidade
>
> Objetivo da validação
> Confirmar que a integração está consumindo corretamente o campo: items.unavailable
>
> Este é um dos principais critérios avaliados durante a homologação e um dos maiores motivos de reprovação.
>
> Cenário 2 — Pedido para Retirada
>
> Aplicável somente às integrações que possuem suporte à modalidade Retirada.
>
> Gravar um vídeo demonstrando:
> - Criação de um pedido para retirada;
> - Recebimento do pedido;
> - Fluxo completo de separação.
>
> Cenário 3 — Pedido Agendado
>
> Gravar um vídeo demonstrando:
> - Criação de um pedido agendado;
> - Recebimento do pedido;
> - Fluxo completo de separação.
>
> ATENÇÃO
>
> Precisamos também dos logs das requests do seu aplicativo de teste, que ficam disponíveis no Developer Portal.
>
> Acesse seu Developer Portal
> Vá em Logs da API
> Procure por HTTP Requests
> Selecione o aplicativo de testes que você usou nas gravações
> Clique em Buscar
> Faça o download dos logs usando o botão disponível
> Anexe os logs neste chamado.

## Registro de decisões do proprietário nesta conversa
- O proprietário pediu uma área separada no GitHub para a homologação iFood, contendo histórico, checklist, evidências, testes, logs, vídeos, rastreabilidade e revisão reiterada.
- Meta declarada: buscar conformidade máxima, “99%”, deixando a decisão final para o analista iFood; não prometer aprovação garantida.
- Proteger GLOBAL e PETs: GLOBAL é núcleo; PETs são personalizações por cliente; não misturar o projeto de homologação com o PDV de produção.
- Não enviar evidências incompletas ou não conferidas; auditar o R8 5.2 e reutilizar apenas materiais válidos.
- Usuário não sabe se o R8 5.2 suporta Retirada; verificar no código/execução, não presumir.
- Trabalho deve continuar mesmo após troca de conversa usando estes registros como fonte de continuidade.

## Materiais anteriores a auditar
Pasta Drive: https://drive.google.com/drive/folders/1IC3xUt2t9P3At1RmKDoU29pJQqV3I5Dm
- 01_Criacao_Confirmacao_Pedido.mp4
- 02_Separacao_Parcial_9407.mp4
- 03_Logs_API_Oficiais.json
- 04_Relatorio_R8_5_2.txt
- 05_Texto_para_o_chamado.txt

Pedido #9407 UUID dae27b3f-f0c2-4912-af6d-09bf91d01235; resultado relatado: açúcar indisponível, arroz 3, feijão 1. Isso NÃO comprova por si só cupom, pedido de cancelamento, operações HTTP de Picking ou consumo correto de items.unavailable. Os logs consultados anteriormente eram insuficientes para demonstrar todos os critérios. Validar novamente a fonte original.

## Estado da organização
- Branch isolada: homologacao-ifood-36296851-evidencias
- Índice: HOMOLOGACAO-IFOOD/CHAMADO-36296851/README.md
- Este registro: HOMOLOGACAO-IFOOD/CHAMADO-36296851/00-CHECKLIST-E-ESCOPO/REGISTRO-CONVERSA-E-CHECKLIST-2026-11-13.md
- Não houve auditoria integral dos vídeos nem dos logs nesta etapa. Não declarar aprovação.
- Não alterar GLOBAL, PET, PR #37, feeds, produção nem enviar dados ao iFood sem autorização.
- Guardar vídeos/logs originais em armazenamento adequado; GitHub pode conter índice, hashes, relatórios e links, sem credenciais ou dados sensíveis.

## Próxima etapa
Auditar código e comportamento R8 5.2, confrontar checklist item a item com vídeos e logs HTTP do Developer Portal, construir matriz de rastreabilidade e relatar lacunas antes de refazer testes. Repetir revisão com critérios distintos (funcional, API, vídeo/cronologia, anexos/acesso, liberação). Entrega final somente após validação e autorização do proprietário.
