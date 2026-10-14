# ANGRA VIDROS — Android 0.1.4 (teste)

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


## Auditoria da geração e ativação pendente (0.1.4)

O usuário confirmou que ainda não configurou o serviço. Não existe URL pública configurada nesta entrega e não foi executada geração real. O bloqueio acontece em MainActivity.request(), antes de abrir a conexão, quando URL ou código estão vazios. O botão já monta projeto e foto, mas isso não equivale a uma requisição enviada.

Arquitetura mínima já existente: Android -> proxy HTTPS de uma implantação real -> gateway Node em 127.0.0.1 -> API de edição de imagem OpenAI -> gateway -> Android. Não criar outro serviço ou banco. Para ativar, ainda é necessário escolher um servidor/domínio controlado pelo operador, configurar TLS e encaminhar ao gateway, configurar OPENAI_API_KEY no ambiente privado do servidor e criar GATEWAY_TOKEN aleatório próprio daquela implantação. Não há URL, senha ou chave fictícia para copiar. Depois, cadastrar no aplicativo a URL pública real e o código GATEWAY_TOKEN. O código é uma credencial de acesso ao gateway, não a chave de API OpenAI.

POST /v1/simulations envia JSON com project (tipo livre, título, configuração opcional e medidas label/value/unit/mm) e photoBase64 (JPEG normalizado). Somente quando o botão é acionado. O gateway valida as medidas canônicas e envia foto/contexto ao provedor. O retorno esperado é simulationId e imageBase64; o Android salva a imagem privada e a apresenta. Alteração dos dados invalida a imagem. Esses componentes existem em código; sem implantação e credencial não existe comprovação ponta a ponta. O botão Mostrar ao cliente continua exigindo uma imagem retornada pelo serviço. Nenhum resultado fictício é incluído.

### Retenção e privacidade

No celular, foto, rascunho e resultado ficam na área privada do app para reabrir projetos, até remoção dos dados do aplicativo/desinstalação; esta versão não implementa expiração automática local. No gateway, jobs contendo foto, resultado e metadados passam a ter limpeza automática: RETENTION_HOURS padrão 24 horas, configurável de 1 a 168 horas. Limpeza no início do processo e a cada 5 minutos enquanto online; após indisponibilidade, limpeza ao reiniciar. A contagem usa a modificação da pasta do job. Não são servidos por URL pública. O provedor externo tem sua própria política de retenção, dependente da conta; esta rotina local não elimina dados no provedor.

A pasta PDV_INBOX só recebe cópias após ação explícita de envio. Essas cópias não são apagadas pela limpeza de jobs e dependem do operador para retenção. Não foi implementado importador, conexão ou alteração no PDV nesta etapa.

### Seletor de dimensões

Largura, Altura, Profundidade, Diâmetro e Outra. Outra abre nome livre e preserva nomes personalizados dos projetos existentes. Valores iguais são permitidos; identificadores repetidos, ignorando maiúsculas/minúsculas e espaços externos, são bloqueados. O payload e o rascunho continuam usando a mesma propriedade label, sem schema novo. Splash e conversão das unidades permanecem intactos.
