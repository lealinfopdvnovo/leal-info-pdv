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

## Decisão substitutiva — limpar pareamento provisório e refazer no 37

**Nova autorização do proprietário (15/11/2026) substitui o portão anterior "validar novo antes de desvincular".** Pode revogar agora exclusivamente o pareamento ANGRA Android→Casa e gerar novo no PDV Angra 37. Não exigir fotos antigas. Autorização não é execução.

Verificação dos mecanismos existentes:
- Android unlink() faz flush, arquiva estado/dados por tenant e chama /sync/unlink assinado com a identidade local; só limpa a seleção/estado ativo depois de revoked=true. Falha de internet mantém operação pendente. Candidata já aprovada acrescenta retry persistente; não presumir que essa candidata esteja instalada.
- Gateway /sync/unlink permite apenas role=app assinando o próprio grant. Marca approved=false e unlinkedAt, com comparação ETag; não apaga sync/data, sync/photos nem o grant do PDV pai.
- /sync/pair recusa códigos emitidos antes do unlinkedAt para essa identidade e códigos expirados/já consumidos/incompatíveis com ticketEpoch. Isso não é revogação global dos códigos Casa.
- Não usar /sync/revoke do PDV Casa, não limpar BUCKET, não apagar chave AndroidKeyStore nem revogar aprovação de criação de imagens, que é separada do pareamento.
- Não existe endpoint administrativo de desvinculação arbitrária na versão auditada; /admin/approve autoriza outro recurso e não substitui a assinatura do aparelho.
- Novo código deve ser produzido pelo PDV Angra via /sync/ticket; não inventar código, forjar identidade ou usar chaves sintéticas para alterar vínculo real.

**Acesso disponível nesta sessão:** repositório/fontes e metadados do gateway. Sem sessão de controle no Android físico e sem conexão ao Windows do proprietário; adb não está disponível e nenhuma ferramenta conectada de controle desses equipamentos foi encontrada. Portanto a assinatura de /sync/unlink e /sync/ticket reais não está acessível. Nenhuma limpeza real executada, código emitido, grant alterado ou solicitação reenviada.

**Única dependência operacional imediata:** executar nos equipamentos o fluxo já existente:
1. Android ANGRA → integração → DESVINCULAR PDV; aguardar confirmação do servidor. Não desinstalar/limpar dados. Se falhar, interromper a troca e preservar pendência.
2. No PDV ANGRA do 37 → conectar/autorizar, se necessário, e GERAR/RENOVAR código; autenticação do proprietário somente na tela oficial.
3. Android → informar diretamente o novo código exibido pelo PDV. Conferir nome ANGRA e fazer pedido sintético novo com foto/medidas/serviço/modelo/detalhes/imagem.
4. Confirmar recibo no PDV Angra e repetir somente a verificação nova de persistência ao reabrir/reiniciar. Conferir que dados antigos permanecem arquivados e que Casa não recebeu o teste. Não transportar automaticamente cinco pendências antigas.

Sem acesso remoto aos equipamentos, esses passos físicos não podem ser executados pelo agente; não transferir códigos de assinatura/chaves ao chat. Acesso físico ou sessão de controle legítima é requisito técnico, não nova autorização do escopo.

**Resultado:** sequência segura identificada; execução nos aparelhos e homologação ponta a ponta PENDENTES. Código do produto, serviços, feeds, GLOBAL, PETs, Firebase/FCM/Alerta, Speed Food, Casa, produção e ambiente 34 intocados. Nenhum backup, teste aprovado repetido ou início da Etapa 6.

Commit QA: d21e1f45d989ac27a79cd0190dd93ae7245eb3ad

## Entrega executada — novo ANGRA37 e APK instalável separado (15/11/2026)

**Não é recuperação do vínculo 003. Não depende do servidor/PC34.** Nova autorização permitiu gateway próprio, reconfiguração local e APK. Escopo de produto restrito ao ANGRA QA.

