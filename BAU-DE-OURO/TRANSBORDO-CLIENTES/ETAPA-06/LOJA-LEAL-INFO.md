# Etapa 6 — Loja Leal Info — QA exclusivo — 23/11/2026

## Transbordo Loja 001 publicado — 10.469 — 23/11/2026

- Canal Loja 001 com oferta **10.469** e PET **1.3**. A atualização usa o botão **ATUALIZAR** nas origens 10.467 e 10.468. Os seletores dos dois binários oficiais reconheceram a URL usada na publicação e rejeitaram a oferta para o cliente 002. Sem teste operacional no PC da Loja, conforme dispensa.
- Código compilado: `418f661d072658c82fde1876a5e90c9b4b04435b`. Publicador: `0722a8b69b6e960af492c9cd945a97e58515382f`. Commit do feed: `d6c244b8a343e307d5fa6579030b7486a20413a2`, blob `493cbdc55df979ab3bed43e4ae6ec1f2a9d5be33`. A revisão do commit confirmou que apenas `clients/001/version.json` mudou. A release anterior 10.468 e o GLOBAL compartilhado foram preservados.
- [Validação Windows 45220051283](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/45220051283), artefato `14841180721`. Contrato PET, integridade, igualdade das dependências nas duas origens, mesma versão do aplicador protegido, barra de backup (percentuais e FECHAR sem cancelar), rollback exato de ambas as origens, retomada SYSTEM e rejeição de backup adulterado. As provas de rollback e saúde usaram dados sintéticos. O teste de interface da barra não enviou backup real. A Cozinha Premium, fotos e bandeirinha foram verificadas por hash. O PET original Loja foi preservado byte a byte na cápsula 1.3 e a validação de licença foi conservada.
- [Publicação 45220185900](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/45220185900). O ZIP hospedado foi baixado e teve o hash conferido após a publicação. [Release da Loja 001](https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/tag/client-001-v10.469-transbordo).
- O roteiro auxiliar da candidata histórica 10.468 foi acionado pelo filtro amplo da branch. A execução 45220185578 encerrou na comparação com a versão 10.469. Essa execução montava a outra candidata e não foi usada como evidência de validação ou publicação da 10.469.

| Componente | SHA-256 |
| --- | --- |
| ZIP 10.469 | `5e911c6b0f2e8e220bab8ddeb9364eb4367dddb5da172a7e9421ad6bf5f47989` |
| LealInfoPDV.exe | `fb6383a320eaf9246be3a64e6cbd5346f1e5a44653dbb3e48d847a3c3f811a69` |
| LealInfoPDV.dll | `eb201541e0613e780635519c4ad3a89a0edbaf8d17d73a3f710661a0a1655ecb` |
| LealInfoPDV.deps.json | `52ab8c90c991a3cac17b23dfe7baa8191c3098e751378f2730d973e1e5374af9` |
| PET Loja 1.3 | `d6c3921d19680cdc04bbab676d750e2e15bd8ba4421d08752944d746262423e3` |
| Aplicador de recuperação conjunta | `cf69303a8a04461d3820fa102eae075a2a233b8bce6fce5f204de00e9a3ca331` |


## Continuação da publicação Loja 001 — verificação essencial interrompida — 23/11/2026

