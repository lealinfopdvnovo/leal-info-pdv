# Continuidade consolidada — 23/11/2026

## Escopo e critério

Organização documental somente. Foram considerados a árvore atual da branch `bau-de-ouro`, o índice geral, os registros das semanas anteriores e o histórico de conversa disponibilizado. Os documentos originais permanecem preservados e prevalecem nos detalhes.

As classificações abaixo têm significado estrito:

- **Pedido:** solicitação ou intenção do proprietário.
- **Ação feita:** alteração registrada como executada.
- **Teste:** evidência técnica registrada ou resultado informado, com a origem indicada.
- **Publicação:** disponibilização comprovada em canal operacional; ausência de prova permanece como pendência.
- **Pendência:** item ainda sem conclusão comprovada.

Nenhum código, feed, banco, licença, Firebase, APK, credencial, ambiente produtivo ou configuração Cloudflare foi alterado nesta organização.

## PDV e separação GLOBAL/PET

### Pedido
Manter GLOBAL como base comum na mesma faixa para todos os clientes e PET como personalização exclusiva, sem misturar arquivos. Alterações de PET não devem modificar o GLOBAL sem autorização explícita.

### Ação feita
O Baú conserva o roteiro de correção definitiva GLOBAL/PET e os registros das correções e entregas anteriores. Esta rodada apenas referencia esses documentos; não transporta executáveis nem equaliza versões.

### Teste
Há evidências históricas de builds e verificações nas etapas anteriores. No histórico acessível mais recente, não há novo teste técnico completo que prove a equalização de todos os clientes ou um ciclo integral de atualização e rollback para cada PET.

### Publicação
Nenhuma nova versão GLOBAL ou PET foi publicada por esta organização documental.

### Pendências
- confirmar a base GLOBAL efetivamente instalada em cada cliente antes de qualquer equalização;
- preservar o PET e o financeiro aprovado da Gelamaq;
- concluir as migrações de personalizações antigas sem substituir dados instalados;
- validar um ciclo completo pelo botão **ATUALIZAR**, incluindo inicialização e rollback;
- evitar colisão de versão e duplicidade do selo; Loja e Casa exigem tratamento conforme seus registros específicos;
- registrar separadamente a barra de backup solicitada para Loja e Casa; o histórico acessível relata atualização que não concluiu a instalação, sem confirmação posterior.

## Vidraçaria / ANGRA VIDROS

### Pedido
Conservar as funcionalidades aprovadas da Vidraçaria e continuar a migração dos clientes sem quebrar unidades, financeiro, agenda, orçamento ou personalizações.

### Ação feita
Os registros anteriores documentam a linha QA37, a identificação de proprietário/funcionários e evoluções visuais e de orçamento do ANGRA. O usuário informou posteriormente que o transbordo do ANGRA VIDROS foi concluído com sucesso.

### Teste
O sucesso do transbordo do ANGRA é um relato do proprietário no histórico acessível. Não foi anexado nesta rodada um novo log técnico independente.

### Publicação
Não houve nova publicação operacional nesta organização.

### Pendências
- realizar e comprovar os transbordos de Casa do Adriano, Loja Leal Info e Gelamaq;
- verificar no estado atual os papéis de proprietário e funcionários;
- manter Casa do Genilson como cliente descontinuado no planejamento, sem afirmar remoção do GitHub enquanto ela não estiver documentada.

## Cozinha

### Pedido
Preservar o desenho aprovado: identidade preta e dourada, mesas e estados de itens, catálogo/importação, preparação, entrega direta e pagamento parcial.

### Ação feita
Nenhuma mudança nova da Cozinha foi encontrada no histórico acessível desta rodada.

### Teste e publicação
Nenhum novo teste ou publicação foi registrado.

### Pendência
Manter o módulo congelado até existir pedido explícito e validar qualquer mudança isoladamente.

## LIA

### Pedido
Preservar o estado existente e não reativar ou restaurar a LIA sem autorização específica.

### Ação feita
O histórico acessível cita artefatos de análise destinados ao Claude, incluindo pacotes e PDFs. A referência aos nomes não comprova leitura, conteúdo, restauração ou implantação.

