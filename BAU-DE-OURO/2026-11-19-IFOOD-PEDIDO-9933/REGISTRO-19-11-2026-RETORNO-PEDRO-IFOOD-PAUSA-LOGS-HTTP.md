# iFood — Registro complementar de 19/11/2026 — bloqueio oficial dos logs HTTP

## Escopo
Registro do processo de homologação Groceries / Orders Virtual Bag discutido hoje. Complementa as evidências já arquivadas na pasta deste pedido. Não modifica GLOBAL, PETs, código ou evidências anteriores.

## Pedido e testes já realizados
- Pedido de teste: #9933; UUID: c93635dd-8343-4634-9932-4dc83a2e0187.
- Cenário 1 — separação parcial: três produtos, três unidades originalmente por produto; resultado final 0/3/1 (açúcar 0, arroz 3, feijão 1).
- Ferramenta local: LEAL INFO — iFood Conferência R8.5.2.
- Resultado local: critérios do cenário 0/3/1 aprovados **localmente**, sem equivaler à aprovação oficial do iFood.
- Evidências produzidas: gravações do pedido e do separador, relatório R8, resumo e JSON de eventos do Developer Portal. Arquivos de vídeo foram arquivados pelo Work em partes; conferir manifestos de integridade antes de afirmar integridade total.
- Nos registros locais: consulta da Virtual Bag HTTP 200, eventos recebidos e ACKs processados. Na aba Eventos do Portal Developer foram vistos DISPATCHED e CONCLUDED; a aba HTTP Requests não apresentou os registros necessários para avaliação.
- O usuário informou anteriormente que a exigência de cupom havia sido dispensada pelo iFood; não recolocar como pendência sem nova orientação expressa.

## Bloqueio encontrado
A pendência central é a visualização/exportação dos logs HTTP oficiais no Portal Developer. HTTP 200 no relatório local não substitui a evidência HTTP oficial solicitada pelo iFood.

## Resposta mais recente do iFood recebida pelo usuário em 19/11/2026
> Boa tarde, Adriano! Espero que esteja bem! Me chamo Pedro, sou do time de Integrações do iFood e vou dar continuidade ao seu processo de homologação. Infelizmente, estamos enfrentando uma instabilidade que está impactando a visualização e a exportação dos logs no Portal Developer. Nosso time de Engenharia já está atuando para normalizar o funcionamento do serviço, com previsão de restabelecimento para a próxima semana. Dessa forma, pedimos que aguarde nossa confirmação de normalização antes de realizar novos testes e gravações, garantindo que todas as requisições sejam devidamente registradas e que os respectivos logs estejam disponíveis para avaliação. Assim que tivermos uma atualização do time responsável, informaremos por meio deste chamado para que possa dar continuidade ao processo de homologação. Agradecemos pela compreensão e ficamos à disposição!

## Decisão operacional em 19/11/2026
- **PAUSAR** novos testes, novos pedidos e novas gravações de homologação, conforme orientação expressa do Pedro/iFood.
- **AGUARDAR** confirmação oficial do iFood de que o Portal Developer voltou a exibir/exportar logs antes de retomar.
- **PRESERVAR** todas as evidências existentes, sem repetir testes ou alterar GLOBAL/PETs.
- O iFood reconheceu instabilidade no serviço de logs e informou previsão para a semana seguinte; **não** confirmou homologação aprovada nem recuperação dos logs antigos.
- Próximo passo após liberação: conferir as instruções mais recentes do próprio chamado e somente então planejar o que efetivamente precisa ser refeito, mediante autorização do proprietário.

## Rastreabilidade
- Chamado mais recente mencionado: #36296851.
- Portal do chamado: https://developer.ifood.com.br/pt-BR/developer/tickets/7f98af53-41cf-4a88-a1c1-2b319186d699
- Pasta deste registro: BAU-DE-OURO/2026-11-19-IFOOD-PEDIDO-9933
- Origem da mensagem do Pedro: texto fornecido diretamente pelo proprietário nesta conversa em 19/11/2026.
