# PAINEL MESTRE — LEAL INFO
Atualizado em 2026-11-15. Documento de coordenação, não é certificado de testes ou inventário automático.

## Regra operacional
Antes de sugerir correção, publicação ou nova funcionalidade:
1. Consultar este painel, o Baú de Ouro, a documentação pertinente, commits e evidências recentes.
2. Identificar ambiente, versão, cliente, GLOBAL/PET, dependências e estado de homologação.
3. Comparar com implementação anterior funcional antes de reconstruir.
4. Classificar cada afirmação: CONFIRMADO (com evidência), RELATADO, HIPÓTESE ou PENDENTE DE VERIFICAÇÃO.
5. Avisar ao proprietário sobre conflito, risco de regressão, mudança de prioridade ou trabalho duplicado.
6. Exigir autorização específica para alterações de código/publicação, testes e registro de resultados.

## Proteções permanentes
- GLOBAL não deve ser alterado sem autorização específica. PET de um cliente não deve afetar outros clientes.
- Preferir correção mínima no PET, após comparação com GLOBAL e versões estáveis.
- Testar antes de publicar; atualizações de PDV pelo botão ATUALIZAR, respeitando canal/cliente.
- Preservar a abertura cinematográfica e a animação AJUDA homologada no QA ANGRA VIDROS (círculo vermelho, seta azul neon, balão, 3 piscadas de 5 segundos). Não remover ou alterar sem autorização expressa.
- Não apagar projetos, fotos, vínculos, pendências, credenciais ou dados de clientes como parte de diagnóstico.

## ANGRA VIDROS — situação relatada em 2026-11-15
- RELATADO: ambiente 34 tinha integração app Android ↔ PDV funcionando anteriormente.
- RELATADO: ambiente 37 está em construção/homologação; não há comprovação de que todas as configurações da integração foram migradas.
- CONFIRMADO POR OBSERVAÇÃO DO USUÁRIO: navegador apresentou mensagem de computador autorizado; no QA o código de conexão não foi gerado, mesmo após GERAR/RENOVAR e ATUALIZAR STATUS.
- CONFIRMADO POR CAPTURA DE TELA: app mostrava vínculo com LEAL INFO PDV • PC-CASA, última sincronização 26/10/2026 15:13, cinco confirmações pendentes e falha de DNS para angra-vidros-gateway.classyglobe9.chatgpt.site. Desvinculação não concluída, segundo usuário.
- HIPÓTESE: ambiente novo ainda não possui todas as dependências/configurações de integração do anterior. NÃO tratar como causa raiz confirmada.
- DECISÃO DO PROPRIETÁRIO: Work pausou investigação/correções da integração para priorizar etapas restantes, segundo relato; preservar diagnósticos, não publicar correção neste momento.
- PRÓXIMO PASSO: localizar o plano oficial de sete etapas, confirmar quais etapas foram homologadas e executar as restantes conforme dependências. A animação AJUDA (QA 005) não deve ser confundida automaticamente com etapa 5/7.
- HOMOLOGAÇÃO FINAL: comparar ambientes 34 e 37, Firebase/gateway, geração de código, desvinculação segura, envio de foto e medidas, sincronização e tratamento das cinco pendências. Não declarar integração aprovada sem testes de ponta a ponta.

## Outros projetos e prioridades (revalidar status antes de agir)
- LEAL INFO PDV: clientes Loja Leal Info, Casa do Adriano, Gelamaq, Casa do Genilson; atualizações GLOBAL e PET separadas.
- iFood Groceries: homologação em andamento conforme histórico; ticket 35288437; evidências e testes ainda precisam de conferência.
- Zé Delivery: contato comercial/API em tratativa; não presumir acesso aprovado.
- Apps Android ANGRA VIDROS, SpeedFood/alertas e Salão MJ: projetos distintos, com dependências diferentes. Não alterar sem escopo definido.

## Protocolo de continuidade entre conversas e Work
No início de nova tarefa: ler este painel e os registros relevantes; confirmar última versão/ambiente e evidências. Ao concluir: registrar data, cliente, branch, commit, teste, resultado, pendências e decisão. Se o registro estiver desatualizado ou contraditório, sinalizar e verificar; nunca inventar progresso.

## Limitações
Este painel é um retrato baseado em relatos, evidências disponíveis e documentação consultada. Não sincroniza automaticamente com conversas ou execução do Work; precisa de manutenção explícita. A criação deste documento não autoriza modificações de código, GLOBAL, PET ou publicação.

## Recuperação do planejamento — relato do proprietário em 2026-11-15
- RELATADO PELO PROPRIETÁRIO: as etapas 5, 6 e 7 do plano de sete etapas tinham como objetivo o transbordo/migração real de todos os clientes para o ambiente novo.
- NÃO RECUPERADO: divisão exata de atividades e critérios de cada etapa 5, 6 e 7. Não inventar nem tratar uma divisão sugerida como plano original.
- ORIENTAÇÃO: antes de executar migração, inventariar clientes, versões, PETs, licenças, dados, integrações e mecanismo de rollback; apresentar plano e pedir aprovação específica. Não realizar migração automática com base neste registro.

## Refinamento do plano reconstruído (2026-11-15)
- RELATO DO PROPRIETÁRIO: a conversa original das sete etapas enfatizava separar GLOBAL de PET e organizar essa arquitetura em ambiente novo, antes do transbordo real dos clientes.
- RECONSTRUÇÃO PROVISÓRIA (não transcrição original): etapa 5 = auditoria/organização da separação GLOBAL/PET no novo ambiente; etapa 6 = transbordo controlado por cliente; etapa 7 = homologação geral.
- O prompt anterior da etapa 5 focado em inventário de migração deve ser REVISTO para colocar a separação arquitetural como objetivo central. Não iniciar migração, nem alterar GLOBAL ou PETs de produção sem autorização específica.


## 15/11/2026 — Etapa 5: execução seletiva comprovada
O proprietário autorizou o escopo de migração seletiva/homologação rápida. Comparação de 28 componentes: o QA aprovado já contém as versões necessárias do 37; nenhuma importação/modificação de produto foi necessária. Preservados os 252 arquivos rastreados do 34. Branch QA `qa/etapa5-selective-20261115`, código `64b5ed6a057cbfdfd9d3ce9212ac001b1b6cdf19`. Windows Actions `43735576962` SUCCESS: 37 verificações existentes e 14 do pacote QA005 real. Dez testes locais de política aprovados. Etapa 5 tecnicamente concluída, aguardando homologação do proprietário. Sem produção, migração de clientes ou início da Etapa 6. Integração Android permanece pausada. Registro detalhado: `TRANSBORDO-CLIENTES/ETAPA-05/RELATORIO.md` e `RESULTADOS.json`. Esta entrada atualiza o estado provisório anterior; não declara transbordo operacional concluído.
