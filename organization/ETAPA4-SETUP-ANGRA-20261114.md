# Setup de testes — PC Angra Vidros — Etapa 4 — 14/11/2026

**ENTREGUE: instalador QA independente. Não é publicação de produção nem prova de migração homologada.**

## Pedido e escopo

O proprietário autorizou preparar um setup seguro para utilizar o PC da Angra Vidros como ambiente de testes da Etapa 4. A entrega é para instalação manual desse ambiente de QA pelo proprietário, em pasta própria. Não autoriza atualização automática, publicação GLOBAL/PET em feeds ou alteração de qualquer instalação real por esta execução.

Base: candidata corrigida 10.468, snapshot `586c512966d20af7e87b47b8ac594b548644f739` (correção d902bf3).
Branch exclusiva: `qa/angra-etapa4-setup-20261114`.
Código testado do empacotamento: `eb92c86db61905715cc91a1316911079c8308de2`.

## O que foi preparado

- `LEAL_INFO_TESTE_ANGRA_ETAPA4.exe`, Windows x64, runtime .NET incluído.
- Pasta de instalação fixa: `C:\Program Files\LEAL INFO PDV - TESTE ANGRA ETAPA4`.
- Atalho próprio: **LEAL INFO PDV - TESTE ANGRA (Etapa 4)**.
- Dados e licença independentes sob `%LOCALAPPDATA%\LEAL-INFO-QA\ANGRA-ETAPA4-20261114`; Database.AppFolder/DbPath/backups e raízes de cache/configuração da cópia QA são redirecionados para esse espaço.
- Perfil de ensaio: **Loja / PET 1.3**, identidade 001. Angra é o computador de testes, não um novo cliente de produção.
- Ativação e login preservados: nenhuma chave ou licença foi embutida, copiada ou criada.
- Atualizações de produção bloqueadas na cópia QA e notificador de inicialização automática desativado nela.
- Instalador recusa sobrescrever pasta existente e recusa troca de destino, inclusive por parâmetro de instalação.
- Desinstalação do aplicativo de testes não inclui exclusão do banco/licença do espaço QA nem toca dados de produção.

A cópia QA contém adaptações de isolamento, somente no build temporário. Os arquivos canônicos da candidata não foram alterados no Git. Isso é um **ambiente funcional inicial separado**, com PET externo pré-instalado, sem a cápsula de primeira migração. **Não é o pacote binário de transição 10.467→10.468**, e seus hashes não devem ser usados para declarar essa migração qualificada.

## Testes efetivamente executados

**Actions [43642987693](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43642987693): SUCCESS**, código eb92c86.

No Windows isolado do GitHub:
1. Compilados core QA e módulo Loja existente.
2. Conferido payload sem banco, licença, chave ou configuração privada de cliente.
3. Verificado que a apresentação legada não contém acesso direto aos provedores de pastas/ambiente/registro recusados pelo empacotamento QA.
4. Gerado e instalado o EXE Inno Setup na pasta exclusiva.
5. Tentativa com diretório alternativo recusada, mantendo um executável-canário existente intacto.
6. Diagnóstico do programa instalado confirmou raiz de banco, backup e configurações no sandbox.
7. Programa instalado abriu a **janela real de ativação**, com IMPORTAR LICENÇA e ATIVAR CHAVE; nenhuma chave foi digitada e nenhuma ativação foi contornada.
8. Hashes e inventário de arquivos nos caminhos tradicionais de dados do PDV permaneceram iguais.
9. Manifesto e hash do PET externo 001 / 1.3 instalados conferidos.
10. Atalho exclusivo presente.
11. Nova tentativa de instalação sobre o mesmo destino recusada, mantendo os binários instalados.

O processo do aplicativo observado foi encerrado somente no runner descartável. Nenhuma instalação do proprietário foi acessada.

As primeiras rodadas não foram distribuídas: o harness ainda propagava um código de saída não inicializado apesar dos checks funcionais concluídos. A rodada final define o retorno de sucesso explicitamente e também verifica as recusas de diretório/reinstalação.

## Entrega e integridade

Arquivo: **LEAL_INFO_TESTE_ANGRA_ETAPA4.exe**
Tamanho: **88.188.530 bytes**.
SHA-256 do EXE, registrado pelo teste e recalculado após download:
`a85296fa3e9c87aff9ca2f433da01a71618435263e58d19356d15fcd863a740d`.

Artefato GitHub **14132408420**, **BAIXAR-SETUP-TESTE-ANGRA-ETAPA4**:
https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43642987693/artifacts/14132408420

SHA-256 do contêiner ZIP do GitHub, também recalculado:
`a947b301fc467a4d5b84243e62e367c2db48dc1c9e2620c57f95d9d02c28281a`.
O contêiner tem exatamente um arquivo, o EXE acima. O proprietário recebe também o download direto do EXE, sem precisar extrair esse contêiner.

Evidências no artefato **14132473190**, SHA-256 informado pelo Actions:
`27f55d028c2582abf1251e12a6209fc62af3ce94cad239138319cc037e134494`.

Download direto persistido do mesmo EXE verificado:
https://chatgpt.com/library?file_id=libfile_cb97e45945948191b5debb719006c38d&account_id=personal

## Instruções ao proprietário

1. No PC da Angra Vidros, baixar e abrir o EXE.
2. Concluir a instalação no destino de testes já definido.
3. Abrir o atalho **TESTE ANGRA (Etapa 4)**.
4. Ativar legitimamente esse ambiente com licença apropriada ao perfil Loja. Não copiar uma licença de outra instalação.

Não há PowerShell, substituição de DLLs ou configuração manual de pastas.

## Pendências e proteção

Apenas a barreira de ativação foi observada. **Login licenciado, operação de vendas/pagamentos, Cozinha, Vidraçaria, impressão e migração completa permanecem pendentes.** Não houve aprovação operacional.

GLOBAL de produção preservado: `48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1`.
Distribuição/feeds preservados: `6f641fc6941b573f94393a437191761243cfa69e`.
Nenhum merge, release de produção, feed alterado, atualização de cliente, Firebase, APK ou iFood.

A instalação no computador real deverá ser realizada pelo proprietário. Esta execução preparou, testou e entregou somente o setup QA solicitado.
