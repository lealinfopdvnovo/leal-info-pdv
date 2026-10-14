# LEAL INFO Vidraceiro — Android0.1.0 (base de teste)

Aplicativo separado da Vidraçaria Windows10.403. Não altera executável, schema, licenças, dados ou canais de nenhum PDV. Não é o SpeedFood.

Implementado: câmera/importação de foto, normalização JPEG/orientação, tipo aberto com sugestões editáveis, modelo/configuração opcionais, até24 medidas nomeadas, mm/cm/m exatos, múltiplos projetos/rascunhos privados persistentes, exibição da simulação retornada pelo serviço e entrega idempotente à caixa de entrada do conector. Alterar dados invalida a simulação anterior. Nenhuma janela é selecionada por padrão.

A foto não mede o ambiente. Valores reais são digitados pelo profissional. Simulação visual não define fabricação, engenharia, pagamento ou estoque.

## Integração e estado real

Não foi encontrada API de imagem nem receptor genérico móvel no PDV10.403. `gateway/` é um serviço novo e isolado. Não reutiliza artificialmente o modelo fixo de janela do PDV e não injeta projetos incompatíveis em `glass_projects_v1`.

IA real exige conta/API ativada, chave no servidor e serviço HTTPS acessível ao celular. O APK não contém chave OpenAI. O código de conexão do gateway é armazenado com Android Keystore. Sem configuração, o app explica o bloqueio; não há imagem simulada fingindo IA.

O envio cria pacote original+simulação+metadados+SHA256 na pasta `PDV_INBOX` configurada pelo operador do servidor, com cliente atribuído no servidor. A resposta é **AWAITING_PDV_IMPORT**, exibida como “aguarda importação no PDV”. Isso NÃO significa que o PDV10.403 já importou/reconheceu o projeto. Falta o adaptador receptor genérico dentro/ao lado do PDV, cuja implementação/publicação deve preservar a versão homologada. Não declarar integração ponta a ponta pronta sem esse componente.

## Serviço

Node22+. Variáveis obrigatórias: `GATEWAY_TOKEN` aleatório forte (32+ caracteres), `CLIENT_CODE`, `PDV_INBOX` absoluto. `OPENAI_API_KEY` ativa IA; sem ela a rota devolve503. Opcionais: `DATA_DIR`, `PORT` (8787), `IMAGE_MODEL` (`gpt-image-1`). Variáveis são configuradas apenas no servidor, nunca no repositório ou APK. Executar `node gateway/server.mjs` e publicar por proxyHTTPS; o servidor escuta somente127.0.0.1. Arquivos privados têm permissão0600 quando suportada. Nenhum segredo ou foto é registrado em logs.

Rotas autenticadas: POST/v1/simulations recebe projeto genérico+JPEG e usa a API oficial de edição de imagem. POST/v1/projects recebe somente a referência da simulação já criada; reenvio retorna o mesmo protocolo. Escrever `ready.json` é o marcador final do pacote completo. O conector não abre SQLite, não cria cash_movements, vendas, contratos ou recibos.

## Build/teste

Java17, Gradle8.9, Android Gradle Plugin8.7.3, SDK35, Android mínimo8.0/API26. `gradle :app:testDebugUnitTest :app:lintDebug :app:assembleDebug`; `node --test gateway/domain.test.mjs`.

APK de desenvolvimento para teste, assinado com chave debug; não representa canal de produção/atualização estável nem homologação física. Antes de distribuição comercial, usar identidade de assinatura persistente e publicar a integração real configurada.