### Infraestrutura e código
- Novo Site: appgprj_6afa12ab4da88191b8432f19c5253973.
- Gateway: https://angra-vidros-qa37.adriansantosangra.chatgpt.site ; R2 BUCKET próprio, sem cópia do antigo.
- Fonte do gateway 0e4b13a7625dc730edc09c397bec27498a880ef3; versão Sites 1; implantação appgdep_6afa148eb8e481919cee7f6d2423555d SUCCEEDED.
- Endpoint raiz declara environment=QA37/client=005. Assinaturas P256, autorização do proprietário, anti-replay, isolamento tenant e SHA256 de fotos reaproveitados. Rotas Admin/CI legadas bloqueadas no novo serviço. App pareado com PDV autorizado recebe aprovação para o recurso de imagem no próprio gateway, sem usar o anterior.
- Branch Android QA: qa/angra37-reconfigure-20261115; fonte funcional 1626fb62..., ajuste de build paralelo a9ac15d..., correção do teste Android a4bc7938b05c3295e9249c0278540d0cc59bc065. PR38 rascunho, NÃO MERGEAR em branch antiga. O commit 0bf19a18... resolve apenas a ancestralidade da PR de laboratório mantendo a árvore aprovada fixada; não houve merge em produção.
- Android: botão RECONFIGURAR CONEXÃO; operação local não espera servidor antigo. Arquivo por perfil, nova workspace, journal de reset atômico e retomada após interrupção. Perfil sem environment QA37 é bloqueado antes de exchange. Callback do coordenador antigo é descartado após fechamento. Perfil sem tenant também é arquivado antes de reset. Nenhum envio automático de pendências antigas.
- MainActivity comparada: todo conteúdo fora da tela integration() idêntico. Recursos visuais comparados byte a byte no Actions.

### Limite decisivo de assinatura e entrega
O APK aprovado 0.3.6 é br.com.lealinfo.vidraceiro.profissional022, code18, certificado SHA256 2ee54920efe7042320f90814e45fd10997f9deb28e213ee92a8955b3e00ffac7. O cache de assinatura correspondente não foi encontrado no Actions. Três cópias privadas debug.keystore encontradas têm certificado 69c5db... (Alerta); NÃO foram usadas nem alteradas.
**Atualização in-place do APK original NÃO entregue:** depende da chave original. Nenhuma desinstalação, limpeza ou substituição incompatível foi proposta.

Para cumprir a compilação independente, foi entregue APK QA37 **separado**, instalável em paralelo:
- Versão 0.3.8-qa37/code20.
- Pacote br.com.lealinfo.vidraceiro.ambiente37.
- SHA256 APK final: 51dde53bb0b13c0ba8c9142f0fc51e8e64621534fcc5024e8138575c2480d9e7.
- Certificado QA37 exclusivo: 653029f2204600e198cb11e00fb805dfcfaf731b8815239bb92628ab427a863d.
- Material de assinatura QA37 retido privadamente para versões futuras; nunca incluído em repositório/APK/log público.
- APK final re-assinado localmente; verificação apksigner PASS. Todos os arquivos de payload fora de META-INF comparados idênticos ao APK exercitado no emulador. O APK final re-assinado não foi novamente instalado em aparelho físico.
- Arquivo entregue: ANGRA_VIDROS_QA37_0.3.8.apk; entrega persistente https://chatgpt.com/library?file_id=libfile_3298814e83648191b79e1d6e4a4d0056&account_id=personal .
- O app antigo continua instalado com fotos/projetos/dados privados intactos; **não houve migração desses dados para o pacote QA37**. Nenhum deles será enviado automaticamente. Arquivamento/reconfiguração foram comprovados com dados sintéticos dentro do pacote QA37, não com leitura do celular do proprietário.

