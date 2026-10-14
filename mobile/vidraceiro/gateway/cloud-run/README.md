# Implantação Cloud Run — ANGRA VIDROS

Estado: preparação de implantação; não constitui URL publicada, geração real nem integração Android concluída. Nunca gravar credencial neste diretório.

## Modificações mínimas no gateway

O mesmo POST /v1/simulations continua recebendo JSON (projeto genérico + JPEG Base64). O gateway continua usando multipart somente entre o servidor e a API OpenAI de edição de imagem. GENERATION_ONLY=true remove a dependência de CLIENT_CODE/PDV_INBOX e de disco: foto original e resultado ficam em memória durante a chamada, retornam na resposta e não geram job/pasta. /v1/projects retorna409 nesse modo; integração PDV permanece fora do escopo. Sem esse modo, o comportamento de arquivos anterior permanece para compatibilidade.

PORT vem do ambiente; HOST é 0.0.0.0 no contêiner. Node22, sem dependências npm. Dockerfile usa usuário não root e copia somente os quatro arquivos de produção. Upload máximo16MB de JSON; foto Base64 até14MB de caracteres; resultado decodificado até12MB. Timeout do provedor150s, servidor170s e Cloud Run180s. O Android atual lê por180s; esse orçamento será revalidado na integração real.

## Proteção e segredos

A primeira implantação é PRIVADA por Cloud Run IAM, além da autenticação Bearer já existente do gateway. Não liberar allUsers para fazer o Android atual funcionar. OPENAI_API_KEY e GATEWAY_TOKEN vêm de versões numeradas do Secret Manager. Conta de runtime tem acesso somente aos dois secrets específicos; conta de build separada recebe roles/run.builder. Nenhuma credencial do servidor deve ir ao APK. O código de conexão atual do app não será embutido como segredo compartilhado: a autenticação automática Android ainda precisa ser concluída depois da implantação e validação real do backend.

Máximo1 instância, concorrência1, mínimo0; contador local de até30 tentativas válidas por hora e por processo. Esse contador reinicia com a instância e NÃO é teto financeiro global. A proteção principal desta fase é IAM privado + Bearer. A versão pública para dispositivos só poderá ser publicada depois de autenticação adequada dos dispositivos, sem token administrativo no APK. Não há endpoint público de imagens nem armazenamento persistente no modo Cloud Run. O provedor mantém sua própria política de dados; limpeza local não significa exclusão no provedor.

## Executar sem senha ou chave na conversa

O workflow .github/workflows/angra_cloud_run.yml verifica a credencial Google já referenciada no repositório (LEAL_FIREBASE_SERVICE_ACCOUNT) e o projeto em ANGRA_GCP_PROJECT ou LEAL_FIREBASE_PROJECT_ID. Esses nomes são referências, não confirmação de que valores existem. Nunca imprimir/exportar o conteúdo da credencial. A presença de um projeto Firebase não adiciona Firebase ao Android: é apenas um projeto Google Cloud que pode ou não conceder permissão para novos recursos isolados.

No Google Cloud Shell autenticado, a alternativa é executar:

```bash
bash mobile/vidraceiro/gateway/cloud-run/deploy.sh PROJECT_ID REGION
```

PROJECT_ID e REGION são argumentos a fornecer com os valores reais do proprietário, não recursos já criados. Região proposta padrão: southamerica-east1; serviço proposto: angra-vidros-images. O script verifica autenticação, habilita APIs, cria contas isoladas e secrets, gera o token de gateway diretamente para Secret Manager e para se faltar a versão da chave OpenAI. Nesse ponto, o proprietário cadastra a chave diretamente em Secret Manager > angra-vidros-openai > Nova versão. Não existe chave placeholder.

A conta que executa precisa poder habilitar APIs, criar contas de serviço, conceder os papéis específicos, administrar os dois secrets e implantar Cloud Run a partir de código. Se IAM/faturamento impedir, o gcloud informa a etapa/permissão; o script para sem substituir por acesso público. A implantação pode gerar custo Google Cloud e OpenAI; não é executada sem o projeto/identidade fornecidos e autorizados pelo proprietário.

A URL só será escrita depois de gcloud run deploy retornar e gcloud run services describe obter status.url real. Não foi codificado endereço run.app fictício.

## Teste real obrigatório (ainda pendente)

Após implantação, executar live-test.mjs com projeto, região, uma foto JPEG real autorizada e pasta de saída privada. O teste obtém URL real e credenciais em memória pelo gcloud, envia POST HTTPS com projeto Dona Maria/Box de banheiro, Largura e Altura2m, exige resposta real com PNG/JPEG e salva resultado + hashes. Não usa imagem fixa de resposta ou mock. A chamada real consome o provedor. Um status401/503, health check ou teste local não comprova geração.

Depois é necessário concluir a autenticação Android, usar somente a URL real e testar foto -> HTTPS -> provedor -> retorno -> ImageView no aplicativo. Nenhum APK será anunciado como integrado antes disso.

## Referências primárias

- https://docs.cloud.google.com/run/docs/container-contract
- https://docs.cloud.google.com/run/docs/configuring/services/secrets
- https://docs.cloud.google.com/run/docs/authenticating/service-to-service
- https://docs.cloud.google.com/run/docs/deploying-source-code
