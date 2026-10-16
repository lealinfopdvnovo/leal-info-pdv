# Bootstrap 10.403 — decisão e evidências 001/003
Data: 16/10/2026.
**DECISÃO: primeira transição NÃO protegida. Não publicar integração nem bootstrap.**
Foi implementada uma bateria de testes negativos reproduzíveis, não uma correção de produção. Sob as restrições atuais não foi encontrado ponto legítimo que execute a proteção antes da primeira sobrescrita.

## 1. Causa confirmada
Ambas as fontes publicadas possuem o mesmo UpdateManager.cs:
- 001: 3dfab93e04ebb1cfef72606c2d52a76c05eead94.
- 003: d1525b64db03bc4608aabe020ecfc6e200ec2f05.
- SHA-256 do texto UTF-8 lido por git: ae3181a75ad05bf387adba987332a048b87056c6716db1940dec261e8595affd.
A integração permanece em feature/angra-pdv-sync, commit 458c6a1812721d729b409eba21bd6301019b61b8. Não foi alterada nem mesclada.

## 2. Fluxo real auditado
| Etapa | Código/comportamento |
|---|---|
| Consulta | MainForm chama UpdateManager.CheckForUpdatesAsync após abrir; usuário confirma no próprio PDV |
| Canal | clients/<ID>/version.json e canal global; valida ID e prefixo da URL do pacote |
| Download | HttpClient no processo PDV; pacote completo em diretório aleatório no TEMP |
| Integridade | SHA256.HashData comparado ao manifesto antes da extração |
| Arquivos permitidos | UpdatePayloadPolicy exige LealInfoPDV.exe na raiz e rejeita banco, licença, config, scripts e travessia de caminho |
| Aplicador | PDV escreve apply-update.ps1 fixo e inicia powershell.exe externo |
| Encerramento | Script encerra LicAi e o PID anterior, com espera limitada |
| Substituição | Copy-Item -Force, arquivo por arquivo, na AppContext.BaseDirectory |
| Reinício | Start-Process do caminho do executável anterior; sem confirmação de saúde |
| Recuperação | Sem cópia prévia dos binários, journal, restauração ou supervisor de retomada neste fluxo |

O manifesto do executável usa asInvoker. A chamada do PowerShell não usa Verb=runas. Permissão de escrita depende das ACLs da instalação; não foi inspecionado o computador do cliente. O instalador histórico declara C:\LEAL INFO PDV e exige admin, mas aceita diretório anterior. Isso não prova caminho ou permissões atuais.
O banco fica em LocalApplicationData/LealInfoPDV, separado dos binários. A integração tem backup SQLite antes da migração, que não recupera executáveis parcialmente sobrescritos.

## 3. Alternativas investigadas
| Alternativa | Resultado |
|---|---|
| Colocar updater seguro no pacote | Só executa depois da etapa vulnerável; não protege a primeira instalação |
| Pacote apenas adicionando auxiliar | O payload exige LealInfoPDV.exe; o aplicador o sobrescreve, mesmo com conteúdo igual. Não existe gancho para executar o auxiliar antes |
| Trocar o script por arquivo do pacote | O aplicador é gerado pelo código antigo no TEMP; scripts não são aceitos no payload |
| Manifesto indicar comando anterior | O modelo aceito não possui essa operação |
| Instalação lado a lado e troca de ponteiro | O aplicador atual usa diretório/caminho fixos; não há seletor/launcher já provisionado nesse fluxo |
| Rota antiga UpdateService/instalador | Program.cs fixa update=null antes desse bloco; não oferece uma rota ativa por canal 001/003 e contraria a entrega sem instalador |
| Voltar manifesto à versão anterior | Não recupera programa que deixou de iniciar; a seleção exige versão maior, não downgrade automático |
| Guardião externo instalado previamente | Pode ser objeto de um trabalho separado de provisionamento e validação. Não existe provisão automática comprovada pela 10.403; não foi instalado nem presumido disponível |

