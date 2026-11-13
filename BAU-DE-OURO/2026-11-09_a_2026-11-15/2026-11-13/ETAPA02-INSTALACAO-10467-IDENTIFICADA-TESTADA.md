# Etapa 02 — referência da instalação real identificada — 13/11/2026

**Diagnóstico executado. Produção protegida. Sem migração ou publicação.**

## Identificação encerrada — não repetir coleta

Relatório recebido: 258 arquivos, quatro obrigatórios presentes, sem conteúdo de banco/licença. SHA-256 do JSON original recebido: `38ce8bf1c123dbcf83edb291ad80df1d94eaff6fa22f1b89e95dcf1a931d27d5`.

| Arquivo | SHA-256 identificado |
|---|---|
| LealInfoPDV.dll | `61ec4bdbbcdb7be92cee0fcac421c360cae5e58cd6604b73ef2eaad0717ca0d7` |
| LealInfoPDV.exe | `45cada6874ed2450cf3ae5564dfdfc7faabe749213d45126de42ef69d9d7d040` |
| LealInfoPDV.deps.json | `73477964d61c59ccf92d0532da4b8c46a18ade13ed0f1f69253b79127424d9e1` |
| LealInfoPDV.runtimeconfig.json | `d0eac8fac04c001178ff9406cf1b9636d7db1a4364271e83a24652bbc5916865` |

O trio DLL/EXE/deps coincide integralmente com a publicação 10.467, pacote SHA-256 `fd979804f5de82c58e1b911a2f0a449e6d8937cae06ef0c0fa39917c48dd161b`. A DLL é diferente da 10.465. A publicação e o artefato histórico 13795928853 também foram conferidos.

A versão pública de arquivo ainda é 10.456.0.0, mas a constante do atualizador dentro do binário é 10.467. A string de interface é literalmente `10.465 • PET Loja V1.3`, aplicada pela receita `qa/logon-loja/pet_display.py` do commit 533537d. Os números exibidos e os metadados antigos não contradizem a identificação por hash e não devem ser usados para rebaixar ou renomear a instalação.

## Cópia de referência preparada e testada

Branch exclusiva: `diagnostic/installed-10467-20261113`, derivada de a9713b8, sem alterar o PR #37. Código de validação final: `bc2ca1564c87f8171908c0c53d3b2873d7b84957`.

- Reconstruídos **257 de 258 arquivos** com SHA-256 idêntico ao inventário, incluindo EXE, DLL, deps e o runtimeconfig original de 439 bytes.
- **Zero arquivos divergentes aceitos** e nenhum ativo de runtime declarado no deps faltante.
- Único arquivo não recuperado: `unins000.exe`, desinstalador antigo. Ele não é necessário para iniciar o PDV e não foi executado nem substituído.
- Runtime .NET/WindowsDesktop 8.0.31 obtido dos pacotes oficiais NuGet, autenticados por SHA-512 do catálogo e SHA-256 de cada arquivo contra o inventário. Dependências de aplicação recuperadas com referências da fonte de publicação, aceitas apenas quando o hash coincide.
- O runtimeconfig foi reconstruído a partir da saída do SDK para o runtime identificado e aceito somente após correspondência byte a byte com o hash recebido. Não foi substituído por configuração histórica ou apenas semelhante.
- O carrier de dependências não fornece código de negócio: seu EXE/DLL/deps gerados são descartados. A cópia executa os binários publicados identificados.

Esta é uma referência binária de QA reproduzível, **não um clone operacional completo**. Banco, licença, configurações privadas, cache PET em AppData, impressoras, periféricos e arquivos não abrangidos pelo coletor não foram copiados. O ambiente Windows é descartável e isolado, sem acesso ao computador do proprietário.

## Testes efetivamente executados

**Actions 43499118050: SUCCESS.**
https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43499118050

Artefato de evidências: **14073551786**. SHA-256 do ZIP baixado e conferido: `9f47c6488bc80057749ab93ef0f3893a3c363a9929e9709dc090d0529278d73c`.

| Teste | Resultado e limite |
|---|---|
| Identificação e comparação com publicações 10.465/10.467 | Aprovado; DLL identifica 10.467 |
| Cópia das dependências e runtimeconfig | Aprovado; 257 hashes iguais, zero divergências |
| Inicialização do EXE 10.467 com binários e runtimeconfig exatos | Executada; janela real de ativação observada. Rede de saída do processo bloqueada durante a observação |
| Inicialização autenticada | BLOQUEADA por ausência de licença válida no Windows de QA; nenhuma chave foi criada ou inserida |
| Atualização histórica 10.465 → 10.467 pelo aplicador original | Aprovada em fixture descartável; não é migração GLOBAL/PET |
| Pacote com SHA incorreto | Recusado, origem preservada |
| Interrupção durante substituição | Recuperação pela tarefa SYSTEM; arquivos originais restaurados |
| Inicialização sem confirmação de saúde ao atualizar 10.465 → 10.467 | Rollback aprovado |
| Falta de saúde partindo dos binários identificados 10.467 | Rollback para os próprios bytes 10.467 aprovado; replay idempotente, não atualização de versão |
| Preservação de dados | Hashes dos arquivos SQLite sintéticos de estoque/caixa, configuração e marcador opaco preservados nos cinco cenários |

