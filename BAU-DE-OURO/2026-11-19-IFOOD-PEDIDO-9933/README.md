# iFood — Evidências 19/11/2026 — Pedido #9933

**Data:** 19/11/2026. **Ambiente:** testes. **Status:** conferência local aprovada; homologação iFood não confirmada.

Pedido UUID: `c93635dd-8343-4634-9932-4dc83a2e0187`
Loja merchantId: `84a8976b-0559-48c6-84ce-fc2502f1364a`
Aplicativo client_id: `756879ff-e999-4782-ae2d-a782619c7e26`

## Resultado
- R8.5.2: eventos PLACED, CONFIRMED, SEPARATION_STARTED, PATCH_REQUESTED, ORDER_PATCHED, SEPARATION_ENDED e READY_FOR_INVOICE; ACKs aceitos, nenhum pendente.
- GET virtual-bag: HTTP 200 em 19/11 às 10:16:21 BRT.
- Sacola final: item cancelado/indisponível 0, item integral 3, item reduzido 1. Subtotal R$ 25,96. Conferência 0/3/1 APROVADA LOCAL.
- Developer Portal > Logs da API > Eventos: eventos oficiais DISPATCHED e CONCLUDED, deliveredVia POLLING, deliveryStatus DELIVERED, error null. A aba HTTP Requests não mostrou registros de 19/11 nas buscas realizadas. Não confundir logs de eventos com logs HTTP.
- Evidência de cupom e requisitos oficiais de homologação ainda precisam de confirmação; nenhuma aprovação externa declarada.

## Arquivos recebidos na conversa (originais ainda NÃO anexados a esta pasta GitHub)
- `pedido ifood.mp4` — vídeo da criação do pedido.
- `separador ifood.mp4` — vídeo da separação.
- `RESULTADO_IFOOD_CONFERENCIA_R8(20261119-132420).txt` — relatório R8.5.2.
- `RESUMO_IFOOD_c93635dd.txt` — resumo do pedido.
- `application-logs (1)(1).json` — exportação de eventos do Developer Portal.
- Prints do R8.5.2 e Developer Portal compartilhados na conversa de 19/11/2026.

**Importante:** esta pasta contém o índice técnico. Os binários originais e exportações não foram transferidos ao GitHub por esta operação. Preservar os arquivos originais para envio posterior. Nenhum código GLOBAL/PET alterado.