- O feed oficial `clients/001/version.json` continua em **10.468**, blob `872d9a92f99e91cb886a22925633610c55cf4c50`, fonte `e00766afb701c3c4f9a0f269d5e5ad53daf8c006`, SHA-256 declarado `47496b92f62755ebc8669593b25a389c9cc8143c5e67676dca600aca6ceb03be`. A URL retornou HTTP 302 e o download do artefato redirecionado expirou nesta execução. O hash acima é o declarado no feed.
- A URL do feed contém a tag `client-001-backup-10.468-20261121`. O filtro `IsSafeClientPackage` nos fontes de referência `7aa5d383` e `e00766af` exige o prefixo `client-001-v`. O manifesto publicado, por si só, não confirma oferta no botão ATUALIZAR. Não houve confirmação em binário instalado.
- O pacote 10.468 da barra foi gerado com EXE, DLL e deps.json da variante de backup, conforme Actions [44916328824](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/44916328824). A ponte inicial do transbordo compara o conjunto de hashes fixo da 10.467 em C# e no worker PowerShell. O rollback da variante de backup como origem não está qualificado pelo Actions [45212037418](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/45212037418). A receita de transbordo também não aplica o patch de barra de backup daquele pacote.
- A busca de commits por 10.469 não encontrou resultados nos dois repositórios. Esta consulta não comprova ausência de tags ou releases. A versão não foi reservada. Nenhum manifesto, release ou artefato publicado foi alterado. Nenhum teste operacional local ou novo GitHub Actions foi executado. Não foi publicada nova atualização do transbordo.


## Variante de distribuição compilada — publicação interrompida por colisão de versão — 23/11/2026

### Execução efetiva
- Implementada `qa/etapa6-loja/prepare_distribution.py`: reutiliza integralmente a receita aprovada até antes das transformações exclusivas QA; não injeta QaIsolation, redirecionamentos de dados, bloqueios da bandeirinha nem retorno incondicional do atualizador. Preserva validação de licença, contrato PET, cápsula inicial e recuperação conjunta.
- Implementado workflow `.github/workflows/etapa6_loja_distribution.yml`, somente compilação/validação e artefato. Não possui etapa de publicação ou credencial de distribuição.
- Commit de implementação: `93f7f45205e2032287a24c1a251a533e88245f7d`.
- [Actions 45212037418 — SUCCESS](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/45212037418), job 137823138215: host real e PET001 compilados, contrato/cápsula aprovados; recursos originais Cozinha/fotos/bandeirinha preservados por hash; ativação permanece anterior aos dados/PET; saúde depende do login real. Sete cenários sintéticos de recuperação conjunta passaram.
- Artefato `etapa6-loja-distribution-validated`, ID `14835674935`, SHA-256 do arquivo de evidências `96b3f8c363f51ba39c965a256eb31e6582fd37a95e8f2996937cb58fadd319f6`. Hashes individuais do pacote e dos componentes estão em `completion-evidence/destination.json` dentro do artefato.
- Testes específicos da variante modificada foram executados; não foram declarados testes físicos. O filtro amplo do workflow QA anterior também acionou a execução 45212037189; esse acionamento adicional foi involuntário e não constitui nova necessidade de homologação. A tentativa de cancelar via CLI não foi executada por ausência de autenticação CLI; nenhuma credencial foi solicitada.

### Falha essencial de pré-publicação
A leitura do feed oficial `clients/001/version.json` confirmou versão **10.468 já publicada**, pacote `LEAL_INFO_BACKUP_001_10_468.zip`, fonte `e00766afb701c3c4f9a0f269d5e5ad53daf8c006`, SHA-256 `47496b92f62755ebc8669593b25a389c9cc8143c5e67676dca600aca6ceb03be`; blob do feed `872d9a92f99e91cb886a22925633610c55cf4c50`.

A nova candidata também é 10.468, com conteúdo diferente. O mecanismo oficial exige avanço estrito de versão (`scripts/distribute_channels.py: plan`); além disso, instalações já em 10.468 não selecionariam outra 10.468. Não substituir manifesto/artefato da versão existente por outro conteúdo. A ponte inicial atual valida os hashes específicos da origem 10.467 e não qualifica automaticamente o pacote de backup 10.468.

**PUBLICAÇÃO INTERROMPIDA**, conforme instrução expressa para falha técnica. A remoção dos bloqueios QA foi implementada e compilada; o transbordo total NÃO está publicado nem disponível como nova atualização no botão ATUALIZAR. O canal continua oferecendo a atualização de backup anterior, não esta migração. A dispensa de homologação física continua válida e não é o bloqueio.

