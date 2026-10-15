# ANGRA VIDROS — oficina profissional Android 0.2.1 (teste)

Estado atual: aplicativo nativo offline, remodulado a partir da 0.1.5, após o splash aprovado e preservado byte a byte. Não transforma fotos por IA/API. Não altera a Vidraçaria Windows 10.403, qualquer cliente, canal, licença ou banco do PDV.

## Disponível

- Painel com 12 cards e navegação adequada ao celular.
- Projetos genéricos, medidas independentes mm/cm/m com conversão física exata e persistência atômica.
- Clientes, fornecedores e materiais como rascunhos locais identificados; seleção explícita do cliente e referências por UUID.
- Orçamento decimal com valores informados, prévia, PDF A4 e impressão Android; cálculo único para tela/documento.
- Contratos editáveis com revisões preservadas; registro de especificações para têmpera sem regras presumidas.
- Calculadora geométrica e representação 2D/3D determinística com arraste, zoom, reset e divisões informadas.
- Plano de corte: adaptação direta de GlassCutPacking do PDV, BigDecimal, separação de material, rotação, peças editáveis, áreas e persistência. Simulação geométrica; não é ordem de fabricação.
- Exportação estruturada JSON para futura importação no PDV.

## Preparado, ainda não integrado

Financeiro oficial, estoque empresarial e receptor/sincronização do PDV. Não há pagamento, venda ou baixa automática local. Orçamento não é recebimento. Exportação não significa entrega ao PDV. Ver contrato completo em `docs/PDV_INTEGRATION_V2.md`.

O transporte HTTPS/P-256 e o gateway existente permanecem preservados para avaliação da futura integração, mas o novo aplicativo não chama o motor antigo. A pasta `gateway/` conserva a infraestrutura anterior; seus documentos e variáveis não são instruções de ativação da interface 0.2.0.

## Build e validação

Java 17, Gradle 8.9, Android SDK35, minSdk26. Workflow `app_vidraceiro_android.yml`: preservação PDV e arquivos congelados, testes de domínio Java/Node, lint, APK, emulador, três aberturas frias, controles reais, reabertura em processo novo, PDFs e evidências em diferentes proporções.

Execução técnica aprovada: GitHub Actions `38758448213`, código `d5988a2d414d9e9cb2dc515b3993e312386e66f6`. Homologação física pendente do usuário.

A instalação de teste usa sufixo `.profissional021` para preservar apps anteriores diante da indisponibilidade da chave privada de assinatura deles. Não migra automaticamente dados entre pacotes Android distintos. Não desinstale a versão anterior para testar esta. Namespace e applicationId de produção permanecem intactos.

Correções pontuais da0.2.1: ver `DELIVERY_0_2_1.md`. Tipos3D legíveis, foto vinculada, ícone adaptativo e home nativa de vidro; splash e PDV intactos.
