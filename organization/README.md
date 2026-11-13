# Organização GLOBAL/PET — preparação de migração

Decisão de Adriano em 04/11/2026: mudanças específicas devem ocorrer somente no PET do cliente; GLOBAL somente com autorização explícita. GLOBAL deve alcançar todos os clientes, preservando PETs. Não bloquear o atualizador.

## Diagnóstico histórico de 04/11 (não representa feeds/instalações atuais)
| Canal atual | Versão anunciada | Problema de separação |
|---|---|---|
| version.json | GLOBAL10.463 | Base comum existente; não promover por iniciativa própria |
| clients/001/version.json |10.464 / PET1.2 | Publicação do selo recompilou aplicação, sem incremento PET |
| clients/003/global/version.json |10.466 / Component GLOBAL | Canal exclusivo distribui executável personalizado |
| clients/003/version.json |10.444 / PET1.1 | Manifesto histórico de migração, não um feed PET independente uniforme |
| clients/002/version.json |10.463 / Component PET / PET1.1 | Rótulo PET não prova pacote restrito à extensão |
| clients/004/version.json |10.463 | Canal legado específico |

Instalação real não pode ser deduzida dos feeds. Usuário mostrou Casa10.463 sem selo e depois10.464 com selo; não há confirmação visual neste registro das10.465/10.466.

## Contrato proposto
- Uma base GLOBAL canônica, um pacote e uma versão10.xxx. Mesmo hash disponibilizado a todos os clientes registrados, sem forçar instalação. Compatibilidade verificada por cliente; se algum cliente precisar de adaptação, preparar no PET e não alegar GLOBAL universal concluído antes disso.
- Um PET por cliente, versão1.x independente, identidade e SHA256 próprios. O pacote PET contém apenas Extensions/PET/manifest.json, exatamente Pet.dll e manifesto; recursos ficam incorporados no módulo. Nunca inclui LealInfoPDV.exe/dll, atualizador, banco, licença ou scripts operacionais.
- Feed GLOBAL version.json; feeds PET clients/<identidade>/pet/version.json. Nomes Loja Leal Info, Casa do Adriano, Gelamaq e Casa do Genilson na interface. IDs somente no roteamento técnico.
- Atualizador apresenta GLOBAL e PET separadamente, conserva ambas as versões e instala cada componente pelo botão ATUALIZAR.
- Recurso individual nunca entra em LoginForm.cs da base comum. Selo e demais personalizações pertencem à extensão PET. O carregamento deve alcançar também o login/primeiro frame, sem depender apenas de Application.Idle após a animação.
- GLOBAL preserva bytes e manifesto PET; PET preserva hashes dos binários da base.
- Backup/rollback por componente com recuperação externa: atualizar PET não restaura/rebaixa GLOBAL; atualizar GLOBAL não apaga PET.
- O verificador release_policy.py valida candidatos offline. Não é conectado à produção, não bloqueia verificações nem atualizações instaladas, e ainda não constitui isolamento de segurança completo.

## Implementação e migração necessárias
1. Recuperar receitas e personalizações reais de cada distribuição. Os SourceCommit atuais são controladores de build, não necessariamente árvore compilável equivalente ao pacote publicado.
2. Extrair e inventariar assemblies/recursos PET existentes e preservar seus hashes. Comparar diferenças de Loja/Casa/Gelamaq com GLOBAL antes de transportar qualquer arquivo. Preservar funções aprovadas de caixa, Cozinha, Vidraçaria, animação de entrada, banco, licença e integrações.
3. Preparar em candidata isolada o carregamento do PET no login e atualização transacional independente. Isso exige mudança do contrato/atualizador da base; esta preparação NÃO autoriza publicar ou alterar o GLOBAL operacional.
4. Mover selo da base personalizada para PET e verificar visualmente captura Windows em96/120/144DPI e resoluções representativas. Corrigir sobreposição/fundo sem redesenhar arte.
5. Testar base sem PET e com cada PET; atualização GLOBAL mantendo PET byte a byte; atualização PET mantendo base byte a byte; erro de hash/identidade, interrupção, falha de inicialização e rollback separados.
6. Produzir relatório com hashes, versões, capturas e diferenças exatas. Submeter a migração concreta à autorização explícita antes de publicar qualquer mudança de GLOBAL.
7. Fazer transição compatível pelo ATUALIZAR, sem setup/ZIP/comandos manuais. Não trocar só rótulos, não apagar feeds legados antes de provar a migração, não rebaixar instalações, não misturar executáveis reconstruídos de clientes diferentes.

## Estado da candidata de 13/11/2026

Implementação isolada em `candidate/global-pet-independent-20261113`, derivada do PR #34. Ver [relatório da candidata](CANDIDATA-20261113.md) para commits, provas e impedimentos.

O contrato v2 exige `PetApiVersion`, `MinGlobalVersion` inclusivo e `MaxGlobalVersionExclusive`. Pacotes PET contêm exatamente `Extensions/PET/manifest.json` e `Extensions/PET/Pet.dll`. Recursos são incorporados na biblioteca do próprio cliente. Não existe publicação autorizada.

A preparação recompõe a base comum histórica 10.463 e perfis preservados; não é uma cópia fiel já qualificada do GLOBAL 10.465/PET 1.3 instalado. `migrationVerified` e `positiveActivatedStartupQualified` permanecem falsos até validação completa. Nenhum feed ou computador real foi alterado.