### Procedimento tecnicamente necessário
Reservar uma versão livre posterior a 10.468; ajustar a compatibilidade PET e os pontos que fixam a versão; preservar a funcionalidade do pacote de backup já publicado e definir/verificar explicitamente quais hashes de origem serão aceitos (10.467 e, se suportada, a distribuição anterior 10.468). Compilar e validar somente essas alterações afetadas antes da liberação exclusiva001. A autorização existente não expirou; não é necessário pedi-la novamente. Nesta continuação, a ordem de interromper diante da falha foi respeitada, sem avançar à publicação.

Nenhum feed, release, canal de cliente, instalação, banco, licença ou ambiente de outro cliente foi modificado. Somente a branch QA exclusiva recebeu implementação e registro. Os relatos históricos de conclusão técnica QA permanecem limitados àquele escopo.

---

## Publicação autorizada — verificação essencial interrompeu a liberação — 23/11/2026

O proprietário autorizou expressamente publicar exclusivamente para Loja 001, dispensando testes operacionais locais. Essa autorização substitui o estado anterior de publicação não autorizada. **A publicação está agora AUTORIZADA, MAS BLOQUEADA TECNICAMENTE; não foi executada.**

Verificação objetiva dos arquivos aprovados no commit 70c381e, sem repetir testes:
- `qa/etapa6-loja/prepare.py` injeta QaIsolation no início do programa, exige contexto LOJA-QA-ETAPA6 e redireciona caminhos de dados/configurações para raízes QA.
- A mesma receita substitui as URLs oficiais de atualização por `https://127.0.0.1:1/QA-LOJA-NO-PRODUCTION/`, impede inicialização/registro da bandeirinha na cópia QA e insere retorno incondicional no ComponentUpdateCenter com mensagem de publicação não autorizada.
- `.github/workflows/etapa6_loja_qa.yml` compila precisamente essa variante isolada e entrega artefatos de evidência. O sucesso 45181025244 comprova essa candidata QA, não um pacote promovível sem alteração para a instalação atual.
- Portanto, publicar os mesmos binários no canal da Loja não é equivalente a promover a implementação aprovada: manteria isolamento e bloqueios de teste e não atenderia ao funcionamento da instalação original.

**Decisão:** interrompida a publicação conforme a cláusula expressa do proprietário sobre falha em verificação essencial. Nenhum feed, canal, release, instalação, licença ou dado foi alterado. Nenhum workflow/teste foi repetido. Apenas este registro foi atualizado na branch exclusiva.

**Requisito técnico para desbloquear:** preparar uma variante de distribuição exclusiva Loja 001, com a mesma arquitetura e payload PET preservado, removendo somente as intervenções de isolamento do harness QA; comprovar compatibilidade da ponte 10.467 e dos manifestos/pacotes no mecanismo oficial, integridade e backup/recuperação/rollback antes de alterar o canal. Isso exige compilar e validar a variante efetiva de distribuição, porque seus binários diferem do candidato QA aprovado. A dispensa de testes físicos permanece válida e não é o impedimento. A autorização de publicação já foi concedida; não falta nova autorização genérica.

**Estado da entrega:** transbordo técnico QA registrado anteriormente permanece concluído dentro daquele escopo. Transbordo total da instalação da Loja e disponibilidade pelo ATUALIZAR NÃO CONCLUÍDOS. Não afirmar atualização disponível. Angra, Casa do Adriano, Gelamaq e GLOBAL/PETs de produção não receberam operações nesta continuação.

---

## Estado vigente — transbordo técnico concluído em QA — 23/11/2026

**TRANSBORDO TÉCNICO DA LOJA LEAL INFO CONCLUÍDO EM QA.** Por decisão expressa do proprietário nesta data, a homologação operacional no computador físico da Loja foi dispensada para o encerramento técnico desta etapa. Essa decisão substitui a exigência de acesso Windows como condição de conclusão técnica registrada nas seções históricas abaixo; não transforma testes operacionais não executados em aprovados.

