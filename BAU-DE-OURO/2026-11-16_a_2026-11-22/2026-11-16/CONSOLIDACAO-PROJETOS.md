# Continuidade consolidada — 16/11/2026

## Escopo e fontes
Organização documental somente. Lidos em 16/11: árvore atual do Baú, índice, registro Etapa5B de 15/11, requisitos de acesso ANGRA e registro iFood #9407. Também utilizado o histórico de conversa disponibilizado. Os registros anteriores são preservados e prevalecem nos detalhes. Não acessamos todos os chats nem examinamos os binários citados. Referência de arquivo não equivale à verificação do conteúdo.
Nenhum teste, compilação, publicação operacional, alteração de feeds ou acesso a equipamento real executado nesta organização.

## PDV / GLOBAL e PET
- **Pedido:** base GLOBAL comum e particularidades somente no PET; não substituir personalizações. Atualização pelo ATUALIZAR, preservando dados/licença/backup.
- **Ações anteriores:** há novos registros de 13/11 sobre ponte 10.467/10.468, recuperação conjunta e validações; não tratar o retrato de feeds de 06/11 como estado atual.
- **Testes/publicações:** consultar os documentos específicos, sem inferir que validação de laboratório equivale à instalação de todos os clientes.
- **Relato de 10/11 disponível no contexto:** PDV sem funcionar e pedido de trocar “Orçamento de serviço” por “Nota de serviço”; resolução exata não demonstrada neste trecho.
- **Pendência:** preservar distinção entre QA Angra, produção e ambientes 34/37. Não reabrir implementação por uma tarefa documental.
- [Ponte e recuperação](../../2026-11-09_a_2026-11-15/2026-11-13/ETAPA2-PONTE-10467-10468-RECUPERACAO-CONJUNTA.md).
- [Roteiro anterior](../../2026-11-02_a_2026-11-08/2026-11-06/ROTEIRO-CORRECAO-DEFINITIVA-GLOBAL-PETS.md).

## ANGRA VIDROS / Vidraçaria
- **Pedido:** vínculo exclusivo ao PDV ANGRA QA37 e gestão de proprietário, funcionário e gerente no app e na Vidraçaria do PDV; testador não é proprietário comercial definitivo.
- **Ação documentada:** gateway QA37 separado; APK paralelo 0.3.8, depois controle individual 0.4.0 e referência posterior ao APK 0.4.1. Não declarar a 0.4.0 como última versão apenas por sua documentação ser mais detalhada.
- **Testes documentados:** protocolo/Windows/emulador e preservação de recursos. Relato do usuário confirma autorização, código e conexão; não comprova envio completo com foto, medidas, imagem e autoria.
- **Publicação documentada:** PET1.14 e depois PET1.15 exclusivamente QA005, com execuções registradas no documento fonte. Atualização posterior e divergência rodapé1.7 versus instalador1.15 exigem verificação própria; não ordenar reinstalação automaticamente.
- **Pendências:** homologação física ponta a ponta, identidade persistida e não duplicação; verificar transferência/assunção do proprietário NA VERSÃO ATUAL, revogando teste e preservando equipe/projetos/licença/vínculo. Não afirmar que existe ou inexiste sem evidência.
- **Dados antigos:** não apagar, migrar ou reenviar automaticamente pendências de outro tenant.
- [Registro detalhado da Etapa5B e incidentes](../../2026-11-09_a_2026-11-15/2026-11-15/ETAPA-5B-INTEGRACOES-QA.md).
- [Requisitos conceituais históricos](../../ANGRA-VIDROS-ACESSOS-FUNCIONARIOS-2026-11-15.md): marcados originalmente não implementados; consultar evolução no registro acima, sem sobrescrever o histórico.
- Funcionalidades históricas aprovadas da Vidraçaria — projetos, financeiro, unidades, 3D e impressão — devem permanecer preservadas.

