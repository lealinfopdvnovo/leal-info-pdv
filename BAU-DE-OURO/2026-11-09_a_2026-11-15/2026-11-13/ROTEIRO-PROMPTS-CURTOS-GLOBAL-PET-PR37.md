# ROTEIRO CONTROLADO — LEAL INFO PDV GLOBAL/PET — 13/11/2026

## Instrução permanente para retomar em novos chats
O proprietário quer executar o trabalho em **prompts pequenos, sequenciais, um por vez**, poupando cota do Work. A assistente deve entregar apenas o prompt da etapa atual, aguardar relatório verificável do Work, analisar se foi concluída, e **somente então** liberar o prompt seguinte. Se falhar, criar correção cirúrgica da mesma etapa; não avançar nem repetir todo o trabalho. Não confundir plano com execução.

## Fonte de trabalho
Repositório lealinfopdvnovo/leal-info-pdv
PR #37 (DRAFT, sem merge): https://github.com/lealinfopdvnovo/leal-info-pdv/pull/37
Candidata: candidate/global-pet-independent-20261113
Base: prepare/organizacao-global-pet-20261104
Instalação real identificada por hash como 10.467; carregador antigo LojaPetBootstrap.Initialize, sem prova de PetRuntime.Initialize nem interfaces candidatas. Testes sintéticos anteriores não substituem teste licenciado. Conferir estado atual antes de agir; não assumir que continua igual.

## Regra de arquitetura
GLOBAL manda; PET recebe e personaliza. GLOBAL único e versionado. PET Loja, Casa e Gelamaq têm versões e personalizações próprias. Uma atualização PET não pode sobrescrever GLOBAL nem outros PETs. Mudança GLOBAL exige autorização explícita. Atualizações via botão ATUALIZAR, com backup, integridade, rollback, preservação de dados. Compatibilidade de interfaces estáveis; quando a migração exige mudança no carregador GLOBAL, pedir autorização específica e validar em ambiente de teste protegido. Nunca alterar produção, releases ou feeds sem aprovação.

## Sequência com portões de validação
### Etapa 1 — Diagnóstico da PR #37 (somente leitura)
Conferir PR, branches, commits, diffs, arquitetura atual, testes, instalador 10.467 e pendências. Gerar relatório conciso com o que já está pronto, o que falta, riscos, provas e menor mudança necessária. Nenhuma alteração no código, publicação, PR, feed, GLOBAL ou PET. Concluir com OK/INCOMPLETO/BLOQUEADO e evidências.

### Etapa 2 — Carregamento independente GLOBAL/PET
Somente após validação da etapa 1 e autorização para mudanças necessárias. Corrigir o carregador para descobrir/carregar PET versionado por cliente sem sobrescrever GLOBAL. Respeitar interfaces estáveis, licenças e dados. Testar com fixtures e demonstrar hashes/diffs. Se precisar alterar GLOBAL, não fazer sem autorização específica. Sem publicação.

### Etapa 3 — Atualizador independente e rollback
Provar PET-only, GLOBAL-only, falha/interrupção, restauração e preservação de dados/configuração. Nunca liberar update que misture arquivos ou toque outros clientes. Usar botão ATUALIZAR, sem setup ou comandos manuais ao cliente. Sem produção.

### Etapa 4 — Auditoria de regressão e segurança de dados
Login/licença, vendas, pagamentos, produtos, financeiro, cozinha, vidraçaria, impressão, backup, múltiplos clientes, versões, integridade, escala/telas. Evidências por cenário; registrar ausências. Não simular sucesso.

### Etapa 5 — Validação Windows licenciada em ambiente protegido
Planejar e executar com aprovação em Casa do Adriano, PC de teste, sem mexer na instalação funcional. Separar teste de produção, backup, licença válida, migração inicial, ATUALIZAR, rollback e testes funcionais. Não contornar ativação. Falha bloqueia publicação.

### Etapa 6 — Preparação para integração iFood
Definir interfaces e limites do módulo iFood, sem implementar integração incompleta nem modificar núcleo sem necessidade. Homologação iFood tem branch e pasta separadas: homologacao-ifood-36296851-evidencias / HOMOLOGACAO-IFOOD/CHAMADO-36296851. Não misturar escopos.

### Etapa 7 — Liberação controlada
Somente após relatório de evidências, revisão e autorização expressa do proprietário: planejar publicação seletiva, rollback e monitoramento. Nunca executar automaticamente.

## Regra de economia de cota
- Um prompt por etapa; escopo mínimo, condição de parada, saída curta e verificável.
- Não repetir coleta JSON nem testes já comprovados sem motivo.
- Ao receber resultado do Work, a assistente decide: APROVADA / CORRIGIR / BLOQUEADA, e emite o próximo prompt somente se APROVADA.
- Se houver bloqueio externo, registrar e parar sem consumir cota em tentativas cegas.
- Registrar decisões e evidências no Baú de Ouro; esta pasta de roteiro é isolada de produção.

## Situação atual
Roteiro registrado; **Etapa 1 ainda não executada neste novo ciclo**. O próximo passo quando o proprietário ligar o PC é enviar o PROMPT 01 de diagnóstico somente leitura. Não antecipar as etapas.
