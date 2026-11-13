# Etapa 2 — ponte inicial da instalação 10.467 — 13/11/2026

## Situação: INCOMPLETA; nenhuma migração autorizada ou executada

Branch isolada `candidate/bridge-10467-stage2-20261113`, derivada do head da PR #37 `a9713b8`. Código de preflight `953a91ccd3b8e93cc863029fd79e1f7ef0d4b94b`. A PR #37 e sua branch permanecem inalteradas. Não houve incremento de versão, pacote de atualização, release, feed ou instalação.

## Implementado de fato

Novo módulo de **preflight de engenharia**, não um atualizador operacional:

- Origem Loja 10.467 fixada pelos quatro SHA-256 já identificados (EXE, DLL, deps e runtimeconfig); sem nova coleta.
- Recusa de versão destino menor/igual a 10.467; a candidata recomposta 10.463 não é aceita e não foi renumerada para disfarçar esse impedimento.
- Leitura de metadados PE, sem executar DLLs: exige contratos do carregador modular existente e recurso do aplicador PET.
- Reutiliza `IndependentUpdater.ValidateCompatibility` e `ClientPolicy` da candidata; não cria carregador alternativo. Genilson continua inativo.
- Verifica pacote SHA-256, limites e exatamente os três arquivos aceitos pelo aplicador legado. Não substitui runtimeconfig ou dependências instaladas.
- Valida a cápsula já existente `LEAL-PET-BOOT-01`, manifesto/Pet.dll, identidade Loja, PET 1.3, API, intervalo GLOBAL e preservação do recurso original Loja por hash.
- Rotinas de conferência do backup preparado pelo aplicador existente e da restauração dos quatro arquivos da origem; detecta PET externo residual como rollback incompleto.
- Plano gerado com `CanApply=false` e `OperationallyApproved=false`. A porta de ativação está explicitamente fechada, inclusive se um chamador fabricar flags de aprovação.

Arquivos novos: `qa/organization/bridge/InitialMigrationBridge.cs`, `Prepare.cs`, `Program.cs`, dois projetos .csproj e workflow `.github/workflows/bridge_10467_stage2.yml`.

**Limite importante:** o preflight não está ligado ao botão ATUALIZAR nem ao `Program.Main`. Não foi criada uma transição aplicável. Compilar esse utilitário não equivale a compilar ou aprovar uma nova base GLOBAL.

## Testes novos

Compilação local .NET 8.0.415 do preflight: zero erros e zero avisos com `-warnaserror`. Vinte e seis verificações focadas passaram localmente em Linux. Actions **43527640006 SUCCESS**, código `953a91ccd3b8e93cc863029fd79e1f7ef0d4b94b`: compilação com zero avisos/erros e os mesmos 26 controles novos aprovados em Windows e Linux. https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43527640006 . Isso não executa o PDV licenciado nem a migração.

Cobertura: ordenação de versões, contratos ausentes, recuperação PET ausente, PE inválido, origem incompleta/desconhecida, preservação dos bytes em recusa, bloqueio de flags fabricadas e política dos clientes. Os objetos de contrato e arquivos são fixtures sintéticos, não uma instalação licenciada. Não foi executado um caminho positivo completo de preparação, aplicação ou rollback da ponte.

Não repetidos: coleta JSON, reconstrução dos 257 arquivos, 42 cenários PET, nove GLOBAL ou cinco cenários antigos do aplicador. As evidências anteriores continuam com seus limites originais.

## Bloqueios concretos e trabalho ainda necessário

1. **Destino compatível ainda não produzido.** A candidata continua recomposta sobre 10.463. É necessário integrar a ponte a um destino modular com versão real posterior a 10.467 e provar preservação funcional. O código deste commit apenas recusa a versão antiga; não resolve essa integração.
2. **Handoff de recuperação ainda não implementado.** O aplicador identificado restaura três arquivos do núcleo. O bootstrap candidato pode criar `Extensions/PET` fora desse conjunto. Falha após criar o primeiro PET exige recuperação que também trate o estado PET. A nova rotina detecta e bloqueia essa lacuna; não a corrige com uma segunda arquitetura nem executa rollback por si só.
3. **Windows legítimo.** Após resolver os dois itens técnicos, permanecem necessários Windows privado/persistente e ativação oficial para início autenticado, login, vendas/pagamentos, Cozinha, Vidraçaria, impressão e recuperação operacional. Nenhuma licença foi inventada ou ignorada.

Portanto, a Etapa 2 não deve ser considerada concluída apenas por esses 26 testes. O próximo trabalho técnico é terminar o destino e o handoff reutilizando o carregador/aplicadores existentes, antes da validação licenciada. Não oferecer atualização PET isolada ao host legado.

## Proteções conferidas

GLOBAL `channel/global`: `48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1`.
Distribuição: `6f641fc6941b573f94393a437191761243cfa69e`.
PR #37: `a9713b8c7810d09d667947c6cca869c9bc5c62c0`, aberta/draft.

Somente arquivos novos de preflight/testes/documentação na branch isolada. Fontes GLOBAL/PET existentes, dados, licenças, personalizações e três clientes ativos preservados. Nenhum merge, publicação, setup, atualização real, Firebase, APK ou iFood.
