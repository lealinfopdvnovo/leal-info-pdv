# Preparação dos PETs — nomes e V1.1

Esta alteração acrescenta um catálogo separado e quatro extensões de identificação visual para a base candidata GLOBAL 10.443. Não altera nenhum arquivo de implementação da base GLOBAL. Os códigos existentes permanecem como identidade interna de compatibilidade.

| Identidade interna | Nome apresentado | Versão inicial preparada |
|---|---|---|
| 001 | LOJA LEAL INFO | V1.1 |
| 002 | GELAMAC | V1.1 |
| 003 | CASA LEAL INFO | V1.1 |
| 004 | CASA GENILSON | V1.1 |

Cada versão PET evolui por demanda própria. Exemplo: LOJA LEAL INFO pode chegar a V1.6 enquanto CASA LEAL INFO chega a V1.26; GELAMAC e CASA GENILSON podem continuar em V1.1. Isso não altera o GLOBAL.

## Implementação preparada

- Catálogo com identificador interno estável, nome exibido e versão PET independente.
- Quatro bibliotecas de identificação visual compatíveis com o contrato IPetExtension já preparado no GLOBAL.
- A identificação visual acrescenta o nome e a versão PET ao título da janela principal, sem escrever banco, licença ou configurações e sem alterar janelas de negócio.
- Prévia Windows separada, com quadros ANTES e DEPOIS. É uma tela real da ferramenta de preparação, não um print das instalações dos clientes.
- Validação de nomes, versões, códigos, independência entre os PETs e identificação das quatro bibliotecas compiladas.

## Limites desta etapa

Nada foi publicado ou instalado. Os clientes em operação continuam com seus aplicativos e particularidades existentes. Esta preparação não migra os executáveis personalizados legados nem demonstra preservação das particularidades após uma migração completa. As extensões são apenas de identificação visual: não substituem as funções locais já existentes.

Os quatro manifestos candidatos recebem CandidateOnly=true e MigrationApplied=false. Não são instaladores nem canais de atualização. A versão inicial V1.1 é a versão local proposta para esta organização, não renumera as versões 10.xxx já publicadas nem apaga o histórico.

A migração de cada instalação e a preservação de suas funções específicas ainda devem ser preparadas e validadas antes de qualquer publicação. É necessária confirmação do usuário para publicar, após apresentar o antes/depois e o prompt.
