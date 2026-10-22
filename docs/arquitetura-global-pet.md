# GLOBAL e PET — preparação conforme fluxograma aprovado

Estado: desenho e planejamento de distribuição; sem publicação, instalação ou migração de clientes.

## Responsabilidades

GLOBAL é o código base principal: funções comuns de Produtos, Clientes, Fornecedores, Cozinha e demais módulos, correções gerais e desempenho. As melhorias comuns chegam a todos os clientes, nas edições STANDARD, PLUS e PRO, respeitando os direitos da licença existente. O cliente utiliza sua aplicação derivada da base, preservando suas particularidades.

PET contém as funções, regras e ajustes locais de uma instalação. Um PET não altera outro cliente. GLOBAL e PET têm duas versões independentes: uma atualização GLOBAL avança apenas a versão comum 10.x; uma atualização PET avança apenas a versão local V1.1, V1.2 etc. Os números 10.468/10.469 citados na conversa são exemplos, não versões publicadas ou reservadas.

A numeração 1.0/2.0/3.0/4.0 da imagem ilustra as ramificações. A instrução verbal posterior prevalece: cada cliente possui sua própria sequência V1.1/V1.2; o sufixo não identifica o cliente.

## Etapas

1. Preparar e validar a base GLOBAL, sem publicar.
2. Posteriormente preparar a separação e identificação dos PETs.
3. Somente após autorização futura, validar a migração e publicar as melhorias comuns para todos, preservando os PETs.

A troca futura de nomes foi definida como: 001 → Loja do Adriano; 002 → Gelamac; 003 → Casa do Adriano; 004 → Casa do Genilso. Nesta etapa não se alteram códigos, identidade de licença, nomes nas telas, canais nem versões locais. Não iniciar PETs em V1.1 automaticamente: a versão inicial exige inventário e migração próprios.

## Diferença para o mecanismo atual

Hoje cada canal compila uma aplicação inteira com uma única CurrentVersion. O controlador existente aumenta também as versões individuais durante uma publicação GLOBAL e pode preservar somente o código antigo dos clientes. Isso não implementa o modelo aprovado de duas versões independentes e não deve ser apresentado como tal.

Esta preparação adiciona um planejador separado, sem publicação, para expressar o contrato correto. Não o conecta ao atualizador operacional. Ainda é necessário separar os pontos de extensão do código comum e os ajustes locais antes de produzir uma distribuição funcional dessa nova arquitetura. Não substituir o aplicativo geral pelo binário personalizado do 003, nem compartilhar seu banco, licença ou configurações.

## Estado da Cozinha e proteção

A Cozinha aprovada no Windows real pelo usuário está na distribuição 003 10.442, commit ba86aa4089a9dfa4a2f081d4d3a5b4f1da3ec4f4. Essa é a referência funcional a portar, não uma base GLOBAL já pronta.

A verificação Windows 39724050399 reproduziu mistura de arquivos após interrupção do bloco de cópia dos atualizadores publicados GLOBAL/002/004 10.408. Essa pendência pertence à futura migração/publicação. Ela não impede preparar o desenho e código em uma branch isolada, nem autoriza modificar instalações agora.

## Critérios para a futura implementação

- Estado separado: CommonVersion, PetVersion e identidade estável da instalação.
- GLOBAL atualiza funções comuns e preserva versão, conteúdo e identidade do PET.
- PET atualiza apenas a instalação selecionada, preservando CommonVersion.
- Licenças, dados, estoque, pagamentos, configurações e personalizações preservados.
- Pacotes identificados, hashes verificados, backup e recuperação da primeira transição testados.
- Nenhum canal operacional é alterado nesta preparação.

## Implementação da base comum — preparação isolada

A branch prepare/global-core-runtime implementa a seleção exclusiva do manifesto Component=GLOBAL, contrato PetApiVersion=1, versão comum 10.443 candidata e carregamento opcional de PET em Extensions/PET. A extensão implementa IPetExtension e possui identidade, versão e hash próprios. Sem PET existente, nenhuma versão local é inventada. A inicialização rejeita identidade, API, caminho ou integridade inválidos.

O pacote GLOBAL aceita somente EXE, DLL e deps.json comuns e não contém arquivos de PET. O aplicador com backup e recuperação externa foi adaptado para o canal neutro GLOBAL. A política de licenças, as rotinas comerciais e os clientes existentes permanecem intactos. O controlador antigo de publicação não está conectado a esta preparação.

Os testes Windows desta etapa verificam seleção comum, extensão local real, preservação de arquivos, backup e recuperação da nova base. Não testam nem executam migração de executáveis legados, publicação, instalação em clientes reais, nem equivalência de todas as funções comerciais. A Cozinha 003 aprovada continua sendo referência para uma futura incorporação funcional na base comum; não foi transplantada nesta etapa de infraestrutura. A numeração candidata não constitui versão publicada.
