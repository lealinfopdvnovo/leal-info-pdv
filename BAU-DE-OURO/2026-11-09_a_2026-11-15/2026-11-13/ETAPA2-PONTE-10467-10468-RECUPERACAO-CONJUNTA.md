# Etapa 2 — destino modular e recuperação conjunta — 13/11/2026

**As duas pendências técnicas foram implementadas e testadas em ambiente isolado. A migração operacional continua NÃO HOMOLOGADA e a publicação NÃO AUTORIZADA.**

Branch: `candidate/bridge-10467-stage2-20261113`, derivada da PR #37. Código final testado: `a0f49da19885c87415fa01f7848dbd85b0577f0b`. A PR #37 permanece em `a9713b8`, aberta/draft; não houve merge.

## Implementado

### Destino modular posterior à origem real

- Candidata **10.468**, com novas implementações efetivas do coordenador inicial e da recuperação conjunta; não é somente renumeração da base 10.463.
- Reutiliza PetRuntime, PetBootstrap, contratos, perfis e aplicadores da PR #37. O preflight anterior foi estendido; não existe segunda arquitetura.
- A constante real do atualizador, os contratos e as chamadas IL do destino compilado foram conferidos. A entrada arma a proteção depois da ativação legítima e antes de inicializar dados/criar o PET. A confirmação de saúde é adiada até a janela principal após login.
- PET Loja continua **1.3**, sem nova funcionalidade. Seu módulo original embutido, SHA-256 `054e30f5ed5d40a0b539511f50c4415f346b8ec20cbefb325acf364d0f2acd8f`, é preservado dentro do wrapper externo existente. O novo wrapper compilado não é apresentado como uma DLL idêntica à instalação antiga.
- Recursos originais da Cozinha e notificador da instalação 10.467 preservados byte a byte. O notificador antigo da referência comum foi recusado e substituído pelo recurso exato da origem identificada.
- Auth.cs, Database.cs e Licensing/InstallationLicense.cs correspondem à fonte de publicação `7aa5d38336e94659c07f0afc6ddfe40169c41a3a`. A equivalência da fonte ignora apenas BOM/CRLF de checkout; a preservação antes/depois também verifica hashes dos bytes brutos. Nenhum arquivo desses foi alterado pela nova integração.
- Fontes dos PETs já preparados permaneceram inalteradas ao acrescentar o destino. Casa e Gelamaq não receberam funcionalidades novas; Genilson mantém a política inativa existente.

### Recuperação conjunta do núcleo e primeiro PET

- O aplicador legado continua responsável pelo transporte inicial dos três arquivos do núcleo. Antes de PetBootstrap criar o primeiro módulo externo, o coordenador exige a origem 10.467 por hashes, destino/nonce compatíveis com o diário legado, runtimeconfig original e ausência de PET externo anterior.
- Uma cópia durável e verificada dos três binários originais é mantida em `.pdv-initial/previous`; o diário e o worker ficam protegidos por ACL. A tarefa SYSTEM é registrada antes de a proteção declarar-se armada.
- O worker conjunto usa o mesmo `transaction.lock` do aplicador legado, evitando concorrência entre as duas recuperações. Não substitui o atualizador normal nem altera feeds.
- A confirmação conjunta exige núcleo e PET íntegros, nonce correto, processo vivo correspondente e confirmação da janela principal após login. Os controles de negócio ficam desabilitados até essa confirmação. Nenhuma ativação foi desabilitada.
- Se não confirmar, recupera o núcleo original e retira o primeiro PET externo do caminho ativo. A pasta PET é movida para quarentena protegida, preservando inclusive arquivos inesperados; não é apagada.
- Interrupção no próprio rollback é retomada pela tarefa SYSTEM. Backup adulterado é recusado antes de sobrescrever a instalação. Runtimeconfig e arquivos de dados/configuração não são substituídos.
- O preflight continua sem autoridade para aplicar uma instalação por si só: gerar um plano não autoriza migração ou publicação.

## Evidências novas — aprovadas

**Actions [43531688211](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43531688211): SUCCESS**, código `a0f49da`.

Compilados o PDV candidato real e o PET Loja existente. Nove verificações novas do destino confirmam versão, carregador, handoff/ordem de ativação, saúde adiada, cápsula e recursos preservados. Compilação sem erros; avisos preexistentes da base permanecem, sem serem mascarados.

Sete cenários novos de recuperação conjunta no Windows:

1. Falha antes de criar o primeiro PET: núcleo original recuperado, nenhum PET ativo.
2. Falha depois de criar o PET: núcleo e ausência anterior do PET recuperados juntos.
3. Interrupção durante restauração da DLL: tarefa SYSTEM real retomou e concluiu ambos.
4. Backup original adulterado: recusa sem sobrescrita; após reparar o fixture, recuperação aprovada.
5. Aplicador legado mantendo o lock: recuperação conjunta aguardou, sem correr em paralelo.
6. Arquivo inesperado no PET: conteúdo preservado na quarentena; caminho ativo revertido.
7. Confirmação conjunta sintética: núcleo candidato e PET foram mantidos, diário marcado Committed.

