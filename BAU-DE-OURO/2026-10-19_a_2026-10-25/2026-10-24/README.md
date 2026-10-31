## BACKUP DE CONTINUIDADE — CONVERSA DE 24/10/2026 (registrado a pedido do proprietário)

**Natureza:** resumo fiel das decisões e preocupações desta conversa; não é backup literal de todas as mensagens nem comprovação de implementação. Preservar registros anteriores desta issue.

### Método de trabalho obrigatório
- O proprietário relatou desgaste e perda de confiança por alterações graves, promessas sem evidências, retrabalho e consumo de cota do Work. A responsabilidade comercial e possível responsabilidade civil por interrupções de clientes recaem sobre sua empresa. Não minimizar esse risco.
- **CONVERSAR → PLANEJAR → AUTORIZAR → EXECUTAR → TESTAR → CONGELAR.** Não transformar discussão em autorização para publicar. Antes de sugerir nova ferramenta ou sistema, **verificar o que já existe**, reaproveitar infraestrutura, escolher menor alteração e avaliar riscos/cota.
- Limite de repetição: primeira tentativa; segunda somente após diagnóstico e mudança de abordagem. Se continuar falhando, parar, relatar bloqueio objetivo e preservar cota para suporte emergencial. Não garantir percentuais de consumo.
- **GLOBAL é intocável como referência, não permanentemente bloqueado:** mudanças globais só mediante autorização explícita. Erro PET: comparar trecho com GLOBAL, corrigir minimamente apenas PET afetado; preservar os outros clientes. Proibir alteração de GLOBAL disfarçada de PET. Sucesso exige evidência real (compilar ≠ publicar ≠ instalar/testar).
- O usuário pediu que a IA consulte o **Baú de Ouro** antes de propor mudanças e não recomece do zero. A conversa tem tom afetivo/humorístico; placar simbólico Adriano 2 × 0 IA, pela lembrança do Android Studio e do Baú.

### Projeto planejado: TESTE DE SAÚDE do PDV (NÃO IMPLEMENTADO)
- Após atualização, verificar inicialização, tela principal/função essencial sem venda, banco e componentes críticos. Preservar cópia local íntegra da última versão comprovadamente estável.
- Se falhar, alerta visual amarelo com **CONTINUAR** / **RESTAURAR AGORA**; rollback só mediante decisão humana, preservando banco, licença, configurações e PET. Se o PDV não abrir, bandeirinha/worker externo deve mostrar alerta e recuperar sem depender do executável quebrado.
- Log com cliente, versão, horário, componente e falha, envio seguro à central/Firebase/GitHub, sem credenciais ou dados sensíveis. Central avisa proprietário e disponibiliza número de ocorrência para Work consultar, comparar GLOBAL, corrigir PET, validar e publicar sob autorização. Fila offline e confirmação de recuperação desejadas.
- Planejamento em duas etapas: (1) implementar, testar isolado e publicar só 003; (2) testar no Windows real com falha controlada e rollback. **Nenhuma publicação do Teste de Saúde comprovada nesta conversa**; o Work respondeu ETAPA 1 BLOQUEADA por ter recebido só controle de etapas. Usuário NÃO enviou o prompt grande posterior; decidiu preservar cerca de 40% da cota Work para clientes. Não iniciar sem nova decisão.

### Projeto novo: LEAL INFO ALERTA — Android (EM ANDAMENTO LOCAL)
- Ideia do proprietário: aplicativo **exclusivo do seu celular**, mínimo, para receber notificações de falhas dos clientes, com Firebase Cloud Messaging gratuito para mensagens. WhatsApp automatizado fica fora enquanto exigir custos; técnicos/contas extras e interface premium ficam para depois.
- Decisão final: primeiro criar **APK vazio**, uma tela, nome LEAL INFO ALERTA, sem Firebase, notificações, login ou conexão com PDV. Só depois implementar pequenos incrementos.
- Tentativa de terceirizar APK a outra IA não deu certo; proprietário lembrou que **já tem Android Studio no computador**. O Android Studio Koala abriu, projeto antigo SpeedFood Rastreamento não deve ser alterado.
- **Projeto novo já criado localmente no Android Studio:** New Project → Empty Activity, nome **Leal Info Alerta**, package sugerido `com.lealinfo.alerta`, Minimum SDK API 24 (Android 7 Nougat). MainActivity.kt aberto; Gradle estava importando. **APK ainda não foi gerado/instalado nem testado nesta conversa.** Próximo passo: aguardar sincronização Gradle e orientar compilação do APK; evitar Work, custos e alterações no PDV.
- Ferramentas existentes a considerar: Android Studio, GitHub, Firebase, bandeirinha updater, PDV WinForms e PETs. O usuário quer assistência que recupere contexto e proponha atalhos existentes, não mais trabalho para ele.

**Status:** registro de decisões e continuidade; sem alteração de código, clientes ou GLOBAL por este backup.