- Arquitetura entregue: **GLOBAL QA 10.468 + PET exclusivo Loja 001 versão 1.3**, conforme a mesma metodologia e mecanismos do Angra/PR37, sem arquitetura alternativa.
- Código aprovado preservado: `702fff5093bf2de793d86a7898f1cc2268c63047`.
- Evidências automáticas reaproveitadas, sem repetição: [Actions 45181025244 — SUCCESS](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/45181025244), artefato `etapa6-loja-qa-evidencias` ID `14822480216`, SHA-256 `26f86523aeaa856f9516f51ff8ec551e34e9281cf8e5856970b35bb309ac0b2e`.
- Conferência de preservação baseada nas evidências já aprovadas: payload original Loja.PET.1.2 conservado integralmente por hash dentro do PET001 1.3; recursos originais de Cozinha Premium e bandeirinha confirmados por hash. Não houve nova modificação de implementação ou personalização.
- Isolamento técnico aprovado no escopo automático: contratos/cápsula, conteúdo permitido, hashes, staging, bloqueios e recuperação conjunta protegem a separação GLOBAL/PET. Os sete cenários sintéticos documentados abaixo passaram; isso não comprova execução operacional com dados/licença reais.
- Não foram repetidos workflows, builds, auditorias ou testes. Esta continuação altera somente este registro na branch exclusiva.
- Licenças, dados, configurações e instalações atuais não foram manipulados. Angra Vidros, Casa do Adriano, Gelamaq, ambiente 34, GLOBAL/PETs de produção e canais de clientes não receberam alterações desta continuação.
- **Nenhuma atualização foi publicada em produção, nenhum canal foi alterado e o botão ATUALIZAR não foi executado.** Os candidatos permanecem artefatos QA; a instalação atual da Loja não foi transbordada nem substituída no computador físico.

### Resultado e limites da conclusão
| Item | Estado vigente |
|---|---|
| Transbordo técnico e compilação GLOBAL QA/PET Loja | CONCLUÍDO — evidências automáticas aprovadas preservadas |
| Preservação e isolamento técnicos das personalizações | APROVADOS no escopo de hashes, contratos e testes automáticos registrados |
| Testes físicos de ativação, login, vendas, estoque, financeiro, Cozinha, Vidraçaria e impressão | NÃO EXECUTADOS / NÃO HOMOLOGADOS — dispensados pelo proprietário como condição desta entrega técnica |
| Backup, atualização e rollback operacionais com cópia privada licenciada | NÃO EXECUTADOS / NÃO HOMOLOGADOS — fixtures automáticos aprovados não equivalem a esses testes |
| Instalação atual da Loja | PRESERVADA — sem aplicação dos candidatos QA |
| Publicação em produção | NÃO AUTORIZADA / NÃO REALIZADA — exige decisão separada do proprietário |

**Encerramento:** entrega técnica concluída, sem homologação operacional declarada. Nenhum acesso ao Windows é exigido para encerrar esta entrega conforme a nova decisão. Publicação permanece parada para autorização separada. Não avançar para outro cliente. O histórico abaixo permanece preservado para rastreabilidade; suas condições anteriores de encerramento ficam superadas somente pela decisão expressa acima.

---

## Escopo
Origem declarada pelo proprietário: Loja 10.467. Origem técnica previamente identificada por hash no PR37: pacote fd979804f5de82c58e1b911a2f0a449e6d8937cae06ef0c0fa39917c48dd161b, DLL 61ec4bdbbcdb7be92cee0fcac421c360cae5e58cd6604b73ef2eaad0717ca0d7. Não foi repetida coleta da instalação.

Branch exclusiva: qa/etapa6-loja-leal-info-20261123. Base existente preservada: 2d575a5a023744b2ff7c2554f68c8b8ddf32c840 (controladores Angra homologado, não seus dados/licença).

