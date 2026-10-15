# ANGRA VIDROS — Home 0.2.2

Base: 0.2.1, commit c655290a9f68dad551f4c43d9a53b377f0536657.
Referência visual do usuário: `01-39294.png` (691 × 1536).

## Auditoria visual

A 0.2.1 apresentava um cabeçalho alto dentro de uma moldura, fachada apenas geométrica, marca pequena em relação à área ocupada, cards altos, fundos abstratos semelhantes entre módulos e ausência de setas. A nova composição usa cabeçalho sem moldura, arquitetura fotográfica, marca nativa integrada, mensagem branca/ciano, duas colunas, ilustrações individuais e indicadores de navegação.

Os textos, títulos, ações e estados reais são preservados. A referência não foi empacotada como tela nem recortada em cards. Todos os textos e alvos de toque são componentes Android nativos; os desenhos decorativos não contêm textos ou controles.

## Recursos gráficos estáticos

Modo: geração de assets em tempo de desenvolvimento pela ferramenta imagegen integrada. Sem API operacional no app, sem credenciais, sem chamadas de inferência, sem cotas de geração. Os recursos são empacotados e funcionam offline.

- `app/src/main/res/drawable-nodpi/home_facade.webp`: 1536 × 1024, foto arquitetônica decorativa. Conversão de formato WebP de alta qualidade.
- `app/src/main/res/drawable-nodpi/home_module_art.webp`: 1448 × 1086 RGBA, atlas transparente 4 × 3 (células de 362 px). Objetos decorativos associados aos doze módulos existentes. Não contém cards nem interface.

Direção dos prompts utilizados:

1. Foto arquitetônica premium de fachada de vidro à noite, vidro ciano iluminado à direita, espaço negativo azul-marinho à esquerda para marca/textos nativos, reflexos em piso molhado, iluminação interna quente discreta, fotografia realista; sem pessoas, marcas, palavras, botões ou interface.
2. Atlas transparente de objetos de vidro/metal em quatro colunas e três linhas: orçamento/clipboard e caneta; pessoas; esquadrias; lâminas de vidro empilhadas; contrato; gráfico financeiro; calculadora; caminhão; cubo de vidro; painéis de corte; cubo estrutural; caixas. Objetos separados, sem palavras, rótulos, números, cards ou UI; iluminação azul/ciano com cores secundárias discretas por módulo.

## Renderização

`HomeVisuals.Hero` desenha a foto com escala proporcional e véus graduais para integrar o fundo e proteger o contraste. `HomeVisuals.Glass` desenha superfícies arredondadas, gradiente individual, objeto decorativo do atlas, borda e seta. A marca continua usando o vetor AV existente e TextViews. Ícone de launcher e splash não participam destes recursos.

## Preservação verificável

A CI compara o código de produção à base 0.2.1. Somente o método `MainActivity.home()`, `HomeVisuals` e os dois novos WebPs podem divergir. O restante de MainActivity é comparado byte a byte após remover somente o método Home. Persistência, foto, 3D, medidas, clientes, contratos, orçamento, plano de corte, launcher e splash permanecem congelados.

Não há alteração no PDV, updater ou canais.

## Instalação de teste e assinatura

O cache de assinatura da 0.2.1 não foi gravado: o log da execução anterior informa `Path Validation Error`, portanto sua chave privada não é recuperável pelo APK. O teste não substitui nem desinstala a 0.2.1: usa sufixo técnico `.profissional022` para manter a instalação e os dados antigos intactos. Não há migração automática entre os espaços privados das duas instalações. O nome visível e o ícone permanecem ANGRA VIDROS. A build passa a usar caminho explícito para a chave de teste e cache persistente, evitando a falha anterior. Nenhuma chave privada é versionada ou incluída no APK/relatório.