A recuperação também foi chamada novamente depois do término, comprovando idempotência. Os hashes de todos os três binários e dos marcadores protegidos foram comparados em cada cenário.

### Limites obrigatórios destas evidências

Os binários 10.467, destino compilado 10.468, PET Loja 1.3, arquivos, processos e tarefa SYSTEM são reais. Os testes começam numa **fixture do estado posterior à aplicação do núcleo**, com o diário legado preparado pelo teste; não executam novamente os cinco cenários antigos do aplicador. Dados/configurações e runtimeconfig da fixture são marcadores sintéticos. A confirmação positiva de saúde do sétimo cenário é sintética e permitida somente pelo modo de fixture restrito ao runner isolado. **Não houve login licenciado ou execução operacional do novo PDV.**

A ordem de ativação e o handoff do programa foram verificados no IL compilado. Isso não substitui executar a transição completa com licença legítima. A prova anterior do runtimeconfig original e dependências exatas foi reaproveitada; não repetida.

**Não repetidos:** coleta JSON, reconstrução dos 257 arquivos, 42 testes PET, nove GLOBAL ou 26 testes anteriores do preflight. O workflow antigo do preflight passou a ser manual, evitando repetições automáticas. As rodadas que falharam na preservação do notificador e na comparação CRLF não são aprovações; a rodada final corrigiu ambos sem enfraquecer os checks.

## Integridade dos artefatos de QA

Artefato de evidências **14087049292**, baixado e SHA-256 conferido:
`c3f1f2cdb77d058e89ac9c3406dabe261757f13bb3a3987ae23506f8adebe1fd`.

Os três JSONs conferidos estão em `organization/evidence/etapa2-10468/`.

| Saída compilada de QA | SHA-256 |
|---|---|
| Apphost com cápsula inicial | `06ebd5bbb538214a2df40573553084b600b0e907b7c9d52c91caee18efb66309` |
| DLL candidata 10.468 | `cffee5e8aa35aea372247322c10f57faddc670b9ff41ac72e8fa61cc6aaf4b70` |
| Deps candidato | `6ed2675a0c592fd46ae972c550a1c92d0a814551d66de0d8da85a9963a59c627` |
| Wrapper PET Loja 1.3 | `795305d9520f8c93f6c0f0b6cfbcc08e9bad2c71827286ec12f0b2795ca6a4b6` |
| Worker conjunto embutido | `c8a9cb678b2b33c0e39d70ac55629a8f7df2fd28d3e60c050376b23cd87883fe` |

Essas saídas existiram somente no workspace isolado de compilação. Não foram enviadas para releases ou feeds, nem oferecidas a clientes. O artefato arquivado contém somente evidências JSON, não uma atualização distribuível.

## Arquivos envolvidos

Em `qa/organization/bridge/`: preflight `InitialMigrationBridge.cs`, coordenador `InitialMigrationCoordinator.cs.source`, worker `JointInitialRecovery.ps1`, integração `prepare_destination.py`, recuperação dos bytes públicos `fetch_origin.py`, preservação do notificador `preserve_origin_notifier.ps1`, `DestinationProbe.cs/.csproj` e `test_joint_recovery.ps1`. O fixture antigo `Program.cs` apenas acompanha o contrato estendido, sem ser reexecutado.

Workflows: novo `bridge_10467_completion.yml`; `bridge_10467_stage2.yml` mantido para execução manual. A preparação altera somente a árvore temporária `candidate/` no runner, não as fontes/instalações de produção.

## Pendência operacional e condição de parada

Estas duas implementações da Etapa 2 estão prontas para revisão. **Não equivalem à aprovação da PR #37 ou da migração real.** Permanecem pendentes Windows privado/persistente e licença legítima para a transição completa 10.467→10.468, início autenticado, login, vendas/pagamentos, estoque/financeiro, Cozinha, Vidraçaria, impressão, preservação real e fluxo ATUALIZAR. Não solicitar outro JSON nem enviar licença/banco/senha ao GitHub.

O aplicador legado tem prazo de saúde: a primeira migração agora só confirma após login; a validação licenciada deve comprovar esse fluxo dentro do prazo, ou confirmar o rollback seguro. Não foi alterado silenciosamente esse timeout.

**Parada na Etapa 2. Etapa 3 não executada. Nenhuma autorização de publicação foi inferida.**

## Produção conferida e preservada

- GLOBAL `channel/global`: `48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1`.
- Distribuição/feeds: `6f641fc6941b573f94393a437191761243cfa69e`.
- PR #37: `a9713b8c7810d09d667947c6cca869c9bc5c62c0`, aberta/draft.
- Nenhum merge, release, feed alterado, setup, instalação real, Firebase, APK ou iFood.


Registro canônico da candidata e dos três JSONs: https://github.com/lealinfopdvnovo/leal-info-pdv/tree/219cd6f3f6dda814c304501affd23983c19b79ab/organization
