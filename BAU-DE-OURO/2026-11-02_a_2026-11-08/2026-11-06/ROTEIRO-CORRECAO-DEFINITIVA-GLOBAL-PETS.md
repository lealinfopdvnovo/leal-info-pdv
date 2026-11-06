# Roteiro para corrigir a separação GLOBAL/PET
Registrado em 06/11/2026 a pedido de Adriano: “guardar essa ideia no bau”.

## Objetivo e regra permanente
GLOBAL é uma base comum, com versão 10.xxx para todos. PET contém apenas particularidades de um cliente, com versão independente 1.x. Correção direcionada incrementa somente o PET; não incrementar a versão GLOBAL para uma correção exclusiva. GLOBAL nunca sobrescreve personalizações PET.

Guardar este roteiro não autoriza sua execução, alterações operacionais ou publicação. GLOBAL exige autorização explícita. Não declarar organização concluída apenas por trocar nomes, números ou manifestos.

## Problema desta conversa
A correção LOGON exclusiva da Loja foi publicada como 10.465 mantendo PetVersion 1.2, contrariando a regra. O assistente reconheceu a responsabilidade. Não repetir essa estratégia de recompilar um executável específico e tratá-lo como PET independente.

Retrato dos feeds consultados nesta conversa (publicação não comprova instalação):
- GLOBAL principal: 10.463.
- Loja: 10.465 / PET registrado 1.2.
- Casa: canal clients/003/global/version.json em 10.466; manifesto antigo clients/003/version.json em 10.444 / PET 1.1. Não assumir que o manifesto antigo prova a versão PET instalada.
- Gelamaq: 10.463 / PET registrado 1.1.
- Casa do Genilson: 10.463 / PET não informado.

Adriano propôs tornar o selo comum a todos e alinhar a base em 10.466, usando o acabamento aprovado da Casa, sem quadrado escuro. Não copiar o executável da Casa para todos. Preservar correção LOGON da Loja. Não prometer que o PET Casa permanecerá intacto antes de validar compatibilidade e ausência de duplicação do selo.

## Reaproveitamento obrigatório
Continuar a candidata existente no PR #34:
https://github.com/lealinfopdvnovo/leal-info-pdv/pull/34
Referência aprovada anterior: c7aa2df9ea8b58a881ff639aa452a79fd6fe5222, execução 41888293183.
Consultar o estado atual ao retomar; não presumir que essa referência seja o head atual.

O registro de 04/11 indica testes aprovados, mas migração completa e inicialização positiva licenciada ainda não qualificadas. Não confundir testes de QA ou bootstrap com migração operacional concluída.

## Caminho objetivo
1. Fixar referências atuais uma única vez: pacotes, hashes, fontes/receitas e personalizações dos quatro clientes, incluindo selo aprovado da Casa e correção LOGON da Loja. Preservar pacotes para recuperação. Não usar main/client antigos como substitutos da fonte efetivamente publicada.
2. Concluir a base comum aproveitando a candidata existente. Incorporar somente o selo aprovado sem quadrado, preservando funcionalidades, animações e atualizador. Meta discutida: GLOBAL 10.466; resolver a colisão com a Casa já em 10.466 para que a transição não seja ignorada. Não promover uma numeração por simples alteração de rótulo.
3. Separar particularidades: cada PET contém somente seus recursos/correções específicos e versão 1.x independente. Inventariar e preservar personalizações embutidas antigas antes da transferência. Não distribuir o executável de um cliente aos outros. Correção direcionada exige incremento PET correspondente.
4. Testar as mudanças e a transição: PET preserva GLOBAL byte a byte; GLOBAL preserva cada PET byte a byte. Conferir login, LOGON, SAIR/ESC, caixa, backup e rollback, além da atualização partindo dos pacotes realmente publicados. Qualificar inicialização positiva licenciada sem bypass e sem modificar dados de clientes.
5. Após autorização aplicável e aprovação dos testes, publicar pelo ATUALIZAR: base GLOBAL comum e canais PET independentes. Confirmar primeiro instalação real na Loja e Casa; depois disponibilizar aos demais clientes compatíveis, respeitando a decisão de Genilson permanecer fora das validações até nova orientação. Sem setup, ZIP ou comandos manuais como entrega.
6. Fechar o Baú com tabela única por cliente: GLOBAL, PET, commits, hashes, testes, publicação e instalação confirmada ou pendente. Usar essa referência em trabalhos futuros.

## Economia de cota
- Não refazer auditoria geral.
- Reutilizar código, receitas, evidências e testes existentes que continuem válidos.
- Reexecutar testes somente quando as mudanças afetarem sua validade.
- Nenhuma publicação intermediária somente para trocar rótulos.
- Trabalhar em dois blocos: separação pronta; depois testes e publicação.
- Interromper apenas por impedimento concreto que possa afetar clientes, apresentando o ponto exato e a decisão necessária.
- Comunicação curta, com resultados e impedimentos; evitar perguntas e confirmações repetidas.

## Critério de conclusão
Atualizar um PET preserva os arquivos GLOBAL byte a byte. Atualizar GLOBAL preserva cada PET. Funcionalidades e dados permanecem preservados, atualização e recuperação são comprovadas e publicação é distinguida da instalação real. Só então declarar a organização concluída.

## Proteções
Preservar banco, licença, estoque, financeiro, configurações, personalizações, Cozinha, Vidraçaria e animações aprovadas. Não alterar Firebase ou APK. Este registro não modifica feeds, pacotes ou instalações.