## Componentes candidatos
- GLOBAL QA 10.468: mesma receita compilável do PR37/Angra, contratos IPetExtension/IPetStartup/IPetReady, PetRuntime/PetHost/PetBootstrap, atualizador independente e transação/recuperação conjunta já existentes.
- PET Loja 001 versão candidata 1.3: personalização Loja.PET.1.2 original incorporada sem recompilar sua implementação; preservação SHA-256 054e30f5ed5d40a0b539511f50c4415f346b8ec20cbefb325acf364d0f2acd8f. Selo original verificado separadamente. Nenhum PET/mídia/identidade Angra incorporado ao PET001.
- Versões são identificadores candidatos, não nova publicação nem comprovação de instalação.
- Mesmos mecanismos de compatibilidade, conteúdo permitido, hashes, staging, saúde e rollback. Não foi criada segunda arquitetura.

## Preservação
Autenticação, licença e banco da origem têm verificações de fonte na receita existente. O licenciamento mantém sua implementação integral; caminhos graváveis ficam em QA/Loja isolado, conforme mecanismo usado no Angra. Não foi copiado banco, configuração privada, senha, licença ou credencial de cliente.
Apenas o PET001 é compilado. Nenhum payload Casa/Gelamaq/Angra é importado. Sem instalador, sem feed, sem release, sem atualização na Loja e sem alterações em produção.

## Validação
Workflow etapa6_loja_qa.yml: build do host real e PET001, contrato/cápsula de migração, isolamento de caminhos executado pelo host e cenários de recuperação conjunta sobre arquivos sintéticos. Os testes antigos de outros clientes/Android não são repetidos. Resultados pertencem ao artefato etapa6-loja-qa-evidencias no run desta branch.

## Pendências obrigatórias de homologação
NÃO HOMOLOGADO operacionalmente: inicialização positiva com licença legítima da Loja, login, vendas/pagamentos, estoque/financeiro, Cozinha, Vidraçaria, impressão física e migração/backup/rollback com cópia privada dos dados reais. CI de fixture não comprova esses fluxos. Nenhuma licença pode ser inventada ou validação contornada para marcar esses gates como aprovados.
Necessário Windows QA privado/persistente autorizado, legitimamente ativado para a Loja, com cópia privada de dados e configurações, sem sobrepor a instalação de uso. Não solicitar banco, licença ou senha pelo GitHub/chat.
A barra de backup 10.468 publicada em outra tarefa não é incorporada automaticamente à origem aqui declarada 10.467; requer confirmação apenas se a origem efetiva mudou.

## Publicação
PROIBIDA nesta etapa. Nenhum job possui contents:write ou segredo de publicação. Somente artefatos de QA; aguardar homologação e autorização específica do proprietário.

## Resultado automático final — 23/11/2026

- Código validado: 702fff5093bf2de793d86a7898f1cc2268c63047.
- Actions SUCCESS: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/45181025244
- Artefato: etapa6-loja-qa-evidencias, ID 14822480216; SHA-256 do arquivo de evidências: 26f86523aeaa856f9516f51ff8ec551e34e9281cf8e5856970b35bb309ac0b2e.
- Host GLOBAL QA 10.468 e PET Loja 001 1.3 compilados no Windows. Contrato modular, ativação antes da inicialização dos dados/PET, saúde adiada até login e cápsula do PET001 verificados no código compilado.
- Recursos originais confirmados por hash: ReadyKitchen.photos.zip, Kitchen.TablesPremium e LealInfoPDV.UpdateNotifier.Native. Personalização original Loja preservada no seu PET. A mensagem genérica herdada da receita financeira menciona Loja/Gelamaq; nesta execução o laço foi restrito a 001: nenhum payload ou perfil002 foi importado/compilado.
- Diagnóstico executado pelo host confirmou pastas QA separadas, produção bloqueada e ausência de contorno de licença. Esse diagnóstico termina antes da ativação e NÃO comprova login licenciado.
- Sete casos PASS: antes do primeiro PET, após primeiro PET, interrupção/retomada SYSTEM, backup original corrompido rejeitado, bloqueio GLOBAL respeitado, arquivo PET inesperado rejeitado e commit conjunto sintético. Integridade dos arquivos-canário preservada.
- destination-10468 contém o pacote/cápsula CANDIDATO para análise em QA, não uma atualização liberada à Loja. Nenhum instalador foi criado.

