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
