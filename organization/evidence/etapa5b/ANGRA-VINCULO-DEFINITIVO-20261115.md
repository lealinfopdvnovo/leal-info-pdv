# ANGRA VIDROS — preparação da vinculação definitiva

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