### Correções de QA realizadas
Run 45179755512 detectou a ausência do contrato financeiro opcional; recuperada seletivamente a receita aprovada do contrato e do carregador da personalização original001. Run 45180184652 foi cancelado por falha de escopo de variável no roteiro de diagnóstico, corrigida com validação do diretório e timeout. Somente a cadeia dependente dessas correções foi reexecutada. O sucesso final não transforma as execuções anteriores em aprovadas.

### Proteção de produção
Refs antes/depois preservadas: channel/global 48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1; client/001 55e3a10d2c1b32b7db1a980e7f462a1d07f5a87c; client/002 39fad30d4bb7d7a13421d726d85dbefb6de043d2; client/003 137612d259a0fcecdbc2c2165893ba73988eb847; Angra qa/angra-etapa4-setup-20261114 2d575a5a023744b2ff7c2554f68c8b8ddf32c840. Repositório updates/main preservado em 9a3cedf1ff85bf50ac2f08c05475626cfb838fde.

### Estado de entrega
Preparação técnica QA aprovada. ETAPA 6 LOJA NÃO HOMOLOGADA OPERACIONALMENTE: falta Windows QA privado e persistentemente ativado para a Loja, com cópia privada de dados/configurações, para os testes de login, vendas, estoque/financeiro, Cozinha, Vidraçaria, impressão, atualização e rollback após inicialização licenciada. Nenhuma operação na instalação real foi executada. Não foi repetida coleta de hashes, solicitado segredo ou alterado o 34. Não publicar e não avançar a outro cliente.

## Homologação operacional autorizada — impedimento de acesso local — 23/11/2026

O proprietário autorizou preparação e testes QA no próprio computador da Loja, mantendo o PDV de atendimento aberto e a instalação 10.467 intacta. Publicação continua expressamente proibida.

**Disponibilidade nesta sessão:** não há conexão de controle do Windows local da Loja. O ambiente de execução fornecido é um workspace remoto Linux; o navegador de nuvem não oferece acesso ao sistema de arquivos, dispositivos ou sessão Windows da Loja. O PDV aberto no computador do proprietário não fica acessível automaticamente. Não foram solicitadas senhas, arquivos de licença ou banco no chat/GitHub.

### Execução efetiva desta continuação
- Consultado o relatório existente na branch exclusiva; preservados código validado 702fff5, Actions 45181025244 e resultados já aprovados.
- Nenhum workflow/teste foi repetido, nenhum pacote recompilado ou feed publicado.
- Nenhuma pasta QA, cópia de banco/configurações, ativação, venda, impressão, backup ou restauração foi executada no computador da Loja, pois o acesso local não está disponível.
- Esta atualização é exclusivamente documental, na branch qa/etapa6-loja-leal-info-20261123.

### Matriz operacional
| Teste | Estado desta continuação | Evidência/impedimento |
|---|---|---|
| Ambiente Windows QA persistente | NÃO PREPARADO | Sem acesso ao Windows local |
| Ativação legítima e inicialização | NÃO HOMOLOGADO | Sem execução com licença legítima da Loja |
| Login e permissões | NÃO HOMOLOGADO | Depende do ambiente local isolado |
| Vendas, pagamentos e estoque | NÃO HOMOLOGADO | Não executar movimentações no banco real |
| Financeiro | NÃO HOMOLOGADO | Testar somente cópia privada consistente |
| Cozinha Premium | NÃO HOMOLOGADO | Sem execução operacional local |
| Vidraçaria | NÃO HOMOLOGADO | Sem execução operacional local |
| Impressão e dispositivos | NÃO HOMOLOGADO | Equipamentos Windows locais indisponíveis |
| Independência GLOBAL/PET operacional | NÃO HOMOLOGADO | Contrato/cápsula aprovados em CI; falta ciclo após login legítimo |
| Backup, atualização QA e rollback operacional | NÃO HOMOLOGADO | Sete fixtures anteriores aprovados; falta cópia privada licenciada |

