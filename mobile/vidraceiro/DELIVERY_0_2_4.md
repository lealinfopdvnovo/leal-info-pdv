# ANGRA VIDROS 0.2.4 — continuidade visual das telas internas

## Base e referências

Base real encontrada no GitHub: e89982dd642d5c9336eab8fc3c94d5513c23983e, entrega Android 0.2.3. Foram inspecionados antes da edição os prints reais 39460.png (Home aprovada), 39461.png (projeto anterior) e a referência local 01-39462.png (composição desejada). Os prints não foram incorporados ao aplicativo.

A diferença encontrada era de composição: a Home tinha fachada, artes específicas dos módulos, iluminação e camadas; a tela operacional repetia fundos e contornos quase uniformes, com rótulos soltos. A correção usa os recursos gráficos originais da Home e painéis de seção ao redor dos controles reais.

## Telas auditadas e apresentação aplicada

| Tela/variante real | Tratamento visual |
|---|---|
| Lista de projetos / entradas de orçamento, têmpera, contrato, corte e 3D | Cabeçalho do módulo, pesquisa em painel, ações e registros com hierarquia |
| Projeto de vidraçaria | Marca compacta, painel com arte de vidro, campos com ícones, cliente dourado, foto roxa, medidas ciano |
| Clientes e fornecedores / cadastro | Cabeçalho e arte coerentes, pesquisa, campos agrupados e listas legíveis |
| Materiais / cadastro / materiais do projeto | Identidade de material, campos e seleções nativas estilizadas |
| Orçamento comercial | Entradas nativas em painéis, resultado e ação de cálculo destacados; matemática e documento intactos |
| Contrato / revisões | Campo de texto e ações na identidade; conteúdo/revisões intactos |
| Projeto para têmpera | Painel de especificações e ações existentes |
| Calculadora | Medidas e unidade reais, painéis e resultado |
| Plano de corte / adicionar-editar peça / resultado | Painéis de dimensões, controles de rotação, ações e indicadores; algoritmo intacto |
| Representação 2D/3D | Cabeçalho e controles ao redor do visualizador; renderizador intacto |
| Foto do local | Painel, miniatura e ações; captura e arquivos intactos |
| Financeiro e Estoque | Estado real de integração pendente preservado e apresentado na identidade do módulo |
| Integração preparada | Estado, exportação e ações existentes; nenhum receptor PDV inventado |
| Diálogos próprios | Seleção, confirmação, mensagens, revisões, foto ampliada e prévia documental na mesma linguagem |

São 18 identificadores internos de página, com variantes. Os componentes externos de câmera, arquivos, compartilhar e impressão continuam sendo do Android.

## Componentes e fidelidade

InteriorVisuals.Workspace desenha superfícies vetoriais de seção atrás dos controles nativos existentes. Não usa screenshot como interface, não intercepta toques, não reparenta campos e não troca listeners. InteriorVisuals.Panel traz gradientes, reflexo discreto e contorno luminoso, reutilizando a mesma atlas de artes dos módulos da Home nos cabeçalhos. O fundo arquitetônico é discreto e limitado ao topo.

Ícones de módulo e símbolo AV são reutilizados sem alterar HomeVisuals ou assets. Ícones operacionais são vetoriais. Campos mantêm estados normal/foco/desabilitado. Seletores têm indicação visual de abertura; não mudam catálogo, valor ou handler. A referência é adaptada aos dados legítimos: não foi criado campo Espessura, catálogo de modelos ou ação Limpar inexistentes. Cada dimensão continua independente.

A linguagem é compartilhada, sem clonar um formulário para todos: a arte e o ícone identificam o módulo; os conteúdos e controles continuam próprios de cada tela. Não foi implementada nova operação financeira, estoque ou IA.

## Arquivos desta alteração

- app/src/main/java/br/com/lealinfo/vidraceiro/InteriorVisuals.java: superfícies, cabeçalhos, agrupamento visual, ícones e estados.
- app/src/main/java/br/com/lealinfo/vidraceiro/MainActivity.java: somente seleção do contêiner visual interno em screen(); Home e todos os demais métodos preservados.
- app/src/androidTest/java/br/com/lealinfo/vidraceiro/InteriorFlow.java: verificação de painéis/hierarquia e captura do projeto vazio.
- app/src/androidTest/java/br/com/lealinfo/vidraceiro/MeasureFlowInstrumentation.java: espera de captura aumentada de 200 para 650 ms para observar o estado estável após animação nativa, sem mudar o aplicativo.
- app/build.gradle: versão 0.2.4/código 11, mesmo applicationId e assinatura.
- .github/workflows/app_vidraceiro_android.yml: artefato novo e atualização direta sobre a 0.2.3 real.
- DELIVERY_0_2_4.md: este relatório.

## Preservação

A CI compara o código funcional e a Home à base aprovada. A única troca em MainActivity é o tipo de contêiner interno dentro de screen(). Home, HomeVisuals, splash/fade-in, barra, transição, launcher, medidas/conversões, armazenamento, foto, renderizador 3D, cálculos, corte e documentos permanecem congelados. Nenhum PDV, updater ou canal foi alterado ou publicado.

## Execução e evidências

