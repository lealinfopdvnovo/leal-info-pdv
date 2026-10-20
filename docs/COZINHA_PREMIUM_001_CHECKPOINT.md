# Cozinha Premium 001 — candidata 10.422

Código da candidata: b156dc46d8b7e2d71e533f23449545c65f4e99ab  
Windows e rollback: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/39501475840

A imagem aprovada foi aberta neste Work. Seu SHA-256 é 3dd5b0fc43faf18c40e2b26a06ad7954f7866277d6f801f0e1bbd3af3909e254. A referência foi adaptada a controles nativos WinForms: preto/dourado, catálogo com fotos à esquerda, dez mesas coloridas com desenho em perspectiva, botão vertical VER MAIS MESAS e janela flutuante com filtros e reserva roxa.

## Implementação

- Reservas e capacidade persistentes nas novas kitchen_tables_v2 e kitchen_table_settings_v2, compartilhadas por servidor e terminal TLS.
- Migração aditiva precedida de backup SQLite e quick_check. Tabelas anteriores de pedidos e dados comerciais preservados.
- Controle de revisão impede sobrescrever uma reserva/configuração atualizada em outro terminal.
- Mesa com pedido ativo não pode ser reservada; redução da capacidade não oculta mesa ativa ou reservada.
- Pedido novo em mesa reservada fica bloqueado com pendência preservada, até liberação. Repetição idempotente de pedido já confirmado continua funcionando.
- Pedidos ativos aparecem nos cartões mesmo com mais de 500 registros recentes de histórico.
- Cadastro padrão, seleção do cardápio, imagens e vínculo por ID dos produtos preservados.
- Quantidade aceita: 10 a 1000; dez mesas permanecem na central. A janela foi testada com 20/30/40/80 e com reserva/liberação.
- Nenhuma venda, pagamento ou baixa de estoque é criada ao enviar ou mudar o estado de um pedido de cozinha.

## Evidências e autorização de publicação

Testes de interface, cadastro real, seleção do menu, servidor/terminal, preservação e pacote executam no Windows isolado do GitHub. O teste de histórico cria 501 registros sintéticos após as verificações de preservação. As capturas amplas usam os mesmos controles nativos em renderização fora da tela, com tamanho real do PNG registrado; não representam uma homologação física de resolução/DPI.

A revisão das capturas exatas está em distribution/premium-001-visual-approval.json, vinculada a SourceCommit, BuildRunId, hash da referência e hashes dos dois PNGs.

O workflow só publica após:
1. Compilação e testes Windows.
2. Pacote com somente EXE, DLL e deps.json e dependências iguais à baseline protegida do 001.
3. Política de payload e identidade do canal.
4. Os 19 cenários reais de worker/rollback no Windows, incluindo interrupção, DLL bloqueada, hash inválido, adulteração e falha nativa de inicialização.
5. Comparação do atualizador com a baseline protegida: worker e código de aplicação do update não mudaram.
6. Revisão visual vinculada aos bytes das capturas.
7. Verificação dos feeds preservados, dos bytes publicados e do manifesto remoto.

Nesta revisão o pacote final ainda aguardava concluir o rollback/publicação; consultar o run acima e o feed 001 para o estado final. O worker não foi alterado e o teste de primeira transição legada 10.415 não é autorização para instalações desprotegidas.

Nenhum setup e nenhum comando ao usuário. Publicação exclusivamente pelo atualizador interno 001. 002, 003, 004 e GLOBAL não integram esta liberação. Não houve teste de queda física de energia ou homologação no computador operacional do cliente.
