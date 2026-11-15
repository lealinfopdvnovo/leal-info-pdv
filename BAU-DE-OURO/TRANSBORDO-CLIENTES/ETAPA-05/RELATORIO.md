# Etapa 5 — migração seletiva / homologação rápida

Data: 15/11/2026. Escopo autorizado pelo proprietário nesta conversa: comparar 34 e 37, aproveitar somente o necessário, preservar etapas 1–4, não migrar clientes, não publicar e não iniciar a Etapa 6.

## Referências exatas

- Origem 34 = PR #34, prepare/organizacao-global-pet-20261104, commit 24a58c77a4f052d86fc6681a8c7a540f7f052d03.
- Candidata 37 = PR #37, candidate/global-pet-independent-20261113, commit a9713b8c7810d09d667947c6cca869c9bc5c62c0.
- Destino homologado utilizado = QA ANGRA 005, branch qa/angra-etapa4-setup-20261114, commit 2fd998403a5826bc665ee6fd33c6363cd75a2cfe, PET 1.12.
- Branch isolada desta etapa = qa/etapa5-selective-20261115. Código de comparação/teste em 64b5ed6a057cbfdfd9d3ce9212ac001b1b6cdf19, após f6b67f813dfd8d201948d58733a888ffb9f4c7d6.
- Consultados: PAINEL-MESTRE.md, DIARIO-DE-CONTINUIDADE.md e PLANO-DE-ETAPAS-LEAL-INFO.md no Baú, commit 310c3458cff4429b62ac96cd1c7dd88e1799a291. O comando atual do proprietário substitui o escopo provisório anterior da etapa 5.

## Comparação e decisão

Foram avaliados 28 arquivos/componentes e dependências diretamente relacionados à separação GLOBAL/PET, carregamento, seleção, staging e recuperação. A lista inclui SHA-256 e decisão individual em COMPARACAO-34-37.json.

A árvore 34 é ancestral da 37, e a 37 é ancestral do QA aprovado. Os 22 arquivos de organização/teste selecionados da candidata 37 estão byte a byte presentes no QA. Dezoito já existiam no 34; quatro foram acrescentados no 37. Seis dependências adicionais do projeto compilado estão fixadas no commit 98915ae5a9eb02825d77f74bb179e0ce27d65bd9. As PRs contêm controladores/receitas: não são, isoladamente, a árvore completa do aplicativo compilado.

| Componentes necessários | Pertencimento | Decisão |
|---|---|---|
| IndependentUpdater, ComponentUpdateCenter/Form | Infraestrutura GLOBAL da candidata QA | Reutilizar o 37; o 34 tem versões anteriores |
| PetTransactionalUpdate.cs.source / .ps1 | Infraestrutura GLOBAL para transação PET | Reutilizar o 37; não copiar worker antigo |
| PetRuntime, PetHost, PetBootstrap, ClientUpdateIdentity | Contrato/carregador GLOBAL em fonte compilável fixada | Manter receita e revisão já aprovadas |
| Profile.cs / Pet.csproj, PreservedPresentation e ProfileCertificationSeal | Implementação/personalização PET | Preservar a versão do destino |
| Receitas de compatibilidade/financeiro/recursos | Dependências de build GLOBAL/PET | Reutilizar; nenhuma execução para reconstruir núcleo |
| Program/ComponentTests/collector, probes e testes transacionais | Dependências QA | Executar apenas seleção/staging necessários; reaproveitar evidências de recuperação aprovadas |

**Arquivos de produto incorporados ou reorganizados: nenhum.** Não foi encontrada lacuna que justificasse importação. Foram acrescentados somente comparador, testes focados e workflow de evidências na branch isolada. Nenhum executável, DLL de produto, perfil, mídia, licença, dado, instalador ou feed foi substituído.

## Preservação da origem e do destino

Os 252 arquivos rastreados do 34 permanecem no commit de origem e sua branch permanece intacta. Os 234 caminhos fora da seleção direta estão listados no JSON como adiados, não como obsoletos. Também permanecem preservadas as versões históricas dos 18 arquivos selecionados que já existiam no 34. Nenhum arquivo foi copiado de volta ao 37 por simples pertencimento à origem.

O feed QA permanece PET 1.12, identidade 005, intervalo GLOBAL [10.468, 10.469), ApiVersion 1. Pacote SHA-256 45c03f52243da61dc68962798085deec9152935c9845698b20d0890687a6d9c7. Só contém Extensions/PET/manifest.json e Pet.dll; hash interno e contrato conferidos.

Etapas 1–4 preservadas por ausência de mudanças no produto e comparação de escopo. Evidências Windows anteriores de abertura/AJUDA/rollback são reaproveitadas, sem apresentá-las como novos testes. MP4 SHA-256 a2b5144645a2412fc21dc18f2685cc78ba50ae5cadbaaea22e24c88074fc9d89. Recuperação anterior comprovou restauração da PET 1.11, dados-canário e licença preservados, sem recompilar núcleo nem modificar bandeirinha.

## Limites e continuidade

Não foi repetida auditoria funcional completa nem acessado o Windows do proprietário. Os testes desta etapa são comparação, contratos, staging, isolamento e rejeições, com o pacote QA real e datasets descartáveis. Não representam migração operacional dos clientes.

Integração Android/PDV e suas cinco pendências continuam pausadas, fora do escopo. Nenhum dos arquivos locais da investigação anterior interrompida foi incluído na branch desta etapa. Não foram retomadas alterações Firebase/gateway/APK. Esse assunto não é certificado como resolvido.

As marcações históricas migrationVerified=false / publicação não autorizada dos clientes permanecem válidas. Elas se referem ao transbordo real, não impedem concluir a organização seletiva do QA definida para esta etapa. Não houve nova versão PET, publicação, merge das PRs, migração de clientes ou início da Etapa 6.

## Testes comprovados e encerramento técnico

- Comparação objetiva: APROVADA; 28 componentes/dependências, nenhuma lacuna que exija importação.
- Windows real do GitHub Actions: execução [43735576962](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43735576962), job 132877323700, commit 64b5ed6a057cbfdfd9d3ce9212ac001b1b6cdf19, SUCCESS.
- 37 verificações C# da implementação existente de seleção/staging GLOBAL/PET: APROVADAS. Cobrem versões independentes, identidade, integridade, caminhos e pacotes malformados, compatibilidade, clientes inativos e preservação de arquivos-canário.
- 14 verificações C# da cópia QA gerada pela receita já existente: APROVADAS. O pacote 005 real foi aceito em staging com apenas dois arquivos; oferta e staging recusados para 001/002/003/004; hash adulterado, endereço de produção e incompatibilidade de GLOBAL recusados; mesma versão não oferecida novamente.
- 10 testes Python locais de organization/test_release_policy.py: APROVADOS. Política histórica de produção foi testada como existe; não foi ampliada para QA nem conectada a feeds.
- Evidências Windows: [artefato 14170401583](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43735576962/artifacts/14170401583), SHA-256 120ff093287a1cdca7d72f736de6d82ea35d55137aaa6fc2055e3a64332416d9. Logs consolidados e resultados registrados nesta pasta.
- GLOBAL de produção permanece 48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1; updates/main permanece 6f641fc6941b573f94393a437191761243cfa69e. Origem 34, candidata PR37 e QA homologado mantêm os commits identificados acima.

**ETAPA 5 TECNICAMENTE CONCLUÍDA no escopo seletivo autorizado, pronta para homologação do proprietário.** Sem pendência técnica impeditiva neste escopo. Transbordo real dos clientes e integração Android não foram executados nem certificados. Etapa 6 NÃO iniciada; exige autorização expressa.
