# ANGRA VIDROS QA — PET 1.5 — sincronização da abertura

Código: 5172af09fd749493bc460267495f93d68d682816.
Publicação QA: 4e4b09986da8f1d4d6cbbf0df3e6c9bfef8b360d.
Actions SUCCESS: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43663665989
Cliente exclusivo 005; branch qa/angra-etapa4-setup-20261114.

## Correção pontual
A gravação feminina pt-BR aprovada foi reutilizada, sem nova síntese. A música e a animação originais foram preservadas, com retenção do último quadro para concluir a fala antes do fade.
- Reprodução explícita uma única vez, após o player estar pronto; retirada a combinação autoplay/seek/play.
- 1,75 s de música antes da voz para preparar a saída de áudio.
- Arquivo de voz original completo: 5,664 s, SHA-256 95bdd2fd3a4d1752e840ef7011a42c43577a18bf18f90b0cfc564b5e7f897bdf.
- Final conservador da trilha de voz em 7,414 s; fade somente em 8,35 s, com margem de 0,936 s.
- Vídeo termina em 9 s; aguarda mais 350 ms para escoar o áudio antes de abrir o login verdadeiro.
- Watchdog de carregamento ampliado para 45 s e interrompido assim que o player confirma reprodução. A fala ativa deixa de ser cortada por esse temporizador.

Texto mantido: “Bem-vindo à Angra Vidros. Na próxima tela, coloque seu usuário e sua senha.”

## Preservação verificável
Não houve compilação ou alteração do núcleo, licença, bandeirinha ou worker. Foi reutilizado o artefato aprovado do QA e o PET 1.4 exato, SHA-256 ab9321010fb04c1a7ef18d331a5e2dbd30b9c1f5b0f547cb8fa2490953db8bcc.

A edição com Mono.Cecil ficou restrita a três métodos da abertura (constantes/player/watchdog), recurso MP4, constante de hash e metadados da próxima versão PET. Todos os demais corpos de métodos e recursos foram comparados antes/depois e permaneceram idênticos. A identidade continua 005, API 1, mesma faixa de compatibilidade 10.468 até antes de 10.469. Não houve mudança no licenciamento nem nova dependência distribuída.

## Testes novos aprovados
- Planejador e staging já existentes aceitam a oferta 1.5 para a instalação 1.4 / 005.
- Pacote real aplicado pelo worker existente no Windows isolado.
- Interrupção após a cópia da DLL: tarefa SYSTEM restaurou o PET 1.4 anterior por hash. Aplicação posterior de 1.5 aprovada.
- Reprodução offline completa: Played=true, Ended=true, LastSeconds=9, Error=null.
- 42 amostras durante reprodução; watchdog observado parado e reprodução ultrapassou o fim da trilha de voz. Login real somente após conclusão e drenagem do áudio.
- Autenticação da fixture fornecida pela API de verificação de licença assinada já existente; nenhuma chave do EXE foi trocada.
- Núcleo e dados sintéticos preservados por hash. As baterias anteriores de funcionalidades e bandeirinha não foram repetidas.
- Oferta pública 005/1.5 e hash do pacote baixado conferidos.

O ensaio total de abertura levou 13,573 s incluindo inicialização do WebView2; a mídia dura 9 s. O som físico e o comportamento no computador do proprietário continuam sujeitos à confirmação dele. Não se afirma execução remota nesse computador ou uso da sua licença real no runner.

## Entrega
Disponível exclusivamente no mesmo canal QA pelo botão ATUALIZAR PET. Sem outro instalador.
Pacote ANGRA_005_PET_1_5.zip: c2ee306da15e99699f49b8f2b67dffb9e5548d8865bf0003326ff2e0d50ce765.
Vídeo: a2b5144645a2412fc21dc18f2685cc78ba50ae5cadbaaea22e24c88074fc9d89.
Evidências 14140324994: ZIP baixado e hash 0350ed1521a821577116df466e1ac08a9c6b11d85a3de048f546753b2bcf6633 conferido.
Artefato de pacote: 14141286062.

GLOBAL de produção reconferido: 48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1.
Distribuição de produção reconferida: 6f641fc6941b573f94393a437191761243cfa69e.
Demais PETs, dados e canais de produção não foram alterados.
