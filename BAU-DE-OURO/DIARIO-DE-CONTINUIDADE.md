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
