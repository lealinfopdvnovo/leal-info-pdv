# Etapa 5B — integrações preparadas em QA; homologação remota pendente

**NÃO CONCLUÍDA / NÃO LIBERAR ETAPA 6.** Há código candidato, compilações e testes seguros, mas não há comprovação Android físico ↔ serviços de QA ↔ PDV licenciado para as três integrações. Nenhum feed, serviço real ou instalação foi alterado.

Branch pública QA: `qa/etapa5b-integrations-20261115`, a partir de `64b5ed6a057cbfdfd9d3ce9212ac001b1b6cdf19`. Produto aprovado permanece PET 1.12 no QA ANGRA. A candidata PET 1.13 e APK 0.3.7-qa005 são artefatos de laboratório, não versões publicadas. Não instalar o APK isolado como atualização do app existente.

## Comparação 34 × 37 e escopo

Origem PR34 `24a58c77a4f052d86fc6681a8c7a540f7f052d03`; candidata PR37 `a9713b8c7810d09d667947c6cca869c9bc5c62c0`; QA homologado `2fd998403a5826bc665ee6fd33c6363cd75a2cfe`. As fontes compiláveis do PDV estão fixadas em `98915ae5a9eb02825d77f74bb179e0ce27d65bd9`; os controladores 34/37 não são projetos Android completos.

- `FirebaseDeliverySync.cs` e `DeliveryManagementForm.cs` são idênticos no 34, no 37 e na fonte compilável fixada. Publicação no RTDB e consulta do rastreamento existem; leitura automática da conclusão do motoboy está ausente nesses três pontos. Não era simplesmente um arquivo perdido na migração.
- ANGRA: o PET QA aprovado contém os serviços de pareamento/sincronização. O projeto Android original está em `feature/angra-pdv-sync`, commit `acb72ed0d1025faa309efffa35a6a94f205fd7c7`, versão 0.3.6. Usa HTTPS assinado P256 + Worker/R2; não o Firebase de licenciamento. O protótipo Cloud Run de geração de imagens é outro serviço, não a comunicação estruturada.
- Speed Food: fonte privada preservada `SpeedFood_PosHomologacao_Fonte.zip`, versão real 1.0.35; pacote `com.leal.info.novo`, code 35. Auth por e-mail/senha, RTDB, localização/serviço GPS, página de acompanhamento. A variante antiga Motoboy 1.0.0 com Functions é uma proposta distinta; não foi importada como dependência da versão 1.0.35. Regras locais têm raiz read/write negados e nós `pedidos`/`rastreamentos`. Isso não comprova as regras atualmente implantadas.
- Alerta: repositório **privado** `leal-info-alerta`. `master` é base inicial; a fonte FCM funcional está em `feature/fcm-casa-adriano` / base `68f9196db478bdda2247079658bb0e986eac9a22`. Há evidência anterior de FCM com som e dois aparelhos; ela não prova PDV→Worker→FCM no 37. O pacote privado **05E**, mais recente que o protótipo 05C, fornece intake D1 e fila/transportes nativos; declara explicitamente ausência de implantação e de remetente FCM. Foi preservado e reaproveitado em candidata QA005, sem copiar serviços do cliente 003. O protótipo Durable Object foi mantido como fixture histórica, não dependência operacional adicional ao D1.
- Salão MJ: fonte e APK 1.9.5, package `br.com.salaomj.agenda`, code 24. WebView local, lembretes nativos, sincronização opcional com API Python/SQLite própria, WhatsApp por ação do usuário. Não usa o PDV. Permanece projeto privado independente; nenhum arquivo foi incorporado ao GLOBAL/PET.

## Correções efetivas no QA

