# DIÁRIO DE CONTINUIDADE — LEAL INFO

## Finalidade
Registro verificável de decisões, motivos, pendências e próximo passo. Conversa, inclusive por voz, não equivale a gravação neste arquivo. Só afirmar que está salvo no Baú após confirmação de commit.

## Entrada — 2026-11-15 — Reconstrução das sete etapas
**Contexto:** conversa anterior do Work intitulada “Etapas do LEAL INFO” atingiu limite/travou. O proprietário lembra ter discutido sete etapas por voz, mas a definição textual completa das etapas 5 a 7 não foi recuperada.

**Fatos relatados pelo proprietário:**
- A nova estratégia foi homologada até a etapa 4, com o ANGRA VIDROS QA como referência.
- O objetivo central das etapas restantes era separar GLOBAL e PETs em um ambiente novo organizado e realizar o transbordo real dos clientes.
- Ambiente 34 tinha integração app/PDV funcionando; ambiente 37 está em preparação. Problemas de integração foram observados no 37 e investigação foi pausada.
- O proprietário prefere discutir e revisar cuidadosamente o escopo antes de enviar comandos ao Work.

**Hipótese/proposta de reconstrução (NÃO é transcrição do plano original):**
- Etapa 5: auditar e organizar separação GLOBAL/PET no novo ambiente e preparar matriz de transbordo.
- Etapa 6: migrar clientes de forma controlada, com backup, validação e rollback.
- Etapa 7: homologar cada cliente e fechar a transição.

**Decisões:**
- Não enviar ainda prompt de execução da Etapa 5; discutir e validar primeiro.
- Não retomar agora a integração Android ↔ PDV.
- Proteger GLOBAL, PETs de produção, ambiente 34 e recursos homologados, inclusive animação AJUDA.
- Criar Painel Mestre, Diário de Continuidade e Plano de Etapas para consulta cruzada.

**Próximo passo:** definir com o proprietário o escopo preciso da Etapa 5, usando registros e arquitetura existentes, sem alterar produção; só então elaborar prompt final para Work.

**Protocolo:** a cada decisão relevante registrar data, origem (evidência/relato/hipótese), justificativa, impacto, restrições, próximo passo e commit. Não prometer registro automático de toda fala.

## Entrada — 2026-11-15 — Regra aprovada: migração seletiva e progressiva
**Origem:** decisão expressa do proprietário em conversa. **Estado:** decisão de planejamento, ainda não implementada nem homologada tecnicamente.

- Etapas 1 a 4 foram aprovadas pelo proprietário e não devem ser reabertas ou repetidas sem defeito concreto.
- Etapa 5: homologação rápida e objetiva do escopo GLOBAL/PET; trazer do ambiente 34 para o 37 apenas componentes e dependências comprovadamente necessários, funcionais e compatíveis. Preservar recursos já homologados no 37.
- Ambiente 34 permanece íntegro como acervo original; não apagar arquivos deixados para trás.
- Arquivos não transportados na etapa 5 NÃO são declarados inúteis ou descartados: poderão ser reavaliados nas etapas 6 e 7 se surgirem necessidades concretas.
- Em cada etapa futura, verificar primeiro se o 37 já resolve a necessidade; depois avaliar substituição/rota mais eficiente; importar seletivamente do 34 apenas se necessário, com dependências, testes e justificativa.
- Não espelhar integralmente o 34 nem propagar duplicações, configurações antigas e acoplamentos indevidos.
- Cada etapa precisa ficar funcional no seu próprio escopo e passar por homologação antes de avançar. Não antecipar migração de clientes ou publicação.
**Próximo passo:** consolidar esta regra no prompt único da Etapa 5; enviar ao Work apenas quando o proprietário decidir.


## 15/11/2026 — Etapa 5: execução seletiva comprovada
O proprietário autorizou o escopo de migração seletiva/homologação rápida. Comparação de 28 componentes: o QA aprovado já contém as versões necessárias do 37; nenhuma importação/modificação de produto foi necessária. Preservados os 252 arquivos rastreados do 34. Branch QA `qa/etapa5-selective-20261115`, código `64b5ed6a057cbfdfd9d3ce9212ac001b1b6cdf19`. Windows Actions `43735576962` SUCCESS: 37 verificações existentes e 14 do pacote QA005 real. Dez testes locais de política aprovados. Etapa 5 tecnicamente concluída, aguardando homologação do proprietário. Sem produção, migração de clientes ou início da Etapa 6. Integração Android permanece pausada. Registro detalhado: `TRANSBORDO-CLIENTES/ETAPA-05/RELATORIO.md` e `RESULTADOS.json`. Esta entrada atualiza o estado provisório anterior; não declara transbordo operacional concluído.

