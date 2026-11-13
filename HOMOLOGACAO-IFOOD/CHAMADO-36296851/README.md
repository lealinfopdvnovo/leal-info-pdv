# LEAL INFO PDV — HOMOLOGAÇÃO IFOOD GROCERIES / ORDER VIRTUAL BAG
## Chamado ativo 36296851 | histórico 35288437 | 13/11/2026

STATUS: ORGANIZAÇÃO INICIADA; EVIDÊNCIAS NÃO HOMOLOGADAS; ENVIO BLOQUEADO ATÉ AUDITORIA.

### Regras
- Este diretório e branch são exclusivos da homologação iFood, sem alterar GLOBAL, PET, produção, PR #37 ou feeds.
- Nunca inventar logs, horários, operações, cupom, resultados, vídeos ou suporte funcional. Distinguir observado / inferido / pendente.
- Não enviar evidências ao iFood sem auditoria de ponta a ponta e autorização do proprietário.
- Preservar originais, SHA-256, data, fonte, pedido UUID, merchant, ambiente e aplicativo de teste; produzir cópias anonimizadas apenas quando apropriado. Não armazenar tokens, senhas, chaves ou dados pessoais sensíveis no GitHub.
- Links de vídeo precisam ser acessíveis ao analista e começar por https://, http:// ou www.; validar acesso sem autenticação de proprietário.
- R8 5.2 é integrador de testes; não assumir que o PDV comercial possui integração completa. Confirmar escopo final e prontidão antes de pedir homologação.
- Nenhum teste ou auditoria implica aprovação iFood.

### Checklist oficial recebido em 13/11/2026
1. Cenário 1 — Separação Parcial: pedido com 3 produtos, 3 unidades de cada, cupom aplicado; item 1 solicitar cancelamento; item 2 separar 3 unidades; item 3 separar 1 unidade; comprovar consumo correto de items.unavailable.
2. Cenário 2 — Retirada: somente se a integração suporta retirada; criação, recebimento, separação completa; documentar objetivamente se aplicável.
3. Cenário 3 — Agendado: criação, recebimento, separação completa.
4. Logs: Developer Portal > Logs da API > HTTP Requests > aplicativo de testes usado nas gravações > Buscar > baixar e anexar ao chamado.
5. Correlacionar vídeo e requests por pedido, aplicativo, ambiente, timestamps com fuso, método, endpoint, status HTTP e payload relevante; não confundir polling, ACK e GET virtual-bag com operações de Picking.

### Materiais já identificados (não equivalem a aprovação)
Pasta Drive: https://drive.google.com/drive/folders/1IC3xUt2t9P3At1RmKDoU29pJQqV3I5Dm
- 01_Criacao_Confirmacao_Pedido.mp4: https://drive.google.com/file/d/1GyM-gsf__vPRdW30J4gyOOERIsed9Enx/view?usp=drivesdk
- 02_Separacao_Parcial_9407.mp4: https://drive.google.com/file/d/1BeZFkBM2jSj1Pc94dokw_T6W2M36GrvV/view?usp=drivesdk
- 03_Logs_API_Oficiais.json: https://drive.google.com/file/d/1p_TcYfzPbU313UOUr3X2ddF7IavVdxVL/view?usp=drivesdk
- 04_Relatorio_R8_5_2.txt: https://drive.google.com/file/d/1O78YMnJKXUg--1OLLRXstfNtGmpYgHDz/view?usp=drivesdk
- 05_Texto_para_o_chamado.txt: https://drive.google.com/file/d/1GGx9_Qcr4UprR-yqPF6czbJeAdcHAqsg/view?usp=drivesdk

Pedido 9407, UUID dae27b3f-f0c2-4912-af6d-09bf91d01235, criado 13/11/2026 09:58:54 BRT, concluído 10:28:33 BRT. Resultado relatado: açúcar indisponível, arroz 3, feijão 1. GET virtual-bag posterior às 11:04:10 não demonstra chamadas de Picking. Verificar cupom, pedido de cancelamento, items.unavailable, logs HTTP específicos e cenários adicionais. Não inferir conclusão a partir de estado final.

### Estrutura documental a completar
- 00-CHECKLIST-E-ESCOPO: checklist original, suporte retirada/agendado, matriz requisito x evidência.
- 01-CENARIO-SEPARACAO-PARCIAL: vídeo original, identificação do pedido, roteiro e logs HTTP correlacionados.
- 02-CENARIO-RETIRADA: evidências se aplicável, ou justificativa técnica verificável de não suporte.
- 03-CENARIO-AGENDADO: vídeo e logs correlacionados.
- 04-LOGS-API: exportação original do Developer Portal, inventário, timestamps e correlação; guardar originais em armazenamento apropriado, não presumir upload binário ao GitHub.
- 05-AUDITORIA: revisões independentes funcional, técnica/API, cronologia, documentação/links, verificação final.
- 06-ENTREGA: índice de links, anexos, mensagem final somente após aprovação interna.

### Critérios de liberação
[ ] Escopo e integração final confirmados
[ ] Cenário 1 completo e demonstrado
[ ] Cupom comprovado (sem pressupor dispensa)
[ ] Cancelamento solicitado e items.unavailable demonstrado
[ ] Retirada avaliada e documentada
[ ] Agendado completo
[ ] Logs HTTP oficiais exportados e associados a cada gravação
[ ] Cronologia e timezone coerentes
[ ] Links testados e permissões verificadas
[ ] Revisões independentes e conferência final sem pendências críticas
[ ] Autorização expressa do proprietário para enviar

### Próxima missão do Work
1. Inventariar sem modificar os cinco materiais do Drive e o código R8 5.2; localizar versão e implementação real de Order/Virtual Bag, Picking, cupom, retirada e agendamento.
2. Produzir matriz requisito→arquivo→trecho de vídeo→request HTTP→status→resultado, indicando AUSENTE quando faltar.
3. Não presumir que vídeo ou GET provam PATCH/POST/DELETE. Conferir se logs oficiais estão completos.
4. Identificar lacunas e propor somente testes faltantes, com roteiros e captura simultânea dos logs.
5. Fazer revisões repetidas baseadas em critérios independentes; não registrar como aprovado o que não foi testado.
6. Não publicar, migrar, abrir chamado ou enviar evidências; entregar relatório de prontidão e aguardar autorização.

Este README é um índice inicial de organização, não uma auditoria executada nem evidência de aprovação.