As capturas reais foram comparadas à Home aprovada e à referência. Gradientes, reflexos, hierarquia e grupos de cliente/foto/medidas foram ajustados nessa inspeção, mantendo os controles operacionais. A transparência acidental dos diálogos foi corrigida antes da entrega.

Configurações de execução:

| Resolução | Densidade | Largura lógica |
|---|---|---|
| 480 × 900 | 240 dpi | 320 dp |
| 540 × 960 | 240 dpi | 360 dp |
| 960 × 1600 | 320 dpi | 480 dp |
| 1080 × 1920 | 240 dpi | 720 dp |

As 20 variantes de página são capturadas em 360 dp; projeto, orçamento, 3D, corte, cadastro de cliente e calculadora nas quatro configurações. Há captura extra de projeto vazio, além de seletores, teclado e diálogos próprios. A verificação exige inputs nativos, ausência de elipses, hierarquia preservada e pelo menos sete painéis no projeto. Compara os dados antes/depois da inspeção e das prévias sem mutação.

Regressão funcional: navegação dos 12 módulos; inteiros/vírgula/ponto e mm/cm/m com equivalência física; cliente novo/existente e vínculo; foto, miniatura, ampliação, substituição/remoção e metadados; orçamento de R$132 e PDF; revisão de contrato; materiais; corte 3000×2000 com peça 1200×1000; modelos 3D, rotação, zoom e reset; reabertura em processo novo. O teste de foto usa imagem de teste licenciada pela fronteira de retorno da câmera/importação, sem alegar teste de hardware físico.

A captura estável revelou reutilização indevida do alpha de Paint da borda no preenchimento seguinte dos painéis. O alpha do preenchimento agora é reiniciado a cada desenho, evitando transparência acidental dos diálogos. Nenhum callback de diálogo foi alterado.

O teste com teclado real usa swipes até o botão Salvar ter pelo menos 44 dp visíveis. Seu gesto agora considera a posição real do botão e corrige o sentido quando passa do alvo; a asserção de área acessível foi mantida. O pipeline instala sobre o APK real 0.2.3 com hash conhecido e confere o marcador privado preservado e a versão 0.2.4. Pacote br.com.lealinfo.vidraceiro.profissional022; mesmo certificado, sem migração de dados.

Não há declaração de homologação física. A aprovação final é do usuário no celular.


## Resultado final técnico

PASS: execução GitHub Actions 38802221536, commit de código testado 1dc80af66666efd091a28fa5cfd904bba78a7629.

- Build, lint, dez testes Java e seis testes Node existentes: aprovados.
- Três aberturas frias com medição de opacidade, progressão e transição: aprovadas, sem mudança no splash.
- Criação e reabertura em outro processo: aprovadas, incluindo cliente, foto/metadados, orçamento R$132, contrato, materiais e plano de corte.
- Quatro configurações da Home e quatro fases de telas internas: aprovadas. Foram produzidas 86 capturas internas, incluindo o estado vazio do projeto e os diálogos.
- Teclado real: o botão SALVAR PROJETO ficou completamente visível junto do IME; a captura e o log comprovam 81 pixels de altura visíveis em 240 dpi, acima dos 44 dp exigidos.
- Dados antes/depois das prévias e inspeção: iguais. Nenhuma operação financeira, estoque, cálculo paralelo ou integração foi criada.
- Atualização direta sobre a 0.2.3: aprovada com marcador privado preservado e PackageManager indicando 0.2.4. Mesmo pacote e certificado.

As quatro imagens integrais da Home foram comparadas em RGB com a 0.2.3: tamanho e pixels idênticos em 320/360/480/720 dp. Foram inspecionados os 20 estados de página em conjunto, os painéis completos de projeto, a navegação, os diálogos estáveis, as unidades e a ação salvar com teclado. O diálogo de seleção agora cobre corretamente o formulário subjacente; nenhuma informação passa através de seu fundo.

Apenas a apresentação foi modificada. 26 outros arquivos de produção continuam byte a byte iguais à base real 0.2.3; Home, fade-in, ícone, domínio, foto, persistência, 3D e medidas preservados. Nenhum PDV, updater ou canal foi alterado.

APK: ANGRA_VIDROS_0_2_4_TESTE.apk — 5529429 bytes.
SHA-256: 36b742c6c3b2efa731be891c90bda1d885c5c38a2ed5948b79df4daad89b8aaf.
Certificado: 2ee54920efe7042320f90814e45fd10997f9deb28e213ee92a8955b3e00ffac7.
Pacote: br.com.lealinfo.vidraceiro.profissional022; versionName 0.2.4, versionCode 11.
Artefato: 12005626116; ZIP SHA-256 ee7fcddc9250c82ea086aec95453626d15ef949bf3fe4a228ace7e73956ee567.

Capturas de entrega: ANGRA_VIDROS_0_2_4_COMPARACAO.png, ANGRA_VIDROS_0_2_4_MODULOS.png e ANGRA_VIDROS_0_2_4_PROJETO_COMPLETO.png. São capturas reais e montagens identificadas dessas capturas, sem reconstrução de interface em imagem. O APK não contém os prints de referência nem a foto de teste da instrumentação.

Aguardando teste físico do usuário no celular. Esta entrega não declara homologação física.
