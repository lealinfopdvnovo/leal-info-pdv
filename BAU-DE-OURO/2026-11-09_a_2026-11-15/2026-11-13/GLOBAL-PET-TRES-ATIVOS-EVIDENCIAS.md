# GLOBAL/PET — três clientes ativos — 13/11/2026

**Candidata isolada. Publicação e instalação BLOQUEADAS até comprovação operacional.**

## IMPLEMENTADO

Continuação do PR #37 sobre o PR #34, sem merge no main e sem segunda arquitetura. Contratos externos, manifestos v2, versões independentes, SHA-256, backup e recuperação por componente reaproveitados.

- `c94e4a7377e8fcf6a9d362074dcd5eeb4113239e`: política de atividade imutável/reversível. Casa do Genilson não recebe ofertas GLOBAL/PET, não inicia migração e não entra nos testes operacionais. Entradas diretas e aplicador PET também bloqueiam novas atualizações. Carregamento de módulo existente e recuperação antiga continuam disponíveis. Perfis e histórico não são apagados.
- `465479b890391747c7e0808d89e9e80e67545e15`: inventário das duas publicações confiáveis da Loja; fixture do aplicador passa a usar a publicação real 10.465, sem alegar que está instalada. Correção LOGON da receita original 69398d3 preservada no núcleo candidato, com teste original de Windows Forms. Coletor portátil somente de leitura, com testes de exclusão de dados sensíveis.
- `0bf088b8b2de6dbb1df835874b4bd48c012a5fbc`: regressão GLOBAL para os três clientes e teste dos botões independentes.
- `d1af022bfdf1b515e1e9800072569b79d06ab442`: leitura concorrente do diário nos testes de recuperação permite substituição atômica; timeout e verificação dos hashes continuam obrigatórios.

O núcleo candidato conserva a identificação da base histórica recomposta. Nenhuma versão de produção foi incrementada, nenhuma PET 1.4 foi inventada e nenhuma numeração comprova migração por si só.

## TESTADO

- **SUCCESS**: [Actions 43482979755](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43482979755), código `d1af022bfdf1b515e1e9800072569b79d06ab442`.
- **37** cenários de seleção, compatibilidade, integridade, inatividade e privacidade do coletor, aprovados em Windows e Linux: Actions 43482048226.
- **42** cenários PET: 14 por cliente ativo; **9** cenários GLOBAL: sucesso, interrupção/recuperação SYSTEM e SHA incorreto por cliente. Hashes dos componentes protegidos comparados antes/depois.
- Três perfis ativos e bootstrap atômico, contratos comuns, saúde de inicialização, módulos originais preservados, relatório financeiro Gelamaq, interface Windows com botões separados e quatro transições usando o aplicador da publicação Loja 10.465 aprovados.
- **10** testes do método LOGON publicado e **6** verificações do cliente inativo aprovados. Nenhum `profile-004.json` ou `bootstrap-004.json` foi gerado na rodada operacional candidata.
- Artefato **14066506967**, `integracao-componentes-Windows`, SHA-256 `77227ede89a579b24575bd4a39467d404bf74483f3618c48dc34ee6a54c9cd3b`. ZIP baixado e hash conferido; JSONs confirmam 42/9 cenários, somente IDs 001/002/003, status inativo e dez checks LOGON.
- A rodada 43482048217 falhou na leitura concorrente do diário durante recuperação; não é aprovação integral. A leitura foi ajustada sem remover timeout ou verificação de integridade, e a rodada final passou.

Referências anteriores continuam em CANDIDATA-20261113.md como histórico. A base recomposta continua sendo 10.463 com o carregador candidato e a correção LOGON recuperada; não é um binário equivalente à instalação 10.465 informado pelo proprietário.

Os testes de transação usam fixtures descartáveis, tarefas SYSTEM reais, arquivos e hashes reais. Dados de vendas/estoque, banco e licença dessas fixtures são sintéticos. Isso não equivale a uma operação licenciada do cliente.

## PRESERVADO

| Referência protegida | Identificador verificado |
|---|---|
| Canal GLOBAL `channel/global` | `48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1` |
| Distribuição de produção | `6f641fc6941b573f94393a437191761243cfa69e` |
| Blob do feed GLOBAL | `61b185f32f9491a4e70030529b5b4bd01857d9c7` |
| Blob do feed Loja | `ac7780488d5a3dccdc3778f251c30888cc0b6b0b` |

Nenhuma instalação real foi acessada ou escrita. Não afirmar comparação de hashes do computador da Loja sem a conferência local. Código do perfil 004 preservado; alterações de status ficam na candidata. Banco, licença e configurações reais não foram enviados ao GitHub. Firebase, APKs e iFood não foram modificados.

### Publicação não é instalação

As duas publicações da Loja foram baixadas e autenticadas pelo SHA-256 do pacote. Ambas contêm somente executável, DLL e deps; não contêm runtimeconfig nem toda a instalação.

