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

## Resultado automático final — 23/11/2026

- Código validado: 702fff5093bf2de793d86a7898f1cc2268c63047.
- Actions SUCCESS: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/45181025244
- Artefato: etapa6-loja-qa-evidencias, ID 14822480216; SHA-256 do arquivo de evidências: 26f86523aeaa856f9516f51ff8ec551e34e9281cf8e5856970b35bb309ac0b2e.
- Host GLOBAL QA 10.468 e PET Loja 001 1.3 compilados no Windows. Contrato modular, ativação antes da inicialização dos dados/PET, saúde adiada até login e cápsula do PET001 verificados no código compilado.
- Recursos originais confirmados por hash: ReadyKitchen.photos.zip, Kitchen.TablesPremium e LealInfoPDV.UpdateNotifier.Native. Personalização original Loja preservada no seu PET. A mensagem genérica herdada da receita financeira menciona Loja/Gelamaq; nesta execução o laço foi restrito a 001: nenhum payload ou perfil002 foi importado/compilado.
- Diagnóstico executado pelo host confirmou pastas QA separadas, produção bloqueada e ausência de contorno de licença. Esse diagnóstico termina antes da ativação e NÃO comprova login licenciado.
- Sete casos PASS: antes do primeiro PET, após primeiro PET, interrupção/retomada SYSTEM, backup original corrompido rejeitado, bloqueio GLOBAL respeitado, arquivo PET inesperado rejeitado e commit conjunto sintético. Integridade dos arquivos-canário preservada.
- destination-10468 contém o pacote/cápsula CANDIDATO para análise em QA, não uma atualização liberada à Loja. Nenhum instalador foi criado.

### Correções de QA realizadas
Run 45179755512 detectou a ausência do contrato financeiro opcional; recuperada seletivamente a receita aprovada do contrato e do carregador da personalização original001. Run 45180184652 foi cancelado por falha de escopo de variável no roteiro de diagnóstico, corrigida com validação do diretório e timeout. Somente a cadeia dependente dessas correções foi reexecutada. O sucesso final não transforma as execuções anteriores em aprovadas.

### Proteção de produção
Refs antes/depois preservadas: channel/global 48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1; client/001 55e3a10d2c1b32b7db1a980e7f462a1d07f5a87c; client/002 39fad30d4bb7d7a13421d726d85dbefb6de043d2; client/003 137612d259a0fcecdbc2c2165893ba73988eb847; Angra qa/angra-etapa4-setup-20261114 2d575a5a023744b2ff7c2554f68c8b8ddf32c840. Repositório updates/main preservado em 9a3cedf1ff85bf50ac2f08c05475626cfb838fde.

### Estado de entrega
Preparação técnica QA aprovada. ETAPA 6 LOJA NÃO HOMOLOGADA OPERACIONALMENTE: falta Windows QA privado e persistentemente ativado para a Loja, com cópia privada de dados/configurações, para os testes de login, vendas, estoque/financeiro, Cozinha, Vidraçaria, impressão, atualização e rollback após inicialização licenciada. Nenhuma operação na instalação real foi executada. Não foi repetida coleta de hashes, solicitado segredo ou alterado o 34. Não publicar e não avançar a outro cliente.
