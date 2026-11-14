# Etapa 3 — validação da migração 10.467 → 10.468 — 13/11/2026

**STATUS: BLOQUEADA PARA APROVAÇÃO OPERACIONAL.**
Verificações independentes possíveis executadas; etapa encerrada com um bloqueio de ambiente e uma falha de contrato reproduzida. Etapa 4 não iniciada.

## Referência preservada

Código da Etapa 2: `a0f49da19885c87415fa01f7848dbd85b0577f0b`. Relatório/evidências: `219cd6f3f6dda814c304501affd23983c19b79ab`.
PR #37 aberta/draft, head `a9713b8c7810d09d667947c6cca869c9bc5c62c0`.
Branch de validação: `candidate/bridge-10467-stage2-20261113`.

Não foram repetidos 42 testes PET, nove GLOBAL, 26 verificações do preflight ou sete cenários de recuperação conjunta. O Actions 43531688211 e os JSONs da Etapa 2 foram consultados como evidência histórica, sem nova execução.

## Ambiente efetivamente acessível

Foi usado um Windows descartável do GitHub Actions somente para o NOVO ensaio independente de arquivos descrito abaixo. Não foi disponibilizado, pelos meios acessíveis nesta sessão, um Windows privado/persistente com uma instalação do PDV legitimamente ativada.

A execução local também estava indisponível: o exec-server recusou criar o processo por ausência do serviço/processo necessário. A consulta a `actions/runners` foi recusada pela lista de endpoints permitidos do conector. Portanto, não foi possível inventariar runners privados; não se afirma que eles não existem.

Nenhuma licença foi criada, copiada, enviada, falsificada ou desabilitada. Não foi pedido novo JSON, senha, licença ou banco do proprietário. Nenhum computador real foi acessado ou alterado.

## Novo teste independente — achado reproduzido

**Actions [43539868352](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43539868352), código de teste `c87a495dd6cc5941ed9ea99f2eb761a3cfc846ea`: execução SUCCESS.**

Esse SUCCESS significa que o ensaio e seu controle foram executados corretamente, **não que a candidata foi aprovada**.

O teste lê as fontes fixadas em `a0f49da` para vincular o experimento ao contrato realmente utilizado:
- `InitialMigrationBridge.ReadPlainFile` abre o arquivo com `FileShare.Read`.
- `InitialMigrationCoordinator.Core` usa essa rotina para ler `.pdv-update/journal.json`; a guarda inicial também a usa para o diário conjunto.
- A persistência do recuperador usa troca atômica por `File.Replace`.

Em arquivos JSON descartáveis, o ensaio manteve aberto um leitor com o mesmo compartilhamento e tentou substituir atomicamente o arquivo:
1. **Contrato atual, FileShare.Read:** a substituição foi bloqueada.
2. **Controle, FileShare.ReadWrite | FileShare.Delete:** a substituição funcionou.

Os bytes finais também foram conferidos pelo próprio teste. Não houve execução do PDV, de ativação, de banco de clientes ou de migração. O ensaio reproduz a colisão de compartilhamento do sistema de arquivos; não pretende reproduzir uma venda ou provar que uma corrida já ocorreu no computador da Loja.

### Impacto e correção mínima recomendada

Há uma interleaving possível em que o coordenador lê o diário enquanto o recuperador tenta persistir uma fase por troca atômica. O compartilhamento atual pode impedir essa gravação, ocasionando erro de transação, confirmação tardia ou rollback desnecessário.

Antes da validação operacional, preparar uma leitura específica de diários que permita a substituição atômica, com snapshot consistente, tratamento de falhas transitórias e prazo limitado. **Não ampliar indiscriminadamente o compartilhamento dos leitores de binários**, nem remover validações de SHA-256. O controle executado utilizou ReadWrite | Delete; a correção de produção deve preservar integridade e ser testada no fluxo integrado.

Nenhum código GLOBAL/PET foi corrigido nesta etapa de validação. O achado permanece ABERTO e impede aprovação operacional. Os testes anteriores continuam válidos dentro de seus escopos; não cobriam essa sobreposição do leitor do coordenador com o escritor do diário.

