# ANGRA QA005 — logo pequena do caixa — PET1.20
Data:16/11/2026. Autorização: “go” após confirmar correção somente da logo pequena no topo da tela de vendas, com transparência. A imagem maior já foi ajustada pelo proprietário nas configurações e deve permanecer.

## Entrega
PET **1.20** disponível exclusivamente pelo **ATUALIZAR PET** do ANGRA005 QA37. Sem novo setup ou APK. APK0.4.2, tutorial PET1.19, abertura/narração/transição/AJUDA/bandeirinha/licença preservados.

## Alteração efetiva
- Helper QA trata somente PictureBox chamado sales_company_logo.
- Ativação exige assembly PET005 carregado com o recurso ANGRA.IntegrationQA.Helper. Ausente esse PET, o observador não instala.
- Remove apenas fundo quase preto neutro conectado às bordas quando ao menos90% do perímetro é preto. Preserva áreas escuras internas fechadas, cores, proporção e canal alfa existente.
- Recorta margens transparentes e usa Zoom/BackColor transparente, deixando a faixa azul aparecer.
- Tratamento exclusivamente em memória da imagem exibida no topo: não grava no banco, nas configurações ou no arquivo original. Não substitui a arte do proprietário por outra.
- Reaplica quando o controle recebe nova imagem. Mantém a opção de alterar logo nas configurações.
- A imagem maior sales_product_picture e o restante da tela não são modificados. Fundos brancos/coloridos permanecem; imagem vazia mantém comportamento seguro.

Arquivos: qa/angra37/SalesHeaderLogo.cs (novo), HeaderLogoTest.cs/.csproj (testes), Resume.cs/.csproj (hook/helper), Patcher.cs (versão1.20), workflow Windows e workflow de publicação QA. MainForm.cs e CompanyBranding.cs somente consultados; nenhuma alteração no GLOBAL.

## Testes e evidências
[Windows e compilação — 43975780939](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43975780939): **SUCESSO**.
- Preto externo transparente, recorte, imagem de origem intacta e preto interno preservado.
- Branco/colorido preservados; PNG com alfa existente; fallback seguro para imagem totalmente preta.
- Ausência de efeito sem PET005 carregado; efeito no cabeçalho após carregar PET005 real.
- Imagem maior preservada por referência e pixels.
- Troca/recarregamento da imagem mantém transparência e geometria.
- Larguras850,1366,1920: controle permanece visível e dimensionado.
- Patcher validou identidade005/versão1.20, SHA da base aprovada1.12, todos os recursos originais e métodos fora dos hooks QA já autorizados. Core/bandeirinha não recompilados.

**Limite das evidências visuais:** a captura Windows usa logomarca sintética com fundo preto e detalhe preto fechado proposital, para verificar o algoritmo. Não representa fotografia do PDV físico nem redesenha a arte do cliente. O JPEG original está salvo no computador do proprietário e será tratado ali em memória. Inspeção dessa arte exata no Windows físico: **PENDENTE**.
Não repetidos testes completos de APK/servidor/tutorial sem alteração.

[Publicação — 43976118470](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43976118470): **SUCESSO**.
Commit do feed contém somente ZIP1.20 e version.json QA005. Sha256 pacote: `289c61b2fcc4a970b7581bb0cb1121015fbe8c885097d3f183391c74b5af90b5`.
Target005, Schema2, PetApi1, intervalo GLOBAL10.468 a10.469 exclusivo preservados.

## Referências
- Fonte final: 1e19d0344b7340e6722c93b2764a29f5b2ebe329, branch qa/angra37-header-logo-20261116.
- Publicador: b68145900f8f22c755a9921a7f8be3bba903d355, branch qa/angra37-header-delivery-20261116.
- Feed QA: e1f566d01f23f66985118fc555c1bf9c67e0dab1, branch qa/angra-etapa4-setup-20261114.
- Evidências Windows: [c52eb96ff27ab92d88dd1f4bcebf5929aee3074c](https://github.com/lealinfopdvnovo/leal-info-pdv/tree/c52eb96ff27ab92d88dd1f4bcebf5929aee3074c/qa/angra37/header-evidence).

## Confirmação física restante
No PDV ANGRA QA, clicar ATUALIZAR PET, confirmar1.20, reabrir a tela de vendas e conferir logo pequena integrada à faixa azul, imagem maior inalterada e troca por configurações funcionando. Não declarar esse teste físico aprovado sem resposta do proprietário.

Produção updates/main segue6f641fc6941b573f94393a437191761243cfa69e. GLOBAL, demais PETs/clientes, Firebase, Alerta, Speed Food e ambiente34 não alterados. Nenhum backup real nem Etapa6.
