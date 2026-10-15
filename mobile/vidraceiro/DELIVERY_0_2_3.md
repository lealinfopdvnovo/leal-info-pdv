# ANGRA VIDROS 0.2.3 — telas internas

## Origem e escopo

Origem real: 0.2.2, commit a427fba14f8072955ab45e5998c70fd061447a8b. Somente apresentação das telas internas. A Home aprovada é a referência oficial e permanece congelada.

## Telas encontradas e padronizadas

Foram identificados 18 tipos de página internas, com variantes de clientes/fornecedores, financeiro/estoque e adicionar/editar peça. Abrangem lista de projetos, edição de projeto, clientes, cadastro de contato, fornecedores, materiais, edição e seleção de materiais, orçamento, contrato/revisões, têmpera, calculadora, financeiro/estoque pendentes, integração, foto, visualizador 2D/3D, plano de corte, peças e resultado.

Também receberam o padrão os diálogos próprios: mensagens/validação, seleção de cliente, revisões de contrato, confirmação de remoção/substituição, foto ampliada e prévia documental. Os componentes externos do Android (câmera, arquivos, compartilhar, impressão) mantêm sua interface de sistema.

Mapeamento de entradas, controles e funções: docs/INTERIOR_AUDIT_0_2_3.md. Nenhum módulo ou função nova foi inventado. Financeiro/Estoque continuam com o estado real de integração oficial pendente.

## Componentes e identidade

- Reutilização de HomeVisuals.Icon e do vetor AV, sem editar a Home, HomeVisuals ou seus recursos.
- Cabeçalho compacto: ação INÍCIO já existente e assinatura da marca na mesma linha, seguida de ícone/título do módulo. Sem fachada gigante sobre formulários.
- Fundo azul-marinho, painéis graduais, bordas ciano discretas, cantos e tipografia coerentes com a Home.
- Campos nativos com contraste e fundos por estado de foco/normal/desabilitado. Seletores legíveis em layout próprio, preservando valores/handlers. Não há nova máscara ou parser.
- Botões principais, secundários e destrutivos distintos dentro da identidade. Listas mantêm os dados originais, alinhados à esquerda e com destaque tipográfico no título.
- Confirmações, seleções e prévias usam a mesma apresentação. Conteúdo dos documentos e motores PDF/impressão não mudaram.

O novo decorador opera exclusivamente sobre views das páginas internas. Não lê nem altera dados de projeto, não troca listeners nem reparenta os campos. Somente a ação INÍCIO foi acomodada na linha do cabeçalho, com callback idêntico.

## Arquivos alterados/adicionados

- app/src/main/java/br/com/lealinfo/vidraceiro/MainActivity.java: construção de screen(), classe visual de Builder e recurso de layout dos seletores.
- app/src/main/java/br/com/lealinfo/vidraceiro/InteriorVisuals.java: novo padrão visual compartilhado.
- app/src/main/res/layout/glass_selector_item.xml: novo item nativo de seleção/dropdown.
- app/build.gradle: versão 0.2.3, código 10; mesmo pacote e assinatura da 0.2.2.
- app/src/androidTest/java/br/com/lealinfo/vidraceiro/MeasureFlowInstrumentation.java: fase adicional de inspeção.
- app/src/androidTest/java/br/com/lealinfo/vidraceiro/InteriorFlow.java: capturas e testes de apresentação sem mutação de dados.
- .github/workflows/app_vidraceiro_android.yml: congelamento, regressão, atualização direta e evidências.
- docs/INTERIOR_AUDIT_0_2_3.md e este relatório.

## Ajuste de apresentação do teclado

O teste real com teclado revelou um defeito preexistente de insets: a altura do IME era aplicada como padding inferior ao mesmo tempo em que o Android redimensionava a janela, deixando o formulário sem área útil. Em screen(), as páginas internas passam a usar o inset estável da barra do sistema no padding, mantendo o redimensionamento de teclado pelo Android. A Home restaura o listener original. Não há modificação em edição, validação, dados ou callbacks. O teste pede teclado real, rola até SALVAR PROJETO e exige a ação visível.

## Regras preservadas

Home, HomeVisuals, imagens, launcher, SplashActivity, arte, duração de 5 segundos, progressão e transições permanecem inalterados. ProjectDrawing, PhotoAttachment, Measures, Commercial, CutPacking, ProfessionalStore, Vault, SecureGateway e restantes recursos/código de domínio não foram modificados.

A CI compara MainActivity à base excluindo exclusivamente screen() e normalizando somente os nomes da classe visual de Builder e do recurso de dropdown. Todo o restante, incluindo a Home e callbacks comerciais/foto/3D, deve ser idêntico. Não há alteração de schema, arquivo de dados ou integração. Nenhuma IA/API foi reativada.

