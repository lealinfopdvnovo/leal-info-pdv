# ANGRA QA005 — orçamento ao cliente pelo WhatsApp — PET1.22

Pedido de16/11/2026: acrescentar, na aba Projetos da Central de Vidraçaria, botão para o proprietário enviar o orçamento diretamente ao cliente.

## Entrega
PET **1.22** publicado exclusivamente pelo ATUALIZAR PET do ANGRA QA005. Novo botão **ENVIAR ORÇAMENTO PELO WHATSAPP**, no grupo de ações do cliente, sem alterar os controles existentes. Sem novo APK ou reinstalação.

## Fluxo e contrato
1. Proprietário entra no PDV e abre Central de Vidraçaria → Projetos.
2. Seleciona o cliente cadastrado e preenche medidas, serviço e valores.
3. Clica no novo botão. A rotina usa o snapshot comercial validado e as linhas do documento comercial já existentes.
4. Consulta somente o telefone do cliente selecionado, normaliza DDD/código55 e abre https://wa.me com mensagem corretamente codificada.
5. **O proprietário confirma o envio no WhatsApp.** O PDV não afirma que o orçamento foi enviado, entregue ou recebido.

O orçamento é preparado **em texto**. Não há envio silencioso, automação de WhatsApp Web, uso de API paga ou anexo PDF automático. O PDF comercial aprovado continua disponível na aba Impressão; mensagens extensas demais para o link orientam a usar esse PDF e anexá-lo no WhatsApp.
Não cria pagamento/recibo, não salva ou altera projetos automaticamente e não escreve clientes ou configurações.

## Permissão e validação
- Somente proprietário conforme Auth.IsOwner nativo; botão oculto/desabilitado para funcionário e demais perfis sem essa condição.
- Permissão conferida novamente ao preparar conteúdo, construir URL e lançar a conversa, inclusive contra uso de um rascunho obtido em sessão anterior.
- Sem cliente válido, telefone ausente/inválido ou medidas/valores inválidos segundo o documento existente, não abre conversa.
- URL tem host fixo wa.me, destino formado apenas por dígitos validados e texto via Uri.EscapeDataString.
- Hook habilitado somente com PET005 e recurso QA carregados. Não afeta outros clientes.

## Testes
[Windows — 43983000651](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43983000651): **APROVADO**.
- Central de Vidraçaria real do código em runner descartável, banco/usuários/cliente sintéticos.
- Ausência de botão sem PET QA carregado; inclusão única após carga do PET005 real.
- Proprietário vê o botão; funcionário não vê/não executa; proprietário recupera acesso após novo login.
- Snapshot atual contém cliente, descrição com caracteres especiais, valores corretos (fixture R$230,00) e aviso de que orçamento não é pagamento.
- Número local e +55 normalizados; telefones vazios, curtos ou com URL/comandos rejeitados.
- Clique real do botão constrói destino correto e mensagem cujo round-trip de codificação é íntegro.
- Launcher substituído somente na memória do teste para capturar URL: **nenhum navegador foi aberto e nenhuma mensagem foi enviada no CI**.
- Quantidade de clientes preservada; helper realiza apenas SELECT e leitura da prévia comercial.
- Build/patch verificaram identidade005/versão1.22, base1.12 fixada por SHA, recursos originais e métodos fora dos hooks QA anteriores idênticos.
- Não repetidos os testes completos já aprovados de estilo, abertura, bandeirinha, APK ou servidor.

[Captura/contrato/logs](https://github.com/lealinfopdvnovo/leal-info-pdv/tree/98063b1e89bbaa3a3fb4e6bd2084552c1c97f402/qa/angra37/whatsapp-evidence). A imagem usa a fixture do código com dados de exemplo; não é uma captura do computador do proprietário nem comprova envio externo.

## Publicação e código
[Publicação — 43983434665](https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43983434665): **APROVADA**.
- Fonte: 598b6fd27c883c7c870b9c218ca53ce21de0e0b0, branch qa/angra37-quote-whatsapp-20261116.
- Arquivos: QuoteWhatsApp.cs, Resume.cs/.csproj, Patcher.cs (versão), QuoteWhatsAppTest.cs/projeto isolado e workflows QA.
- Publicador: 71cf0e14d28e306cd18f1c1c5dd863fea0156336, branch qa/angra37-whatsapp-delivery-20261116.
- Evidências: 98063b1e89bbaa3a3fb4e6bd2084552c1c97f402.
- Feed: 2d575a5a023744b2ff7c2554f68c8b8ddf32c840, branch qa/angra-etapa4-setup-20261114.
- SHA256 pacote1.22: e14ef5b1cbf887d074b6205a51cc7e5cf11d65d4820255ff8124e56042055176.
- Somente ZIP1.22 e version.json QA005 alterados no canal. Target005, Schema2/PetApi1 e faixa GLOBAL10.468 a10.469 exclusivo preservados.

## Preservação e pendências
Preservados estilo1.21, logo1.20, tutorial1.19, login, licença, abertura/narração/transição, AJUDA, bandeirinha e APK0.4.2. Sem modificações em GLOBAL, outros PETs/clientes, Firebase, Alerta, Speed Food ou ambiente34; sem backup real e sem Etapa6.
Refs conferidas: GLOBAL48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1; produção updates/main6f641fc6941b573f94393a437191761243cfa69e; Loja client/00155e3a10d2c1b32b7db1a980e7f462a1d07f5a87c.

## Confirmação do proprietário e fechamento — 16/11/2026

Após ser informado de que faltava o teste real de selecionar cliente de teste, abrir o orçamento no WhatsApp, conferir número/valores e verificar recebimento no celular, Adriano confirmou: “Tá, pode fechá essa etapa, tá conferido já”.

**Teste físico do WhatsApp confirmado pelo proprietário; pendência acima encerrada por seu relato. Etapa do ANGRA VIDROS encerrada a pedido do proprietário, até o PET1.22.** Esta confirmação é testemunho do proprietário no computador real, não uma nova execução do CI nem uma captura adicional.

Fechamento documental somente. Não inicia Etapa6, transbordo/migração, backup real ou publicação; não autoriza mudanças no GLOBAL ou em outros clientes. Alerta e Speed Food não foram revalidados nesta confirmação.
