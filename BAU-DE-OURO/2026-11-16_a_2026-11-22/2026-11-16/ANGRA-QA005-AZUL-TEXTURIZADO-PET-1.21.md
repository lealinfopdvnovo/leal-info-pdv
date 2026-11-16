# ANGRA QA005 — sexto estilo Azul Texturizado — PET1.21
16/11/2026. Pedido: o seletor mostrava cinco opções; faltava o Azul Texturizado já utilizado na Loja Leal Info.

## Resultado
PET **1.21** publicado exclusivamente no canal QA005. Instruções: **ATUALIZAR PET → abrir vendas → ESTILO → AZUL TEXTURIZADO**, ao lado de PDV Rosa.
Os cinco cartões originais permanecem. Não muda a escolha atual até o usuário selecionar o novo estilo. Sem reinstalação ou novo APK.

## Reaproveitamento e implementação
- Textura original Assets/tema_azul_texturizado.jpg da Loja, blob Git c841b9860baffc481f847eae7f6fc63cfcec154c, incorporada sem alterações ao helper em qa/angra37/BlueTexture.jpg.
- Paleta Azul Texturizado já existente e sincronizador original da tela reutilizados. O aplicador existente restaura a base antes de aplicar azul, evitando resíduos do tema anterior.
- Novo cartão na célula livre, coluna1/linha2, mantendo as duas colunas e três linhas.
- Preferência exclusiva angra_qa005_sales_style preserva a seleção quando o aplicador antigo normaliza sales_theme ao reabrir. Selecionar qualquer um dos cinco estilos existentes atualiza a preferência e mantém seu aplicador original.
- Observação habilitada somente com PET005 e recurso ANGRA.IntegrationQA.Helper carregados; identificação da tela pelo controle sales_company_logo, sem depender de título ou fabricante.
- Textura embarcada funciona sem internet. Não escreve arquivos do GLOBAL nem modifica lógica de venda ou identidade do cliente.
- Arquivos efetivos: SalesStyles.cs, Resume.cs/.csproj, BlueTexture.jpg, versão Patcher.cs e testes/workflows em QA. MainForm.cs, SalesVisuals.cs e CompanyBranding.cs não modificados no Git ou no pacote entregue.

## Validação
[Windows — 43981165184](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43981165184): **APROVADO**.
- Seis opções únicas no seletor real do código do PDV.
- Clique real no novo cartão, textura original comparada por dimensões e amostras de pixels; paleta e textura da tabela sincronizadas.
- Troca para Futurista Azul, Dark Premium, Clean Pro, Blue Red Racing e PDV Rosa: opções originais e preferências corretas; textura azul não permanece nos temas sem textura.
- Retorno ao azul após PDV Rosa e reabertura da tela de vendas: seleção e textura preservadas.
- Não seleciona azul automaticamente antes da escolha do usuário.
- Patcher: identidade005/versão1.21, base aprovada1.12 fixada por SHA, recursos originais e métodos fora dos hooks QA acumulados preservados.

A tela real foi compilada apenas como fixture descartável no runner, com usuário e licença sintéticos. O binário GLOBAL dessa fixture não foi distribuído. Sua captura mostra a marca padrão porque não usa os arquivos/configurações privados do computador ANGRA. A entrega preserva a logo do proprietário e o tratamento do PET1.20.
O proprietário da fixture ficou oculto para excluir rotinas de início/consulta de atualização fora deste teste. Marcadores temporários de diagnóstico em MainForm.cs foram inseridos somente na cópia do runner; não entraram no Git nem no PET.
Falhas anteriores de preparação do harness foram corrigidas: diretório de restauração compartilhado entre alvos, checkout aninhado incluído no glob, namespace de fixture e busca pelo título plural em vez do controle estável. Somente a execução final aprovada é considerada validação.

[Evidências Windows](https://github.com/lealinfopdvnovo/leal-info-pdv/tree/93c83036e0366bd843895ec04e77a92b0ec976fb/qa/angra37/style-evidence):
- six-style-selector.png
- blue-textured-sales.png
- blue-textured-reopened.png
- style-test.txt / patch-scope.json / source.json

## Publicação e rastreabilidade
[Publicação — 43981497888](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43981497888): **APROVADA**.
- Branch de fonte: qa/angra37-blue-style-20261116.
- Fonte compilada/testada: dd2b0651201a6f111cc98e811532fd342de1816a.
- Evidências: 93c83036e0366bd843895ec04e77a92b0ec976fb.
- Publicador: c570db08d417e805dbb3ea4d0d291f213f0f7c6e, branch qa/angra37-style-delivery-20261116.
- Feed/pacote: af235145e779aae76737cbbccd301b7b6fe8b5aa, branch qa/angra-etapa4-setup-20261114.
- SHA256 pacote1.21: ae204f44576d984356cc33f24c4ee21260d6a0c7b888bd2f98be978eded1f400.
- Commit do feed altera somente o ZIP1.21 QA005 e seu version.json. Target005, Schema2/PetApi1 e faixa GLOBAL10.468–10.469 exclusivo preservados.

## Proteções e pendência física
GLOBAL segue48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1; updates/main produção segue6f641fc6941b573f94393a437191761243cfa69e; Loja client/001 segue55e3a10d2c1b32b7db1a980e7f462a1d07f5a87c.
Licença, login, abertura/narração/transição, AJUDA, logo1.20, tutorial1.19, APK0.4.2, demais clientes e funcionalidades preservados. Firebase/Alerta/Speed Food/ambiente34 não alterados. Sem backup real ou Etapa6.

**PENDENTE no computador real:** atualizar para1.21, escolher Azul Texturizado, conferir imagem/legibilidade/identidade e fechar/reabrir vendas. Não declarar esse teste físico nem a Etapa5B integralmente homologados nesta execução.
