# Etapa 6 — Loja Leal Info — QA exclusivo — 23/11/2026

## Escopo
Origem declarada pelo proprietário: Loja 10.467. Origem técnica previamente identificada por hash no PR37: pacote fd979804f5de82c58e1b911a2f0a449e6d8937cae06ef0c0fa39917c48dd161b, DLL 61ec4bdbbcdb7be92cee0fcac421c360cae5e58cd6604b73ef2eaad0717ca0d7. Não foi repetida coleta da instalação.

Branch exclusiva: qa/etapa6-loja-leal-info-20261123. Base existente preservada: 2d575a5a023744b2ff7c2554f68c8b8ddf32c840 (controladores Angra homologado, não seus dados/licença).

## Componentes candidatos
- GLOBAL QA 10.468: mesma receita compilável do PR37/Angra, contratos IPetExtension/IPetStartup/IPetReady, PetRuntime/PetHost/PetBootstrap, atualizador independente e transação/recuperação conjunta já existentes.
- PET Loja 001 versão candidata 1.3: personalização Loja.PET.1.2 original incorporada sem recompilar sua implementação; preservação SHA-256 054e30f5ed5d40a0b539511f50c4415f346b8ec20cbefb325acf364d0f2acd8f. Selo original verificado separadamente. Nenhum PET/mídia/identidade Angra incorporado ao PET001.
- Versões são identificadores candidatos, não nova publicação nem comprovação de instalação.
- Mesmos mecanismos de compatibilidade, conteúdo permitido, hashes, staging, saúde e rollback. Não foi criada segunda arquitetura.

## Preservação
Autenticação, licença e banco da origem têm verificações de fonte na receita existente. O licenciamento mantém sua implementação integral; caminhos graváveis ficam em QA/Loja isolado, conforme mecanismo usado no Angra. Não foi copiado banco, configuração privada, senha, licença ou credencial de cliente.
Apenas o PET001 é compilado. Nenhum payload Casa/Gelamaq/Angra é importado. Sem instalador, sem feed, sem release, sem atualização na Loja e sem alterações em produção.

## Validação
Workflow etapa6_loja_qa.yml: build do host real e PET001, contrato/cápsula de migração, isolamento de caminhos executado pelo host e cenários de recuperação conjunta sobre arquivos sintéticos. Os testes antigos de outros clientes/Android não são repetidos. Resultados pertencem ao artefato etapa6-loja-qa-evidencias no run desta branch.

## Pendências obrigatórias de homologação
NÃO HOMOLOGADO operacionalmente: inicialização positiva com licença legítima da Loja, login, vendas/pagamentos, estoque/financeiro, Cozinha, Vidraçaria, impressão física e migração/backup/rollback com cópia privada dos dados reais. CI de fixture não comprova esses fluxos. Nenhuma licença pode ser inventada ou validação contornada para marcar esses gates como aprovados.
Necessário Windows QA privado/persistente autorizado, legitimamente ativado para a Loja, com cópia privada de dados e configurações, sem sobrepor a instalação de uso. Não solicitar banco, licença ou senha pelo GitHub/chat.
A barra de backup 10.468 publicada em outra tarefa não é incorporada automaticamente à origem aqui declarada 10.467; requer confirmação apenas se a origem efetiva mudou.

## Publicação
PROIBIDA nesta etapa. Nenhum job possui contents:write ou segredo de publicação. Somente artefatos de QA; aguardar homologação e autorização específica do proprietário.
