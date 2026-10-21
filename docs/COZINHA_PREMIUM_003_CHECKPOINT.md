# Cozinha Premium 003 — estado atual

A versão **10.423 está publicada exclusivamente no feed 003**, para atualização pelo botão do PDV. 001, 002, 004 e GLOBAL permanecem intactos.

O usuário confirmou a instalação local 10.422 antes da liberação. A instalação real da 10.423 ainda não foi confirmada.

Correção: impedir que o shell/tema geral repinte a Cozinha; medir textos desenhados em seus limites reais; ajustar cabeçalho, números/estados das mesas, botões e altura dos cards conforme a escala; manter o enquadramento das janelas flutuantes. Cadastro, pedidos, banco, estoque, licença e configurações não foram alterados.

Fonte do pacote: 8d3ec93138ee8962700369da51bdfaf96c112b83.
SHA-256: 53076441f304f102fbde9091ed794cca46f7fd5516f6452c25b0aae23f9e06f4.
Commit do feed: 7868303fc2e183dec7b107497bdaa98e891822f5.

## Evidências

- Windows: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/39519486382 — compilação e 110 verificações aprovadas. Shell de produção instalado no teste; NOVO PEDIDO e mesas flutuantes preservam seu tema. Capturas completas inspecionadas em três tamanhos e escalas por software 100/125/150%. Isso não equivale a teste de monitores físicos nesses DPIs.
- Rollback e publicação: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/39519903363 — pacote exato 10.423 aplicado sobre os três executáveis imutáveis 10.422; backup íntegro, interrupção real do processo e recuperação SYSTEM para os hashes exatos anteriores, rejeição de SHA inválido e preservação de sentinelas de dados/licença/configuração.
- Worker, inicialização protegida, política de payload e caminho do botão comparados à fonte 10.422: idênticos, exceto o número de versão no UpdateManager. As suítes anteriores aprovadas foram reaproveitadas.
- Download público com SHA-256 e manifesto servido na URL do atualizador verificados; somente clients/003/version.json mudou. Publicação latest=false.

## Uso

No computador do 003 já na 10.422, abrir o botão de atualização da tela principal e aceitar a 10.423. Sem setup, ZIP manual ou comandos. Solicitar print após atualizar para confirmar instalação e aparência no computador real.

## Histórico

A 10.415 tinha falha de cópia interrompida. A primeira transição exigiu proteção externa validada; seus registros anteriores estão em PROTECAO_003_ATIVACAO_LOCAL.md e PROTECAO_003_ARMED_R2.md. A presente liberação parte da 10.422 informada pelo usuário; não declara homologação do atualizador antigo.
