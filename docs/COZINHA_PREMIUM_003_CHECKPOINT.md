# Cozinha Premium 003 — estado atual

**10.425 publicada exclusivamente no atualizador interno do 003.** Instalação no computador real ainda não confirmada. 001, 002, 004 e GLOBAL não foram alterados.

## Entrega

Referência inspecionada: Dashboard Futurista de Cozinha e Pedidos(2).png. Recomposta a interface com catálogo/fotos/categorias à esquerda, dez mesas 3D por estado à direita, VER MAIS MESAS flutuante, pedido integrado, quantidades/observações e editor NOVO PEDIDO flutuante. Três ações iluminadas: enviar, prévia/impressão de comanda e marcar pronto. Pedidos em andamento horizontais com cronômetros reais. Cadastro existente, importação seletiva do PDV, reservas e configuração de capacidade preservados.

Corrigidos durante a conferência Windows: cortes na primeira linha do pedido, sobreposição dos cronômetros, altura das mesas, quebra em CONFIGURAÇÕES e rolagem horizontal indevida nas categorias. Capturas finais 1366×720 e 1680×940 inspecionadas. A composição segue a referência; fotos, preços e categorias são os do cadastro, sem sobrescrever personalizações. Não se declara identidade pixel a pixel nem aprovação visual no computador do usuário.

## Evidências

- Fonte: 66b473648998bb7f4e2f9595c72cff82157dd9be.
- Windows: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/39527535881 — compilação e 264 verificações, incluindo fechamento/disposição de todas as janelas. Capturas em artefato cozinha-layout-003-windows. Fluxo integrado real: produto, envio, edição/quantidade/observação compartilhada com janela flutuante, transições válidas até pronto; comanda paginada sem envio à impressora física; preços e estoques da base de teste preservados.
- Rollback/publicação: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/39529160429 — pacote exato sobre 10.424: backup com hashes, aplicação confirmada, interrupção de processo durante cópia de DLL, recuperação automática SYSTEM para hashes anteriores e recusa de SHA inválido. Sentinelas de banco, licença, estoque e configurações intactas.
- Worker, política de payload, inicialização protegida e caminho do botão comparados com a implementação já validada; somente CurrentVersion mudou no UpdateManager. Suítes anteriores válidas reaproveitadas.
- SHA-256: 47db067c2bb796b1a7c9515bd939bc5063c40ea9e8607e58c24667a8bda23cc5.
- Commit exclusivo do feed 003: 928b8a16856e02503d84775740ae21bd2e810695. Download público e manifesto consultado pelo botão conferidos; demais quatro feeds inalterados; release latest=false.
- Não foram feitos testes de impressora física nem de monitores físicos em todos os DPIs. As escalas ampliadas são simulações de fonte no Windows.

## Atualização no computador real

No PDV 003, usar o botão de atualização e aceitar 10.425. Sem setup, ZIP manual ou comandos. Solicitar print com a versão após atualizar; não declarar instalação antes da confirmação.

## Histórico preservado

10.424: fonte a6462ce82b8d55e0d2a0f9646238423038f7a5d8; Windows 39521451223; rollback/publicação 39521805378. O usuário havia confirmado 10.422 protegida; 10.415 exigia a ativação externa já validada (PROTECAO_003_ATIVACAO_LOCAL.md e PROTECAO_003_ARMED_R2.md).
