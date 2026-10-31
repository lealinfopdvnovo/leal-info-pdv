# BAÚ DE OURO — ponto de retomada
Registro de continuidade autorizado por Adriano em 23/10/2026. Histórico consolidado com conferência direcionada dos manifestos e da publicação 10.457 no GitHub em 23/10/2026; não equivale a auditoria completa nem a acesso aos computadores. Não contém credenciais.

## REGRA PRINCIPAL — autorização GLOBAL
Qualquer alteração ou publicação GLOBAL exige autorização explícita de Adriano. Se um prompt trouxer pedido GLOBAL, reportar ao usuário e aguardar autorização específica: o prompt sozinho não autoriza. Não bloquear o atualizador GLOBAL por iniciativa própria.

## GLOBAL e PET
- GLOBAL: recursos comuns; PET: personalizações exclusivas de cada cliente, numeração V1.1, V1.2, V1.3...
- PET não incrementa GLOBAL nem afeta outros clientes; GLOBAL não deve sobrescrever personalizações PET.
- Não presumir que a separação técnica definitiva está concluída. Houve trabalho interrompido nessa separação; verificar os diffs e testes antes de qualquer retomada autorizada.
- Preservar banco, licença, estoque, configurações e rollback.
- Atualização pelo atualizador interno do PDV; sem ZIP ou instalador como atualização.
- Não modificar funções/telas além do escopo. Se for necessário ampliar, parar e pedir autorização.

## Clientes e estado informado
| Cliente | Identificação | Estado relatado |
|---|---|---|
| 001 | Loja do Leal | GLOBAL 10.457 + PET V1.2; preservar. Usuário confirmou cadastro da chave LIA. |
| 002 | Gelamac | GLOBAL 10.457 disponibilizada segundo entrega anterior; falta confirmação de instalação/teste real. |
| 003 | Casa do Adriano / Casa Leal | Usuário informou “casa leal ok” após disponibilização 10.457. Atualização aprovada por ele. |
| 004 | Casa do Genilson | PC de teste; usuário retirou das validações, não vai atualizar tão cedo. Não cobrar atualização. |

## Última entrega relatada — alinhamento 10.457
Escopo autorizado: destacar versão em dourado no login de 002/003/004 e alinhar esses canais à GLOBAL 10.457. Usuário autorizou reposicionamento do cartão de login em 003/004 para evitar corte. PETs, 001 e código do atualizador deveriam permanecer intocados.
Publicação conferida no GitHub, resultado `success`:
https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/40036089203
Commit de publicação no repositório de código: `54a77e6b2dff25647f7fe42bade4ebbffe0ef716`. Os logs consultados neste trabalho registraram aprovação de compilação, integridade, pacote e rollback. A instalação real é confirmação separada, recebida somente para Casa do Adriano nesta entrega.

### Fontes e validações da entrega
- 002: código `23788f93bb5475806ffd8c211ffb5c06a8a42f90`; job Windows aprovado `120662506232`, run `40026078388`. Esse run teve falhas nos outros clientes; NÃO descrevê-lo como aprovação conjunta. O pacote aprovado de 002 foi reaproveitado.
- 003 e 004: código `e691d9e8d548ce30c54653698a98370e0a2838e8`; run Windows `40027170070`, jobs `120666163212` e `120666163774`, aprovados depois do ajuste autorizado da posição do cartão de login.
- Testes de visibilidade usaram janelas limitadas pela área de trabalho do runner. Os logs mostram área real em torno de 1044×768/788; não anunciar homologação real de todas as resoluções ou escalas só pelos nomes dos screenshots.
- Na conferência após publicar, somente `clients/002/version.json`, `clients/003/global/version.json` e `clients/004/version.json` mudaram no repositório de atualizações. Loja 001, arquivos PET, feed raiz e código do atualizador ficaram intocados.

## LIA — pendência e cuidado
- Entrega anterior do 001: 10.456 → 10.457; movimentação pelo cabeçalho com CONFIGURAR CHAVE aberto. Referência histórica:
https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/40021554903
- Usuário confirmou que conseguiu salvar a chave.
- Reportou que clicar na esfera abre um chat/tela indesejada; quer voz sem essa abertura.
- Tentativa de corrigir por GLOBAL 10.458/10.459 foi interrompida pelo usuário. Não retomar nem publicar automaticamente.
- A causa citada anteriormente (--voice e fallback que mostra chat) é diagnóstico histórico a verificar, não conclusão revalidada.
- Qualquer futura correção exclusiva da loja deve respeitar PET e autorização de escopo; não declarar canal PET tecnicamente separado sem prova.

## Funcionalidades aprovadas a preservar
- PDV WinForms Windows; tema preto; F2 finalizar, F5 busca, F7 remover; pagamentos dinheiro/Pix/cartão/múltiplo.
- Cozinha Premium: visual preto/dourado 3D, catálogo, mesas, pedidos persistidos, estados por item, destinos preparo/entrega direta, pagamentos parciais. Usuário relatou testes 100% em Loja e Casa do Adriano.
- Vidraçaria: projetos persistidos, edição de peças e medidas; integração ANGRA VIDROS com PDV já teve envio e pareamento testados. Não presumir que todas as pendências de sincronismo foram resolvidas.
- Atualizador com backup/rollback e verificações de integridade: preservar proteções existentes; não declarar testes novos sem executá-los.