## Entrada — 2026-11-15 — Auditoria 5B, backup do 34 e organização do Baú
**Origem:** decisões expressas do proprietário na conversa de 15/11. **Status:** planejamento aprovado; nenhuma execução da auditoria, backup ou desativação confirmada.
- Após a Etapa 5 e antes de qualquer Etapa 6, auditar GitHub, Firebase, configurações e integrações do PDV com ANGRA VIDROS, Speed Food e LEAL INFO Alerta. Inventariar o Salão MJ como projeto independente e preservar seu código/APK sem misturá-lo ao PDV.
- Comparar dependências do 34 e do 37; conferir código-fonte, capacidade de recompilar APKs e conexões. Evitar retrabalho, mudanças em produção, cópia de segredos e migração indiscriminada.
- O 37 deve se tornar autônomo e definitivo. Depois da homologação de todos os clientes, serviços e integrações e da verificação de backup histórico privado, íntegro e restaurável, solicitar autorização expressa para desativar e remover a cópia operacional do 34. Não desativar nem excluir agora.
- Backup histórico do 34 com inventário e pacotes organizados para recuperação excepcional; não usar o Baú documental como depósito público de bancos, segredos ou dados pessoais.
- Preservar a estrutura semanal/diária existente do Baú; sincronizar os índices e registrar resultados sem presumir execução. O registro da Etapa 5 está nos documentos centrais, mas o README de 15/11 estava desatualizado e foi corrigido nesta atualização.
**Próximo passo:** conferir aceite da Etapa 5; preparar auditoria 5B em modo leitura e submeter resultados ao proprietário antes do transbordo.


## 15/11/2026 — Etapa 5B executada em QA; homologação remota NÃO concluída
- Candidatos preparados e testados: ANGRA (retomada da autorização, desvinculação persistente, isolamento e fotos), conclusão Speed Food no PET QA/emulador e Alerta privado reaproveitando 05E D1/filas com remetente FCM desativado por padrão. Nenhuma publicação. Salão MJ 1.9.5 inventariado e preservado como independente.
- PDV: branch qa/etapa5b-integrations-20261115, código 11f6b50dedc33d18528ab81975cca1d92264b926, Actions 43740129386 SUCCESS (Windows, protocolo e emulador Android); relatório/inventário f1654323c25299bc38726f218b022d03fa905c5d. Alerta privado: código b290a2a069e76469c54f5a40e287d0d967c9d01d, Actions 43739635956 SUCCESS, documentação 4858417710d85b29c8f45c09559785fd337175e8.
- Firebase Console encontrado sem sessão autenticada; estado administrativo implantado não auditado. Serviços QA exclusivos e circuito real Android/PDV não qualificados. Fonte Git do Alerta encontrada em 1.1/code2, enquanto APK aprovado é V1.2/code5; fonte exata atual ainda requer reconciliação. Não apagar/revogar/migrar as cinco pendências reais.
- Testes seguros não equivalem a homologação remota: 18 protocolo ANGRA; 11 verificações no Android de laboratório; 15 Speed Food/SQLite; Alerta 19 D1/FCM simulado + 22 fila/transporte + DPAPI Windows; Salão dois scripts DOM/API + 18 política Java; três casos de backup sintético.
- Produto aprovado permanece PET 1.12; candidata 1.13 não publicada. Origem 34, PR37, GLOBAL/PETs de produção e assinaturas APK preservados. Backup operacional do 34 não executado; plano e ferramenta candidatos com restauração sintética.
- **Etapa 5B NÃO CONCLUÍDA. Etapa 6 NÃO iniciada/autorizada.** Retomar com autenticação administrativa segura e serviços/dispositivos QA; resolver os bloqueios dentro da 5B antes do transbordo.
- Registro: `2026-11-09_a_2026-11-15/2026-11-15/ETAPA-5B-INTEGRACOES-QA.md`, matriz, plano e logs na estrutura semanal existente.


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

## 15/11/2026 — ANGRA37: APK separado entregue e receptor QA publicado
Gateway novo independente, APK0.3.8 instalável em paralelo (pacote ambiente37) e PET1.14 exclusivamente no atualizador QA005. APK original não pode ser atualizado in-place sem sua assinatura: cache ausente; cópias disponíveis eram do Alerta e não foram usadas. Dados do app original permanecem nesse app, sem migração automática. 11 verificações novas em emulador, 3 gateway, compilação Windows e preservação de recursos aprovadas. Testes físicos, imagem e recebimento ponta a ponta pendentes. GLOBAL/produção/outros clientes/34/Firebase/Alerta intactos, sem Etapa6. Registro detalhado no arquivo semanal Etapa5B; relatório QA f41d447cdb88646708cf612030144f4d6b1c033a. Não declarar missão integralmente homologada nem confundir APK paralelo com atualização do original.