| Referência publicada | SHA-256 do pacote | SHA-256 de LealInfoPDV.dll |
|---|---|---|
| 10.465 — LOGON | `728b20ff2e38d96deaf538ad45e6297bced845a6460c9a827a17ade3960d5fd6` | `9730e73043091d25b931951afd9986dea25e183e34ddfadc8b8e5821ed8d7c6b` |
| 10.467 — exibição PET 1.3 | `fd979804f5de82c58e1b911a2f0a449e6d8937cae06ef0c0fa39917c48dd161b` | `61ec4bdbbcdb7be92cee0fcac421c360cae5e58cd6604b73ef2eaad0717ca0d7` |

Executável comum aos dois pacotes: `45cada6874ed2450cf3ae5564dfdfc7faabe749213d45126de42ef69d9d7d040`.
Deps comum: `73477964d61c59ccf92d0532da4b8c46a18ade13ed0f1f69253b79127424d9e1`.

Feed atualmente oferece 10.465/PET registrado 1.2. A publicação que exibiu 1.3 teve binário 10.467. A informação do proprietário continua preservada como referência, mas a combinação instalada 10.465/PET 1.3 precisa de identificação por hash. Não usar 10.463/10.464 como equivalentes.

## CLIENTES

| Cliente | Situação candidata | Situação operacional |
|---|---|---|
| Loja Leal Info | Ativa; perfil, LOGON e atualização PET separados | Migração exata e Vidraçaria PENDENTES |
| Casa do Adriano | Ativa; perfil e atualização PET separados | Instalação licenciada PENDENTE |
| Gelamaq | Ativa; perfil, adaptador financeiro e atualização PET separados | Instalação licenciada PENDENTE |
| Casa do Genilson | INATIVO; sem ofertas ou primeira migração | Fora da migração; histórico e módulo preservados |

Reativação futura: alteração explícita da política na candidata, preservando o perfil existente e repetindo os gates aplicáveis antes de qualquer distribuição. O teste `WithActivity` comprova reversibilidade e ausência de efeito sobre os ativos. Não há reativação automática por feed.

## PENDÊNCIAS

| Critério | Situação |
|---|---|
| Identificação da instalação realmente usada na Loja | PENDENTE: falta confronto local dos hashes |
| Inicialização autenticada com licença válida | PENDENTE: não há ambiente clonado/licenciado acessível |
| Todas as funcionalidades, incluindo vendas, pagamentos, Cozinha, Vidraçaria e impressão | PENDENTE operacional; testes de perfil, financeiro e LOGON não substituem esse gate |
| Atualização independente dos três PETs | APROVADO em fixtures; operação real PENDENTE |
| GLOBAL preservando PETs | APROVADO em fixtures; operação real PENDENTE |
| Versões GLOBAL diferentes | Contratos testados; duas instalações reais compatíveis PENDENTES |
| Backup e rollback | APROVADO em fixtures; retorno após inicialização positiva licenciada PENDENTE |
| Dados/licenças preservados | Preservação em fixtures; comprovação da instalação exata PENDENTE |
| Botão ATUALIZAR interno | Integração e UI candidatas; fluxo completo licenciado PENDENTE |
| Desativação reversível 004 | APROVADO nas seis verificações de ciclo de vida |

### Coleta simples já preparada

Abrir `LealInfo_Conferir_Instalacao.exe`, selecionar a pasta do PDV e salvar `LEAL_INFO_CONFERENCIA_INSTALACAO.json` fora da instalação. Sem comandos, setup, atualização ou envio automático. O relatório contém somente nomes relativos, tamanhos, versões públicas e SHA-256 dos binários, deps/runtimeconfig, módulos/manifestos PET e dependências binárias. Não contém conteúdo dos arquivos, banco, licença, configuração ou senhas.

Coletor compilado no run 43482048217, artefato 14066110808. EXE SHA-256: `c8d93500f30c987bb926ac834f8e6c0b06ee37ca58f6603a0a3faf4b3dcc06e1`. O hash do ZIP de transporte foi verificado antes da extração; a entrega ao proprietário é o EXE, sem instalação.

Após confrontar esse JSON com os artefatos confiáveis, preparar cópia privada isolada da instalação identificada. Runtimeconfig/dependências exatas e licença válida devem ser conferidos no ambiente autorizado; não enviá-los indiscriminadamente ao repositório. Executar os gates operacionais sem desabilitar nem falsificar ativação. Não testar destrutivamente dados reais.

## PUBLICAÇÃO

**BLOQUEADA.** Não há autorização de publicação e os critérios operacionais continuam pendentes. A primeira transição exige novo carregador GLOBAL candidato; alterações futuras de PET não substituem a base. Distribuição somente após pacote de transição qualificado, ausência de downgrade, todos os gates positivos e autorização expressa. Nenhum setup, substituição manual de DLL ou comando PowerShell será exigido para atualizar o PDV.


Registro no Baú: candidata final `a9713b8c7810d09d667947c6cca869c9bc5c62c0`, PR #37. Documentação/evidências somente; nenhuma autorização de publicação ou instalação.
