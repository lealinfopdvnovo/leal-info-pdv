# Proteção 003 R2 — retomada de Armed legado

Fonte validada: 6e1a9deadef88e61a60c3dbb5cd8ba0abc06042a.
Execução Windows e entrega: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/39516687225 (success).

O ativador anterior recusava o journal legado Armed / Protection003-NoUpdate de 10.415 como recuperação pendente. A R2 reconhece somente essa estrutura com identidade 003, caminho e hashes da versão original verificados. Preserva o journal original e um backup íntegro antes de desativar as tarefas SYSTEM vinculadas ao worker exato dessa instalação e preparar a transação protegida. Estados diferentes continuam bloqueados.

Onze novos cenários Windows aprovados: retomada válida, tarefa antiga alternativa, sete recusas de identidade/estado/integridade, duas interrupções reais e retomada. Incluem recuperação automática SYSTEM e preservação dos dados de teste, licença e configurações. Interface do executável portátil também verificada. Evidências anteriores aprovadas foram reaproveitadas, sem repetir a compilação do PDV ou seus testes anteriores.

Download público sem autenticação verificado por SHA-256: https://github.com/lealinfopdvnovo/leal-info-pdv/releases/download/protecao-003-r2-armed-10.415/LEAL_INFO_PROTECAO_003_R2.zip
ZIP SHA-256: e8e34ac7358b2e2bd292375f57aa9ba6ca7a684b11451f893c5470ae123623ba

Nenhum feed foi alterado: 003 permanece em 10.415; 001, 002, 004 e GLOBAL preservados. Esta entrega é exclusivamente o ativador de proteção 003, sem setup. A instalação no computador do cliente ainda depende de execução local e confirmação.

Procedimento local: fechar o ativador anterior e o PDV; baixar R2; usar Extrair Tudo; abrir LealInfoProtecao003.exe dentro da pasta extraída; executar a verificação/preparação e concluir somente quando a preparação estiver validada. Não excluir .pdv-update ou backups.