## 2026-11-15 — ANGRA QA37 controle individual entregue
APK 0.4.0-qa37/código21 entregue: https://chatgpt.com/library?file_id=libfile_244281a887548191a1b4392e9ce2509d&account_id=personal
Atualiza o pacote QA37 0.3.8 (mesma assinatura), preservando o aplicativo original paralelo. SHA256 cf34605b9d11e08b413b6a8277bfab294d0456db4e6961a43ad7d36cbe5e4e81. Release sem depuração.

Proprietário/funcionário/gerente, matrícula fixa não reutilizável, ativação única e senha própria, administração somente pelo proprietário, permissões individuais, sessões e revogação no servidor, proteção JSON local e autoria autoritativa implementados. Primeiro proprietário depende da aprovação da conta proprietária do gateway; não há autopromoção do primeiro aparelho.

Código: qa/angra37-access-20261115 @ e9cd7e12a61b52b75c932f0d7b092a0bf4d7c436 (PR39 draft, não mesclar em GLOBAL).
APK/emulador: Actions43762805202, 17 verificações aprovadas.
Gateway: 7 grupos aprovados no run43762096329; fonte a7e3c3a522353802bd947b88136b251e5c1557ea, deployment appgdep_6afa204d25248191806e701ca315493c com sucesso.
Windows/PET: run43761367253 aprovado, ponte real preserva autoria e todos os demais métodos/recursos originais permanecem idênticos.
PET1.15 publicado QA005: run43778195000, feed commit de851df1c370d351321922e663b249536bb2f5dc. Somente version.json e ANGRA_005_PET_1_15_QA37.zip alterados.
Relatório completo: commit871a2939a33bf15b3c8144960d74769ec79ca4c2, organization/evidence/etapa5b/ANGRA37-APK-040-CONTROLE-ACESSO.md.

Teste físico de proprietário, funcionário, revogação, pareamento, foto/medidas/imagem e recebimento no PDV: **NÃO HOMOLOGADO**, depende dos equipamentos reais. Não declarar integração ponta a ponta aprovada nem Etapa5B concluída. Fotos permanecem no armazenamento privado Android; JSON cifrado AES-GCM. Acesso protegido exige confirmação recente de sessão; offline não implica apagamento remoto.

GLOBAL 48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1 e produção 6f641fc6941b573f94393a437191761243cfa69e preservados. Firebase, Speed Food, Alerta, demais clientes e ambiente34 intocados, sem backup real e sem Etapa6.

## 2026-11-15 — Registro de teste real, falhas de orientação e pendência de transferência

Registro solicitado expressamente por Adriano: salvar o ocorrido no Baú de Ouro. Este acréscimo preserva os registros anteriores; corrige o entendimento de continuidade e NÃO declara homologação final.

### Fatos do teste relatados pelo proprietário
- O atualizador do PDV informou PET1.15 instalado, enquanto o rodapé ainda exibia1.7. A divergência visual foi relatada e não deve ser tratada automaticamente como atualização não instalada nem exigir repetição sem diagnóstico.
- A integração apresentou “Assinatura ou identidade inválida” e depois “Acesso seguro temporariamente indisponível”.
- Após ajustes informados na conversa, o navegador passou a mostrar “PDV autorizado”; o usuário confirmou que o código apareceu no computador e que o aplicativo conectou. Isso comprova o pareamento RELATADO pelo usuário, não o envio completo de orçamento.
- O usuário ainda não havia enviado um projeto de teste quando confirmou a conexão. Foto/medidas/imagem/recebimento e autoria no PDV continuam sem homologação ponta a ponta registrada aqui.
- Houve rejeição da conta lealinfopdvnovo@gmail.com. A conversa informou correção da autorização QA e o usuário confirmou autorização/código/conexão posteriormente. Não registrar essa conta como proprietário comercial definitivo apenas por ter autorizado o teste.
- O usuário confirmou depois que uma atualização chegou ao PDV e que existe botão de funcionário/proprietário na integração; ao clicar, abre página web solicitando nome completo, senha de oito caracteres, confirmação e configuração de proprietário.
- Há referência posterior ao APK ANGRA_VIDROS_QA37_0.4.1.apk (libfile_4290fd6e0ea8819199167562574b6906). O registro anterior de entrega0.4.0/PET1.15 não pode ser apresentado como descrição suficiente dessa atualização posterior. Código, versão PET e transferência da atualização posterior NÃO foram verificados nesta ação de registro.

