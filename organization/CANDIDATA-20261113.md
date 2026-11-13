> Estado atual: [missão de três clientes ativos](MISSAO-FINAL-20261113.md). Casa do Genilson está INATIVO na candidata. As referências de quatro perfis abaixo são históricas; não autorizam migração ou atualização do cliente 004.

# Candidata GLOBAL/PET — 13/11/2026

Estado: implementação e validação isoladas; NÃO autorizada para distribuição. PR #37 deriva do PR #34, sem merge no GLOBAL.

## Arquitetura executável

- GLOBAL fornece contratos `IPetExtension`, `IPetStartup`, `IPetReady` e contrato financeiro opcional. `PetRuntime` valida identidade, versão, API, SHA-256 e faixa de compatibilidade antes de carregar o módulo externo.
- Cada cliente tem `Extensions/PET/manifest.json` e `Pet.dll`. Recursos exclusivos ficam incorporados no módulo. Os perfis 001/002 preservam os módulos originais, verificados por hash; 003/004 usam os perfis recuperados no PR #34.
- Feed independente v2: GLOBAL em `version.json`; PET em `clients/<id>/pet/version.json`. São contratos candidatos, não feeds publicados. Versões não são acopladas: GLOBAL 10.x e PET 1.x.
- `MinGlobalVersion` é inclusivo; `MaxGlobalVersionExclusive` é exclusivo. API incompatível impede atualização. O alvo GLOBAL deve aceitar o PET instalado. Falha de um feed não suprime a outra seleção válida.
- Verificação manual e automática usam o centro de componentes. O aplicador PET aceita exatamente os dois arquivos PET; não aceita arquivos GLOBAL, banco, licença, scripts ou recursos avulsos.
- Transações e backups separados, hash do pacote e conteúdo, confirmação de saúde por componente, exclusão mútua e recuperação por tarefa externa SYSTEM. O cliente não executa comandos. São preservadas as proteções de diretório e processo existentes no PR #34.
- A adaptação da projeção financeira da base para o módulo original da Gelamaq fica no PET 002. Expressões desconhecidas continuam rejeitadas.

## Commits

| Commit | Mudança |
|---|---|
| 44a51dc840b1b216c89427b8294f10b77eede6fe | Contratos, payload exato, centro de atualização e 31 testes |
| 0299b5cf10aaf76074f45ffab4444ab9ea7c73b1 | Compatibilidade no carregador e entrada direta GLOBAL |
| fc35245f4e704c02f3945f8d8d21fba1751f39a7 | Fixture histórico fixado e manifesto de migração compatível |
| 0a6869a6fa1cc17d7b6711d35036cc19767d8687 | 26 cenários transacionais de Loja e Casa |
| 8523b382fdb71c604da2a6d0b8bffc64413f5294 | Adaptador financeiro exclusivo da Gelamaq |
| 10b7c42f1560a472869f04ef509b9c1bf94132e0 | Regressão da adaptação financeira |

## Evidências

- Run final **43479008291**, commit **10b7c42f1560a472869f04ef509b9c1bf94132e0**: **SUCCESS**. Compilação integrada Windows, contratos comuns, saúde de inicialização, perfis e bootstrap 001/002/003/004, relatório financeiro Gelamaq (vazio/venda/manual/misto), contrato dos módulos preservados, quatro transições pelo aplicador legado e 26 cenários transacionais PET passaram.
- Artefato `integracao-componentes-Windows`, ID `14064318363`, SHA-256 `2327f239fd884f2864f0eb1342afc3d74c94e4b6f78a3c49e7f8da0b71f5f13a`: JSONs, hashes, relatórios e capturas de teste. Download pelo run do GitHub, sem distribuição ao PDV.

