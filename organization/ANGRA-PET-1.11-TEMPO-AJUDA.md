# ANGRA VIDROS QA 005 — PET 1.11 — três piscadas de 2,5 segundos

Base aprovada 1.10. Branch exclusiva qa/angra-etapa4-setup-20261114. Código 7887a42942ea21236957fea6de73decc0da892f7.

## Mudança exclusivamente de tempo/repetição

A mesma sequência visual homologada é reproduzida três vezes. Cada ciclo percorre a sequência de desenho/acendimento/apagamento em 2,5 segundos reais, total programado de 7,5 segundos. Círculo vermelho desenhado, seta azul-neon, pulsação, balão, texto, cores, fontes e geometria permanecem no renderizador original.

prepare.py aplica transformação reversível de quatro trechos sobre o Coach.cs aprovado (SHA 0388f399703c15e481b642244101392303947f0df0cfe1677b898c8a3866bccb): constantes de tempo/repetição; metadados da evidência; limite do relógio para 7,5 s; mapeamento do relógio em três ciclos de 2,5 s para a linha do tempo original de desenho 0–3 s. Reverter esses trechos produz exatamente a fonte anterior. Nenhum comando de desenho, cor, fonte, posicionamento, entrada ou sessão foi modificado.

No PET, todos os métodos anteriores são idênticos. Somente o recurso ANGRA.HelpPremium.Helper e metadados da versão foram atualizados. Todos os demais recursos (incluindo Etapa 4, login, vídeo, voz, player e transição) permanecem byte a byte idênticos. Núcleo, licença e bandeirinha preservados e não recompilados. Nenhuma alteração no GLOBAL ou em clientes/PETs de produção.

## Testes Windows aprovados

Actions: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43676791986
Capturas/evidências: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43676791986/artifacts/14146643757

Três sessões reais de MainForm em desktop descartável 1920x1080, com menus em fontes 100%, 125% e 150%. Isso amplia a fonte da fixture; não altera a escala/DPI do Windows. Cada sessão executa um único grupo de três piscadas, inclusive após Customize repetido. Nova sessão recebe o grupo novamente.

- Capturas ligadas/desligadas de cada uma das três piscadas nas três sessões (18 imagens).
- Pixels vermelhos ligados: 595–730 por amostra; apagados: zero nas nove amostras de apagamento. Isso confirma alternância visual real, não somente parâmetros numéricos.
- Duração nominal configurada/verificada: 3 × 2,5 = 7,5 s. Encerramento medido no runner: 7,629 / 7,628 / 7,644 s; a captura/gravação PNG na thread de UI acrescenta atraso de processamento ao fechamento. O limite do relógio é 7,5 s, e o círculo já está apagado na última captura. Campos CircleCompletedAt/ArrowStartedAt/BubbleStartedAt/FadeStartedAt da evidência conservam a escala virtual 0–3 do renderizador anterior; não são tempos globais da nova sequência.
- 246 / 252 / 250 quadros, sem erro do helper.
- Clique real chegou ao AJUDA durante a animação, teclado/foco preservados e camada nativa click-through/nonactivating mantida.
- Posição acompanha menu real após mover/redimensionar; controles mantêm geometria, fonte, visibilidade e estado habilitado.
- Nenhuma repetição adicional na mesma sessão; crescimento de objetos GDI igual a zero nas três sessões.
- Oferta/staging/contrato existentes aprovados. Pacote aplicado e interrupção recuperada pelo SYSTEM com restauração exata do PET 1.10. Canary e hashes de núcleo intactos.

Pacote SHA-256: 5262acdcaabb1ac80b037e1b1e12f5fd8b1d2252657f6cf952c3cec62e59e947.
MP4 intacto: a2b5144645a2412fc21dc18f2685cc78ba50ae5cadbaaea22e24c88074fc9d89.
GLOBAL channel/global: 48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1. Produção updates/main: 6f641fc6941b573f94393a437191761243cfa69e.

## No PDV de teste

ATUALIZAR → ATUALIZAR PET → 1.11, sem reinstalação. Após entrar, aguardar a tela principal carregada para o grupo de três piscadas. O computador físico do proprietário não foi acessado; confirmar nele após atualizar.