### Decisão funcional do usuário — manter explícita
Adriano está SOMENTE testando. O proprietário comercial definitivo será o dono do ANGRA VIDROS.
O proprietário deve administrar a equipe pelo aplicativo E pela área Vidraçaria do PDV.
Funcionário não cria matrícula, não cadastra pessoas e não se promove a proprietário. Matrícula é criada pelo proprietário; ativação com senha própria não equivale a criar matrícula.
Gerente recebe somente permissões individualmente autorizadas.
A configuração inicial/administração precisa de área protegida e autorização no servidor.
O acesso de teste de Adriano não pode impedir a futura assunção pelo dono nem criar dependência permanente do celular do testador.

### Falhas de comunicação da assistência — registrar sem encobrir
- A assistência afirmou em voz que uma correção estava pronta sem comprovação atualizada correspondente neste fluxo.
- Em seguida foi informado que a correção ainda não havia sido entregue, com base em verificação de uma branch antiga; o usuário depois confirmou atualização e botão no PDV. Portanto essa negativa anterior NÃO é prova da ausência da funcionalidade na versão posterior.
- Foram dadas orientações contraditórias sobre configuração inicial e transferência: outro atendimento disse que seria possível transferir; este atendimento respondeu sem verificar a implementação atual e não conseguiu confirmar.
- A assistência pediu repetição de escolhas/cliques e perguntas sobre versão/conta já respondidas, prolongando o teste. O usuário relatou travamento de texto/voz, demora e consumo de cota, expressando insatisfação e intenção de preservar provas.
- Não há comprovação de restituição de cota; não prometer restituição nem atribuir os travamentos a causa técnica não demonstrada.
- O erro central de continuidade foi responder sobre versões e transferência sem consultar o estado técnico mais recente. Não repetir esse comportamento.

### Pendência concreta — NÃO CONFIRMADA
Verificar NA VERSÃO ATUAL a existência e o funcionamento da transferência/assunção de proprietário:
1. Se o cadastro provisório feito por Adriano pode ser substituído/transferido para o dono.
2. Se a operação exige autorização do dono e revoga efetivamente o acesso provisório e as sessões de teste.
3. Se preserva equipe, matrículas, projetos, fotos, históricos, identidade, licença e vínculo com o PDV.
4. Se o vínculo funciona em outro celular e após reinicialização/atualização.
Até haver evidência de código e teste, não afirmar “tem transferência” nem afirmar “não tem” com base somente na entrega0.4.0. Não instruir o usuário a finalizar um proprietário definitivo apoiado em suposição.

### Limite desta ação
Ação atual: documentação do incidente e atualização do Diário de Continuidade, somente na branch bau-de-ouro.
Nenhuma compilação, implantação, alteração de contas, licença, GLOBAL, PET, produção, Firebase ou ambiente34 foi feita nesta ação. Nenhum teste aprovado foi repetido. Etapa6 não iniciada.

## 2026-11-16 — SALÃO MJ FABIANA1.9.6: exclusão definitiva de agendamento
Pedido confirmado: cancelar mantém status Cancelado; depois Excluir deve remover o agendamento e lembrete, preservando cadastro da cliente. Executado em projeto independente do PDV.
Causa confirmada no fonte1.9.5: handler visit-delete somente atribuía status Cancelado.
Entregue APK1.9.6/versionCode25, pacote br.com.salaomj.agenda e mesma assinatura anterior. APK SHA256: 2baff27398ef628c53d32a47bff4838f58a3534bc5ae76af410423d75a50839d. Certificado SHA25621ef579603185e9b5783d5d1ac19bae4e5a7b6e8beabd75a29aa6c0ec8edd26e.
Exclusão remove somente registro de agendamento da coleção ativa/histórico, grava marca de ID para impedir recriação por backup/sync antigos e atualiza a ponte Android de lembretes (que cancela alarmes/notificações de IDs ausentes). Mantidos cliente, demais atendimentos, galeria e registros clínicos independentes. Cancelamento não alterado. Falha de gravação reverte a exclusão em memória.
Servidor independente incluído no projeto também trata exclusões e impede recriação por dispositivos antigos; fonte/testes apenas, nenhum serviço remoto implantado.
Testes:8 cenários de exclusão aprovados;2 testes específicos do servidor aprovados;10 regressões do servidor aprovadas; cenário existente de horários/domingo/confirmação JPEG aprovado; compilação/assinatura válidas, outros6 assets idênticos. Script legado test_delete181 contém expectativa obsoleta /Cola/ que falha igualmente na base1.9.5, documentada sem alterar cuidados fora do escopo.
APK atualizado no mesmo item Library: https://chatgpt.com/library?file_id=libfile_d46313f0c39081918bb1103750ca9621&account_id=personal (versão Library24).
Fonte e evidências preservados no mesmo projeto: https://chatgpt.com/library?file_id=libfile_3d390f729a748191bd555d20a78695ff&account_id=personal (versão Library24).
Instalar por cima sem desinstalar/limpar dados. Teste físico no celular de Fabiana PENDENTE; não declarar homologação física. PDV, GLOBAL/PET, Angra, Firebase, Alerta e ambiente34 não modificados.

