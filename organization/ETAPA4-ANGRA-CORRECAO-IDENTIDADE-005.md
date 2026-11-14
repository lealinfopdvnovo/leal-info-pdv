# QA Angra — correção de identidade da licença e PET — 14/11/2026

## Causa confirmada
A licença fornecida pelo proprietário contém identidade 005 e plano Standard ativo. Sua assinatura RSA-PSS/SHA-256 foi verificada localmente com a chave pública original do vendedor. O instalador anterior incluía manifesto e módulo PET 001 / 1.3. A rejeição ocorre corretamente no carregador ao comparar essas identidades. Renomear o estado da licença não corrige esse empacotamento.

Nenhuma licença, serial, chave, payload assinado ou estado DPAPI do proprietário foi adicionado ao GitHub ou incluído no instalador.

## Correção limitada ao QA
Branch exclusiva: qa/angra-etapa4-setup-20261114.
Código testado: f668333891259b3210df8693e49115299a29a7e7.
Somente receitas qa/etapa4-setup e seu workflow alterados.
A cópia temporária do perfil Loja é compilada com identidade 005 e versão 1.3. O manifesto também declara 005, mantendo comparação estrita com a licença. A apresentação histórica da Loja é reutilizada por um adaptador de procedência exclusivo do build QA; seus hashes originais continuam obrigatórios. Nenhum PET canônico de produção foi editado.

Validação de licença, assinatura, serial, plano, revisão e validade preservada. Chave pública original preservada; verificador e fontes de licenciamento conferidos por hash no Windows.

O novo setup instala em C:\Program Files\LEAL INFO PDV - TESTE ANGRA ETAPA4 CORRIGIDO e redireciona o atalho de testes para essa cópia. Os binários do QA anterior permanecem intactos. A mesma raiz de dados/configuração/licença QA é reutilizada, sem apagar nem renomear arquivos: %LOCALAPPDATA%\LEAL-INFO-QA\ANGRA-ETAPA4-20261114. A ativação existente pode ser lida pelo mesmo usuário/computador.

## Validação
Actions 43649507477: SUCCESS.
https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43649507477

- Instalador executado no Windows isolado; recusou destino alternativo e reinstalação sobre destino já existente.
- Binários antigos de QA, canários de dados existentes e caminhos de produção preservados.
- Janela real de ativação observada sem contornar a barreira.
- Probe separado, não distribuído, utilizou licença sintética Standard 005 assinada com emissor efêmero e a opção de chave de verificação já existente na API de licenciamento.
- Rotinas reais de assinatura/serial/ativação/persistência DPAPI testadas. Chave incorreta e serial incorreto recusados.
- No host instalado sem alteração de binário, PET 005 / 1.3 carregou com metadados, API, hash e faixa de GLOBAL válidos; autenticação de usuário de fixture aprovada e janela nativa de login renderizada.
- Identidade anterior 001, API incorreta, hash adulterado e divergência entre manifesto e metadados do módulo recusados.
- Nenhum probe, emissor, chave efêmera ou licença de teste incluído no EXE entregue.

Limites: o probe valida a sequência licenciamento/contrato/login em componentes reais instalados com fixture assinada. Não executa uma ativação da licença do proprietário no hardware do runner nem o ciclo integral de Program.Main licenciado nesse hardware. A abertura no computador real permanece para confirmação pelo proprietário. Não homologa vendas, migração 10.467→10.468 ou operação de produção.

## Entrega
Arquivo: LEAL_INFO_TESTE_ANGRA_ETAPA4_CORRIGIDO.exe
Tamanho: 88.192.646 bytes.
SHA-256: a229b993f51dea09eedb60ea19761b51ecd6dcee5f474a7e89e46105aced124b.
ZIP do artefato: a3f2047d0ecc2486fc2151fdad8e021198996c9dc70d2e2018a99a367222d74a.
Evidências: 3a9d8717ba89b4ba84aa73cfc66c3e6bf78aa29c8d025001e7cc98cc22e51be6.
Ambos os ZIPs baixados e hashes recalculados; hash do EXE comparado ao teste.
https://chatgpt.com/library?file_id=libfile_6585379f44e08191bf69b992e5cfc317&account_id=personal

Instruções: fechar o QA anterior, executar o novo instalador e abrir o atalho TESTE ANGRA (Etapa 4). Não desinstalar nem renomear a licença. Se houver pedido de ativação no mesmo PC, importar a licença 005 já fornecida.

## Produção preservada
GLOBAL channel/global: 48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1.
Distribuição/feeds: 6f641fc6941b573f94393a437191761243cfa69e.
Referências reconferidas após os testes. Sem merge, atualização GLOBAL/PET de produção, feed alterado, nova licença ou alteração de licença existente. Registro somente nesta branch QA conforme escopo autorizado.
