# ANGRA VIDROS — contrato de integração estruturada v2

Estado: **exportação JSON implementada; importação/sincronização com PDV pendente**. Não existe confirmação fictícia de entrega. A Vidraçaria Windows 10.403 e todos os seus canais permanecem intocados.

## Origem auditada

Android: commit real `2a2f4e611ec82802039bcadfce6fe9e73628c4c4` (0.1.5). Referências anexadas: quatro fotografias WhatsApp 2026-10-15 11.07.46 (sem sufixo, 1, 2) e 11.07.47. As referências mostram categorias, cores, ícones, navegação e organização; não contêm uma tela detalhada do editor 3D. Nenhum ativo, logotipo ou interface literal do concorrente foi incorporado.

PDV, somente leitura: `GlassProject.cs`, `GlassBox3DView.cs`, `Glass3DTechnicalOverlay.cs`, `GlassCutPlan.cs`, estruturas `GlassProjectStore`, `GlassQuote`, documentos e operações. O projeto legado do Windows possui um modelo fixo de janela de duas folhas. Essa restrição **não foi copiada** para a fundação mobile. A projeção ortográfica, yaw/pitch, ordenação de faces e zoom inspiram a adaptação Canvas; divisões somente quando informadas. Profundidade ausente = plano, não espessura inventada.

`GlassCutPacking.Calculate` usa ordenação estável por área/maior lado, separação de material, seleção por área livre/menor lado e partição guilhotinada disjunta; rotação global e individual, limites de 1000 peças e 1.000.000mm. Não é algoritmo industrial e desconsidera kerf/folgas. O módulo mobile usa `CutPacking.java`, adaptação decimal direta dessas mesmas regras, incluindo o mesmo desempate e a mesma partição, com testes de área, rotação, separação por material e ausência de sobreposição. Não se substitui por heurística diferente.

## Envelope exportado

`kind=angra.professional.project`, `schemaVersion=2`, `source=ANGRA VIDROS`, `exportedAt` epoch ms e `syncState=PENDING_PDV_IMPORT`.

- `project.id`: UUID local estável. `project.pdvId`: null até conciliação legítima.
- `client`: referência/rascunho com UUID local, nome, documento, contato, endereço e `pdvId=null`. Não equivale a ID do cadastro oficial. Recebedor futuro deve localizar/conciliar e pedir confirmação quando ambíguo; não duplicar automaticamente cliente por nome.
- `project.clientLocalId`: vínculo explícito, persistente, sem copiar cliente para cada projeto.
- `type`: texto extensível. `model`, `configuration`: informados pelo profissional.
- `measures[]`: label único, valor visual positivo com ponto/vírgula, unidade mm/cm/m e `mm` decimal canônico como string; exportação recalcula/valida mm. Nenhum float define fabricação.
- `materialLocalIds` e `materials[]`: dados fornecidos; `pdvId` opcional; nenhuma movimentação.
- `budget`: quantidade, preço por m², serviço, desconto e acréscimo monetários explícitos. Tela e documento consomem a mesma função `total`. Receptor deve validar e apresentar, sem gerar venda/recebimento automaticamente.
- `contract.revisions[]`: número, epoch de salvamento e conteúdo integral congelado; `contract.text` editável atual. Revisão antiga não é sobrescrita.
- `temperaSpecification`: texto do profissional, nenhuma regra técnica inferida.
- `visualPanels`: divisões visuais informadas, 1 a 20. Geometria 2D/3D derivada das medidas canônicas; imagem não é fonte de verdade.
- Foto: anexo local opcional. JSON exportado não inclui bytes nem screenshot. Envio futuro de anexo requer rota autenticada separada e consentimento operacional.

## Requisitos do receptor futuro (não implementado nesta etapa)

1. Autenticação e autorização da empresa/aparelho; HTTPS e limites de corpo. O transporte P-256 em `SecureGateway` e o backend Sites continuam preservados para avaliação futura, mas **não são chamados pelo novo fluxo**. A rota atual `/v1/projects` nega importação (409), portanto não se anuncia sincronização.
2. Idempotência por empresa + UUID de projeto + revisão; conflitos devem ser apresentados, sem sobrescrever silenciosamente projeto existente.
3. Backup e migração aditiva somente se necessários. Nenhuma alteração de schema/PDV agora.
4. Conciliar cliente e materiais com IDs oficiais antes de importar; não copiar banco/configuração/licença.
5. Orçamento, contrato e exportação jamais criam cash_movements, venda ou baixa de estoque.
6. Recibo apenas a partir de recebimento oficial real — não implementado no app.
7. Plano de Corte: `project.cut` contém chapa, unidades individuais e peças reais; posições são recalculadas deterministicamente pela regra adaptada. O receptor deve revalidar as dimensões, rotação e materiais; não importar como ordem de fabricação.

## Persistência e instalação

Rascunhos offline ficam em `professional.json`, escrita atômica Android `AtomicFile`, com restauração de backup `.bak`. Arquivos de fotos/rascunhos legados dentro do mesmo sandbox não são sobrescritos; importação por UUID é idempotente. JSON corrompido não é substituído silenciosamente.

A chave de assinatura privada das builds de teste anteriores não está disponível. A 0.2.0 utiliza pacote debug isolado `br.com.lealinfo.vidraceiro.profissional` para não desinstalar/sobrescrever 0.1.4/0.1.5 nem seus dados. Não consegue acessar automaticamente dados privados de outro pacote Android. O aplicativo anterior permanece instalado. Namespace e package de produção continuam intactos. Uma futura atualização no mesmo pacote exige a mesma chave de assinatura.

## Escopo funcional

Implementados: painel nativo, navegação/retorno, projetos genéricos, medidas individuais exatas, clientes/fornecedores/materiais como rascunhos locais, vínculo explícito, orçamento decimal, prévia/A4/PDF/impressão nativa, contrato com revisões, registro de especificações de têmpera, calculadora geométrica, 2D/3D determinístico, plano de corte geométrico com edição de peças, rotação e exportação JSON.

Preparados: financeiro oficial, estoque empresarial e sincronização/recepção PDV. Os cards identificam essa condição e mostram a explicação, não dados fictícios. Não há pagamentos locais, notas fiscais, assinatura digital ou regra de fabricante presumida.

Fluxo cancelado: MainActivity não possui geração por foto, autorização para IA, solicitação ao motor, exibição de imagem IA ou envio dependente de simulationId. Testes CI de geração e autorização do motor foram retirados. Código/recurso de splash, parser de medidas, câmera/normalização e infraestrutura segura reaproveitável foram preservados. O antigo serviço remoto não foi destruído nem republishado; o app novo não o aciona.

Nota da0.2.1: metadados `project.photoAttachment` identificam o JPEG permanente por UUID/projeto, MIME, tamanho, SHA256 e instante de vínculo. Exportação inclui manifesto, sem upload automático. A build de teste usa pacote isolado `.profissional021`; instalações anteriores e seus dados permanecem preservados.