## 2026-11-16 — ANGRA QA: “Confirmação inválida” no cadastro de proprietário
Evidência do usuário: POST do formulário respondia403 e JSON accessDenied. Log real11:39 (Brasília) em /access/owner mostrou Origin:null, Sec-Fetch-Site:same-origin, requestId e75f8ecd9c4516b06fd947c4262f9ae8. Não é prova de senha errada nem falha de licença.
Causa identificada: página administrativa emitia Referrer-Policy:no-referrer, causando origem opaca no POST de navegação. Ajuste pontual para strict-origin, que mantém a origem utilizável e não divulga caminho/query do convite em Referer.
Validações de Origin exato, cookie HttpOnly/Secure/SameSite=Strict e igualdade do token CSRF foram mantidas. Origin:null continua rejeitado; não houve bypass.
Três testes direcionados aprovados: cabeçalhos/formulários /access/owner e /access/admin; rejeição de origemnull/estrangeira/cookie ausente/token adulterado; cadastro sintético válido e acesso posterior à administração/transferência. Testes em armazenamento isolado, sem criar conta real.
Fonte Site commit be10e4c538e71587361f1e3c1641c50629a7d0e0; versão5; deployment appgdep_6afb1698aa488191a23bcc093c411e0c SUCCEEDED. Espelho GitHub QA: 671a91055835b8dce52d9f055c5e7d3d531a19b0.
Sem novo APK/PET e sem alteração de contas, licenças, dados reais, GLOBAL, outros clientes ou ambiente34.
Para testar no aparelho: fechar a página antiga do erro e reabrir a administração pelo botão Funcionário/Proprietário do PDV ou pelo acesso administrativo do APK, obtendo formulário e cookie novos. Preencher novamente o cadastro. A submissão física corrigida ainda depende dessa confirmação; não declarar homologação final.

## 2026-11-16 — Prompt do tutorial aplicado e fluxo de orçamentos corrigido
Entregues APK0.4.2 e PET1.18 exclusivamente QA005. Novo orçamento cria diretamente; Orçamentos salvos lista identificação/data/status e permite conferir/enviar, sem afirmar recebimento antes de confirmação do PDV. Funcionários e gerentes agora aceitam permissões individuais pelo proprietário no APK e servidor, mantendo001/senha/cadastros. Tutorial Como cadastrar funcionários no PDV e navegador, sete passos somente de leitura, reutilizando Overlay/Glow do Coach aprovado. Reemissão de ativação invalida código anterior; revogação testada. APK atualizado com mesma assinatura0.4.1.
FonteQA9221340c7cc321ccd4703a15bdf75ee1626bef41; Android/servidor43907124116 aprovados (17 testes servidor); Windowsfinal43909223671 aprovado; publicação43909718508 aprovada, feedcommit782947f4be39affa42236f918174751be4a41709. Gateway294ade199b2f2bfef6b53475b0be4ef65fec6f9c publicado como versão7. Corrigida separação de parágrafos vista no print; PETfinal1.18.
APK: https://chatgpt.com/library?file_id=libfile_a9e824a56e148191bc6112493b819737&account_id=personal . Relatório completo na pasta existente bau-de-ouro/solicitacoes-pendentes/tutorial-cadastro-funcionarios/ENTREGA_2026-11-16.md; prompt marcado implementado, aguardando homologação física.
Teste físico de tutorial/escala, permissões002 e fluxo foto/medidas/envio/recebimento permanece pendente. Não declarar Etapa5B concluída. GLOBAL, produção, outros clientes, Firebase, Alerta, Speed Food e ambiente34 preservados; sem backup real e sem Etapa6.
