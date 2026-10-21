# Cozinha Premium 003 — estado atual

**10.424 publicada exclusivamente no atualizador interno do 003.** A instalação dessa versão no computador real aguarda confirmação. 001, 002, 004 e GLOBAL permanecem intactos.

Pedido atual: centralizar os textos com erros mostrados nos prints image(20261021-001629).png e image(20261021-001650).png.

Correções: cabeçalho centralizado, com limpeza e redesenho completo após redimensionamento para eliminar letras duplicadas; categorias sem quebra de palavras; nomes e preços alinhados, com moeda e valor completos; NOVO PEDIDO centralizado; campos e rodapé ajustados à altura medida da fonte, com lista de itens preservada. As áreas automáticas podem crescer e encolher sem ocupar a lista. Fotos e créditos originais do catálogo pronto foram reaproveitados do pacote imutável aprovado, sem importação automática ou alteração de produtos.

Fonte: a6462ce82b8d55e0d2a0f9646238423038f7a5d8.
Pacote SHA-256: 120e85f3c03d9266b3867e8a6d6b8185dbbf320cbe266589b54816894251bbaa.
Commit do feed: a1b011e3f7024b8899be48edf18099b338d12f13.

## Evidências

- Windows https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/39521451223 — compilação e 184 verificações aprovadas. Capturas completas do pedido inspecionadas em 1366 e 1909 pixels, com fontes ampliadas por software em 100/125/150%; título, ações e lista visíveis. Comparação de pixels capturados do desktop Windows antes/depois de redimensionar a janela: cabeçalho sem letras residuais. Não equivale a teste de monitores físicos em todos os DPIs.
- Rollback/publicação https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/39521805378 — pacote exato aplicado sobre a 10.423 imutável; backup verificado, interrupção real e recuperação SYSTEM para hashes anteriores, recusa de SHA inválido e preservação de sentinelas de banco/licença/estoque/configuração.
- Caminho do atualizador, política de payload, worker e inicialização protegida permanecem idênticos aos já aprovados, salvo o número da versão no UpdateManager. Suítes anteriores aprovadas reaproveitadas.
- Download público, SHA-256 e manifesto na URL consultada pelo botão verificados. Somente clients/003/version.json mudou; publicação latest=false.

## Próximo passo no computador real

Na tela principal do PDV 003, usar o botão de atualização e aceitar 10.424. Sem setup, ZIP manual ou comandos. Pedir print após atualizar para conferir a centralização na tela real.

## Histórico

A 10.423 anterior teve compilação/layout aprovados em 39519486382 e rollback/publicação em 39519903363. A 10.415 exigia proteção externa na primeira transição; ver PROTECAO_003_ATIVACAO_LOCAL.md e PROTECAO_003_ARMED_R2.md. O usuário confirmou 10.422 protegida durante a etapa anterior; os novos prints não exibem o número da versão.
