# Auditoria e implementação — Vidraçaria 07/08

Base real: 001/10.401, commit 536b9a49e2142ba12d8583e9383f39986b51f25b; 003/10.401, commit c52fb75e97ac8c805a05d383ed3ca1eecee9b95a. Os manifestos dirigidos e os hashes dos pacotes são conferidos no Windows antes do build. Cada cliente mantém sua própria base e particularidades; nenhum banco, licença ou configuração entra no pacote.

## Estruturas auditadas

- `MainForm.BuildReceipt`, `ShowReceipt`, `PrintReceipt` e `ThermalPrinterService`: comprovante existente de venda finalizada, dependente de `sales`/`sale_items`/`sale_payments`. Preservados. Um recebimento manual da Vidraçaria não é convertido artificialmente em venda.
- `CashFlow.RegisterForGlass`, `cash_movements` e `glass_cash_links_v1`: movimento legítimo e vínculo transacional com projeto/cliente. Usados somente para leitura pelos recibos. `GlassOperationsStore.Read` continua sendo a fonte do resumo financeiro, inclusive das condições capturadas no contrato.
- `settings` (`company_name`, `company_trade_name`, `company_document`, `company_phone`, `company_address`, `company_city_state`) e `customers` por ID: dados originais reutilizados. Dados ausentes são identificados como não informados. Não existe tabela paralela de clientes.
- `GlassProjectStore`/`glass_projects_v1`: projeto salvo, revisão, modelo, medidas/unidades, quantidade e orçamento. Inalterados.
- `GlassCommercialDocument`: renderização comercial, prévia e impressão permanecem iguais. Seu único motor de PDF foi exposto como função interna com renderizador por página para reutilização nos documentos 07/08. O teste compara o orçamento com a implementação 10.401 byte a byte.
- `service_orders`: OS existente, não representa contrato. Não foi reutilizada de forma artificial. Não havia estrutura de contratos.
- `Database.Open`, conexões `PdvConnection`, transações e backup SQLite existente: infraestrutura compartilhada, inclusive a configuração de rede. Terminal sem schema preparado orienta abrir Contratos no servidor atualizado.

## Recibos

A lista consulta apenas ENTRADA positiva vinculada ao projeto. Identidade documental `VID-REC-{cash_movement_id}`, valor e data vêm do movimento; cliente vem do ID do vínculo. Saída, movimento excluído ou de outro projeto e projeto sem recebimento são bloqueados. Quando não há forma registrada, isso é explicitado. Se houver venda realmente vinculada, as formas existentes são identificadas sem inventar método para lançamento manual.

Não há tabela nova de recibos nem cópia de valores financeiros. Reimpressão lê novamente o lançamento existente. Gerar PDF/visualizar/imprimir não insere nenhum movimento. O documento declara somente o valor do recebimento escolhido, sem quitação dos demais valores.

## Contratos

Única extensão de schema: `glass_contract_revisions_v1`, aditiva e idempotente, com backup consistente `ANTES_VIDRA_CONTRATOS_V1_*.db` antes do DDL. Chave composta projeto/revisão; referência ao projeto e cliente originais; revisão do projeto, estado RASCUNHO/FINALIZADO, data UTC, payload documental e SHA-256 do conteúdo. Não há alteração de schema financeiro ou dos projetos.

Objeto, valor, condição de pagamento, prazo, instalação, responsabilidades e observações são editáveis. Campos sem informação ficam em branco, sem prazos ou obrigações inventados. Recarregar dados atuais é ação explícita. O documento preserva o snapshot comercial capturado, não passa a controlar o financeiro.

Todo salvamento acrescenta uma revisão; não atualiza o payload de revisão anterior. Finalizar exige ação explícita; editar e salvar após finalizado cria outra revisão. Concorrência usa revisão esperada e bloqueia sobreposição entre sessões. A prévia exige conteúdo salvo, reabre a revisão persistida e verifica seu hash. Não há assinatura digital ou garantia jurídica automática.

## Verificação

Teste Windows próprio: duas entradas 50/60 e saída20, documentos individuais, saída/projeto sem recebimento/outro projeto bloqueados, reimpressão sem duplicidade; contrato editado, rascunho/final/revisão posterior, concorrência, conteúdo/hash em novo processo, PDF A4. Verifica ausência de vendas, pagamentos, baixa de estoque e duplicação de clientes/movimentos. Executa também a regressão 10.401 do histórico financeiro e as suítes 01–06 de cada cliente antes de qualquer publicação.