- Run 43477816296: 31 testes de seleção/staging aprovados em Linux e Windows. Hash errado, identidade incorreta, travessia, sobrescrita GLOBAL/PET e incompatibilidade são rejeitados.
- Run 43478236793: compilação Windows, contrato comum, saúde de inicialização e quatro perfis/migrações de teste aprovados. 26 cenários do aplicador PET aprovados (13 para 001 e 13 para 003), com hashes dos três binários GLOBAL e arquivos sintéticos de banco/licença preservados. Este run falhou no relatório financeiro Gelamaq, corrigido nos commits seguintes; não é aprovação integral.
- Os cenários incluem sucesso, faixa compatível/incompatível, API errada, SHA errado, cliente incorreto, manifesto incorreto, downgrade, travessia, transação GLOBAL pendente, stage adulterado, interrupção com recuperação SYSTEM e falha de saúde com rollback.
- Dados de migração são sintéticos: usuário QA, estoque, caixa, clientes e configurações. Isso não comprova vendas reais nem licença real.

## Produção protegida

O canal `channel/global` permaneceu em `48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1`. O repositório de distribuição permaneceu no commit `6f641fc6941b573f94393a437191761243cfa69e`. Blobs dos feeds preservados: GLOBAL `61b185f32f9491a4e70030529b5b4bd01857d9c7`; Loja `ac7780488d5a3dccdc3778f251c30888cc0b6b0b`. Nenhum feed, release ou instalação real foi escrito por este trabalho. Não foram solicitados nem usados segredos de publicação.

## Situação dos clientes e impedimentos

| Cliente | Evidência candidata | Aceitação operacional |
|---|---|---|
| 001 Loja | Perfil carregado e bootstrap atômico; 13 cenários transacionais | Pendente cópia exata instalada 10.465/PET 1.3, vidraçaria e regressão completa |
| 002 Gelamaq | Perfil e bootstrap; adaptador financeiro preserva módulo original | Regressão financeira corrigida e aprovada; teste operacional pendente |
| 003 Casa Adriano | Perfil e bootstrap; 13 cenários transacionais | Pendente confronto com instalação e licença reais |
| 004 Casa Genilson | Perfil e bootstrap | Pendente inventário e confronto operacional completo |

A base comum reproduzível do roteiro é 10.463, não uma cópia comprovada do GLOBAL 10.465 informado pelo proprietário. O fixture legado Loja é 10.464 fixado no commit 54fe3086 do repositório de distribuição, não o instalado PET 1.3. Não declarar equivalência por número ou renomear versões.

Ainda não foram comprovados: inicialização positiva com licença válida, migração da instalação exata, fluxo de vendas completo, vidraçaria e demais funcionalidades por cliente, atualização de duas versões reais diferentes do GLOBAL com os mesmos PETs e rollback GLOBAL completo após sucesso autenticado. `migrationVerified`, `positiveActivatedStartupQualified` e `operationalCustomerTested` continuam falsos.

## Continuação segura da migração

1. Identificar o binário instalado por SHA-256: `LealInfoPDV.exe`, `LealInfoPDV.dll`, `LealInfoPDV.deps.json`, `LealInfoPDV.runtimeconfig.json`, conteúdo e manifesto de `Extensions/PET` se existente. Não enviar senhas, chaves, banco de clientes ou arquivos de licença ao repositório.
2. Reproduzir instalação isolada com mesmo runtime e permissões. Qualificar a origem dos binários e realizar confronto de recursos e funções por cliente. Licença válida deve ser exercitada em ambiente autorizado, sem falsificar ou desabilitar a ativação.
3. Validar migração pelo atualizador legado, backup verificável, login, licença, vendas, personalizações e todos os checkpoints de interrupção; repetir para os quatro clientes e versões GLOBAL reais compatíveis.
4. Preparar candidato de transição e feeds v2 apenas após todos os gates positivos. A transição inicial precisa instalar o novo carregador GLOBAL; alterações posteriores do PET não substituem a base. Nenhum rebaixamento automático é permitido.
5. Solicitar autorização expressa de distribuição com pacote e provas concretos. Não publicar, instalar, editar feeds nem oferecer setup/ZIP/comandos manuais nesta etapa.

O trabalho não satisfaz ainda o critério integral de aceitação. A ausência de instalação/licença reais é um impedimento de validação, não um resultado aprovado.
