# ANGRA VIDROS QA 005 — PET 1.9 — espaço entre plano e frase

Base 1.8. Branch exclusiva qa/angra-etapa4-setup-20261114. Código deb67712a2af2d8845761ef96a87a311acc41a11. Publicação 8bd7de63b5ff190fd324c04b210071de537c9e52.

Somente o espaço vertical das duas linhas do login foi alterado: altura da linha do plano suficiente para a fonte real, altura da frase e posição vertical da frase com folga mínima de 4 pixels, proporcional ao DPI real. Fontes, cores, alinhamento horizontal, posição do plano, ANGRA VIDROS, fabricante, campos e botões preservados. O vínculo acompanha a animação, o redimensionamento e alterações de fonte/DPI sem interferir nos demais controles.

Nenhuma modificação nos métodos/recursos anteriores, exceto chamadas do novo vínculo em AngraBrand.Apply e ProfileExtension.Initialize. Todos os recursos originais do PET 1.8 são byte a byte idênticos, incluindo vídeo, narração, transição e helpers de identidade/tela principal. Núcleo, licença e bandeirinha não recompilados nem modificados.

## Testes Windows

Actions: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43673080329
Evidências e 18 capturas: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43673080329/artifacts/14145217780

Standard, Plus e Pro exercitados em LoginForm real com PetRuntime, sem personalização manual. Medição das fontes, folga e centralização verificadas com fontes de tamanho normal e crescimento simulado de 125% e 150%. Fonte ampliada pertence somente à fixture; a atualização não muda fontes. DPI do Windows do runner não foi modificado. Foram solicitadas larguras 1366/1920, mas o Windows do runner limita a janela à área rastreável do monitor 1024x768 (capturas até 1044x788). Não afirmar homologação nativa dessas resoluções nem escalas do Windows. As 18 capturas cobrem os três planos e os três tamanhos de texto, com repetição de solicitações de largura.

Verificação de escrita tardia da animação/layout: reposicionar a frase no mesmo topo do plano faz o vínculo recuperar a separação, mantendo geometria/fontes/cores dos demais controles. Os três planos passaram; capturas Standard 150%, Plus 150% e Pro 100% inspecionadas visualmente, sem corte nas duas linhas.

Aplicação do pacote real pelo worker existente aprovada. Interrupção simulada restaurou exatamente PET 1.8 pelo SYSTEM. Canary de dados preservado; hashes de núcleo intactos; objeto de licença preservado nas provas.

SHA-256 do pacote: e61b914da110dec282f124daeeed52b7da1b2db514264224084b09be313cc9de.
MP4 preservado: a2b5144645a2412fc21dc18f2685cc78ba50ae5cadbaaea22e24c88074fc9d89.

GLOBAL channel/global permanece 48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1. Produção updates/main permanece 6f641fc6941b573f94393a437191761243cfa69e. Nenhuma alteração em outros clientes/PETs.

## Uso

PDV TESTE ANGRA: ATUALIZAR → ATUALIZAR PET → 1.9. Sem reinstalação. Confirmar visualmente no computador físico do proprietário, ainda não acessado. Capturas e simulações no runner não substituem essa confirmação.