1. ANGRA: candidata sem rota ativa para o gateway compartilhado; origem `.invalid` somente no código QA até provisionamento do gateway isolado. Isso não altera o DNS/endpoint de nenhum cliente instalado. Continuação nativa após autorização usando os métodos assinados originais; não gera código sem confirmação. Para de renovar automaticamente após gerar e ao fechar. Guardas de administrador/servidor do serviço original preservadas.
2. Android ANGRA: retry persistente da desvinculação a cada 30 segundos e após reinício; não mente que revogou offline. Arquiva dados/outbox/fotos por tenant. Respostas de fotos do vínculo anterior não alteram a nova sessão.
3. Speed Food: componente de conclusão + hook PET, limitado a **emulador 127.0.0.1** e configuração privada QA. Valida código/publicador/status/timestamp, atualiza somente o ID+código correto em SQLite e não duplica nem reabre cancelada. Sem senha/login em background. Não reutiliza sessão de produção. Falhas, tipos inválidos e respostas grandes preservam pendência. Sem configuração privada, o hook fica inerte.
4. Patcher PET: modifica o construtor da integração ANGRA, o ponto de customização PET para os hooks QA e somente os literais de origem nos métodos de transporte ANGRA para impedir chamadas ao gateway compartilhado; adiciona helper. Compara todos os outros métodos e recursos. Vídeo, AJUDA, login, identidade, licenciamento, host e bandeirinha preservados. Nenhuma recompilação do núcleo GLOBAL.
5. Alerta (somente repositório privado): variante QA005 da solução 05E D1/filas e composição FCM desativada por padrão. Remetente reaproveitado da referência existente, exclusivo de projeto QA; chave somente ambiente servidor. Recibo D1 não é prova de entrega. Envio ambíguo fica em revisão, sem repetição automática. Testes não usam OAuth/FCM reais. Não foi inserida DLL privada no PET público.
6. Preparação de backup: ferramenta de arquivo privado AES-256-GCM, inventário SHA-256 e restauração verificada, sem chave no arquivo/relatório; três testes com arquivos sintéticos. Limite explícito de 256 MiB por segmento. Não é backup operacional do 34.

## Matriz de homologação

| Aplicativo | Dependências | Estado no 37 | Testes executados | Pendências |
|---|---|---|---|---|
| ANGRA VIDROS | DPAPI/AndroidKeyStore P256, HTTPS, Worker/R2, SQLite/PET | Candidata compilada; **NÃO HOMOLOGADO ponta a ponta remoto** | 18 testes protocolo, WinForms/ponte PET real, emulador Android API29, preservação de cinco projetos/foto/operações sintéticos | Correlacionar identidade/grant do PC QA; acesso a gateway/tenant QA; teste físico; recuperar as cinco operações reais somente após backup e inventário |
| Speed Food | Firebase Auth/RTDB, GPS, página Hosting e módulo Motoboy | Leitura de conclusão + hook QA preparados para emulador; **NÃO HOMOLOGADO remoto** | 15 testes Windows/SQLite/transporte controlado; conferência APK 1.0.35 e cinco assets iguais à fonte | Autenticação/configuração Firebase QA; validar regras implantadas e fluxo APK→RTDB→PDV/página; GPS/segundo aparelho e ciclo de entregas |
| LEAL INFO Alerta | SDK FCM Android, Worker/D1, identidade HMAC, DPAPI, OAuth FCM servidor | Serviços candidatos privados QA005; ingestão/envio desativados; **NÃO HOMOLOGADO ponta a ponta** | Fixture 05C: 7 unitários, runtime local e Windows DPAPI; 05E D1 + composição FCM: 19 testes D1/FCM simulado, 22 de fila/transporte e DPAPI real no Actions Windows privado | Provisionar recursos QA, credencial mínima FCM e assinatura/vínculo de teste; completar fonte rastreável do APK V1.2; teste PDV→push no Android |
| Salão MJ | WebView/armazenamento local, agendador Android, API própria opcional | **INDEPENDÊNCIA CONFIRMADA**, fora do PDV | APK/fonte 1.9.5; sete assets idênticos; domingo/lembretes/JPEG no DOM; duas interfaces autenticadas via API local sem duplicação; 18 verificações da política Java | Sem nova homologação física do APK neste trabalho; nenhuma mudança necessária para integrar ao PDV |

