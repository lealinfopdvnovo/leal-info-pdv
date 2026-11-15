# ANGRA VIDROS QA 005 — PET 1.12 — leitura em três ciclos de cinco segundos

Base aprovada 1.11. Branch exclusiva qa/angra-etapa4-setup-20261114. Código testado f1e74cbabf8897092efba202de438f83be7419be (produto a2c3711df69b8538a474a11381c1c229a49ead87; último commit melhora apenas a medição do teste).

## Somente duração

Três piscadas completas de 5 s, total nominal 15 s, um grupo por login. O tempo adicional foi colocado no intervalo do balão já totalmente aceso, preservando os tempos de entrada e apagamento da versão anterior. Em cada ciclo, a mensagem fica totalmente visível aproximadamente de 1,217 s a 4,583 s: 3,367 s contínuos para leitura. A pulsação permanece ativa durante essa permanência.

O renderizador de círculo, seta, balão, textos, cores, fontes, geometria, posicionamento e interação é idêntico. Somente a constante de duração, o mapeamento da linha do tempo e a versão da evidência foram alterados. Transformação reversível sobre a fonte exata da 1.11, SHA 3aa81ef950bd1d4b57be02209ca817044b793e1335d944117c2a6aceb1db5858.

Todos os métodos do PET original são idênticos. Somente o recurso ANGRA.HelpPremium.Helper e metadados de versão foram atualizados; todos os demais recursos permanecem byte a byte iguais. Etapa 4, login, vídeo, narração, transição, licença, bandeirinha e funcionalidades anteriores preservados. Nenhuma recompilação do núcleo.

## Testes Windows

Actions: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43677708973
Capturas/evidências: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43677708973/artifacts/14147555239

Três sessões de MainForm real em desktop 1920x1080, menus com fontes 100/125/150%. Isso é ampliação da fonte da fixture, não alteração da escala/DPI do Windows. Cada sessão executa um único grupo de três piscadas, inclusive após Customize repetido. Nova sessão recebe novo grupo.

- Nove comparações do balão em capturas separadas por 3,068–3,117 s. Entre 99% e 100% dos pixels interiores permaneceram idênticos; texto integralmente aceso e legível no início e no fim dos intervalos.
- Três fases ligadas e três apagadas por sessão. Pixels vermelhos acesos: 595–731; apagados: zero nas nove amostras.
- Configuração nominal 3 × 5 = 15 s. Encerramento observado: 15,137 / 15,129 / 15,115 s, com gravação das capturas PNG na thread de UI. O limite de relógio é 15 s; latência de processamento do runner não altera a programação dos ciclos. Campos internos CircleCompletedAt/ArrowStartedAt/BubbleStartedAt/FadeStartedAt continuam na escala virtual 0–3 do renderizador original, não representam tempos globais da sequência.
- 538 / 551 / 535 quadros, sem erro do helper. Clique real no AJUDA, foco/teclado, reposicionamento e propriedades dos controles preservados.
- Aplicação pelo atualizador existente aprovada, sem reinstalação. Recuperação SYSTEM após interrupção restaurou exatamente PET 1.11. Canary, licença e hashes de núcleo preservados.

SHA-256 do pacote 1.12: 45c03f52243da61dc68962798085deec9152935c9845698b20d0890687a6d9c7.
MP4 preservado: a2b5144645a2412fc21dc18f2685cc78ba50ae5cadbaaea22e24c88074fc9d89.
GLOBAL channel/global permanece 48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1. Produção updates/main permanece 6f641fc6941b573f94393a437191761243cfa69e. Nenhum outro cliente/PET alterado.

## Uso

No PDV TESTE ANGRA: ATUALIZAR → ATUALIZAR PET → 1.12. Sem reinstalação. Após novo login, aguardar a tela principal para ver o grupo de três ciclos de 5 s. O PC físico do proprietário não foi acessado; confirmar visualmente após atualizar.
