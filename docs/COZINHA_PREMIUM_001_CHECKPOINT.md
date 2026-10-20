# Cozinha Premium 001 — preparação, sem publicação

Repositório: lealinfopdvnovo/leal-info-pdv  
Branch: feature/setup-seguro-001  
Código validado: 76799e9550229975ba70a2b0396a0b81cf26b452  
Windows CI: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/39495601269  
Resultado: testes de interface e compilação win-x64 self-contained aprovados.

## Implementado nesta preparação

- Catálogo de produtos com fotos à esquerda da central; apresentação escura e botões com gradiente e borda iluminada restritos à cozinha.
- Dez mesas na central. VER MAIS MESAS abre formulário flutuante não modal; quantidade de 10 a 1000, testada com 20, 30, 40 e 80.
- Ocupação operacional derivada dos pedidos ativos: AGUARDANDO = ocupada, PREPARANDO, PRONTO = pronta; sem pedidos ativos = livre. Referências explicitamente no formato Mesa NN; referências livres antigas preservadas.
- Consulta de todos os pedidos ativos para mesas, sem o limite de 500 aplicado à consulta de histórico.
- Clique na mesa abre pedido flutuante com a referência. Rascunho existente não é substituído silenciosamente.
- NOVO PEDIDO preserva sua janela flutuante e fila durável; envio real, reabertura, edição e prevenção de duplicação testados.
- + ADICIONAR PRODUTO abre ProductRegistrationForm existente. Teste Windows salva por sua ação real, com preço/foto, e confirma atualização dos dois catálogos após fechar o cadastro.
- IMPORTAR PRODUTOS DO PDV seleciona IDs existentes para o cardápio sem copiar ou modificar produtos. Preferências locais protegidas em KitchenMenu, separadas por identidade da base/endpoint.
- Importador de catálogo pronto existente preservado.
- Proteção contra criação de novos controles em thread de trabalho e dimensionamento após o ajuste automático de fonte do Windows.

## Evidências e limites

O artefato cozinha-premium-001-preparacao no run acima contém log, fixed.json e screenshots. A base Windows é descartável e protegida pelo guard GITHUB_ACTIONS; não é homologação no computador operacional do cliente.

Os testes aprovados verificam fotos, preços, busca, TLS de terminal, mesas, transições, pedidos gravados, rascunhos, pendências protegidas, seleção do menu e cadastro real. Conferem preservação das tabelas de negócio antes da inclusão intencional do novo produto e preservação de preços/estoques dos produtos anteriores após o cadastro.

Não houve alteração do worker do atualizador, nem execução de nova validação transacional de pacote nesta etapa. Não foi produzido ou publicado pacote de atualização desta preparação.

## Pendências antes de liberar

- Abertura da imagem aprovada e comparação visual: bloqueadas neste Work por ausência de ambiente local; view_image e exec_command retornam No usable execution environment is available. A descrição textual não substitui homologação visual.
- Estado de reserva de mesas, solicitado no escopo inicial, ainda não implementado. A projeção atual não representa reserva ou fechamento financeiro de mesa.
- Quantidade configurada na janela de mesas é mantida durante a sessão, não é uma nova configuração persistente.
- Validar pacote incremental com dependências aprovadas, preservação, segurança e rollback antes de qualquer publicação. Incrementar versão apenas na preparação da futura liberação.

## Publicação bloqueada

O workflow novo_pedido_001.yml agora faz apenas testes/compilação e upload de evidências: sem job publicar, sem UPDATE_REPO_TOKEN e sem escrita em feeds. Não reativar publicação automática sem concluir as validações pendentes.

O feed 001 permanece na 10.421, SourceCommit 41c8d6c7c0df5ce5778431344445599558a07848, SHA-256 cc244e005ef8bf9b86f8ab19ca5bf8641dab2f3275962463ea933e540162f56f. Esta preparação mantém a versão de código em 10.421; não deve ser distribuída como pacote dessa versão.

002, 003, 004 e GLOBAL não foram publicados ou alterados. Entrega futura exclusivamente pelo atualizador interno do 001; sem setup e sem comandos ao usuário.
