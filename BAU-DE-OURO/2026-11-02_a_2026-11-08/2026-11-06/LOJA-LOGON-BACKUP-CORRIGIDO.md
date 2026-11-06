# Loja — LOGON e fechamento durante backup — 06/11/2026

Adriano relatou: primeiro login funciona; primeiro clique LOGON parece não responder; segundo clique volta ao login com InvalidOperationException em MainForm_FormClosing/BeginInvoke. Confirmou explicitamente cliente Loja e pediu correção. GLOBAL e demais clientes não autorizados.

## Causa e correção
A guarda antiga retornava quando automaticBackupRunning era true sem cancelar a nova tentativa de fechamento. O segundo clique destruía a janela; o finally do primeiro backup chamava BeginInvoke(Close) no handle destruído.
Correção aplicada por overlay estrito do único método MainForm_FormClosing sobre a receita publicada da Loja: cancelar todos os fechamentos durante o mesmo backup; mostrar espera e desabilitar temporariamente a janela; sair do primeiro evento com Task.Yield; fechar na continuação da thread UI somente com handle válido e janela viva. Backup, timeout e log de erro preservados. Nenhuma alteração em banco/licença/Firebase/APK ou funções de caixa.

## Provas
- Branch: fix/loja-logon-backup-20261106.
- Commit de fonte/controlador: 69398d3e5f018749df19f2e2e4be3a2e9d9a13bc.
- Receita histórica da Loja recuperada da publicação 10.464; não usar client/001 antigo como base direta.
- Windows/build/publicação aprovados: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/42281822141
- Dez verificações de ciclo de fechamento com método real extraído da fonte reconstruída, loop Windows Forms real e I/O de backup simulado: primeiro clique, repetição de fechamento, conclusão automática, backup síncrono, falha registrada, descarte antes da conclusão e três sessões novas; nenhuma exceção UI.
- Probe da interface antes/depois aprovado; regressão de caixa com persistência/reabertura aprovada.
- Três cenários do aplicador original: sucesso, interrupção e SHA inválido aprovados; recursos embutidos PET, notificador e worker preservados byte a byte; contrato de dependências preservado.
- Estes testes não equivalem a login real licenciado no computador do Adriano.

## Publicação comprovada
- Exclusivamente clients/001/version.json: 10.464 → 10.465; PetVersion 1.2 preservado (canal legado com executável específico; não afirmar migração PET independente concluída).
- Feed commit: 52165590baf9e191c3f80ff987ea7e2524e2f07b.
- Pacote SHA256: 728b20ff2e38d96deaf538ad45e6297bced845a6460c9a827a17ade3960d5fd6.
- Release: https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/tag/client-001-v10.465-logon-backup
- Nome histórico do ZIP mantém V10_463; metadados/versão compilada e feed são 10.465. Usuário instala pelo ATUALIZAR; não enviar ZIP/setup.
- Download e hash verificados antes do feed; comparação de toda a árvore comprovou alteração de apenas clients/001/version.json. Feed GLOBAL, Casa e demais canais intocados.
- Usuário ainda precisa instalar pelo ATUALIZAR da Loja e confirmar LOGON no primeiro clique. Não declarar instalação real concluída.

A organização/migração GLOBAL/PET anterior continua com suas pendências; esta entrega não retoma aquela missão.
