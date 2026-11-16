# Guia visual de funcionários — ANGRA VIDROS QA005 — PET 1.19

Pedido aprovado em voz e confirmado por “go”: aproveitar o tutorial de sete passos e organizar como o Guia visual • Cadastro, com fotografia da tela e explicação ao lado.

## Entrega
PET **1.19**, publicado exclusivamente no canal QA005 em 16/11/2026. APK **0.4.2-qa37** preservado: não precisa baixar nem instalar outro APK.
Uso: **ATUALIZAR PET** no PDV ANGRA QA → Vidraçaria / Integração → **COMO CADASTRAR FUNCIONÁRIOS**.

## Implementação
- Imagens reais dos formulários administrativos do portal e da tela Android de ativação, capturadas com dados sintéticos em fixture isolada.
- Sete passos, título e explicação, Anterior / Próximo / Concluir, Ver novamente, Ampliar imagem e Sair; teclas esquerda/direita e Escape.
- Estilo reaproveitado do Guia visual • Cadastro; brilho vetorial aprovado Coach.Glow reutilizado sem modificar o componente original.
- Layout lado a lado em janela larga e empilhado em janela estreita; imagem pode ser ampliada. A captura alta do Android e as imagens com muitos campos exigem ampliação para leitura em janelas pequenas.
- Imagens incorporadas ao helper: tutorial funciona sem internet. Não preenche campos, cria contas, executa cadastros ou gera códigos.
- Código de ativação ocultado antes de salvar a captura. Captura da senha antes de preenchimento; nenhuma credencial real incluída.
- Instrumentação Android somente de captura e chave de debug efêmera no runner, sem inclusão no APK distribuído; captura via desenho da view em emulador descartável.

## Commits e evidências
| Item | Referência |
|---|---|
| Implementação inicial | db0ea15c0f20cf064101e479d4d0de9e19811563 |
| Fonte final antes das imagens | 51f5c9639492dca0adfccb4ce36e363a9dbc4c40 |
| Fonte compilada com imagens | e12d1defcce56c09e9c4dd0123f6e8585f9ffe2b |
| Capturas Windows preservadas | 4f608db642a38a3898bc73e79cc7612f754d36f1 |
| Workflow de publicação | c47e940186d92a0674b6e49671b5cf166dcf9f58 |
| Feed QA005 publicado | f53a5ae14a7d1fda8158e9ffab3a5cb067815d1b |
| Captura/compilação/testes Windows aprovados | [Actions 43969096936](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43969096936) |
| Publicação aprovada | [Actions 43969732257](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43969732257) |

[Captura Windows larga](https://github.com/lealinfopdvnovo/leal-info-pdv/blob/4f608db642a38a3898bc73e79cc7612f754d36f1/qa/angra37/visual-evidence/visual-guide-wide.png)
[Captura Windows estreita](https://github.com/lealinfopdvnovo/leal-info-pdv/blob/4f608db642a38a3898bc73e79cc7612f754d36f1/qa/angra37/visual-evidence/visual-guide-narrow.png)
[Escopo verificado](https://github.com/lealinfopdvnovo/leal-info-pdv/blob/4f608db642a38a3898bc73e79cc7612f754d36f1/qa/angra37/visual-evidence/patch-scope.json)

SHA256 pacote publicado: `8b7a72407067d6df793598835a6ee0ad8b1fa37542c86f025d32fea048d0cbcd`.
O commit do feed contém somente o novo ZIP QA005 e seu version.json. MinGlobal10.468 / MaxGlobalExclusive10.469, identidade005, Schema2 / PetApi1 preservados.

## Testes desta alteração
- Portal: seis capturas reais em Playwright, servidor HTTPS local e armazenamento em memória, sem acessos a produção. Passou.
- Android API29 em emulador: inicialização, tela real ATIVAR MATRÍCULA RECEBIDA e captura com quatro campos vazios. Passou; não houve ativação real.
- Windows: build .NET8, sete imagens embarcadas, próximos/anteriores, limites0..6, replay, layout1100x720 e650x600, fonte16, fechamento e ausência de botão duplicado. Passou.
- Destaques aprovados Coach.Glow resolvidos e renderizados no guia. Passou.
- Contrato do transporte administrativo existente e preservação de autoria/null/dados de outros clientes pelo bridge real do PET. Passou.
- Patcher: SHA da base1.12 aprovado, identidade005/PetVersion1.19, todos os recursos originais idênticos e métodos fora dos hooks já autorizados idênticos. Core e bandeirinha não recompilados. A comparação é com a base fixada1.12; os hooks acumulados QA37 aprovados foram mantidos.
- Inspeção visual das sete imagens e das duas capturas Windows realizada. A imagem estreita dispõe de Ampliar imagem para leitura.
- Não repetidos os testes de segurança/servidor/APK0.4.2 já aprovados: nenhum código operacional Android ou servidor alterado.
- Falhas da infraestrutura de captura registradas: Playwright1.56.1 não suporta ubuntu26.04; runner fixado24.04. Contexto de teste Android retornava diretório externo nulo; captura passou a usar diretório privado da aplicação no emulador e run-as. Nenhuma alteração no produto para isso.

## Limites e homologação física
**PENDENTE no equipamento real:** atualizar para1.19 e confirmar aparência/escala, abertura, passos, ampliação e saída no Windows do proprietário. Os testes automáticos não substituem essa confirmação.
Não foi executado novo envio comercial ou ponta a ponta APK → PDV nesta tarefa; não declarar homologação geral da Etapa5B.

GLOBAL, outros PETs, produção, Firebase, Speed Food, Alerta e ambiente34 não foram modificados por esta entrega. Produção updates/main conferida em6f641fc6941b573f94393a437191761243cfa69e. Nenhum backup real; Etapa6 não iniciada.
