# Cozinha Premium 003 — candidata 10.422, PUBLICAÇÃO BLOQUEADA

## Estado confirmado em 20/10/2026
O canal 003 permanece na 10.415. Não foi criada release pública 10.422 do 003 e nenhum feed foi escrito neste trabalho.
A publicação anterior do 001 era exclusiva: workflow e autorização estavam fixados em CHANNEL=001.
O 003 ainda tinha o placeholder “Em desenvolvimento para próxima atualização” no seu MainForm.

## Código e pacote preparados
- Branch: feature/cozinha-premium-003; baseline própria 137612d259a0fcecdbc2c2165893ba73988eb847.
- Código do pacote compilado: be3d36780eec0bf90aef7666ac5df158115a8e99.
- Cozinha reaproveitada integralmente de b156dc46d8b7e2d71e533f23449545c65f4e99ab.
- MainForm recebeu somente a integração OpenKitchen; Vidraçaria e demais funções permanecem na base própria do 003.
- Inicialização e atualizador transacional foram integrados para atualizações FUTURAS, sem alegar proteção da primeira transição.
- Pacote exclusivo, três arquivos, dependências compatíveis com baseline 003.
- SHA-256: 546c776f7b9f48a3462f30ba257c8120c1f7bc144d957698565efe98f246e646.

## Evidências reais
Windows UI/compilação: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/39504222643
Interface passou: NOVO PEDIDO flutuante, catálogo/fotos, cadastro existente e atualização automática, seleção do cardápio, 10 mesas, 20/30/40/80 flutuantes, reservas e servidor/terminal TLS, preservação de tabelas comerciais. Compilação, pacote e isolamento de canal passaram.
O teste de payload desse primeiro run não compilou porque havia sido reaproveitado um harness de testes de restauração do 001 com BlockForRestore, ausente na base 003. Foi restaurado o harness da própria baseline 003, sem alterar o produto.
Continuação sem repetir UI ou reconstruir o pacote: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/39504754135
Mesmo pacote e hashes das evidências UI foram conferidos; política de payload passou. Os 19 cenários do novo worker transacional passaram.

## Bloqueio obrigatório
O bloco EXATO do atualizador INSTALADO 10.415 foi executado no Windows isolado com arquivo bloqueado de verdade.
Resultado: deps.json substituído, EXE e DLL ainda anteriores, saída 1, sem rollback automático.
rollback-validation.json registra SafeForPublication=false; candidate.json registra PublicationAuthorized=false, RollbackPassed=false, PreparedOnly=true.
Artefato de evidências: premium-003-validation-NOT-AUTHORIZED, ID 12303270366 (no segundo run).
Instalar um worker novo dentro do pacote não protege a cópia antiga que precisa instalar esse mesmo pacote.
É necessário um mecanismo de proteção ativo ANTES da primeira substituição da 10.415; este Work não tem acesso ao Windows operacional do 003 para ativá-lo. Não contornar o teste, não trocar o feed diretamente, não entregar setup/comandos como atualização.

## Preservação e limitações
Todos os cinco SHAs dos feeds foram consultados pela API GitHub e comparados ao release-plan.json original: idênticos. Relatório persistido em distribution/premium-003-validation.json.
A etapa de conferência de feeds do segundo CI falhou por caminho de importação Python; a conferência independente posterior passou. O caminho foi corrigido para execução futura. Nenhum teste de cozinha ou rollback precisa ser repetido só por essa correção.
O workflow de continuação passa a disparar somente por alteração explícita de qa/003-release-trigger.txt, evitando repetir os testes por manutenção de evidências.
Não houve publicação, setup, comandos ao usuário, acesso a banco/licença/configurações reais, instalação no PC do cliente ou aprovação visual física/DPI.
