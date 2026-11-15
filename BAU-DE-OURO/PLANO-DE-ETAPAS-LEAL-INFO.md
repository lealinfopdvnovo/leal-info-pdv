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
