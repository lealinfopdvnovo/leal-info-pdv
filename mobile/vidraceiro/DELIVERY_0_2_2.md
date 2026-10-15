# ANGRA VIDROS 0.2.2 — fidelidade visual da Home

## 1. Origem e diferenças encontradas

Base real 0.2.1, commit c655290a9f68dad551f4c43d9a53b377f0536657. O usuário aprovou as correções funcionais e reprovou a fidelidade visual. Referência: 01-39294.png, 691 × 1536.

A Home anterior tinha cabeçalho alto em uma moldura, fachada geométrica simplificada, marca pouco integrada, cards altos com fundos abstratos semelhantes e sem setas. Também usava três colunas em larguras grandes, diferente da composição solicitada.

## 2. Implementação visual

- Cabeçalho sem moldura com fotografia arquitetônica de fachada de vidro, marca AV vetorial já existente, ANGRA/VIDROS em textos reais e tagline. Título branco/ciano com proporção mais próxima da referência.
- Fundo azul-marinho contínuo; imagem proporcional, véus graduais e contraste sob os textos. Não há imagem da referência empacotada como tela.
- Doze módulos existentes em duas colunas, com exceção de fonte de acessibilidade acima de 1,4, que conserva uma coluna para legibilidade.
- Cards nativos arredondados, cores profundas distintas por módulo, bordas com brilho discreto, ícones de traço, título, subtítulo, setas e objetos decorativos individuais. Nenhum hotspot sobre um print.
- A primeira execução revelou espaçamento vertical excessivo e alturas diferentes em pares com estados pendentes. A segunda iteração reduziu os espaços e alinhou os cards pela altura real da linha.
- Continuar projeto é uma ação legítima já existente e continua abrindo o projeto atual. Financeiro e Estoque mantêm o estado de integração pendente, sem inventar capacidades.

Os recursos decorativos são estáticos e offline. Não foi restaurada geração de imagem, IA ou API no aplicativo. Detalhes dos assets em docs/HOME_VISUAL_0_2_2.md.

## 3. Arquivos

Produção: método home() de MainActivity.java; HomeVisuals.java; novos drawable-nodpi/home_facade.webp e home_module_art.webp.

Empacotamento: app/build.gradle (versão de teste e assinatura explícita).

Testes: MeasureFlowInstrumentation.java; novo HomeFidelityFlow.java; tests/cold_start.py (somente pacote técnico de teste); workflow Android.

Documentação: este relatório e docs/HOME_VISUAL_0_2_2.md.

## 4. Preservação

A CI compara a produção à base 0.2.1 e permite diferenças somente no método Home, no renderizador visual da Home e nos dois novos assets. O restante de MainActivity é comparado byte a byte após excluir exclusivamente o método Home.

ProjectDrawing, PhotoAttachment, ProfessionalStore, Measures, Commercial, CutPacking, Vault, SecureGateway, Manifest, ícone AV, recursos do launcher, SplashActivity, arte/animações/styles e demais funções não foram alterados. Não há mudança de schema ou formato persistido.

Nenhum arquivo do PDV, updater ou canal de cliente foi alterado. 001, 002, 003, universal e 004 fora do escopo e intactos.

## 5. Testes e evidências

Configurações de Home: 480 × 900 / 240 dpi (320 dp), 540 × 960 / 240 dpi (360 dp), 960 × 1600 / 320 dpi (480 dp), 1080 × 1920 / 240 dpi (720 dp).

Capturas reais do emulador: topo, meio, final e conteúdo completo renderizado para cada configuração. Verificação nativa de textos sem elipse/corte, duas colunas, rolagem e abertura do projeto atual pelo card Continuar.

Regressão: dez testes Java de medidas/comercial/corte; seis testes Node existentes sem inferência externa; lint/build/APK; três aberturas frias registradas em vídeo e verificadas por frames de opacidade/progresso (0→100% em 5000 ms); navegação dos doze módulos; projetos e clientes; orçamento R$132; A4/PDF; contrato; materiais; corte; 3D por tipo, órbita/zoom/reset; foto no projeto, substituição/remoção e metadados; force-stop/novo processo e persistência; ícone adaptativo e margens seguras.

O teste de foto usa a fotografia licenciada da instrumentação e entra pelo retorno real da câmera. Não equivale a operar uma câmera física. Homologação do usuário no celular continua pendente.

## 6. Limite da assinatura anterior

O log da build 0.2.1 comprova que a chave de teste não foi salva: Path Validation Error no cache. O certificado público do APK não permite recuperar sua chave privada. Por isso esta build não sobrescreve/desinstala a 0.2.1: instala separadamente em br.com.lealinfo.vidraceiro.profissional022. O nome e o ícone visíveis continuam ANGRA VIDROS.

A instalação antiga e seus dados ficam intactos. Projetos privados da 0.2.1 não são automaticamente copiados para o novo pacote. A persistência da nova build é exercitada em processo novo e seu código é idêntico ao aprovado. O pipeline passa a usar chave de teste com caminho explícito e cache efetivamente salvo. Nenhuma chave privada é versionada ou entregue no APK.

Certificado público da nova linha de teste: 2ee54920efe7042320f90814e45fd10997f9deb28e213ee92a8955b3e00ffac7.

## 7. Resultado final técnico

PASS — execução GitHub Actions 38772269767, código 27c169e5a2e08f027faf8baae8929f6161bf70cc. Build/lint, 10 testes Java, 6 testes Node, três aberturas frias, criação/reabertura em novo processo e quatro fases de telas aprovadas.

As capturas finais foram inspecionadas em 320, 360, 480 e 720 dp. Cabeçalho, proporções da fachada, marca, textos, cores por módulo, ícones, setas, alinhamento das linhas e rodapé foram comparados com a referência. A comparação preserva a largura e a proporção de cada imagem, sem deformar a referência para simular correspondência. A Home mantém os textos e estados reais; não foram inseridos botões fictícios de notificações/configuração.

Há capturas de viewport pelo UiAutomation (topo/meio/final) e captura do conteúdo completo renderizado pela árvore nativa. A comparação entregue usa esta última, identificada como renderização nativa completa sem as barras do sistema. Não é um mockup.

Teste de rolagem: exige deslocamento quando há conteúdo excedente; em 720 dp o conteúdo pode caber inteiro, situação válida. Textos foram verificados sem elipse/corte. O botão Continuar abriu o projeto real nas quatro configurações. A instalação 0.2.1 e seu arquivo marcador permaneceram presentes e na mesma versão após todos os testes.

A assinatura foi restaurada do cache com sucesso e corresponde à execução anterior da nova linha de teste. Não se confunde esse certificado com o certificado perdido da 0.2.1.

APK final: ANGRA_VIDROS_0_2_2_TESTE.apk, 5.518.413 bytes.
SHA-256: 6d0c517abd214b01659d22d3e39334aa011cf513e9989e39a5c2a76b485d3821.
SHA-256 do ZIP de evidências: 0761990fecc22efc8b635e425f874b4a5b391095bd3f69cf0da26d0f860e0059.

O ZIP do APK foi validado; manifesto contém ANGRA VIDROS, versão 0.2.2 e pacote de teste correto; ícone presente; imagem de referência, fotos da instrumentação e evidências não foram incluídas no APK.

3D, foto/persistência, launcher AV e fade-in aprovados da 0.2.1 permanecem preservados. Nenhum PDV foi alterado/publicado. Não se declara homologação física ou aprovação visual do usuário.
