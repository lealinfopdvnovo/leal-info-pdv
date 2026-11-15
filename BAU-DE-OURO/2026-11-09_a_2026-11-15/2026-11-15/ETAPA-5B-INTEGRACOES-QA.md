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


Relatório QA versionado: https://github.com/lealinfopdvnovo/leal-info-pdv/blob/f1654323c25299bc38726f218b022d03fa905c5d/organization/ETAPA5B-INTEGRACOES-20261115.md

Plano/ferramenta de backup e provas sintéticas: mesma revisão QA em organization/evidence/etapa5b/. Nenhum backup operacional do 34 foi realizado.

## 2026-11-15 — Prioridade exclusiva: vínculo definitivo ANGRA Android ↔ PDV nativo

**Decisão expressa do proprietário:** 003/Casa do Adriano era destino provisório de testes. Destino exclusivo pretendido é o PDV próprio ANGRA VIDROS. Não revogar o vínculo provisório antes de comprovar novo recebimento. Não modificar Casa, outros clientes, Alerta/FCM/Firebase/QA005, ambiente 34, GLOBAL nem iniciar Etapa 6.

### Evidências consultadas
Painel Mestre, Diário, Plano de Etapas e este registro semanal. Fontes Android/PDV feature/angra-pdv-sync (acb72ed...), candidata QA e gateway original (99a16048...). Metadados do Site oficial confirmam gateway ativo versão 18, sem implantação nova. Testes anteriores aprovados não reexecutados.

### Onde o vínculo está persistido — confirmado por código, não por leitura do aparelho
- Android: diretório privado files/pdv-sync-state.json, gravado por AtomicFile. Campos tenant/name/links, estado de revogação, revisão, pendências e confirmação de recebimento. Perfis antigos ficam em files/pdv-profiles/<SHA256(tenant)>.json; dados/fotos devem continuar segregados.
- AndroidKeyStore: alias angra-vidros-device-v1; deviceId é SHA256 da chave pública. Não exportar chave, não limpar dados/reinstalar para trocar vínculo.
- Gateway: objeto R2 sync/grants/<deviceId>, com role=app, tenant, pdv e linkEpoch; dados em sync/data/<tenant>/... e fotos em sync/photos/<tenant>/... . Registro do PDV e epoch devem continuar compatíveis.
- Windows: base de referência usa %LOCALAPPDATA%/LealInfoPDV/angra-sync.config (tenant/enabled protegido DPAPI CurrentUser), angra-sync.identity (chave P256 protegida DPAPI) e lealinfo.db/angra_sync_links_v1. O caminho efetivo do QA instalado depende do isolamento; ainda não foi obtido no PC real.
- O número comercial 003 não substitui tenant/deviceId. Não trocar strings, renomear perfil, copiar chave do Casa ou forçar tenant 005.

### Limitação real da troca
SyncCoordinator.connect recusa conexão quando já há tenant ativo. /sync/pair no gateway recusa aparelho ativo de outro tenant. O fluxo atual, portanto, NÃO comprova novo vínculo antes de remover o antigo no mesmo deviceId. Não usar DESVINCULAR agora e não desativar essa proteção.

Estratégia a preparar após identificar o PDV real: validação paralela com identidade Android de homologação separada e dados sintéticos, sem tomar o grant da identidade antiga. Confirmar código assinado do PDV ANGRA, tenant, destino, recibo pdvId e foto íntegra. Para a troca no mesmo aplicativo preservando o antigo até validar o novo, será necessário candidato Android com preparação de segunda identidade/perfil e promoção transacional após recibo; isso ainda NÃO foi implementado nem publicado. Não atribuir o sucesso de um aparelho paralelo à migração da identidade original. Após confirmação da promoção, revogar somente o grant app antigo mediante assinatura dele; nunca /sync/revoke do PDV Casa, que pode afetar irmãos.

### Identidade/licença/PET do PC
Cliente esperado conforme decisão atual: ANGRA VIDROS (QA); referência homologada 005/PET 1.12 no repositório. Evidência antiga contract-005.json é fixture assinada e declara ActualCustomerLicenseUploaded=false / CustomerMachineTested=false; NÃO prova licença/contrato instalado hoje. Não houve acesso remoto ao Windows nem ao Android reais nesta sessão, portanto licença vigente, hash da identidade do dispositivo, tenant, contrato carregado, versão instalada e código real permanecem **NÃO VERIFICADOS**.

### Portão antes de qualquer mudança
Obter diagnóstico somente de leitura no computador ANGRA: cliente mostrado/estado da licença (sem chave/serial), versão GLOBAL/PET, identidade/contrato carregado e tela da integração com estado de autorização/conexão. No Android: versão instalada e estado do vínculo/pendências; não limpar, desvincular ou reenviar dados reais. Não coletar chaves privadas/config DPAPI para o GitHub. A operação física requer disponibilidade desses equipamentos, sem nova autorização de escopo.

### Critérios ainda pendentes
1. Validar pedido sintético com foto/medidas/serviço/modelo/detalhes/imagem e recibo no PDV ANGRA, comprovando tenant exato; Casa não recebe.
2. Verificar fechamento/reabertura, reinício e atualização preservando identidade/chaves/perfil e idempotência.
3. Inventariar cinco pendências antigas sem apagá-las ou transportá-las automaticamente. Recuperação exige decisão por operação e controle de duplicação.
4. Só após o novo vínculo funcionar, revogar exclusivamente a identidade provisória antiga e comprovar que Casa mantém suas próprias funcionalidades.
5. Promoção futura do QA deve preservar tenant/deviceId/chaves/grant/epoch e infraestrutura. Licença e contrato continuam validados; troca de rótulo/versão não preserva identidade sozinha.

Resultado desta retomada: investigação e preparação documental concluídas; **vinculação real NÃO EXECUTADA / ponta a ponta NÃO HOMOLOGADO**. Nenhum dado, vínculo, código do produto, feed ou serviço publicado foi modificado. Nenhum teste aprovado repetido. Bloqueio concreto: identificação/acesso de teste aos equipamentos reais e ausência de fluxo transacional de troca que cumpra validar antes de revogar. Não declarar missão concluída.

Registro QA: fbab4d38434761dcdaf61d7459ad4c409ee908b7
