# Etapa 3 — correção complementar da leitura dos diários — 13/11/2026

**RESULTADO: falha de compartilhamento corrigida e validada em Windows isolado. Aprovação operacional continua BLOQUEADA. Etapa 4 não iniciada.**

Base funcional: candidata 10.468 / `a0f49da`. Branch exclusiva da correção: `candidate/journal-reader-fix-20261113`, derivada do registro 49d49cc. Código final testado: **`d902bf3c598203dc937f6dd8f9b676f2d99fec1b`**.

## Correção mínima realizada

O problema da Etapa 3 era ler o diário mutável pela mesma rotina restritiva usada para binários. Essa rotina impedia a troca atômica do arquivo enquanto o leitor estivesse aberto.

Foi acrescentado `JournalSnapshot`, exclusivo para diários:
- abre somente para leitura, com `FileShare.ReadWrite | FileShare.Delete`;
- lê e confere duas vezes o mesmo handle, limitando o conteúdo a 64 KiB;
- permite que o caminho seja substituído atomicamente enquanto o handle mantém um snapshot do arquivo anterior;
- fecha o handle antes de devolver o JSON em memória, inclusive quando há erro de análise;
- tenta novamente somente falhas transitórias de I/O ou JSON, com prazo e quantidade de tentativas limitados; o prazo padrão é 500 ms;
- mantém rejeição de caminhos redirecionados, arquivos inválidos/grandes e cancelamento.

As três leituras de diário no coordenador e a conferência do diário do backup passam por essa rotina. **ReadPlainFile, hashes de binários, motor de recuperação, estados, nonce, locks, licenciamento e personalizações não foram relaxados ou alterados.**

A receita de compilação copia a nova classe. Os dois projetos standalone do preflight receberam apenas a referência de compilação necessária. Foram compilados, sem executar sua bateria histórica.

## Arquivos alterados

Implementação:
- `qa/organization/bridge/JournalSnapshot.cs` — novo leitor.
- `qa/organization/bridge/InitialMigrationCoordinator.cs.source` — três chamadas de leitura.
- `qa/organization/bridge/InitialMigrationBridge.cs` — leitura do diário na conferência do backup.
- `qa/organization/bridge/prepare_destination.py` — inclusão da classe no build temporário.
- `qa/organization/bridge/BridgePrepare.csproj` e `BridgeTests.csproj` — inclusão da dependência; nenhuma execução dos testes antigos.

Validação:
- `qa/organization/journal-fix/JournalSnapshotTests.cs/.csproj`.
- `qa/organization/journal-fix/RecoveryJournalPolling.cs/.csproj`.
- `qa/organization/journal-fix/test_concurrent_recovery.ps1`.
- `.github/workflows/journal_snapshot_fix.yml`.

O arquivo `JointInitialRecovery.ps1` ficou **inalterado** em relação a a0f49da. Os testes comparam sua fonte e seu hash. A implementação integral do leitor de binários também foi comparada com a base aprovada, ignorando somente CRLF de checkout, e permaneceu equivalente.

## Testes novos e resultado real

**[Actions 43542417535](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43542417535): SUCCESS**, código d902bf3. Compilação da candidata 10.468 e dos dois consumidores standalone aprovada. Não foi gerado pacote/cápsula de atualização para clientes.

### Treze verificações novas do leitor — aprovadas

1. Substituição atômica enquanto o leitor real continua aberto.
2. Liberação de todos os handles antes do retorno.
3. JSON retornado independente do arquivo, preservando o snapshot anterior.
4. Controle de compartilhamento restritivo dos binários continua bloqueando substituição.
5. **300 gravações atômicas e pelo menos 300 leituras concorrentes**, sem erro de compartilhamento ou JSON misturado.
6. Lock exclusivo temporário: leitura funciona após sua liberação.
7. Lock persistente: recusa dentro do prazo limitado.
8. JSON persistentemente inválido: recusa sem vazamento de handle.
9. Arquivo acima do limite: recusa sem vazamento de handle.
10. Cancelamento durante as tentativas.
11. Corpo original do leitor de binários preservado.
12. Motor de recuperação, backup núcleo/PET e checkpoints de interrupção preservados.
13. Todas as leituras afetadas do coordenador encaminhadas ao novo leitor.

