# iFood — logs e sacola do pedido 9407

Registro da conversa de 13/11/2026. Somente documentação; GLOBAL e PETs não alterados.

## Etapa acompanhada
- Adriano procurou os logs oficiais no portal. Inicialmente a busca estava vazia; ele lembrou que precisava abrir o conferenciador R8. Após executá-lo, as chamadas apareceram.
- O primeiro relatório, R8.1, recebeu 9 eventos de hoje, mas rejeitou a sacola final com mensagem genérica de validação. A causa exata não foi demonstrada. A orientação para procurar a sacola rejeitada na lista foi equivocada.
- Adriano indicou a versão correta, R8.5.2. Nela apareceu a sacola de hoje; os prints confirmaram a seleção do pedido #9407.
- O TXT exportado do R8.5.2 confirma autenticação e acesso à loja, recepção ativa, nenhuma falha fatal, zero ACK pendentes e três sacolas persistidas (incluindo históricas).

## Evidências conferidas
Pedido #9407: dae27b3f-f0c2-4912-af6d-09bf91d01235. Criado 13/11/2026 às 09:58:54 -03:00; último evento CONCLUDED às 10:28:33 -03:00. Modalidade DELIVERY / IMMEDIATE.

Sacola lida às 11:04:10 -03:00: quantidades finais 1, 3 e 0; linha indisponível com quantidade disponível zero. Subtotal dos produtos R$ 25,96 + frete bruto R$ 8,90 + taxa R$ 1,99 = pagamento informado R$ 36,85. O relatório associa a sacola ao evento RFI local e marca sacola persistida True. Isso não demonstra captura em tempo real às 10:04:27: a consulta ocorreu depois.

O JSON oficial exportado contém quatro chamadas: consulta da loja 200, polling 200, acknowledgment 202 e GET virtual-bag do mesmo UUID 200, em torno de 10:52:45 -03:00. Contém metadados, não o corpo da resposta. O TXT posterior registra novas consultas virtual-bag 200 e polling 204 a cada aproximadamente 30 segundos.

## Conversa e decisões
- Os dois vídeos já estavam separados no índice de vídeos do Baú.
- Cupom dispensado conforme informação de Adriano; não reabrir como pendência pelo aviso genérico do programa.
- Os avisos genéricos do TXT de que vídeos/logs permanecem pendentes não verificam anexos externos: os dois vídeos e o JSON foram recebidos nesta conversa.
- Não foi necessário refazer pedido ou vídeo nesta etapa. Não houve correção de código nem publicação.

## Situação e próximo passo
Evidências deste cenário reunidas: dois vídeos, log oficial da API e relatório da sacola R8.5.2. Próximo passo: preparar o encaminhamento no chamado iFood 35288437 para avaliação. Nenhum envio foi realizado nesta etapa; homologação oficial não comprovada. Não declarar todos os cenários concluídos: este pedido é entrega imediata, não retirada ou agendado.

## Arquivos
- [Relatório R8.5.2](../logs/RESULTADO_IFOOD_CONFERENCIA_R8_5_2.txt)
- [Log oficial HTTP](../logs/application-logs.json)
- [Índice dos dois vídeos](../../../VIDEOS-HOMOLOGACAO-IFOOD/2026-11-13/README.md)

Este registro resume a conversa disponível; não é transcrição literal integral.
