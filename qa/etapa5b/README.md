# ANGRA VIDROS QA005 — candidato de integração, sem publicação

Atualização da Etapa 5B: relatório completo em `organization/ETAPA5B-INTEGRACOES-20261115.md`. Homologação remota NÃO concluída; não liberar transbordo.

Base aprovada: PET 1.12, commit 2fd998403a5826bc665ee6fd33c6363cd75a2cfe.
Somente a branch qa/etapa5b-integrations-20261115. Nenhum feed é atualizado.

## Evidência e diagnóstico

- Baú: BAU-DE-OURO/HISTORICO-CONSOLIDADO.md, branch bau-de-ouro. Origem técnica: feature/angra-pdv-sync, commit acb72ed0d1025faa309efffa35a6a94f205fd7c7, mobile/vidraceiro/docs/PDV_SYNC_V3.md.
- Integração estruturada usa gateway Sites/Worker + R2, assinatura ECDSA P256, Windows DPAPI e AndroidKeyStore. Não usa o Firebase de licenciamento. Não foi alterada configuração Firebase.
- Gateway em produção foi consultado somente por metadados e logs. Fonte 99a1604875bf2ee86cb57333f81027317ca2037c, versão 18. A cópia em gateway/ serve exclusivamente aos testes locais com R2 em memória. Não há deploy, credencial ou acesso a registros reais neste workflow.
- Evidência Android relata UnknownHostException para o gateway antigo e vínculo PC-CASA. Os logs recentes do mesmo hostname mostram POST /sync/request-pdv, /sync/status e /sync/exchange com HTTP 200. Há uma requisição exchange cancelada. Isto confirma comunicação intermitente/recuperada; NÃO confirma DNS permanentemente quebrado nem prova a origem da falha de resolução no aparelho.
- Código Windows preservado em extensions/business/AngraPairingForm.cs, fonte 98915ae5a9eb02825d77f74bb179e0ce27d65bd9: o botão gera somente após aprovação. O polling Refresh atualiza status, mas não retoma Generate após voltar do navegador. A autorização do navegador é específica à identidade assinada; o aviso sozinho não comprova que foi autorizado o mesmo PC/usuário/ambiente QA. A identidade real do aparelho e do PC não foi lida nem substituída.
- Código Android: unlink exige resposta do servidor; uma falha deixa detaching=true e o tick retorna imediatamente sem nova tentativa. Só outra ação manual tenta concluir. As operações e os arquivos ficam preservados, mas o vínculo permanece bloqueado.

## Candidato

Windows: helper nativo retoma a consulta ao retornar à janela e, apenas após status aprovado do protocolo existente, chama o Generate original. Mantém guardas de administrador/servidor, assinatura, código e validade originais. Para após um código gerado e ao fechar. Patch binário sobre PET 1.12: altera o construtor da janela de integração e o ponto de customização PET para instalar os hooks QA (ANGRA e conclusão SpeedFood limitada ao emulador); adiciona recurso e ponte próprios, metadados PET 1.13. Compara todos os demais métodos e recursos, incluindo abertura e AJUDA, para garantir preservação. GLOBAL, host, bandeirinha e licença não são recompilados.

Android: nova tentativa de unlink a cada 30 segundos enquanto o pedido persistido aguarda confirmação; retomada após reinício do coordenador. Nunca informa revogação sem confirmação. Arquivo por tenant mantém cadastros, cinco pendências sintéticas, revisões e operationId. Não envia dados antigos ao novo PDV. Respostas atrasadas de foto são rejeitadas se o estado do vínculo mudou; caminho de foto é capturado antes do trabalho assíncrono.

APK de teste possui applicationId exclusivo br.com.lealinfo.vidraceiro.angraqa005, versão 0.3.7-qa005. É fixture isolada, não é atualização assinada do APK instalado e não acessa suas cinco pendências. A chave de assinatura do APK instalado e um backup privado do estado real ainda são necessários para uma atualização desse APK com preservação verificada. Não instalar este APK como substituto do app de trabalho.

## Testes e limites

Workflow etapa5b_integrations_qa.yml: contents:read, sem id-token, sem deploy, sem edição de feed. Artefatos candidatos apenas.

- Node: testes do Worker com chaves reais de teste e R2 em memória; autorização, isolamento, código, expiração, idempotência, conflito e foto. Caso adicional com cinco projetos sintéticos, medidas 2000 × 1500 mm, upload/download JPEG com hash, confirmação exclusiva do PDV e retry sem duplicação.
- Windows: helper compilado e executado com WinForms em runner Windows; transporte simulado na fixture. Testa autorização negada, rede indisponível, retorno autorizado, código único e encerramento do polling. Patcher verifica métodos e recursos do PET real. Não equivale a executar o PDV real com a licença do proprietário.
- Android: compilação integral da fonte original com overlay QA; instrumentação em emulador API29 com rede simulada. Testa falha DNS, persistência, reinício, retry, cinco pendências, fotos e restauração no tenant original. Não chama servidor real.

Pendente no ambiente real: correlacionar identidade DPAPI do PDV com o grant autorizado; observar DNS no Android; exportar backup privado de professional.json, pdv-sync-state.json, pdv-profiles/ e images/ antes de trocar vínculo; inventariar quais das cinco operações já foram aceitas pelo PC-CASA. Não migrar automaticamente operações PC-CASA para QA005 nem criar novos operationId para tentar reenviar ao mesmo tenant. Depois validar foto/medidas na Central do PDV e ausência de duplicações com os equipamentos reais.

Nenhum vínculo real foi revogado e nenhum projeto/foto/registro real foi apagado. Não publicado. A aprovação de publicação deverá ocorrer somente depois da revisão do candidato e dos bloqueios acima.
