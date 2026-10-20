# Cozinha Premium 003 — candidata 10.422, não publicada

Baseline própria do 003: 137612d259a0fcecdbc2c2165893ba73988eb847 (10.415).
Implementação da cozinha reaproveitada do commit b156dc46d8b7e2d71e533f23449545c65f4e99ab.
Somente o método OpenKitchen foi integrado ao MainForm original do 003; funções da Vidraçaria e demais personalizações permanecem na baseline do 003. Não há alteração de canais 001, 002, 004 ou GLOBAL.

O workflow desta branch não possui job de publicação nem token de escrita no repositório de atualizações. Ele compila a candidata exclusiva, testa interface/cadastro/mesas/reservas e o worker transacional e verifica a primeira transição com o bloco exato de cópia do atualizador instalado 10.415.

A primeira transição não é autorizada por testes somente do novo worker. Se a cópia antiga deixar versões mistas, a liberação deve permanecer bloqueada. Um pacote do PDV não consegue tornar transacional o código antigo que o instala.

Sem setup, sem comandos ao usuário, sem acesso ao perfil real, sem declaração de instalação. Consultar o run da branch para resultados efetivos.
