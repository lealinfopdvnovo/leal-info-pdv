# PLANO DE ETAPAS — NOVA ARQUITETURA LEAL INFO

**Status:** RECONSTRUÇÃO PROVISÓRIA em 2026-11-15. Não é cópia literal do plano original discutido por voz. Nenhuma migração real está autorizada por este documento.

## Objetivo geral recuperado
Organizar a separação GLOBAL/PET na nova arquitetura e realizar transbordo seguro de todos os clientes existentes, sem perda de dados, licenças, personalizações ou funcionalidades.

## Etapas 1 a 4 — histórico parcial
1. Preparação da nova estrutura de instalação/distribuição — escopo parcial; critérios originais a confirmar.
2. Organização/isolamento dos ambientes e PETs — reconstrução provável; critérios originais a confirmar.
3. Instalação e ativação no Windows, compatibilidade/atualização — testes relatados; evidências a vincular.
4. Cliente-piloto ANGRA VIDROS QA: identidade visual, licença, abertura cinematográfica e atualizações PET — homologação relatada pelo proprietário. Preservar animação AJUDA homologada.

## Etapas 5 a 7 — proposta a discutir
5. **Separação GLOBAL/PET e preparação da matriz de transbordo:** auditar o que é comum e o que é exclusivo, identificar dependências, inventariar clientes/versões/dados e testar em isolamento. Não tocar produção.
6. **Transbordo controlado:** somente após aprovação explícita; um cliente por vez, backup íntegro, rollback validado, proteção de PET e dados.
7. **Homologação e encerramento:** conferir funcionamento, atualizações, identidade, dados e personalizações de cada cliente; documentar evidências e pendências.

## Portões de segurança
- GLOBAL e PETs de produção intocados sem autorização específica.
- Ambiente 34 intocado; ambiente 37 é referência QA, não prova automática de equivalência.
- Não presumir que integração Android ↔ PDV está pronta no 37; investigação pausada.
- Nenhuma etapa considerada concluída sem critérios, testes e aceite do proprietário.
- Não publicar ou migrar com base apenas neste plano provisório.

## Pendência atual
Revisar e aprovar escopo da Etapa 5 antes de redigir/enviar o comando definitivo ao Work.

Ver também: `BAU-DE-OURO/PAINEL-MESTRE.md` e `BAU-DE-OURO/DIARIO-DE-CONTINUIDADE.md`.

## Regra de execução definida pelo proprietário — 2026-11-15
**MIGRAÇÃO SELETIVA E PROGRESSIVA, NÃO ESPELHAMENTO.**
- Etapas 1–4: homologadas pelo proprietário; não repetir testes completos sem causa identificada.
- Etapa 5: homologar rapidamente a organização GLOBAL/PET e implementar somente o necessário em QA 37, importando do 34 apenas arquivos/dependências essenciais, comprovados e compatíveis. Testar o que mudar.
- Etapa 6: no transbordo autorizado de cada cliente, reavaliar arquivos do 34 não migrados na etapa 5; preferir solução já existente no 37 ou alternativa mais eficiente quando tecnicamente viável; importar apenas o estritamente necessário.
- Etapa 7: repetir avaliação para lacunas remanescentes e homologar o conjunto, sem espelhamento.
- Não apagar ou modificar acervo 34; arquivo não importado continua disponível, não é automaticamente obsoleto.
- Preservar funcionalidades homologadas do 37, isolamento GLOBAL/PET e atualizações; não tocar produção nem iniciar etapas posteriores sem aprovação.
**Status:** planejamento registrado, não implementação executada.


## 15/11/2026 — Etapa 5: execução seletiva comprovada
O proprietário autorizou o escopo de migração seletiva/homologação rápida. Comparação de 28 componentes: o QA aprovado já contém as versões necessárias do 37; nenhuma importação/modificação de produto foi necessária. Preservados os 252 arquivos rastreados do 34. Branch QA `qa/etapa5-selective-20261115`, código `64b5ed6a057cbfdfd9d3ce9212ac001b1b6cdf19`. Windows Actions `43735576962` SUCCESS: 37 verificações existentes e 14 do pacote QA005 real. Dez testes locais de política aprovados. Etapa 5 tecnicamente concluída, aguardando homologação do proprietário. Sem produção, migração de clientes ou início da Etapa 6. Integração Android permanece pausada. Registro detalhado: `TRANSBORDO-CLIENTES/ETAPA-05/RELATORIO.md` e `RESULTADOS.json`. Esta entrada atualiza o estado provisório anterior; não declara transbordo operacional concluído.

## 15/11/2026 — Etapa intermediária 5B e encerramento do ambiente 34 (decisão do proprietário)
- **Etapa 5B — auditoria pré-transbordo obrigatória:** após a Etapa 5 e antes da Etapa 6, comparar GitHub, Firebase e dependências do 34/37, incluindo ANGRA VIDROS, Speed Food e LEAL INFO Alerta. Salão MJ: localizar e preservar como projeto independente, sem incorporá-lo ao GLOBAL/PET.
- Conferir código-fonte, recompilação dos APKs, vínculos, configurações e integrações necessárias. Não presumir equivalência entre ambientes nem copiar segredos/dados de produção; iniciar com inventário de leitura, sem modificar recursos aprovados.
- **Destino final:** 37 como ambiente único, autônomo e oficial. O 34 fica preservado até a conclusão das etapas 6–7 e homologação de clientes, licenças, atualizações, APKs e integrações.
- **Encerramento controlado do 34:** somente após backup histórico privado e segmentado com inventário, hashes e restauração testada, e autorização expressa do proprietário, desativar/remover sua cópia operacional. O backup não deve ser dependência de execução do 37.
- Não espelhar 34 no 37, não duplicar estruturas do Baú, não alterar produção, não executar transbordo nem exclusão nesta fase.
**Status:** decisão de planejamento registrada; auditoria 5B, backup e desativação não executados nem homologados.