### Receptor PDV QA005
- Compilação Windows/Patcher no Actions 43757868194: job receiver SUCCESS. APK desse run falhou por cache ausente, não confundir conclusão global com sucesso de receiver.
- PET 1.14 parte do pacote aprovado 1.12; Patcher compara todos os métodos/recursos não relacionados, inclusive mídia. Só origem ANGRA e continuação de autorização; helper não inclui o hook Speed Food.
- Novo workflow de entrega, branch qa/angra37-delivery-20261115, commit ba375c4b50b110c85cb53187c347ac6454e836fc, Actions 43759023437 SUCCESS.
- Publicado exclusivamente no feed QA005 da branch qa/angra-etapa4-setup-20261114, commit 6607edf8e3cb551743f3b9bfe9715d8000a2c116, pai 2fd9984.
- Diff de publicação CONFIRMADO: somente qa/etapa4-setup/channel/version.json e ANGRA_005_PET_1_14_QA37.zip.
- Feed TargetClientCode=005, PET1.14, MinGlobal=10.468/MaxExclusive=10.469; SHA256 pacote 3fb46113ebed5d84801fe8f390b5b35298cd05ace7846b1e1d2a11dcfb67a8a0. Manifesto interno mantém Identity005, limites/API e SHA256 DLL atualizado.
- Disponível por ATUALIZAR PET no PDV QA. **Instalação efetiva desse PET no PC do proprietário ainda NÃO observada.** Sem reinstalação de setup.

### Testes novos e resultado
- Gateway local: 3 testes de fronteira novos PASS; pacote Worker ESM validado.
- Android Actions 43758573125 SUCCESS, commit a4bc7938... . 11 verificações: pacote isolado; retenção dos arquivos em atualização de fixture QA; quarentena do tenant antigo; reset com sync ocupada sem contato legado; 5 projetos e operationIds arquivados; foto idêntica; dados antigos não transferidos para nova workspace; restart; recuperação de reset interrompido; endpoint novo.
- A fixture de atualização é compilada da fonte0.3.6 com package QA37 e chave descartável de laboratório; NÃO comprova atualização do APK original com seu certificado.
- HTTP real do gateway novo validado pelo Actions e pelo navegador; resposta environment QA37/client005. A requisição Python do executor recebeu 403/1010; navegador e curl do Actions receberam resposta correta. Não inferir indisponibilidade universal.
- Emulador API29, não celular físico. Compilação receiver/manifesto/recursos não equivale a operação real do PDV.
- **PENDENTE:** atualizar PET no PC QA; gerar/autorizar código nesse PDV; instalar/abrir app QA37 e parear; enviar projeto real de teste com foto/medidas/serviço/modelo/detalhes/imagem; confirmar recebimento/ID/arquivo no PDV; reiniciar ambos; verificar persistência e não duplicação. Geração de imagem contra provedor externo e foto fim a fim não executadas nesta entrega. Não declarar homologação ponta a ponta.

### Preservação e próximo passo
GLOBAL channel/global permanece 48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1; feed de produção leal-info-pdv-updates/main permanece 6f641fc6941b573f94393a437191761243cfa69e. Casa/Loja/outros PETs, Speed Food, Alerta/Firebase/FCM e ambiente34 não alterados. Gateway antigo não alterado nem chamado para revogação. Sem backup real, migração geral ou Etapa6. Testes antigos aprovados não reexecutados; novas tentativas corrigiram apenas falhas de compilação do teste e a indisponibilidade da assinatura original.

Instrução mínima: PDV ANGRA QA → ATUALIZAR PET1.14; instalar APK QA37 em paralelo sem apagar o antigo; abrir o novo APK, conectar com código do PDV Angra e enviar teste. Etapa5B continua parcialmente pendente da homologação física. Limitação de atualização in-place permanece aberta até recuperar a assinatura original.

Relatório QA: f41d447cdb88646708cf612030144f4d6b1c033a, organization/evidence/etapa5b/ANGRA37-APK-038-ENTREGA.md
