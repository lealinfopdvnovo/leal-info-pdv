# Auditoria de desempenho — Leal Info PDV

**Registro:** 19/11/2026  
**Status:** planejamento; nenhuma auditoria executada ou alteração autorizada.

## Objetivo
Preparar o Leal Info PDV para futura integração iFood sem comprometer desempenho, sobretudo em computadores modestos (incluindo cliente com 2 GB de RAM).

## Escopo de verificação
1. Inspeção somente leitura do código: dependências, módulos não utilizados, inicialização, tarefas em segundo plano, consultas ao banco, imagens e potenciais vazamentos de memória. Não remover nada sem análise de dependências.
2. Medições controladas e reproduzíveis de RAM, CPU, disco, tempo de abertura e resposta do caixa para Standard, Plus e Pro, com funcionalidades efetivamente habilitadas e cargas equivalentes.
3. Avaliação separada de impacto estimado e posteriormente medido do iFood (polling, eventos, pedidos, logs e sincronização), sem executar novos testes de homologação até liberação do suporte iFood.
4. Testar perfis de hardware e número de caixas simultâneos. Produzir requisitos **mínimos, recomendados e de alto desempenho**, distinguindo plano, módulos ativos e volume operacional.
5. Relatório com evidências, gargalos, riscos, recomendações e comparação antes/depois, caso otimizações sejam posteriormente autorizadas.

## Referências preliminares de mercado
Foram discutidos Linx POS, Linx Postos POS, TOTVS Moda PDV e MarketUP, que divulgam requisitos de hardware em formatos distintos. Uma abordagem relevante é dimensionar por quantidade de caixas e transações simultâneas. Faixas **hipotéticas para investigação**, NÃO homologadas para Leal Info PDV: 4 GB RAM (mínima), 8 GB (recomendada), 16 GB (alto desempenho). A viabilidade de 2 GB deve ser medida, sem promessas.

## Regras e bloqueios
- GLOBAL e PETs intocáveis; nenhuma alteração, remoção, atualização ou publicação sem autorização expressa.
- Primeira etapa exclusivamente de leitura; testes de desempenho somente em ambiente controlado quando autorizados.
- Não confundir revisão estática do código com medição real de desempenho.
- Homologação iFood permanece pausada: suporte confirmou instabilidade de visualização/exportação de logs HTTP e pediu aguardar normalização antes de novos testes/gravações.

## Próximo passo
Autorizar explicitamente a auditoria somente leitura; produzir inventário e relatório. Depois decidir se haverá testes e otimizações.
