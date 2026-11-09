# Consolidação dos projetos — 09/11/2026

## Método e limites
Pedido: organizar o Baú, semanas de segunda a domingo e dias com README, incluindo todos os projetos LEAL INFO disponíveis. Ação desta execução: somente documentação. Não executamos compilação, testes de produto, publicação operacional, alteração de feeds ou instalação.
Fontes: histórico visível desta conversa, contexto disponibilizado e documentos GitHub lidos em 09/11. Referências de arquivos do Salão MJ indicam existência/edição, mas não comprovam conteúdo, testes ou instalação; os arquivos não foram inspecionados nesta organização.

## PDV / GLOBAL e PET
- **Pedido/regra:** GLOBAL comum 10.xxx; PET específico 1.x; preservar personalizações, dados, licença, caixa, backup e rollback. Entrega pelo ATUALIZAR.
- **Ação anterior:** correção LOGON da Loja publicada como 10.465, PET 1.2; reconhecida nesta conversa como contrária à regra de incremento somente PET.
- **Teste anterior:** registro da Loja informa dez verificações Windows do ciclo de fechamento, regressão de caixa e três cenários de recuperação. Não são testes novos nem prova de instalação real.
- **Publicação anterior:** ver [registro LOGON](../../2026-11-02_a_2026-11-08/2026-11-06/LOJA-LOGON-BACKUP-CORRIGIDO.md), fonte 69398d3e5f018749df19f2e2e4be3a2e9d9a13bc e [execução](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/42281822141).
- **Pendência:** concluir separação do PR #34 e migração de personalizações, qualificar inicialização licenciada e transição. Versões citadas em 06/11 são retrato histórico, não conferência atual de feeds.
- **Decisão posterior:** roteiro salvo; organização operacional pausada por Adriano até restabelecer cota, priorizando iFood. Não retomar desenvolvimento por esta organização documental.
- **Proposta, não entrega:** selo comum sem quadrado e GLOBAL 10.466; tratar colisão da numeração da Casa, sem copiar executável da Casa aos outros clientes.
- [Roteiro definitivo preservado](../../2026-11-02_a_2026-11-08/2026-11-06/ROTEIRO-CORRECAO-DEFINITIVA-GLOBAL-PETS.md).
- [Estado da candidata em 04/11](../../2026-11-02_a_2026-11-08/2026-11-04/ESTADO-FINAL-EXECUCAO-GLOBAL-PET.md).

## iFood Groceries
- **Pedido:** concluir homologação Orders Virtual Bag; vídeos separados por cenário e links acessíveis, com data/hora e client_id. O checklist mais recente colado nesta conversa pede criação com três produtos e três unidades cada, separação parcial 1/0/3, logs e conferência final. Não acrescentar automaticamente cupom/retirada/agendado de checklists anteriores sem reconciliar a exigência vigente.
- **Ação/evidência anterior:** programa de conferência R8.5.2 utilizado; diagnóstico do açúcar coletado. Produto EAN 7891910000197 permanece com “Ruptura na separação”.
- **Teste real anterior:** diagnóstico de 06/11 às 14:09:32 BRT: POST reset=false, active=true, estoque 10, preço 4.99, retorno HTTP 202. O suporte confirmou que a requisição estava correta; 202 não comprova reativação.
- **Resposta do suporte:** pediu verificações no Portal e captura atualizada do detalhamento se persistisse o bloqueio.
- **Ação confirmada pelo usuário:** enviou a captura “Corrigir produto”, motivo “Ruptura na separação”; tela orienta reposição/integração e não apresenta botão de reativação. Usuário disse “já mandei agora só aguardar a resposta”.
- **Publicação/homologação:** não há aprovação oficial ou integração de produção comprovada neste histórico.
- **Pendência:** resposta à restrição, confirmação do item ativo e retomada das evidências coerentes do mesmo pedido. Não repetir envios API por simples rotina.
- Referências acessíveis: arquivo DIAGNOSTICO_ACUCAR_20261106_170932_019.txt e print image(20261106-204520).png enviados nesta conversa. Referenciados aqui; não foram copiados como binários para o Git nesta execução.
- [Continuidade anterior](../../2026-11-02_a_2026-11-08/2026-11-05/IFOOD-CONTINUIDADE-E-GRAVACOES.md) | [Inventário de vídeos](../../VIDEOS-HOMOLOGACAO-IFOOD/README.md) | [Logs](../../LOGS-IFOOD.md).