## iFood
- **Pedido:** reunir evidências da separação e encaminhar à homologação; não confundir programa conferenciador com integração produtiva homologada.
- **Ação de 13/11 documentada:** R8.1 rejeitou sacola; R8.5.2 correto mostrou pedido #9407, sacola final1/3/0, subtotal25,96, frete8,90, taxa1,99 e pagamento36,85.
- **Teste/evidência:** log oficial contém loja200, polling200, ACK202 e virtual-bag200. Consultas posteriores não provam captura em tempo real da separação.
- **Decisão:** cupom dispensado segundo Adriano; não reabrir por alerta genérico. Dois vídeos e JSON recebidos no histórico registrado.
- **Publicação/envio:** o registro de 13/11 não declara envio ao chamado nem homologação oficial; não inventar resposta posterior sobre ruptura.
- **Pendência:** encaminhamento/avaliação oficial ticket35288437 e reconciliação do checklist vigente. Não presumir retirada/agendado concluídos.
- [Evidências #9407](../../2026-11-09_a_2026-11-15/2026-11-13/conversas/IFOOD-LOGS-E-SACOLA-9407.md).

## ALERTA / Cloudflare
- **Pedido:** preservar produção, Firebase e ambiente34; trabalhar somente em QA autorizado.
- **Ação documentada:** candidata privada05E/QA005 com D1/filas/composição FCM; origem operacional bloqueada/desativada por padrão.
- **Teste:** D1/FCM simulado, fila e DPAPI Windows registrados; não equivalem a push real recebido.
- **Publicação:** não demonstrada implantação operacional ponta a ponta do Alerta.
- **Pendência:** recursos/credenciais QA legítimos, fonte reproduzível do APKV1.2 e teste PDV→Worker→FCM→telefone. Segredos permanecem privados.

## Speed Food
- **Ação documentada:** fonte privada1.0.35, hook candidato de conclusão em ambiente controlado.
- **Teste:** transporte/SQLite e comparação de assets; não circuito remoto/GPS/segundo aparelho.
- **Publicação:** candidata QA não implica atualização de produção.
- **Pendência:** configuração Auth/RTDB/Hosting QA, regras implantadas e ciclo completo.
- Não mover ou reorganizar binários/integradores por esta tarefa; usuário pediu preservar projetos interligados.

## Salão MJ
- **Pedido:** app independente do PDV; preservar agenda, lembretes, histórico, financeiro, galeria, catálogo e WhatsApp.
- **Ação documentada em Etapa5B:** fonte/APK1.9.5, WebView local e API própria opcional; independência do PDV confirmada no registro.
- **Teste documentado:** DOM, assets, API local e política Java. Não equivale a nova homologação física neste trabalho.
- **Publicação:** não houve nova distribuição nesta organização.
- **Pendência:** validar no app atual os requisitos ainda relatados antes de prometer implementação. Manter separado de GLOBAL/PET.

## LIA
- **Pedido histórico/reforçado:** retirar botão e funções exclusivas, preservar animação de entrada; backup visual reutilizável e origem rastreável.
- **Ação/teste/publicação:** permanecem nos registros históricos; nenhuma nova execução aqui.
- **Pendência:** não reativar voz/experimentos nem apagar funções do PDV por organização documental.

## Cozinha Premium
- **Pedido:** preservar visual aprovado, mesas, estados por item, catálogo/importação, preparo/entrega e pagamento parcial.
- **Ações/testes históricos:** aprovados em etapas anteriores conforme consolidação anterior; não foram repetidos nesta organização.
- **Publicação:** nenhuma nova nesta tarefa.
- **Pendência:** conservação dessas funções durante futuras mudanças autorizadas de GLOBAL/PET.

## Próximo passo e limites
Etapa5B permanece parcialmente pendente de homologação física; não liberar Etapa6 por testes simulados. Priorizar a verificação concreta da versão atual e do fluxo de proprietário ANGRA, evitando repetir perguntas/testes já comprovados.
[Consolidação anterior](../../2026-11-09_a_2026-11-15/2026-11-09/CONSOLIDACAO-PROJETOS.md) conserva o histórico mais antigo; novos registros acima não o apagam.
