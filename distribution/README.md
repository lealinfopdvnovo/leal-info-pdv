# Publicacao independente do PDV

O operador continua usando ATUALIZACOES DO SISTEMA → ATUALIZAR.

O workflow central `Distribuir PDV - canais independentes` publica GLOBAL ou o ID escolhido. Cada canal usa sua propria arvore e dependencias instaladas. GLOBAL usa `channel/global`; os clientes usam `client/001` a `client/004`. Os wrappers das branches chamam o mesmo publicador.

Canais individuais usam o pacote incremental original de tres arquivos. O seletor compilado prioriza o proprio canal e recusa outro ID, feed invalido ou indisponivel; GLOBAL nao substitui personalizacoes.

Versoes antigas ainda podem escolher GLOBAL quando o feed individual falha. Por isso, o pacote GLOBAL nunca inclui `LealInfoPDV.dll`: ele preserva esse arquivo e inicia a base correspondente ao hash instalado, entre os DLLs exatos aprovados de cada cliente. Binario desconhecido ou perfil ausente/corrompido mantem o original. Nao ha leitura ou alteracao de banco/licenca/configuracao para escolher a base. A inicializacao continua em STA, com o aplicativo WinForms original.

A publicacao GLOBAL preserva tambem o historico de hashes e reserva versoes compativeis nos canais individuais usando suas proprias bases. Nao transplanta a arvore GLOBAL sobre um cliente.

Todos os candidatos passam nos testes Windows antes da publicacao. O GLOBAL passa adicionalmente por inicializacao nativa dos cinco perfis, validacao pela politica original do PDV e testes com binario personalizado desconhecido e perfis ausentes/corrompidos. Dependencias devem permanecer compativeis. Somente os bytes testados sao publicados.

O publicador verifica URLs publicas, SHA-256, versao e os manifestos usados pelo PDV. A gravacao dos feeds usa um unico commit; mudancas concorrentes sao recusadas. Uma publicacao individual altera somente seu manifesto. Erro de verificacao restaura os manifestos quando nao houve outra gravacao concorrente.

Os workflows antigos com versoes fixas foram arquivados em `legacy-workflows`; nao concorrem com o publicador central. Nenhuma etapa solicita AnyDesk, comandos no caixa ou reativacao de protecao.