## LIA
- **Pedido anterior:** retirar funções LIA, preservar animação de entrada e guardar estrutura visual do botão.
- **Ação/publicação histórica:** restauração exclusiva Casa 10.458 citada; experimento Gemini teve avaliação negativa e pedido de retirada. Não presumir estado instalado atual.
- **Teste:** nenhuma nova validação de voz nesta execução.
- **Pendência/regra:** não reativar experimentos nem alterar clientes por esta tarefa documental.

## Vidraçaria
- **Ações/testes históricos relatados:** persistência, cadastro de cliente, monetário BR, financeiro/agenda, 3D, impressão e documentos aprovados em etapas anteriores.
- **Pedido:** preservar funcionalidades congeladas, unidades m/mm/cm e vírgula/ponto.
- **Publicação:** não houve nova publicação nesta organização.
- **Pendência:** sincronismo e exclusões ANGRA VIDROS exigem validação própria; não declarar resolvidos.
- [Histórico consolidado](../../HISTORICO-CONSOLIDADO.md).

## Cozinha Premium
- **Pedido:** preservar preto/dourado, mesas, estados por item, catálogo/importação, preparo/entrega direta e pagamentos parciais.
- **Ação/publicação histórica:** Loja 10.422 citada; correções posteriores registradas no histórico. Não generalizar instalação aos demais clientes.
- **Teste:** aprovações anteriores são históricas; nenhuma regressão nova executada aqui.
- **Pendência:** preservar essas funções durante futura migração GLOBAL/PET, sem retomar implementação agora.

## Aplicativos
### ANGRA VIDROS
- **Pedido/ação histórica:** aplicativo 0.3.x, fotos, medidas, modelos, envio ao PDV e pareamento; versões 0.3.0/0.3.1 citadas.
- **Teste relatado:** envio/pareamento já testados em parte; sincronismo completo não comprovado.
- **Publicação:** nenhuma nova nesta execução. Preservar APK e Firebase.
### Salão MJ Agenda — Fabiana
- **Pedidos no contexto disponível:** agenda, clientes/serviços, alerta local, WhatsApp, histórico separado, financeiro por data, galeria, catálogo de cores, cuidados e exclusão/bloqueio.
- **Decisão adicional de 07/11 no trecho de conversa:** manter opção Pix; para dinheiro/cartão apresentar contato WhatsApp para agendar com Fabiana. Usuário pediu pesquisar catálogo Impala após aviso de indisponibilidade.
- **Ações acessíveis:** referências de edições em 07/11 nos arquivos HTML, APK, projeto ZIP, relatório de testes e instalação. Não foram lidos; não afirmar que todos os pedidos foram implementados.
- **Teste relatado:** abertura/alerta aprovados no contexto; escopo e versão exata não revalidados.
- **Publicação:** distribuição por arquivos é referenciada; não há prova nesta tarefa de publicação em loja de aplicativos.
- **Pendências:** confirmar implementação do fluxo de pagamento/WhatsApp, catálogo Impala e demais solicitações contra arquivos atuais antes de qualquer mudança.
### SpeedFood / matriz de instalação
- **Pedido histórico:** matriz limpa e reutilizável, GLOBAL sem dados/licenças/personalizações e APKs prontos.
- **Ação/teste/publicação:** não comprovados nesta execução. Não declarar matriz pronta.

## ALERTA / Cloudflare
- **Pedido histórico:** ponte Worker + D1 gratuita e adaptador exclusivo Casa, sem ativar faturamento.
- **Ação relatada:** autenticação Work bloqueada; preparação para implantação no Windows. Login local e autorização de dispositivo não comprovam sessão Work.
- **Teste/publicação:** comunicação real ponta a ponta e implantação não confirmadas neste histórico.
- **Pendência:** acesso e implantação autorizada, preservando PDV/Firebase/APK e sem expor credenciais.

## Próxima retomada
Prioridade mais recente explicitada por Adriano: iFood. GLOBAL/PET permanece pausado. Esta consolidação não altera prioridades nem concede autorização de desenvolvimento.