Nenhum arquivo ou canal do PDV foi alterado ou publicado: 001, 002, 003, universal e 004 intactos.

## Testes

Build/lint, dez testes Java e seis Node existentes, três aberturas frias com vídeos/frames de opacidade/progresso, navegação dos 12 módulos, medidas/conversões, clientes/vínculo, orçamento R$132 e PDF, contrato/revisão, materiais, corte, 3D por tipo/rotação/zoom/reset, foto/miniatura/substituir/remover e persistência em processo novo.

A instrumentação visual cobre 20 variantes de páginas na largura de 360 dp, mais seis telas principais (projeto, orçamento, 3D, corte, cadastro de cliente e calculadora) em todas as quatro configurações. Também cobre diálogos e seletor de unidade. Campos continuam EditTexts nativos, textos sem elipse, dados antes/depois das prévias iguais e ação salvar acessível por rolagem com teclado.

| Resolução | Densidade | Largura lógica |
|---|---|---|
| 480 × 900 | 240 dpi | 320 dp |
| 540 × 960 | 240 dpi | 360 dp |
| 960 × 1600 | 320 dpi | 480 dp |
| 1080 × 1920 | 240 dpi | 720 dp |

Há capturas de viewport pelo UiAutomation e conteúdo integral pela árvore nativa. As capturas são execução real, não mockups. Testes de foto entram pela fronteira de retorno da câmera com foto licenciada; não equivalem a operar o celular físico.

Atualização: mesmo package br.com.lealinfo.vidraceiro.profissional022 e certificado 2ee54920efe7042320f90814e45fd10997f9deb28e213ee92a8955b3e00ffac7. O pipeline instala o APK 0.2.2 com hash conhecido, cria um arquivo marcador privado e instala a 0.2.3 com -r. Verifica a versão nova e permanência do marcador; os fluxos de projeto/anexo são verificados em processo novo.

Não é declarada homologação física da 0.2.3. A aprovação final é do usuário no celular.

## Resultado final técnico

PASS: GitHub Actions 38779709766, código testado 6f097a43717f113bce49f48fa23c7694a1e49d21.

Build/lint, dez testes Java, seis Node, três aberturas frias, criação e reabertura em outro processo, quatro fases Home e quatro fases de telas internas aprovadas. Foram produzidas 84 capturas internas. As 20 variantes em 360 dp foram inspecionadas em conjunto; projeto, orçamento e cadastro também foram conferidos nos diferentes tamanhos, além de diálogos, seletor e teclado.

As quatro capturas integrais da Home foram comparadas à 0.2.2: mesmo tamanho e pixels idênticos em todas. O ajuste de teclado mantém o formulário visível; a instrumentação usou gestos reais de swipe até SALVAR PROJETO ficar acessível com área de toque de pelo menos 44 dp. A captura comprova botão e teclado simultaneamente visíveis. Não houve edição de valores ou acionamento de pagamento.

Cada uma das quatro fases de apresentação confirmou que os dados JSON antes/depois da inspeção e das prévias permaneceram iguais. A regressão existente também confirmou orçamento R$132, vínculo do cliente, contrato/revisão, foto e corte persistidos após processo novo. Visualizador 3D, unidade canônica, algoritmo e documentos permanecem com código aprovado.

Atualização direta aprovada: marcador files/approved-022-marker continuou presente e PackageManager informou versionName=0.2.3 após instalar com -r sobre a 0.2.2. Mesmo pacote e certificado; não há novo pacote paralelo ou migração de dados.

APK: ANGRA_VIDROS_0_2_3_TESTE.apk — 5.523.097 bytes.
SHA-256: 910c83f18ae67af973a3431680e960aaa6dc09ed7bc978fd69cceac6bd896ee9.
Certificado V2: 2ee54920efe7042320f90814e45fd10997f9deb28e213ee92a8955b3e00ffac7.
ZIP de evidências: SHA-256 97b41dd1c7ba2e291be9f0b60cca5f78b96d3ad6bd80c9fc0e2d3e91c5b2d022; artifact 11996590350.

O ZIP do APK foi validado, versão/marca/pacote conferidos e assinatura comparada com a 0.2.2. Nenhuma foto da instrumentação, print de referência ou captura de QA integra o APK de produção.

Home, fade-in, ícone, regras de 3D/foto/medidas/persistência/cálculos/documentos preservados. Nenhum PDV ou canal foi alterado/publicado. Homologação física da 0.2.3 pendente do usuário.
