# ANGRA VIDROS QA 005 — PET 1.10 — AJUDA Premium

Base homologada 1.9. Branch exclusiva qa/angra-etapa4-setup-20261114. Código dc1ae9bbce86f5b697cc2483ed56341a4ceb2fc8.

## Entrega

Animação nativa vetorial Windows Forms de 3 segundos: traço vermelho progressivo ao redor do item AJUDA entre 0 e 0,8 s; seta azul-neon com brilho/pulsação a partir de 0,8 s; balão compacto com PRECISA DE AJUDA? / Conheça nossos tutoriais! a partir de 1,3 s; fade de todos os elementos entre 2,5 e 3 s.

Camada com alfa por pixel, WS_EX_TRANSPARENT, WS_EX_NOACTIVATE e WS_EX_TOOLWINDOW, sem foco nem captura de mouse. Não adiciona ou move controles da tela principal. Posicionamento calculado a cada quadro a partir do ToolStripMenuItem AJUDA real e dos limites do monitor. O balão fica abaixo das barras superiores. A execução espera o item estar totalmente visível e a tela principal ativa/estável; não aponta para item fora da janela ou recolhido no overflow.

Uma execução por instância de MainForm, correspondente ao ciclo de login do Program existente (novo MainForm a cada sessão). Redimensionar, mover, reativar ou chamar Customize novamente não reinicia. Nova sessão recebe nova execução. Ao fechar/minimizar a tela ou perder o alvo visível, a indicação é encerrada sem interferir na operação.

Nenhum tutorial em vídeo foi implementado. Nenhum recurso da Etapa 4 foi alterado; todos os recursos e métodos originais do PET 1.9 são idênticos, exceto chamadas do novo vínculo em AngraBrand.Apply e ProfileExtension.Initialize. Vídeo, voz, player, transição, login, espaçamento homologado, identidade e apresentação principal preservados byte a byte nos recursos anteriores. Núcleo, licença e bandeirinha não recompilados.

## Testes Windows aprovados

Actions: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43675666170
Capturas das quatro fases e JSONs: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43675666170/artifacts/14146136804

Desktop descartável 1920x1080; janela real movida e redimensionada durante a animação. Três sessões com menu nativo em fontes 100%, 125% e 150%. Ampliação de fonte é fixture, não alteração da configuração de escala/DPI do Windows; não afirmar homologação física de todos os monitores/DPI. O helper usa DeviceDpi e limites reais do menu em produção.

- Três sessões executaram uma vez cada, mesmo com Customize repetido antes e depois da animação.
- Duração medida: 3,003854 s / 3,002511 s / 3,005996 s.
- Fases medidas: círculo/seta 0,802–0,817 s; balão 1,305–1,319 s; fade 2,504–2,530 s.
- 91 / 92 / 93 quadros nativos, sem erros do helper.
- Posição do desenho conferida contra RectangleToScreen do AJUDA após movimento, redimensionamento e ampliação do menu.
- Clique de mouse real chegou ao AJUDA e abriu seu dropdown enquanto a camada estava visível.
- Foco permaneceu na janela principal; botões e controles mantiveram limites, fonte, visibilidade e estado habilitado.
- Camada eliminada ao fim e crescimento de objetos GDI igual a zero nas três sessões.
- Oferta/staging/contrato do atualizador existente aprovados. Aplicação real e recuperação SYSTEM após interrupção restauraram exatamente o PET 1.9. Canary e hashes do núcleo preservados.
- Conferência pública Linux passou sem recompilação: oferta 1.10 exclusiva 005 e SHA do pacote.

SHA-256 do pacote 1.10: 1793692691bb598f0ffa81fc6f26d30cc74f472843fc4ca17bc932d9a9a48932.
MP4 anterior intacto: a2b5144645a2412fc21dc18f2685cc78ba50ae5cadbaaea22e24c88074fc9d89.

GLOBAL channel/global permanece 48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1. Produção updates/main permanece 6f641fc6941b573f94393a437191761243cfa69e. Nenhuma atualização em outros clientes/PETs, GLOBAL ou produção. Inclusão futura no GLOBAL exige autorização do proprietário.

## Uso

PDV TESTE ANGRA: ATUALIZAR → ATUALIZAR PET → 1.10, sem reinstalação. Após entrar, aguardar a tela principal: a animação dura 3 s e reaparece somente em outra sessão de login. O computador físico do proprietário não foi acessado; confirmação visual nele permanece necessária.