## Firebase, Cloudflare e DNS: limites observados

O console Firebase redirecionou para conta Google **desconectada**. Não houve credencial digitada, alteração de conta, acesso a dados ou tentativa de contornar autenticação. Assim, usuários/permissões/regras/Functions/Hosting implantados ainda não foram auditados administrativamente. Não há conexão CLI administrativa disponível nesta sessão. Não fornecer senhas ou contas de serviço pelo chat/GitHub.

O gateway ANGRA foi lido por metadados/logs: Worker versão 18, fonte `99a1604875bf2ee86cb57333f81027317ca2037c`, R2. Os nomes de variáveis secretas existem, mas valores são ocultos; valor oculto não significa segredo ausente. Logs recentes mostram `/sync/status` e `/sync/exchange` HTTP 200 no mesmo hostname relatado. Portanto o print de DNS é evidência de falha naquele aparelho/momento, **não** comprovação de hostname permanentemente inválido. Nenhuma troca especulativa de endpoint, escrita R2, registro/revogação de dispositivo ou alteração Firebase foi executada.

As cinco pendências reais continuam no aparelho/vínculo PC-CASA; os testes usaram cinco registros sintéticos. Não foram recuperadas, removidas, renumeradas ou enviadas a outro tenant por esta etapa.

## Rastreabilidade e preservação

APK Salão MJ SHA-256 `cb61ef31f32c8d54dcfcd3673dc9d0b90cef536769936aa36c74d6c8e3be9c77`; fonte ZIP `c64b521e01bb50028a6298df840151775d9f96620dfb5c38ef42472072f04a54`.

Speed Food 1.0.35 SHA-256 `5ede49e8825b329951de55c4f97309c8f890ef4c999a96e46f885bc09733419d`; fonte ZIP `608efff02a56157279c89268f11d8fda1fbf072bb7138f714d08103965fb8518`.

Alerta V1.2/code5 SHA-256 `d7f78ba390663d3219664f6397c08dbc4b26b48c2a59ffcfa8a2ceaba268e781`. O fonte Git encontrado declara 1.1/code2; o pacote Work V1.2 contém somente APK-base, chave e arte, não projeto atualizado. Não alegar compilação reproduzível exata da V1.2 sem recuperar esse fonte/checkpoint. A inspeção do APK confirmou a classe adicional TestTopic.subscribe e tópico 003; o código encontrado no Git não contém essa classe. Chaves e ZIPs sensíveis permanecem privados e não foram adicionados ao GitHub público.

Repositórios de produção, branches 34/37, feed QA aprovado, assinaturas dos APKs preservados. A confirmação final de refs, commits, Actions e resultados fica no registro semanal da Etapa 5B.


## Execuções de compilação/teste

