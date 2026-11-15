# ANGRA VIDROS — integração automática V3 (candidato de teste)

Base auditada: Android 0.2.4, commit e1101c59; PDV 001/003 10.403, fontes 3dfab93e / d1525b64. A branch client/001 estava desatualizada e não foi usada. Os manifestos publicados não foram modificados. Esta implementação não publica atualização do PDV.

## Antes e depois

Antes: AtomicFile professional.json no Android, UUID local e exportação manual. Nenhuma comunicação dos clientes do SQLite para o app; por isso BOB não aparecia. O JSON de contingência permanece. Agora: serviço periódico no PDV servidor e coordenador no app aberto/retomado, ambos no HTTPS Sites já existente. Não há promessa de serviço Android em segundo plano com o app encerrado. A fila sobrevive ao encerramento e retoma automaticamente na próxima abertura.

## Infraestrutura reutilizada

- SQLite original via Database.Open(); customers, suppliers, products e glass_projects_v1. Nenhum banco paralelo.
- Projeto Sites existente, Worker JavaScript e R2 já vinculado: https://angra-vidros-gateway.classyglobe9.chatgpt.site.
- Identidade Android P-256 já existente, AndroidKeyStore. Identidade própria do conector Windows protegida por DPAPI CurrentUser.
- Cadastro e cálculo originais do PDV, campos e design system aprovados do Android, foto JPEG por UUID.
- ViaCEP já usado por DeliveryAddress.LookupAsync no PDV. O Android usa o mesmo serviço, com debounce, timeouts, proteção contra resposta atrasada e entrada manual sempre disponível.
- Não usa Firebase/licenciamento, projeto novo-91da7436, SQL remoto público ou API de imagens.

## Entidades e correspondência

| Entidade | Direção | Regra |
|---|---|---|
| Clientes | Ambos | customers real; ID numérico oficial + UUID estável por instalação |
| Fornecedores | Ambos | suppliers real; mesma estratégia |
| Produtos/materiais oficiais | PDV → app | products real; nome/código/categoria/preço/estoque/ativo; somente consulta no app |
| Materiais de campo legados | Dentro do projeto | Descrição preservada em materialDrafts; não vira produto automaticamente |
| Projetos | Ambos | Registro original glass_projects_v1, formato aditivo 2 para projeto genérico; não converte box em janela |
| Orçamento | Dentro do projeto | Quantidade e valores decimais canônicos; cálculo original; sem pagamentos |
| Plano de corte | Dentro do projeto | Chapa, unidades, peças, quantidades e rotação; dados originais preservados; algoritmo intocado |
| Foto | Ambos, transferência binária | JPEG autenticado, UUID, tamanho e SHA-256; nunca caminho Android nem base64 no JSON |
| Calculadora | Local | Não existe dado compartilhável obrigatório |
| Financeiro, vendas, estoque movimentado, recibos | Não sincronizados | Criar orçamento/projeto não autoriza operação financeira ou baixa de estoque |

Projeto genérico recebido possui visualização própria mínima no PDV para preservar tipo/modelo/medidas/orçamento/foto e editar nome/observações; não se faz passar pelo renderizador técnico de janela. Projetos compatíveis com o modelo original continuam na Central original. Textos e dados adicionais do app são mantidos no payload estruturado. Não se infere fabricação, geometria técnica, ferragens ou pagamento.

## Segurança e identidade

Autorização inicial do PDV exige administrador local e confirmação do proprietário autenticado do Site. Em seguida o PDV gera código aleatório de uso único, válido dez minutos, para parear o celular. A empresa/instalação é vinculada à identidade autorizada, não a texto digitado ou ID de cliente confiado ao APK. Aparelho pareado não muda de empresa silenciosamente. Cada requisição assina rota, corpo, horário e nonce; nonce repetido é recusado. Nenhum segredo fixo administrativo no APK ou Git.

CI usa GitHub OIDC verificado por JWKS, limitado a esta branch/workflow e namespaces test-<run_id> temporários. Essa autorização não concede acesso a namespace de produção. Logs não incluem JWT, chave privada ou credenciais. Artefatos de teste não incluem identidade privada/configuração DPAPI.

## Persistência, offline e conflitos

Android: AtomicFile pdv-sync-state.json, junto do professional.json existente, mantém baseline/revisão/outbox com operationId antes da rede. Windows: única tabela aditiva angra_sync_links_v1, metadados e fila no SQLite existente. Backup consistente antes da primeira migração; criação idempotente. Identidade local e inserção de cliente/projeto entram na mesma transação para impedir duplicação após interrupção.

Retry preserva UUID/operação e não insere outra entidade. Escrita remota exige revisão esperada e CAS no R2. Conflito preserva local/remoto para escolha explícita; não há último escritor automático. Revisão do projeto SQLite também é verificada. Registros excluídos não são recriados silenciosamente: exclusão remota não é propagada nesta versão e requer revisão.

Documento já existente no PDV não cria outro cliente automaticamente. A integração informa o conflito e permite vincular explicitamente ao cadastro original. Aliases de protocolo preservam referências antigas do projeto, mas a lista do app apresenta uma vez o mesmo ID oficial.

Fotos via manifesto assinado e URL de capacidade aleatória de dois minutos, no mesmo HTTPS. Tamanho/hash/JPEG verificados antes de gravação atômica. Foto pendente não é anunciada como transferida. Falha de rede preserva o JPEG local.

## Preservação

Home, InteriorVisuals, imagens/ícone, splash, fade-in/barra, PhotoAttachment, ProjectDrawing, Measures, Commercial, ProfessionalStore e algoritmos de corte permanecem sem alteração de regras. MainForm.cs não é copiado entre 001/003: a particularidade de dimensionamento dos atalhos do 001 e a implementação própria do 003 são preservadas. Sem alteração de licenças, dados do cliente ou canais do updater.

## Teste físico após validação técnica

1. Usar somente candidato de teste do cliente correto; fechar PDV anterior e fazer backup. Não substituir banco/licença/configurações com arquivos de outro cliente.
2. No PDV servidor: Central de Vidraçaria → Integração com app ANGRA VIDROS → Conectar/autorizar este PDV. Confirmar a autorização de segurança do proprietário no navegador. Gerar código do celular.
3. Instalar o APK compatível sobre 0.2.4; no celular abrir Integração com o PDV e informar esse código uma vez.
4. Com ambos abertos e internet, aguardar sincronização automática. Pesquisar BOB no app; cadastrar CLIENTE TESTE APP e confirmar no PDV. Editar telefone e conferir atualização sem duplicar.
5. Criar box de 2 m × 2 m, vincular cliente, fotografar, salvar; conferir no PDV tipo, dimensões, cliente, orçamento e foto. Não esperar venda, recebimento ou baixa de estoque.
6. Sem rede no celular, salvar alteração; fechar/reabrir; reconectar e conferir envio único. Reiniciar também o PDV e conferir persistência.
7. Editar o mesmo cadastro nos dois lados antes de sincronizar e revisar as duas versões na área de conflitos. Não autorizar sobrescrita sem conferir.

Homologação física não é declarada pelo build/CI. Resultados finais e hashes serão registrados depois da execução concluída.