### Um novo ensaio integrado de I/O da recuperação — aprovado

O leitor corrigido consultou continuamente **os dois diários** enquanto o worker real de recuperação era interrompido durante a restauração da DLL. A tarefa **SYSTEM real** retomou o rollback sob essa concorrência.

Resultado da rodada final:
- **626 snapshots, zero erros de leitura**;
- três binários do núcleo restaurados e conferidos contra os hashes confiáveis 10.467;
- primeiro PET retirado do caminho ativo junto com a recuperação do núcleo;
- marcadores de dados/configuração/runtime preservados por hash;
- worker de recuperação não modificado.

Esta é uma condição de concorrência nova, ausente nos sete cenários anteriores; não é reexecução daquela bateria.

### Limites da prova

O worker, processos, arquivos, leitor, tarefa SYSTEM e binários originais de backup são reais no runner Windows. **Alvos de núcleo, primeiro PET, runtimeconfig e dados desse ensaio são marcadores sintéticos**, sem executar aplicação ou módulo PET. Não representam uma instalação licenciada nem homologam as funcionalidades do PET Loja 1.3.

A preservação do PET e do carregador é sustentada pela ausência de mudanças fora da leitura dos diários e pelas evidências anteriores reaproveitadas, não por novas vendas ou início licenciado nesta etapa.

Não foram repetidos os 42 testes PET, nove GLOBAL, 26 testes de preflight ou sete cenários antigos de recuperação. A preparação/compilação reutiliza receitas aprovadas e confere suas entradas; isso não executa essas baterias. A primeira rodada direcionada (43542034962, d1deb24) passou; depois da inclusão da dependência nos projetos standalone, a rodada final também passou. Nenhuma rodada direcionada falhou.

## Evidências e hashes

Artefato **14091824883**, `etapa3-correcao-diarios-evidencias`.
SHA-256 registrado nos metadados e log do Actions:
`19335353c221d009adc43d3bfd6895a620025fc808192789f3ef3b695e1a80f7`.

Não se afirma recálculo local do ZIP; a execução local da sessão permanece indisponível. O artefato contém somente JSONs de evidência, incluindo hashes de fontes e os resultados detalhados.

| Build de validação / componente | SHA-256 |
|---|---|
| DLL candidata corrigida | `114d79968eaf13682b67428e54c24d35d3d74cd479d9f1874592a07e622b86e7` |
| Apphost neutro, sem cápsula | `f9dbc2f39b263d1bdad694726bca4167c6afbc3c5b84f6136ebb49c2bce678b0` |
| Deps | `6ed2675a0c592fd46ae972c550a1c92d0a814551d66de0d8da85a9963a59c627` |
| Worker conjunto preservado | `c8a9cb678b2b33c0e39d70ac55629a8f7df2fd28d3e60c050376b23cd87883fe` |

Esses hashes são saídas do build isolado; **não são uma publicação nem arquivos instalados no cliente**. O apphost acima não contém a cápsula inicial que existia no pacote de QA da Etapa 2.

Os resultados do log e a lista dos 13 checks estão registrados em `organization/evidence/etapa3-journal-fix/`.

## Preservação e pendências

- GLOBAL de produção: `48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1`.
- Distribuição/feeds: `6f641fc6941b573f94393a437191761243cfa69e`.
- PR #37 continua aberta/draft em `a9713b8`, sem merge.
- Nenhuma instalação real, licença, cadastro, dado ou personalização do cliente foi alterado.
- Nenhum incremento de versão, release, feed, setup ou pacote de atualização para clientes.
- Nenhuma alteração em Firebase, APKs ou iFood.

**O achado de bloqueio da troca atômica está resolvido na candidata testada.** Continua pendente a validação integrada em Windows privado/persistente com PDV legitimamente ativado: migração 10.467→10.468, login, vendas/pagamentos, produtos, Cozinha, Vidraçaria, impressão, ATUALIZAR e recuperação operacional.

**Aprovação operacional BLOQUEADA. Encerrada somente a correção complementar da Etapa 3; aguardar autorização para qualquer avanço.**


Relatório canônico e evidências: https://github.com/lealinfopdvnovo/leal-info-pdv/blob/586c512966d20af7e87b47b8ac594b548644f739/organization/ETAPA3-CORRECAO-LEITURA-DIARIOS.md