Os binários, aplicador, processos, arquivos e tarefa SYSTEM são reais no Windows isolado. O banco de teste contém tabelas `qa_stock` e `qa_cash`, e o marcador de preservação **não é uma licença válida**. Não foram executadas vendas ou pagamentos de negócio, não foi liberado login sem ativação e não foram usados dados de clientes.

Os testes independentes da candidata permanecem válidos e foram reaproveitados: Actions 43482979755 (42 cenários PET, nove GLOBAL, três perfis, Genilson inativo, financeiro de QA e LOGON). São evidências da candidata em fixtures; não qualificam automaticamente migração desta instalação identificada.

## Carregador: compatibilidade e acoplamento comprovados

A análise de metadados e chamadas IL do binário identificado confirmou:

- `PetRuntime` e `IPetExtension` existem.
- A entrada `Program.Main` chama `LojaPetBootstrap.Initialize`, não `PetRuntime.Initialize` para Extensions/PET.
- O bootstrap legado extrai o recurso embutido `Loja.PET.1.2` e chama `PetRuntime.Load` para o cache da Loja. SHA-256 desse recurso: `054e30f5ed5d40a0b539511f50c4415f346b8ec20cbefb325acf364d0f2acd8f`.
- `IPetStartup`, `IPetReady` e `IFinancialMovementsPet`, usados na candidata, estão ausentes nesse host.
- O inventário não contém módulos em `Extensions/PET`. Isso não significa ausência do cache legado em AppData, que não foi coletado e não será solicitado novamente nesta etapa.
- Selo, recursos da Cozinha e aplicador original foram identificados por seus hashes, sem alterar conteúdo.

Consequência: a exibição PET 1.3 não comprova um PET independente instalado. O primeiro carregador da candidata exige uma transição inicial qualificada do núcleo; não é seguro tentar atualizar somente o PET da Loja atual. Essa transição não foi executada nem autorizada nesta etapa.

## Falhas e correções na preparação de QA

A preparação inicial tentou impor a versão de runtime ao pacote do Windows SDK e foi recusada pelo NuGet. O publish padrão subsequente forneceu runtimes com hashes diferentes dos coletados; eles foram recusados, não executados como equivalentes. A reconstrução passou a obter os pacotes oficiais 8.0.31 pelo catálogo, validando SHA-512 e os hashes instalados. A rodada final tem zero divergências aceitas.

Não foi identificada falha de inicialização dos binários recuperados: eles chegaram à ativação. O bloqueio de operação é a ausência de licença legítima no ambiente de QA. As falhas de pacote, interrupção e saúde são injeções controladas e tiveram os resultados de proteção esperados.

## Bloqueio real e próxima ação concreta

A identificação e a reconstrução binária estão concluídas. **Não repetir a coleta nem pedir outro JSON.**

Para continuar é necessário um Windows de testes privado e persistente, ativado legitimamente pelo mecanismo oficial para esse ambiente. Não copiar chaves para o GitHub, não falsificar licença e não desabilitar validação. Somente nesse ambiente poderão ser aprovados:

- login ADMIN e inicialização positiva licenciada;
- vendas, pagamentos, persistência de estoque e financeiro do PDV;
- Cozinha Premium, Vidraçaria, impressão e periféricos;
- migração inicial do carregador e atualização pelo botão ATUALIZAR;
- rollback após inicialização licenciada e preservação das configurações e particularidades reais.

Até lá, essas verificações ficam PENDENTES. Não requer nova decisão de programação do proprietário; o impedimento é disponibilizar um ambiente legitimamente ativado.

## Produção preservada

Nenhuma instalação do proprietário foi acessada. Sem merge, release, feed, atualização de cliente, setup, Firebase, APK ou iFood. PR #37 preservado em a9713b8; GLOBAL no canal 48d8f50; distribuição de produção em 6f641fc. Loja, Casa do Adriano e Gelamaq preservados; Casa do Genilson continua inativo e reversível.

Registro de documentação: `7a32dd0e6a546172e796172437401af5af32c690`. Identificação concluída; não repetir coleta. Sem publicação ou migração.