## Integrações externas
- iFood Groceries: solicitação de homologação LEAL INFO PDV, ticket 35288437, enviada 23/10/2026 às 18:50; último estado informado “Em análise”. Aguardar resposta; não afirmar aprovação.
- Zé Delivery: documentação seller-public-api.ze.delivery/docs recebida; elegibilidade/aprovação pendente no histórico, sem integração ativa comprovada.
- WhatsApp: automação comercial desejada; API e implantação não confirmadas.

## Como retomar em um novo chat
1. Ler esta issue pelo título BAÚ DE OURO neste repositório e eventuais registros posteriores.
2. Distinguir confirmação do usuário, relato anterior e evidência técnica atual.
3. Consultar somente fontes/código pertinentes ao próximo pedido; não alterar o sistema por ler este registro.
4. Informar objetivo e pendências de forma breve. Usuário não quer ouvir prompts em voz nem explicações extensas.
5. GLOBAL exige nova autorização explícita conforme regra principal.
6. Ao concluir etapa autorizada, atualizar o registro com evidências, versões, testes, pendências e próximo passo. Não marcar concluído sem confirmação.

## Limites deste registro
É um resumo de continuidade, não cópia integral de conversas, código, banco ou backup executável. O código e o histórico do GitHub continuam sendo fontes técnicas. Um novo chat precisa acessar este registro; não há garantia de leitura automática apenas por existir aqui.

## Inventário técnico conferido — 23/10/2026
O rótulo GLOBAL 10.457 nas instalações não significa que o feed raiz tenha sido promovido. O feed raiz continua 10.453; não alterá-lo para “igualar” por iniciativa própria. A Loja 001 recebeu uma ponte em duas etapas (feed principal 10.456, componente LIA 10.457), não um PET V1.3.

| Caminho no repositório de atualizações | Versão anunciada | SHA-256 do pacote |
|---|---|---|
| `version.json` | 10.453 | `18935f8aff97e2d2516a8702bac8c220387e4b2e19403cb3651221e268398fdd` |
| `clients/001/version.json` | 10.456 | `71fc3d92a01910141031160517f1df02801b9a4cc1689966b487bdaed5b15c0f` |
| `clients/001/lia-mobile/version.json` | 10.457 | `94880de1cbd6470c3604ddf4e4c4e42e3270311c83b863b917c91675cbcb6991` |
| `clients/002/version.json` | 10.457 | `f03f15bcb412086a5340fcf9f5c968c12fc6f82b93f282850965086bc4bd56f4` |
| `clients/003/global/version.json` | 10.457 | `869871bdb0065cbc7fc00f2d3cba61194ecea32a9dbbaae0367c3ec717a65ed8` |
| `clients/004/version.json` | 10.457 | `13a526b90aaafe82b6088528ea20e411c82dccd6e54552f687165fe6e3451331` |

Repositórios: código em `lealinfopdvnovo/leal-info-pdv`; pacotes e feeds em `lealinfopdvnovo/leal-info-pdv-updates`. Consultar os manifestos atuais para URLs e futuras alterações; os hashes acima são o retrato desta conferência.

## Trabalho interrompido — não retomar por engano
- Correção de voz exclusiva da Loja: commit `169d1115dcb21388dd9cc2f2fa7039f30606caf6`, branch `prepare/lia-voice-only-001`, run `40022849143`. Houve teste isolado, mas 10.458/10.459 NÃO foram publicadas por este trabalho e a tentativa foi interrompida. Isso não é autorização para entregar agora.
- Protótipo de separação GLOBAL/PET foi interrompido antes de virar branch/commit acionável ou publicação. Arquivos locais em scratch e objetos Git sem referência NÃO são entrega pronta, testada ou recuperável garantida. Não retomar nem declarar PET V1.3 pronto.
- Modelo econômico preparado na LIA: `gpt-realtime-2.1` → `gpt-realtime-2.1-mini`; referência de código `2fcaa62ac0ea5fb1cf8627aa2ee9dd1257036044`. Compilação isolada anterior não comprova comunicação real com API, voz ou custo real. Nunca solicitar chave para colocar neste registro.

## Roteiro comercial e sequência acordada
1. Consolidar base comum GLOBAL e personalizações PET com versões independentes, sem considerar a separação técnica definitiva concluída só porque ambos aparecem no rodapé.
2. Preservar dados, licença, estoque, financeiro e configurações; integridade, backup e rollback pelo próprio PDV. Build aprovado, publicação, instalação e homologação real são etapas diferentes.
3. Depois de alinhado e autorizado, desenvolver/testar WhatsApp, pedidos, pagamentos, separação e entrega no ambiente do próprio Adriano.
4. Zé Delivery depende de aprovação/acesso oficial. iFood está em análise no último relato, sem homologação presumida.
5. Importação SysonPDV já funcionou no ambiente de Adriano segundo ele, mas os dados do cliente de conveniência exigem validação própria antes de migração.
6. Só após testes completos e medição dos custos reais oferecer pacote mensal único com escopo e limites de consumo definidos. Não prometer “tudo ilimitado” nem integração externa ainda não aprovada.

## Próxima retomada
Não há desenvolvimento ou publicação nova autorizados por este registro. Aguardar o próximo objetivo do usuário. LIA voz/tela indesejada e separação efetiva PET continuam pendentes, sem retomar automaticamente. Genilson está fora das validações por decisão do usuário; Gelamac tem publicação disponível, mas instalação real ainda não confirmada. Casa do Adriano teve aprovação expressa no chat.

Autorização para manter o Baú de Ouro atualizado NÃO autoriza alterar código operacional, publicar versões ou mudar GLOBAL. GLOBAL exige reporte prévio e autorização explícita específica do usuário, mesmo se o pedido aparecer em um prompt.