### Procedimento seguro para execução por uma sessão Windows local autorizada
1. Confirmar a instalação de uso em modo somente leitura e reaproveitar os hashes já coletados. Não fechar, substituir, atualizar ou registrar outro agente sobre o PDV em atendimento. Se a origem efetiva mudou, interromper a aplicação do pacote que exige os hashes 10.467 já identificados; não presumir equivalência pelo número.
2. Preparar pasta QA persistente, privada e exclusiva com acesso restrito ao operador autorizado. O executável desta candidata exige a pasta LOJA-QA-ETAPA6 e o marcador .qa-test-only. Preservar runtime/dependências compatíveis conforme inventário já aprovado, sem reinstalar o PDV.
3. Com o SQLite de produção em uso, obter snapshot transacional consistente usando SQLite Online Backup para destino QA privado, abrindo a origem somente para leitura. Não copiar apenas o .db com WAL ativo, não executar checkpoint/restore/migração de esquema no banco real e não disparar o backup/SMTP do PDV de produção. Validar integridade e registrar apenas hashes/contagens/resumo sem dados pessoais.
4. Copiar somente as configurações necessárias para a pasta QA. Antes do primeiro login, conferir caminhos de dados, bancos de rede, portas, mutex, impressoras e agentes. O QA não pode apontar para banco compartilhado de produção, operar sincronizações reais, emitir documentos fiscais, cobrar pagamentos ou enviar mensagens/e-mails de clientes. Alterações de redirecionamento devem ocorrer somente na cópia QA; se o isolamento não puder ser garantido, parar sem interferir no atendimento.
5. Exercitar a licença legítima no próprio hardware por mecanismo oficial suportado. Não editar identidade, serial, assinatura, plano, validade ou verificador; não copiar credenciais para artefatos. Se a licença ou um singleton não permitir execução paralela isolada, parar e registrar a restrição, sem encerrar o PDV em uso.
6. No banco QA, executar casos identificados como TESTE: login administrativo/restrito, venda com formas de pagamento disponíveis sem cobrança externa, estoque, financeiro, mesa/pedido Cozinha e projeto de Vidraçaria. Confrontar com a cópia inicial e registrar resultados privados. Impressão somente de documento de teste explicitamente identificado, sem acionar gaveta/produção ou transmitir documento fiscal.
7. Usar pacotes e manifestos locais de teste para exercitar os mesmos trabalhadores GLOBAL/PET e atualização independente, sem habilitar feed de produção. Não remover o bloqueio QA de rede por conveniência. Atualização GLOBAL deve conservar PET/dados; atualização PET deve conservar GLOBAL/dados.
8. Fazer backup da cópia QA; testar restauração e interrupção/rollback exclusivamente nessa cópia. Comparar hashes e integridade do banco, identidade da licença e manifestos antes/depois. Nunca restaurar sobre a pasta de atendimento.
9. Guardar evidências detalhadas em armazenamento privado local. No Baú, registrar apenas resultados, versões, hashes e capturas sem dados pessoais/credenciais. Não enviar banco ou licença ao GitHub/chat.
10. Somente após aprovação de todos os testes operacionais, apresentar pacote identificado e evidências para autorização específica de publicação. Não publicar, migrar outro cliente ou considerar esta etapa concluída por aprovação dos fixtures.

**Estado final desta continuação:** BLOQUEADA PARA HOMOLOGAÇÃO OPERACIONAL E PUBLICAÇÃO. A autorização do proprietário já existe; o requisito faltante é acesso técnico efetivo a uma sessão Windows local isolada e autorizada, não uma nova autorização genérica. O PDV de atendimento e todos os clientes permanecem sem qualquer ação operacional desta sessão.