Um componente de recuperação precisa já estar ativo/recuperável antes de qualquer arquivo operacional ser sobrescrito. Acrescentá-lo depois não resolve a janela inicial. Não foram exploradas brechas em validação de caminho, execução de arquivos ou permissões.

## 4. Implementação e arquivos
Branch isolada: qa/bootstrap-10403-recovery-audit.
Commit final dos testes: 7bafb8b7f9d7d85d7af734ee29c4091288ed503b.
Arquivos novos:
- .github/workflows/bootstrap_10403_audit.yml
- qa/bootstrap-10403/LegacyApplyEvidence.ps1
- qa/bootstrap-10403/HashEvidence.ps1
- qa/bootstrap-10403/evidence-summary.json
- docs/bootstrap-10403-decision.md (este documento)

Nenhum arquivo de produção foi alterado. O workflow tem contents:read, executa apenas na branch QA e não possui etapa de publicação nem credenciais de distribuição.
O script original é extraído de cada SHA fixo, sem substituir o loop de aplicação. São interpolados diretórios descartáveis e um PID inexistente para não encerrar aplicações externas. Nos cenários de falha existe injeção explícita no comando de cópia. O teste não certifica o encerramento do processo real do PDV.

## 5. Testes executados e limites
Execução Windows final: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/38895463139
Jobs 001 e 003: success. Isso significa que a bateria comprovou os comportamentos esperados, incluindo os defeitos; NÃO que o bootstrap está seguro.

Foram seis cenários do aplicador por cliente e um teste unitário do guard de hash por cliente.
Windows/PowerShell e operações de arquivo/lock/processo reais. Executáveis antigos/novos são fixtures inofensivas, não builds completos do PDV. SQLite é um banco real de teste contendo BOB; licença/configuração são bytes sentinela, não credenciais nem validação de licença real. A comparação comprova preservação desses bytes neste mecanismo, não homologação dos computadores.

| Cenário | 001 | 003 |
|---|---|---|
| Aplicação normal | 3 arquivos novos; fixture iniciou | Igual |
| Bytes de download corrompidos | Guard C# exato rejeitou antes da aplicação | Igual |
| Falha na segunda cópia | 1 novo + 2 antigos; sem restauração | Igual |
| Saída inesperada após primeira cópia | 1 novo + 2 antigos; sem restauração | Igual |
| Processo morto após primeira cópia | 1 novo + 2 antigos; sem restauração | Igual |
| Arquivo bloqueado por FileShare.None | 2 novos + 1 antigo; sem restauração | Igual |
| Falha ao reiniciar: destino inexistente | 3 novos; não iniciou nem restaurou | Igual |
| Preservação SQLite/licença/config de teste | Hashes iguais em todos os seis cenários | Igual |
| Recuperação automática dos anteriores | Não ocorreu nos cenários de falha | Igual |
| Falta de energia e durabilidade de disco | NÃO TESTADAS; matar processo não equivale a desligar máquina | Igual |
| Segunda atualização após transição segura | BLOQUEADA: não existe transição segura implementada | Igual |

O teste de corrupção não realiza download HTTP: compila e executa a condição C# de verificação extraída da fonte sobre bytes locais adulterados.
Não foram executados builds completos, migrações, interface/3D ou integração Android nesta etapa: não fazem parte da comprovação desse bloqueio.
Duas execuções anteriores detectaram defeitos do harness (leitura do exit code e conversão PSObject→string); foram corrigidos apenas nos testes. Não foram apresentadas como evidência de falha do PDV. O resultado final é a execução vinculada acima.

## 6. Evidências persistidas
Linhas selecionadas dos logs originais:

### Cliente 001 — job 116954756846
```text
2026-10-16T11:42:01.5368182Z EVIDENCE 001 normal exit=0 changed=3 old=0 dataPreserved=True
2026-10-16T11:42:03.4928713Z EVIDENCE 001 copy-failure exit=1 changed=1 old=2 dataPreserved=True
2026-10-16T11:42:07.4417300Z EVIDENCE 001 process-exit exit=91 changed=1 old=2 dataPreserved=True
2026-10-16T11:42:09.1531552Z EVIDENCE 001 process-kill exit=-1 changed=1 old=2 dataPreserved=True
2026-10-16T11:42:11.0037508Z EVIDENCE 001 locked-file exit=1 changed=2 old=1 dataPreserved=True
2026-10-16T11:42:12.7761356Z EVIDENCE 001 restart-failure exit=1 changed=3 old=0 dataPreserved=True
2026-10-16T11:42:13.5106864Z PASS evidence harness; NOT an approval of bootstrap safety. No customer data or updater feeds accessed.
2026-10-16T11:42:17.8232014Z PASS exact hash guard 001; corrupted bytes rejected before application
```

### Cliente 003 — job 116954757072
```text
2026-10-16T11:42:02.1992891Z EVIDENCE 003 normal exit=0 changed=3 old=0 dataPreserved=True
2026-10-16T11:42:04.0458423Z EVIDENCE 003 copy-failure exit=1 changed=1 old=2 dataPreserved=True
2026-10-16T11:42:07.8614185Z EVIDENCE 003 process-exit exit=91 changed=1 old=2 dataPreserved=True
2026-10-16T11:42:09.4288491Z EVIDENCE 003 process-kill exit=-1 changed=1 old=2 dataPreserved=True
2026-10-16T11:42:11.0801434Z EVIDENCE 003 locked-file exit=1 changed=2 old=1 dataPreserved=True
2026-10-16T11:42:12.6855715Z EVIDENCE 003 restart-failure exit=1 changed=3 old=0 dataPreserved=True
2026-10-16T11:42:13.3503657Z PASS evidence harness; NOT an approval of bootstrap safety. No customer data or updater feeds accessed.
2026-10-16T11:42:16.9119448Z PASS exact hash guard 003; corrupted bytes rejected before application
```

Resumo estruturado em qa/bootstrap-10403/evidence-summary.json. O workflow também guarda scripts extraídos, stdout/stderr e resultados em artefatos por 30 dias. Os testes e este resumo estão versionados para recuperação posterior.

## 7. Riscos restantes e segurança inicial
**Não é seguro declarar a primeira atualização protegida.**
Existem janelas demonstradas de instalação parcial e falha sem restauração. Corte real de energia, ACLs reais e inicialização do PDV completo não foram homologados.
Mesmo uma recuperação externa futura precisará de backup verificado dos binários, dados/journal duráveis, retomada após reboot, privilégios adequados e validação de saúde sem alterar banco/licença/configurações.
Nenhum mecanismo novo foi colocado em produção. Não foi entregue ZIP ou instalador como procedimento.

## 8. Condições para publicação posterior
1. Provisão legítima de recuperação independente ANTES da primeira sobrescrita, ou comprovação de componente já instalado que ofereça isso.
2. Evidências reproduzíveis da primeira transição real 10.403 em ambiente representativo das duas instalações, incluindo reinício do sistema, permissões, bloqueios, falha de cópia e restauração verificada.
3. Validação de segunda atualização com o mecanismo protegido.
4. Somente depois: builds/regressões sobre as bases individuais e publicação conjunta nos canais 001/003, conforme autorização aplicável.
A arquitetura inspecionada não fornece o item 1 sozinha. É necessária intervenção externa de provisionamento, cujo meio e acesso não estão disponíveis/validados neste trabalho. Não foi presumida autorização para reinstalação, manipulação manual de produção ou envio de pacote de atualização fora do mecanismo aprovado.
001/003 continuam sem nova publicação nesta execução; 002/004/universal não foram alterados. Instalação da integração nos PCs permanece não confirmada.
