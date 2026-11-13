# Coletor — entrega segura — 13/11/2026

## Coletor entregue

`LealInfo_Conferir_Instalacao.exe`, 26.112 bytes, portátil, um executável, sem DLLs laterais ou setup. Usa .NET Framework 4.8 do Windows; não exige o runtime .NET 8. Compatibilidade alvo: Windows com .NET Framework 4.8 disponível. Interface real aberta e fechada em Windows isolado com token sem grupo Administradores e sem elevação (`asInvoker`).

**SHA-256 entregue: `34ecd02c7dbffd3913e87403ed2137d8339c67b57f2d7c73b29ed12688698d4b`.**

Código testado: `5e1237a718ffc3b965404642f0cf2803868a63e3`, branch `diagnostic/collector-secure-20261113`, derivada da candidata `a9713b8`. Nenhuma atualização de GLOBAL/PET.

O artefato original 14066110808 foi recuperado. SHA-256 do ZIP: `a4c2379db22565867e5b8215c969bcaa71f6c9739f853d019c2a94be0e478c68`; SHA-256 original do EXE: `c8d93500f30c987bb926ac834f8e6c0b06ee37ca58f6603a0a3faf4b3dcc06e1`. Ambos conferidos. O original abriu a interface no ambiente de teste, mas tinha lacunas de proteção de caminhos e destino. Foi corrigido e reempacotado no mesmo coletor. **O hash antigo não corresponde ao novo executável.**

## Evidências Windows

Execução final **SUCCESS**: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43490475705

- Artefato do EXE: **14067824879**; SHA-256 do ZIP de transporte `00cd858a3767137eda824892da9352706d2d1a70bb19c8baae6f5ffc0a7d4091`.
- Evidências: **14069092349**; SHA-256 `2c0f58c301c1a14b13b9f6107033e5ff2b9cf74ff2d1945759f286f7503232fb`.
- Ambos baixados e verificados. EXE extraído sem alteração, hash confrontado com `executable.json` e `windows-tests.json` e novamente verificado após disponibilização para download ao proprietário.
- 29 verificações aprovadas; interface e screenshot inspecionados. Testes de seleção/cancelamento usam interface real com escolhas controladas; o EXE entregue também foi iniciado sozinho, com token restrito, e sua janela real foi verificada.
- Uma primeira rodada de QA falhou ao chamar `cmd.exe` com token restrito para preparar uma junção. O fixture passou a criar a junção diretamente pela API Windows, preservando o teste sem administrador. As rodadas finais passaram; a falha inicial não é apresentada como aprovação.

| Cenário exigido | Resultado |
|---|---|
| Pasta correta e hashes autorizados | Aprovado |
| Pasta incorreta | Recusada com erro gráfico |
| Obrigatórios ausentes | Nomes ausentes registrados |
| Arquivo em leitura por outro processo | Leitura permitida |
| Arquivo aberto para escrita e tentativa de alteração durante leitura | Recusados/bloqueados; nenhum relatório misturado aceito |
| Pasta sem leitura | Falha segura, sem pedir elevação |
| Junção em dependências ou em caminho-pai | Recusada antes de seguir redirecionamento |
| Hardlink de arquivo para fora | Recusado |
| Cancelar seleção, leitura ou salvamento | Nenhum JSON criado |
| Salvar JSON dentro do PDV, por junção ou sobre hardlink existente | Recusado |
| Salvar externamente | JSON novo criado e interpretado |
| Nomes reservados de dispositivos e caminho de rede | Recusados |
| Arquivos originais | Conjunto e hashes preservados |
| EXE sem DLLs auxiliares e sem administrador | Janela real abre e fecha normalmente |

## Restrições verificadas

Somente nomes relativos, tamanhos, versões públicas e SHA-256 dos binários autorizados, deps/runtimeconfig e módulos/manifestos PET entram no relatório. Campos internos de identificação do diretório não são serializados. Conteúdo de JSONs, banco, licença, senha, configuração e documentos de clientes não é copiado.

A fonte fica aberta somente para leitura, sem compartilhamento de escrita/remoção durante a leitura. Hash é calculado duas vezes; identidade, tamanho e carimbo do handle são conferidos. Diretórios e arquivos são abertos com proteção contra redirecionamento, incluindo verificações iniciais de presença. Raiz, ancestrais, subpastas e caminho final são verificados; hardlinks e caminhos de rede são recusados.

A única gravação solicitada pelo aplicativo é um JSON novo no destino escolhido, fora da pasta selecionada. Não sobrescreve arquivo existente, não cria staging/temporário e não salva preferências próprias. O scanner de fontes confirmou ausência de APIs de rede, controle de processos e gravação no registro pelo coletor. Não há atualização, migração, acesso ao banco/licença ou envio automático. Testes de QA controlam somente seus próprios processos e fixtures descartáveis, fora do código entregue.

## Proteção dos sistemas

PR #37 permanece em `a9713b8c7810d09d667947c6cca869c9bc5c62c0`; GLOBAL, PETs e perfis não foram alterados por esta etapa. Loja, Casa do Adriano e Gelamaq continuam ativos na candidata; Genilson continua inativo, reversível e preservado. Nenhum feed, release, instalação real, Firebase, APK ou iFood foi modificado. Não houve merge.

## Instruções ao proprietário

1. Baixe e abra `LealInfo_Conferir_Instalacao.exe`.
2. Selecione a pasta onde está instalado o LEAL INFO PDV.
3. Salve `LEAL_INFO_CONFERENCIA_INSTALACAO.json` fora da pasta do PDV.
4. Envie o JSON para análise.

## Próxima ação

Aguardar o JSON real. Só depois comparar com os pacotes confiáveis 10.465/10.467, identificar DLL/indicação PET 1.3, dependências, runtimeconfig, módulos externos e divergências. Origem não coincidente permanece não identificada até investigação. Não solicitar banco, senha ou licença no GitHub. Migração e publicação permanecem proibidas.
