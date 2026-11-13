# Planejamento futuro — Catálogos por segmento (13-11-2026)

**Estado:** apenas discussão e planejamento; nenhuma implementação ou publicação autorizada. **GLOBAL intocável**, PETs preservados.

## Ideia central
Criar uma biblioteca de catálogos de produtos por ramo comercial, montada gradualmente: oficina de motos, lojas de peças de moto, roupas, hortifruti, materiais de construção, papelaria e outros. Pesquisar fontes confiáveis na internet para cada segmento, organizar e revisar dados, e armazenar os catálogos versionados no Baú de Ouro/GitHub, em arquivos estruturados (por exemplo JSON), sem presumir que existam catálogos completos e livres para redistribuição. Verificar licenças, atualização, qualidade e origem de cada catálogo.

## Experiência prevista no PDV
No futuro, disponibilizar opção de buscar/importar catálogo por segmento. Priorizar consulta online, para não pesar a instalação do PDV; considerar API de consulta e cache local pequeno. O GitHub pode guardar os arquivos-fonte, mas não precisa servir como API diretamente. Cliente seleciona somente produtos que realmente comercializa e ajusta preço, quantidade, códigos de barras; importação evita duplicatas, não sobrescreve dados existentes e é transacional. Produtos importados ficam locais para vendas sem internet. Opções de busca na biblioteca própria e consulta online complementar.

## Peças de moto: busca e segurança
Duas formas de consulta: (1) por peça/código/marca; (2) por fabricante, modelo, ano e versão da moto, com aplicação/compatibilidade baseada em fontes técnicas verificáveis. Estados: compatibilidade confirmada, não verificada, incompatibilidade conhecida. Nunca inferir ou prometer compatibilidade a partir de resultados genéricos da internet. Ao selecionar peça, exibir balão central alertando para confirmar modelo, ano, versão, código e catálogo do fabricante, com ações **Voltar e conferir** e **Confirmei a verificação**; a confirmação humana não transforma dado não verificado em confirmado. Repetir aviso antes de finalizar venda se compatibilidade permanecer não verificada. Não bloquear toda venda manual, mas registrar confirmação e distinguir risco.

## Outros segmentos
Oficina mecânica pode futuramente exigir OS com veículo, placa, km, mão de obra e peças; materiais de construção, unidades/medidas; roupas, variações de tamanho/cor; hortifruti, produtos e unidades adequadas. Evitar criar módulos desnecessários para segmentos atendidos pelo cadastro padrão.

## Processo acordado
Por enquanto, apenas amarrar ideias e requisitos, um segmento por vez. Levantar catálogos, procedência, direitos de uso e qualidade; planejar interface e arquitetura; implementar e testar somente quando houver autorização expressa do proprietário. Sem mudanças de código, sem atualização ou publicação neste registro.
