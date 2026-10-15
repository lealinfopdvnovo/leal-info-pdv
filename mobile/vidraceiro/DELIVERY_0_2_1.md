# ANGRA VIDROS 0.2.1 — quatro correções pontuais

Origem real: 0.2.0, código testado a0edd8ea2e807f746f1fd07f6dba5a2a6e95262c, documentação04e33e0802aadd61d4ddc675f639540f36661227. Referência visual:01-39294.png enviada pelo usuário. Não incorporada como bitmap.

## Auditoria e correções

- 3D anterior ignorava tipo/modelo e desenhava sempre o mesmo plano. Profundidade zero e yaw livre até90° faziam o plano desaparecer de perfil. A causa no código está comprovada; não se inferem medidas/unidades digitadas no celular sem acesso aos dados dele.
- Agora superfícies são identificadas pelo tipo. Box frontal; frente e retorno se Profundidade for explicitamente informada. Janela/esquadria e porta têm contornos simbólicos distintos, sem largura de perfil/ferragem presumida; espelho tem acabamento reflexivo; vidro fixo, divisória, fechamento e guarda-corpo mantêm as superfícies efetivamente definidas. Tipos livres continuam aceitos como superfície, sem promessa de modelo industrial não cadastrado. Divisões vêm de visualPanels ou indicação explícita de folhas/painéis no modelo/configuração. Nenhuma medida de fabricação é inventada.
- Unidades ficam integralmente visíveis no seletor; nenhuma conversão foi alterada. Órbita limitada a yaw±65° e pitch±55°, zoom.55–2.5, pinça, controles+/− e reset. Unidades canônicas preservadas. Proporções reais muito estreitas geram aviso para conferir unidades, sem distorcê-las. Profundidade ausente não é preenchida artificialmente.
- Foto antes era salva em images/<UUID>/photo.jpg, mas aparecia apenas em tela separada e tinha sóum booleano. Agora a miniatura e a ampliação estão no projeto. O UUID de destino da captura/importação persiste enquanto a câmera abre, inclusive se a Activity for recriada.
- Foto normalizada JPEG, escrita atômica e metadados id/projectId/fileName/MIME/tamanho/SHA256/attachedAt/estado de transferência. Substituição e remoção exigem confirmação. JSON de exportação inclui o manifesto do anexo, não URI temporária nem imagem em base64. Upload de anexos continua pendente do receptor real; nenhum PDV foi modificado.
- Manifest Android antes não tinha icon/roundIcon. Agora usa vetor próprio AV, fundo azul, foreground com margem segura e adaptive icons API26+, incluindo resolução vetorial independente de densidade. Arte do splash não foi recortada nem alterada.
- Home: marca AV+ANGRA VIDROS integrada, fachada geométrica nativa de vidro, gradientes escuros/ciano, bordas luminosas controladas, cards reais com ícones de traço e subtítulos; sem quadrados laranja, sem imagem de referência usada como UI, sem botões fictícios novos. Todos os módulos e estados pendentes preservados.

## Preservação e limites

SplashActivity, angra_abertura, animações, styles, Measures e operações comerciais/corte/financeiro/PDV permanecem intactos. Não há retomada do fluxo IA/API.

Chave privada de assinatura da0.2.0 não está disponível. Para não apagar nem sobrescrever dados anteriores, esta build de teste instala separadamente como br.com.lealinfo.vidraceiro.profissional021. Namespace/applicationId de produção inalterados. Dados privados de outros pacotes não migram automaticamente. A chave debug desta linha de teste passa a ser conservada no cache privado do CI, nunca no Git/APK; cache não equivale a custódia definitiva de assinatura de produção.

## Testes previstos nesta entrega

Regressões existentes de domínio Java/Node, lint/build, três aberturas frias, controles de projeto/cliente/orçamento/contrato/corte, processo novo e diferentes proporções. Novos testes de captura na fronteira de retorno da câmera com fotografia licenciada, miniatura, substituição/remoção, metadados e persistência; tipos3D, órbita, zoom e área rasterizada não linear; ícone instalado via PackageManager em diferentes tamanhos. Esses testes são de emulador, não homologação física da câmera/celular do usuário.

## Resultado técnico final

PASS: GitHub Actions38758448213, código d5988a2d414d9e9cb2dc515b3993e312386e66f6. Build, lint, testes de domínio, regressões existentes, três aberturas frias e nova instrumentação concluídos com sucesso. Capturas reais do emulador inspecionadas: home360/720dp, box com retorno, foto no projeto e AV192px. Ícone conferido pelo PackageManager e rasterizado com máscara circular64/108/192px; primeiro plano dentro da margem segura. Cantos dos componentes nativos têm teste raster de transparência, independente de densidade.

A foto de teste é a fotografia licenciada já presente nos assets de instrumentação, excluída do APK de produção. O teste chama a fronteira real de retorno da câmera/normalização/armazenamento, não declara ter operado a câmera física do celular. A foto permaneceu vinculada após force-stop e nova instrumentação. Ampliação/substituição/remoção permanecem controles nativos. Manifesto exportado contém SHA256, tamanho, MIME e UUID; upload ao PDV ainda não foi implementado.

Tipos testados: box, janela, porta, espelho, vidro fixo, divisória, fechamento, guarda-corpo e esquadria. São representações geométricas básicas e esquemáticas. Box com retorno depende de profundidade explicitamente informada. Não há catálogo de ferragens/perfis ou geometrias industriais não cadastradas. Tipos livres não são reclassificados como janela.

APK: ANGRA_VIDROS_0_2_1_TESTE.apk,4.590.169bytes. SHA256:7eac8217c73a9b6aa5e2681ba88c7b7d1a51430c9433f97b23dd3efe2237e171.

Arquivos: MainActivity.java, ProjectDrawing.java, HomeVisuals.java(novo), PhotoAttachment.java(novo), AndroidManifest.xml, vetores brand_foreground/brand_safe, mipmap-anydpi e mipmap-anydpi-v26 ic_launcher/ic_launcher_round, launcher_colors.xml, app/build.gradle, MeasureFlowInstrumentation.java, PolishFlow.java(novo), cold_start.py, workflow Android e documentação. Commercial, CutPacking, Measures, ProfessionalStore e SplashActivity não foram alterados. Arte/animações/styles congelados idênticos. Nenhum arquivo Windows/PDV ou canal foi alterado.

Assinatura pública V2 do APK novo:ede6e2bdb6998d1cce3ca9f989ec2590cb39ff80758b7484ee06a503d529bfa8; assinatura pública da0.2.0:d3a4ebe22f20f8f3eeda598b960c769c227fa122e51a2a00a7c30ab7a2faaf47. A diferença comprova que não é possível atualizar a instalação antiga diretamente com esta chave. Por isso a instalação de teste é isolada e preserva a0.2.0; não há migração automática entre os pacotes. Nenhuma chave privada é exposta ou incluída no APK.

Homologação física da0.2.1 pendente do usuário. Não confundir emulador com teste no celular real.
