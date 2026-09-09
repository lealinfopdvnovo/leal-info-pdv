# LEAL INFO PDV — DEMO DE APRESENTAÇÃO

Base: V10.160 aprovada.

## Regras
- Abertura oficial da LIA permanece congelada e não deve ser alterada.
- Uma única base de código identifica a edição por licença: DEMO ou PRO.
- Sem licença válida, a edição de apresentação entra em DEMO automaticamente.
- DEMO deve abrir mesmo sem internet e nunca pode falhar por validação remota.
- PRO é identificado por chave própria vinculável ao dispositivo.
- O tutorial de primeiro acesso não pode bloquear a apresentação se o WebView2 falhar.
- Nenhuma alteração desta branch deve ser publicada como atualização automática da V10.160 de produção sem validação.

## Formato de licença inicial
- DEMO: `LIPDV-DEMO-AAAAMMDD-DEVICEID` ou `LIPDV-DEMO-AAAAMMDD-ANY`
- PRO: `LIPDV-PRO-DEVICEID` ou `LIPDV-PRO-ANY`

Esta implementação é a fundação local. Antes de comercialização, a assinatura da chave deve migrar para assinatura criptográfica assimétrica/servidor de licenças, evitando chaves forjadas pelo cliente.