- PDV/PET/ANGRA: código `11f6b50dedc33d18528ab81975cca1d92264b926`; branch QA isolada. Actions final e artefatos no registro semanal. Candidata PET 1.13 e APK 0.3.7-qa005 não publicados. Última correção impede endpoint compartilhado nos candidatos.
- Alerta privado: código `b290a2a069e76469c54f5a40e287d0d967c9d01d`, Actions [43739635956](https://github.com/lealinfopdvnovo/leal-info-alerta/actions/runs/43739635956) SUCCESS. 19 testes do D1/remetente simulado, 22 verificações de fila/transporte e prova Windows DPAPI/reabertura/recibo. O texto genérico histórico do teste menciona Linux, mas a execução registrada ocorreu em windows-latest; DPAPI foi verificado separadamente no próprio Windows.
- Não confundir os 18 testes do gateway ANGRA em R2 em memória com uma implantação real, nem o remetente FCM simulado com um push recebido no telefone.

## O que falta antes do transbordo

1. Autenticação administrativa segura Firebase/Cloudflare, conferência das regras/serviços realmente implantados e provisionamento exclusivo de QA; não modificar serviços compartilhados automaticamente.
2. Gateway QA ANGRA e grant/identidade de teste; configuração de Auth/RTDB/Hosting QA do Speed Food; configuração FCM/Worker/D1 QA do Alerta. Os candidatos permanecem sem acesso operacional a produção por padrão.
3. Reconciliar a fonte versionável do APK Alerta V1.2 com o artefato aprovado; projeto Android QA deve registrar a própria identidade Firebase antes de receber push de teste.
4. Instalar candidatos apenas nos dispositivos de laboratório e validar o circuito completo com PDV legitimamente ativado e Android de teste, incluindo foto/medidas, finalização de entrega e push recebido. Não apagar nem migrar as cinco pendências reais durante esse teste.
5. Só depois registrar homologação ponta a ponta e submeter a liberação da Etapa 6. **A Etapa 5B não está concluída e a Etapa 6 não foi iniciada.**

Execução PDV final: [43740129386](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43740129386), código `11f6b50dedc33d18528ab81975cca1d92264b926`, Windows/protocolo/Android SUCCESS. Origem ANGRA dos candidatos bloqueada até provisionar QA. Evidências e APK isolado: artefatos da execução, sem publicação em feed. Prova de inventário e plano de backup em `organization/evidence/etapa5b/`.

## Verificação autenticada Firebase — continuidade após f165432 (2026-11-15)

Consulta somente de leitura no Console Firebase, com sessão Google autenticada pelo proprietário. A integração oficial ChatGPT continua instalada=false; login no Console não equivale à instalação/autorização do plugin. Não repetir login ou instalação já tentados.

Confirmado no projeto existente novo-91da7436 (Spark):
- Apps Android LEAL INFO ALERTA (com.example.lealinfoalerta) e Speed Food (com.leal.info.novo); terceiro app Web Gerador de Licenças.
- Cadastro Android do Alerta corresponde ao pacote do APK V1.2 inventariado. A tabela de certificados SHA estava sem entradas; não foi alterada. Isso, isoladamente, não comprova causa de falha de FCM.
- Configurações / Cloud Messaging: API Firebase Cloud Messaging (V1) ATIVADO; API legada DESATIVADA. Nenhum serviço foi habilitado por esta consulta.
- Visão geral apresenta campanhas Alerta de 25/10/2026, duas entradas com 2 envios cada; histórico não constitui teste atual de entrega em QA37.
- Não foi identificado projeto QA separado na lista exibida desta conta. O projeto existente também contém o Gerador de Licenças e Speed Food; não tratá-lo como ambiente descartável.
- Permissões IAM, regras e serviços restantes não foram integralmente auditados; não inferir privilégios administrativos de escrita pelo acesso ao Console.

Confronto com candidata privada qa37-005-d1: o remetente exige projeto leal-info-qa37-005, ainda não provisionado, e os recursos D1/Worker continuam placeholders/desativados. O login não provisiona esses recursos. Não substituir essa identidade pelo projeto compartilhado automaticamente. APK V1.2 aprovado ainda contém assinatura de tópico de teste 003; não enviar teste 005 para o tópico 003 nem atualizar o aplicativo em uso.

Próxima dependência humana: indicar projeto QA existente autorizado ou autorizar especificamente um projeto separado e seus recursos exclusivos de teste. A instrução vigente impede criar outro projeto ou modificar o existente; portanto não executar provisionamento nem concessões IAM sem decisão específica. Nunca pedir senha/token/chave pelo chat. Depois de haver destino seguro, configurar credencial mínima somente no runtime servidor e preparar APK/PDV de laboratório com identidade 005 isolada; a divergência fonte V1.1 / APK V1.2 continua pendente.

Roteiro físico preparado: celular de teste com notificações permitidas; PDV QA legitimamente ativado; um evento sintético com identificador único; conferir fila persistida, recibo do serviço e recebimento com som no telefone; testar reconexão sem duplicação. Recibo de aceite não equivale à entrega. Proprietário participa apenas da instalação/autorizações do aparelho e confirmação de recebimento. Nenhum desses testes reais foi executado neste checkpoint.

Sem novos testes/workflows, backup, envio de push, geração de chaves, implantação, publicação ou alteração em GLOBAL/PET/produção. Checkpoint f165432 preservado como ancestral; atualização documental apenas. Etapa 5B NÃO HOMOLOGADA ponta a ponta; Etapa 6 bloqueada.

## Provisionamento Firebase QA autorizado — 2026-11-15

Após autorização expressa do proprietário, a lista autenticada foi verificada: só havia novo-91da7436; nenhum QA adequado. Foi criado o projeto **LEAL INFO ALERTA QA37 005**, ID **leal-info-qa37-005**. A criação havia ficado em preparação antes de interrupção por limite da ferramenta; na retomada foi verificada a existência, sem repetir a criação.

Verificações no Console:
- Plano Spark; indicação "Sem custos (US$ 0/mês)". Não foi vinculado faturamento, ativado Blaze ou serviço pago.
- Android registrado: **com.example.lealinfoalerta.qa005**, apelido LEAL INFO Alerta V1.2 QA005. Identidade separada para futura instalação paralela, não atualização do APK operacional.
- API Firebase Cloud Messaging (V1): **Ativado**; API legada: **Desativada**.
- Gemini e Analytics foram desmarcados no assistente de criação.
- Página verificada: https://console.firebase.google.com/project/leal-info-qa37-005/settings/cloudmessaging/android:com.example.lealinfoalerta.qa005
- Nenhuma nova consulta/escrita no projeto compartilhado após selecionar o QA.

O download google-services.json foi tentado pelo botão oficial, mas a espera expirou sem caminho de arquivo confirmado. Não alegar configuração Android integrada, download validado ou APK compilado. O app está cadastrado; isso não comprova push.

Dependência seguinte: criar identidade técnica dedicada com permissão de envio FCM somente neste projeto QA e configurar autenticação exclusivamente no runtime privado do remetente. A conta Admin automática do Firebase não foi usada para gerar chave ampla. Criação de credencial/permissão exige confirmação específica no momento; nenhuma chave foi gerada ou exportada.

Serviço Worker/D1 QA segue sem implantação; ingestão/envio continuam desativados. Não habilitar Functions/Blaze nem provisionar serviços pagos para contornar. Fonte rastreável exata V1.2 e futura variante Android QA continuam pendentes; nenhuma reconstrução foi apresentada como reprodução do APK aprovado.

Testes novos: verificações administrativas de existência, identidade, Spark e API ativa. Nenhum teste aprovado ou workflow foi repetido. Não houve envio de notificação nem homologação física PDV→serviço→Android. Próximo teste físico depende de remetente QA autenticado, APK QA configurado e aparelho do proprietário.

Checkpoint f165432 e commit 935ad20 mantidos como ancestrais; alteração somente documental nesta branch. Produção, GLOBAL, PETs, Speed Food, Gerador de Licenças e ambiente 34 preservados. Sem backup real, publicação ou Etapa 6. ETAPA 5B AINDA NÃO CONCLUÍDA.

## Conta técnica FCM QA autorizada — impedimento de acesso IAM (2026-11-15)

Autorização expressa recebida para identidade técnica exclusiva de leal-info-qa37-005, mínima permissão FCM e preferência por autenticação sem chave permanente. Não conceder Owner, Editor ou Admin amplo; chave privada, se indispensável, somente em gerenciador seguro de segredos QA.

Console Firebase autenticado continua exibindo projeto QA, Spark sem custos e FCM V1 ativo. O link oficial "Gerenciar as permissões das contas de serviço" abriu:
https://console.cloud.google.com/iam-admin/serviceaccounts/project?authuser=0&project=464377590790&hl=pt-BR
O Console Google Cloud retornou **Site Unavailable / Unable to access this site**. Uma recarga de recuperação apresentou o mesmo resultado. Não há evidência de erro de senha, falta de permissão IAM ou bloqueio antirrobô; causa da indisponibilidade não determinada. Não insistir em rotas alternativas nem considerar o Google Cloud indisponível para o proprietário só com essa evidência desta sessão.

Nenhuma conta técnica criada, permissão concedida, chave gerada ou segredo armazenado. Autenticação sem chave não pôde ser avaliada/configurada no IAM. A opção Admin automática do Firebase não foi usada como substituto amplo.

Dependências pendentes:
1. Restabelecer acesso autorizado ao Console IAM do projeto QA para criar/verificar identidade técnica com permissão de envio mínima.
2. Definir identidade do runtime QA e federação/impersonação sem chave quando disponível; sem isso, não afirmar viabilidade nem provisionar chave fora de cofre.
3. Registrar autenticação somente no runtime privado; serviço Worker/D1 permanece inativo.
4. Integrar configuração Android QA e recuperar origem rastreável V1.2; registro do app não significa APK pronto.
5. Executar envio real sintético e confirmar recebimento no aparelho físico com identificador único, preservando clientes existentes.

Não executados envio/recebimento FCM real, compilação Android nova ou homologação ponta a ponta. Testes aprovados não repetidos; nenhum workflow, backup, publicação, alteração GLOBAL/PET/produção ou ambiente 34. Checkpoints anteriores preservados; esta continuação modifica somente o relatório. Etapa 5B NÃO CONCLUÍDA; Etapa 6 bloqueada.

## Plano alternativo FCM — verificações independentes (2026-11-15)

Escopo: apenas QA. Não foi repetida a concessão OAuth cloud-platform nem chamada equivalente para criar identidade após a rejeição automática.

### Identidade e execução existentes
- Console Firebase QA confirma **2 contas de serviço** e mostra a identidade automática firebase-adminsdk-fbsvc@leal-info-qa37-005.iam.gserviceaccount.com. Isso comprova existência; não comprova papel IAM mínimo, credencial utilizável ou federação. Não gerar chave Admin ampla nem assumir autorização de impersonação.
- Não existe arquivo de credencial ADC no diretório isolado usado pelo SDK neste trabalho. Login de navegador não é identidade autenticada do remetente.
- Inventário das conexões Sites disponíveis retornou somente ANGRA VIDROS — Simulação e Salão MJ — Ficha de cuidados. Nenhum serviço Alerta QA foi identificado nessa conexão. Não é um inventário completo da conta Cloudflare/GCP; não concluir ausência global de recursos.
- Candidata qa37-005-d1 continua com workers_dev=false, ingestão/envio/FCM=false e database_id placeholder. É código preparado, não runtime provisionado. O remetente atual requer FCM_SERVICE_ACCOUNT no servidor; não contém um provedor de identidade sem chave já configurado.
- Resultado: **nenhuma combinação identidade + runtime + autenticação QA suficiente foi comprovada**. Remetente não ligado e nenhum push disparado.

### Android — inspeção adicional sem repetir testes aprovados
Artefato preservado V1.2: inspeção estática do manifesto e identificadores DEX:
- INTERNET e POST_NOTIFICATIONS declaradas.
- allowBackup=false.
- AlertaMessagingService e serviço FirebaseMessagingService não exportados.
- default_notification_channel_id = leal_alertas_003.
- Identificadores compilados: leal_alertas_003 e leal_info_alerta_003_teste.
- Pacote do artefato NÃO é com.example.lealinfoalerta.qa005; não instalar como candidato QA005.
Fonte disponível na branch privada: MainActivity pede POST_NOTIFICATIONS no Android >=33; em versões anteriores abre as configurações pertinentes. Refaz a consulta do token ao abrir/verificar, trata falha sem declarar sucesso e confere persistência. TokenStore usa AndroidKeyStore com AES/GCM; tokens não foram obtidos, copiados nem incluídos no relatório. Canal da fonte é IMPORTANCE_HIGH, porém permissão efetiva, som e configurações do usuário precisam do aparelho. Essas observações da fonte não são prova de execução do APK V1.2, cuja rastreabilidade integral continua pendente.

Cadastro Firebase QA005 já existe; google-services.json integrado ao APK e token FCM QA real **NÃO COMPROVADOS**. Sem APK QA configurado e aparelho com Google Play Services não inventar token ou simular entrega como homologação. Não reconstruir o APK aprovado a partir da fonte V1.1 divergente.

### Demais integrações — sem dependência de envio FCM
- ANGRA: protocolo assinado HTTPS/Worker/R2 independe de Firebase FCM. Candidatos e testes existentes preservados; falta gateway/tenant QA exclusivo e circuito físico foto/medidas. Não reutilizar gateway compartilhado nem tocar cinco pendências reais.
- Speed Food: Auth/RTDB/GPS/Hosting independem do remetente Alerta. Componente de conclusão e 15 testes Windows existentes mantidos; identidade, regras e fluxo remoto QA continuam sem homologação. Não transferir configuração do projeto compartilhado.
- Salão MJ: independência do PDV confirmada na etapa anterior; preservado separado, sem necessidade de migração para o Alerta.
- Leitura dos resultados Actions existentes (sem reexecutar): PDV 43740129386 SUCCESS, código 11f6b50dedc33d18528ab81975cca1d92264b926; Alerta 43739635956 SUCCESS, código b290a2a069e76469c54f5a40e287d0d967c9d01d. Nenhum novo teste desses workflows.

### Bloqueio preciso e próximo procedimento
Operação impedida: autorização do SDK/ADC com OAuth cloud-platform para criar conta técnica, papel mínimo e vínculo IAM no projeto QA. Revisão automática rejeitou o escopo amplo; a confirmação do proprietário não suprimiu a restrição de execução nesta sessão. API IAM oficial declara cloud-platform para serviceAccounts.create, roles.create e serviceAccounts.setIamPolicy; não foi encontrada alternativa de escopo OAuth menor para essas operações. Não insistir ou ocultar o mesmo consentimento por outra ferramenta.

Procedimento viável depende de um caminho administrativo permitido, fora da operação bloqueada: disponibilizar identidade técnica já provisionada com permissão efetiva cloudmessaging.messages.create somente em leal-info-qa37-005 e runtime QA com federação/impersonação já autorizada; ou resolver institucionalmente o bloqueio da integração administrativa para provisionar essa identidade. Não basta a existência da conta Admin. Sem runtime federado verificado, não prometer autenticação sem chave. Se chave for indispensável, somente em cofre QA, nunca como arquivo partilhado/chat/APK/Git.

Depois: recuperar fonte exata V1.2 ou reconciliar explicitamente as diferenças; gerar variante paralela QA005 com projeto leal-info-qa37-005, canal leal_alertas_005 e destino 005; compilar somente a variante afetada e executar verificações novas de configuração/permissão/token. Só após remetente autenticado executar evento sintético único PDV→serviço→FCM→celular, confirmar som/reconexão/ausência de duplicação. Estes passos continuam pendentes, não executados.

Não houve nova conta/chave, concessão IAM, alteração GLOBAL/PET/produção, backup do 34, publicação ou Etapa 6. Nenhum teste anteriormente aprovado foi repetido. Checkpoints f165432, 935ad20, 7f99510 e c79765b permanecem ancestrais. **Etapa 5B NÃO CONCLUÍDA / ponta a ponta NÃO HOMOLOGADO.**
