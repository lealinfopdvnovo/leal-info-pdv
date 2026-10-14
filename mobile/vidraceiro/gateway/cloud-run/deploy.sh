#!/usr/bin/env bash
set -euo pipefail
# No tracing, no provider secret on the command line or in this repository.
project="${1:?Uso: bash cloud-run/deploy.sh PROJECT_ID [REGION]}"
region="${2:-southamerica-east1}"
[[ "$project" =~ ^[a-z][a-z0-9-]{4,28}[a-z0-9]$ ]] || { echo 'PROJECT_ID inválido'; exit 2; }
[[ "$region" =~ ^[a-z]+-[a-z]+[0-9]+$ ]] || { echo 'REGION inválida'; exit 2; }
command -v gcloud >/dev/null || { echo 'BLOQUEIO: Google Cloud CLI indisponível. Execute no Cloud Shell autenticado.'; exit 3; }
account="$(gcloud auth list --filter=status:ACTIVE --format='value(account)' | head -n 1)"
[[ -n "$account" ]] || { echo 'BLOQUEIO: autenticação Google ausente. Execute gcloud auth login.'; exit 3; }
service=angra-vidros-images
runtime="angra-images-runtime@$project.iam.gserviceaccount.com"
builder="angra-images-build@$project.iam.gserviceaccount.com"
trap 'echo "BLOQUEIO: etapa Google Cloud falhou; consultar a permissão ou API indicada pelo gcloud acima. Nenhum segredo foi impresso." >&2' ERR
gcloud projects describe "$project" --format='value(projectId)' >/dev/null
gcloud services enable run.googleapis.com cloudbuild.googleapis.com artifactregistry.googleapis.com secretmanager.googleapis.com iam.googleapis.com --project="$project" --quiet
for name in angra-images-runtime angra-images-build; do
  if ! gcloud iam service-accounts describe "$name@$project.iam.gserviceaccount.com" --project="$project" >/dev/null 2>&1; then
    gcloud iam service-accounts create "$name" --project="$project" --display-name="ANGRA VIDROS - $name" --quiet
  fi
done
gcloud projects add-iam-policy-binding "$project" --member="serviceAccount:$builder" --role=roles/run.builder --condition=None --quiet >/dev/null
for secret in angra-vidros-openai angra-vidros-gateway; do
  if ! gcloud secrets describe "$secret" --project="$project" >/dev/null 2>&1; then
    gcloud secrets create "$secret" --replication-policy=automatic --project="$project" --quiet
  fi
  gcloud secrets add-iam-policy-binding "$secret" --project="$project" --member="serviceAccount:$runtime" --role=roles/secretmanager.secretAccessor --condition=None --quiet >/dev/null
done
gateway_version="$(gcloud secrets versions list angra-vidros-gateway --project="$project" --filter='state=ENABLED' --sort-by='~createTime' --limit=1 --format='value(name)')"
if [[ -z "$gateway_version" ]]; then
  node -e 'process.stdout.write(require("node:crypto").randomBytes(48).toString("base64url"))' | gcloud secrets versions add angra-vidros-gateway --project="$project" --data-file=- --quiet >/dev/null
  gateway_version="$(gcloud secrets versions list angra-vidros-gateway --project="$project" --filter='state=ENABLED' --sort-by='~createTime' --limit=1 --format='value(name)')"
fi
openai_version="$(gcloud secrets versions list angra-vidros-openai --project="$project" --filter='state=ENABLED' --sort-by='~createTime' --limit=1 --format='value(name)')"
if [[ -z "$openai_version" ]]; then
  echo 'BLOQUEIO: falta a versão do segredo angra-vidros-openai.'
  echo "AÇÃO DO PROPRIETÁRIO: Google Cloud Console > projeto $project > Secret Manager > angra-vidros-openai > Nova versão; cadastrar a chave OpenAI no formulário seguro. Não enviar a chave no chat."
  exit 4
fi
openai_version="${openai_version##*/}";gateway_version="${gateway_version##*/}"
source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
gcloud run deploy "$service" --source="$source_dir" --project="$project" --region="$region" --no-allow-unauthenticated \
  --service-account="$runtime" --build-service-account="projects/$project/serviceAccounts/$builder" \
  --set-env-vars=GENERATION_ONLY=true,HOST=0.0.0.0,HOURLY_REQUEST_LIMIT=30 \
  --set-secrets="OPENAI_API_KEY=angra-vidros-openai:$openai_version,GATEWAY_TOKEN=angra-vidros-gateway:$gateway_version" \
  --port=8080 --timeout=180 --concurrency=1 --max-instances=1 --min-instances=0 --cpu=1 --memory=512Mi --quiet
url="$(gcloud run services describe "$service" --project="$project" --region="$region" --format='value(status.url)')"
[[ "$url" == https://* ]] || { echo 'Implantação não forneceu URL HTTPS válida'; exit 5; }
printf 'CLOUD_RUN_URL=%s\n' "$url"
echo 'Serviço privado por IAM; ainda é obrigatório testar geração real e depois integrar autenticação Android, sem inserir segredo no APK.'