### Evidência do novo ensaio

Artefato **14091295951**, `etapa3-contrato-diario-evidencias`.
SHA-256 informado pelo GitHub Actions:
`7c4821a0d2a943ff51909829890c17e1de6b48fa47e0387dfa22c5fb937860e7`.

O digest foi consultado nos metadados e no log de upload; não foi recalculado localmente, pois a execução local estava indisponível. O artefato contém somente o JSON do ensaio. O JSON registra aprovação operacional falsa, migração não executada e origem do código aprovado.

## Conferências somente de leitura realizadas

Foi executada uma conferência de consistência dos registros já existentes, sem reexecutar testes:
- Actions 43531688211 permanece SUCCESS e aponta exatamente para a0f49da.
- Evidências referem origem 10.467, destino 10.468 e PET Loja 1.3 de maneira coerente.
- Destination.LicensedOperationalStartup permanece false.
- Todos os sete casos históricos declaram ausência de início licenciado e runtime/dados sintéticos.
- Apenas o caso histórico explicitamente chamado synthetic-joint-commit possui confirmação sintética de saúde.
- O preflight mantém o hash da DLL real da origem informado pelo proprietário.
- Referências protegidas GLOBAL e distribuição continuam iguais.

Essas são verificações documentais/contratuais. Não substituem rehash de uma instalação, teste operacional ou migração licenciada.

## Matriz de execução da Etapa 3

| Verificação | Resultado |
|---|---|
| Contrato de leitura versus substituição atômica do diário | EXECUTADO: colisão reproduzida |
| Controle permitindo substituição do arquivo | EXECUTADO: funcionou |
| Consistência de referências e limites das evidências anteriores | CONFERIDA, somente leitura |
| Migração integrada 10.467 → 10.468 | NÃO EXECUTADA: ambiente ativado indisponível e achado de diário aberto |
| Ativação legítima e login | NÃO EXECUTADOS; nenhuma ativação contornada |
| Vendas, pagamentos e produtos | NÃO EXECUTADOS |
| Cozinha Premium e Vidraçaria | NÃO EXECUTADAS |
| Impressão e periféricos | NÃO EXECUTADOS |
| Preservação operacional de banco, configurações e personalizações | NÃO COMPROVADA nesta etapa |
| Botão ATUALIZAR, backup e rollback conjunto no fluxo licenciado | NÃO EXECUTADOS; resultados de fixtures da Etapa 2 não foram promovidos a homologação |

O centro de atualização tem URLs de produção nas fontes. Ele não foi executado contra produção para oferecer a candidata. O ensaio completo precisará de transporte/feed de QA controlado, sem alterar os feeds existentes.

## Arquivos novos desta etapa

- `qa/organization/stage3/JournalShareProbe.cs`
- `qa/organization/stage3/JournalShareProbe.csproj`
- `.github/workflows/bridge_10467_stage3.yml`
- Este relatório e registro correspondente no Baú de Ouro.

O diff `219cd6f...c87a495` contém apenas os três arquivos do novo harness/workflow. Nenhuma fonte da aplicação, coordenador, worker, PET ou perfil foi modificada.

## Produção preservada

- GLOBAL `channel/global`: `48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1`.
- Distribuição/feeds: `6f641fc6941b573f94393a437191761243cfa69e`.
- PR #37 permanece sem merge; Loja, Casa e Gelamaq não receberam atualização; Genilson continua inativo.
- Nenhuma release, setup, instalação, migração real, Firebase, APK ou iFood.

## Próxima ação e parada

Corrigir o compartilhamento da leitura dos diários na candidata e validar especificamente esse ajuste; depois executar a migração integrada em Windows privado/persistente com PDV legitimamente ativado e canal QA. Não repetir coleta ou baterias aprovadas sem alteração/falha que justifique.

**Não há aprovação operacional da migração. Etapa 3 encerrada com bloqueios registrados; aguardar autorização para a Etapa 4.**


Registro canônico: https://github.com/lealinfopdvnovo/leal-info-pdv/blob/49d49cca44493db534b7d601f7587a17a6014fa7/organization/ETAPA3-VALIDACAO-MIGRACAO-10467-10468.md
