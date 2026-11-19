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
- Conforme correção expressa do proprietário em 19/11/2026, cupom não é mais exigido. Não registrar cupom como pendência. Nenhuma aprovação externa declarada.

## Arquivos novos arquivados em 19/11/2026

Cinco originais recebidos e preservados, identificados em `INTEGRIDADE.json`, com tamanhos e SHA-256.

- `pedido ifood(1).mp4`: vídeo da criação do pedido.
- `separador ifood(1).mp4`: vídeo da separação, armazenado em duas partes binárias sem recompressão por ultrapassar o limite de 100 MiB por arquivo do GitHub.
- `application-logs (1)(2).json`: exportação com dois eventos, DISPATCHED e CONCLUDED, do pedido #9933; não equivale à exportação HTTP Requests.
- `RESULTADO_IFOOD_CONFERENCIA_R8(20261119-140710).txt`: relatório da comunicação API R8.5.2, preservado integralmente.
- `RESUMO_IFOOD_c93635dd(1).txt`: resumo original preservado; suas pendências automáticas são anteriores à conferência dos vídeos. Cupom NÃO é pendência vigente.

### Restaurar o vídeo do separador

Baixar as duas partes para a mesma pasta e concatenar em ordem, sem conversão:

Windows (Prompt de Comando):
```bat
copy /b "separador ifood(1).mp4.part01"+"separador ifood(1).mp4.part02" "separador ifood(1).mp4"
```

Linux/macOS:
```sh
cat 'separador ifood(1).mp4.part01' 'separador ifood(1).mp4.part02' > 'separador ifood(1).mp4'
```

A reconstrução foi verificada byte a byte antes do commit. O SHA-256 do vídeo completo consta no inventário.

### Conferência

Vídeos mostram pedido em modo de teste e fluxo de separação; API confirma 0/3/1 e ACKs aceitos. O JSON exportado contém dois eventos oficiais; não declarar que contém registros HTTP. Não houve envio ao iFood, nova execução de testes, alteração de código, GLOBAL ou PET.