### Teste e publicação
Nenhum novo teste ou publicação da LIA foi comprovado.

### Pendência
Analisar os artefatos somente em tarefa autorizada e manter a LIA sem alteração operacional até decisão expressa.

## Aplicativos independentes

### Pedido
Tratar os aplicativos como projetos separados e não reorganizar APKs ou integrações operacionais por efeito desta documentação.

### Ação feita
Registros anteriores mantêm ANGRA VIDROS, SpeedFood e Salão MJ com seus respectivos estados de QA. Também foi registrada como proposta futura, ainda não aprovada para implementação, uma loja virtual autogerenciável no plano Plus e integrações com marketplaces/redes no plano Pro quando as APIs permitirem; o plano Standard fica fora dessa ideia.

### Teste
Os testes históricos permanecem nos documentos originais. Não houve novo teste nesta rodada.

### Publicação
Nenhuma nova publicação de aplicativo ocorreu.

### Pendências
- não confundir testes locais, emulador ou QA privado com homologação física;
- confirmar canais, versões e propriedade de cada aplicativo antes de nova entrega;
- tratar a proposta Plus/Pro como estudo futuro, não como funcionalidade existente.

## ALERTA / Cloudflare

### Pedido
Concluir a parte do alerta da Etapa 5B somente após funcionamento real.

### Ação feita
Em 17/11, Adriano informou que o Work realizou o teste, que o alerta funcionou corretamente e autorizou o encerramento dessa parte e o registro no Baú.

### Teste
Resultado confirmado pelo proprietário na conversa: alerta funcionando. O documento de 17/11 deixa claro que a evidência disponível é esse relato e não inventa identificadores técnicos ausentes.

### Publicação
Foi publicado apenas o registro documental de encerramento. Esta consolidação não altera Worker, D1, FCM, DNS ou qualquer serviço Cloudflare.

### Pendências
- o encerramento vale para a parte do **ALERTA** aprovada, não para toda a Etapa 5B;
- manter credenciais e dados de servidor fora do repositório público;
- eventuais mudanças futuras exigem teste e autorização próprios.

Referência: [Alerta — encerramento aprovado](../../2026-11-16_a_2026-11-22/2026-11-17/ALERTA_ENCERRAMENTO_APROVADO.md).

## iFood — Orders Virtual Bag / Groceries

### Pedido
Concluir a homologação com cenários gravados separadamente, data/hora e identificação do aplicativo de teste, logs do Portal do Desenvolvedor e conferência final no sistema da integradora.

### Ação feita
O Baú contém as evidências do pedido #9933 de 19/11, vídeos, relatórios, logs e registro complementar. A conferência local documenta separação parcial, item indisponível e item completo. O histórico também registra orientação do iFood para reativação de item por API e para envio de evidências quando a ruptura persistisse.

### Teste
- O registro do pedido #9933 indica eventos e ACKs aceitos e sacola final coerente no ambiente de testes.
- O bloqueio de logs HTTP solicitado pela homologação foi documentado no registro complementar.
- A ruptura do Açúcar Refinado União permaneceu visível no Portal do Parceiro; a requisição de reativação foi relatada como aceita para processamento assíncrono, o que não equivale a reativação concluída.
- Não há, nesta consolidação, confirmação oficial de homologação final pelo iFood.

### Publicação
Nenhuma publicação de integração em produção foi realizada por esta organização.

### Pendências
- aguardar ou registrar a resposta oficial ao chamado sobre a ruptura;
- comprovar no Portal do Parceiro que o item voltou a **Ativo**, antes de repetir o cenário que dependa dele;
- concluir e disponibilizar os vídeos exigidos em links separados, com data/hora e identificação do aplicativo;
- encaminhar as evidências à homologação e registrar a decisão oficial;
- não repetir pedido, vídeo ou teste sem necessidade quando a evidência já estiver válida.

Referência: [Evidências iFood — pedido #9933](../../2026-11-19-IFOOD-PEDIDO-9933/README.md).

## Publicação desta consolidação

Esta entrega é exclusivamente documental na branch `bau-de-ouro`. Não declara êxito técnico onde só existe pedido, relato ou material pendente. Não modifica produção e não autoriza implementação futura por si só.
