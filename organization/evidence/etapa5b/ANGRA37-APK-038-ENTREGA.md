# ANGRA VIDROS — entrega QA37

## Entrega executada — novo ANGRA37 e APK instalável separado (15/11/2026)

**Não é recuperação do vínculo 003. Não depende do servidor/PC34.** Nova autorização permitiu gateway próprio, reconfiguração local e APK. Escopo de produto restrito ao ANGRA QA.

### Infraestrutura e código
- Novo Site: appgprj_6afa12ab4da88191b8432f19c5253973.
- Gateway: https://angra-vidros-qa37.adriansantosangra.chatgpt.site ; R2 BUCKET próprio, sem cópia do antigo.
- Fonte do gateway 0e4b13a7625dc730edc09c397bec27498a880ef3; versão Sites 1; implantação appgdep_6afa148eb8e481919cee7f6d2423555d SUCCEEDED.
- Endpoint raiz declara environment=QA37/client=005. Assinaturas P256, autorização do proprietário, anti-replay, isolamento tenant e SHA256 de fotos reaproveitados. Rotas Admin/CI legadas bloqueadas no novo serviço. App pareado com PDV autorizado recebe aprovação para o recurso de imagem no próprio gateway, sem usar o anterior.
- Branch Android QA: qa/angra37-reconfigure-20261115; fonte funcional 1626fb62..., ajuste de build paralelo a9ac15d..., correção do teste Android a4bc7938b05c3295e9249c0278540d0cc59bc065. PR38 rascunho, NÃO MERGEAR em branch antiga. O commit 0bf19a18... resolve apenas a ancestralidade da PR de laboratório mantendo a árvore aprovada fixada; não houve merge em produção.
- Android: botão RECONFIGURAR CONEXÃO; operação local não espera servidor antigo. Arquivo por perfil, nova workspace, journal de reset atômico e retomada após interrupção. Perfil sem environment QA37 é bloqueado antes de exchange. Callback do coordenador antigo é descartado após fechamento. Perfil sem tenant também é arquivado antes de reset. Nenhum envio automático de pendências antigas.
- MainActivity comparada: todo conteúdo fora da tela integration() idêntico. Recursos visuais comparados byte a byte no Actions.

### Limite decisivo de assinatura e entrega
O APK aprovado 0.3.6 é br.com.lealinfo.vidraceiro.profissional022, code18, certificado SHA256 2ee54920efe7042320f90814e45fd10997f9deb28e213ee92a8955b3e00ffac7. O cache de assinatura correspondente não foi encontrado no Actions. Três cópias privadas debug.keystore encontradas têm certificado 69c5db... (Alerta); NÃO foram usadas nem alteradas.
**Atualização in-place do APK original NÃO entregue:** depende da chave original. Nenhuma desinstalação, limpeza ou substituição incompatível foi proposta.

Para cumprir a compilação independente, foi entregue APK QA37 **separado**, instalável em paralelo:
- Versão 0.3.8-qa37/code20.
- Pacote br.com.lealinfo.vidraceiro.ambiente37.
- SHA256 APK final: 51dde53bb0b13c0ba8c9142f0fc51e8e64621534fcc5024e8138575c2480d9e7.
- Certificado QA37 exclusivo: 653029f2204600e198cb11e00fb805dfcfaf731b8815239bb92628ab427a863d.
- Material de assinatura QA37 retido privadamente para versões futuras; nunca incluído em repositório/APK/log público.
- APK final re-assinado localmente; verificação apksigner PASS. Todos os arquivos de payload fora de META-INF comparados idênticos ao APK exercitado no emulador. O APK final re-assinado não foi novamente instalado em aparelho físico.
- Arquivo entregue: ANGRA_VIDROS_QA37_0.3.8.apk; entrega persistente https://chatgpt.com/library?file_id=libfile_3298814e83648191b79e1d6e4a4d0056&account_id=personal .
- O app antigo continua instalado com fotos/projetos/dados privados intactos; **não houve migração desses dados para o pacote QA37**. Nenhum deles será enviado automaticamente. Arquivamento/reconfiguração foram comprovados com dados sintéticos dentro do pacote QA37, não com leitura do celular do proprietário.

### Receptor PDV QA005
- Compilação Windows/Patcher no Actions 43757868194: job receiver SUCCESS. APK desse run falhou por cache ausente, não confundir conclusão global com sucesso de receiver.
- PET 1.14 parte do pacote aprovado 1.12; Patcher compara todos os métodos/recursos não relacionados, inclusive mídia. Só origem ANGRA e continuação de autorização; helper não inclui o hook Speed Food.
- Novo workflow de entrega, branch qa/angra37-delivery-20261115, commit ba375c4b50b110c85cb53187c347ac6454e836fc, Actions 43759023437 SUCCESS.
- Publicado exclusivamente no feed QA005 da branch qa/angra-etapa4-setup-20261114, commit 6607edf8e3cb551743f3b9bfe9715d8000a2c116, pai 2fd9984.
- Diff de publicação CONFIRMADO: somente qa/etapa4-setup/channel/version.json e ANGRA_005_PET_1_14_QA37.zip.
- Feed TargetClientCode=005, PET1.14, MinGlobal=10.468/MaxExclusive=10.469; SHA256 pacote 3fb46113ebed5d84801fe8f390b5b35298cd05ace7846b1e1d2a11dcfb67a8a0. Manifesto interno mantém Identity005, limites/API e SHA256 DLL atualizado.
- Disponível por ATUALIZAR PET no PDV QA. **Instalação efetiva desse PET no PC do proprietário ainda NÃO observada.** Sem reinstalação de setup.

### Testes novos e resultado
- Gateway local: 3 testes de fronteira novos PASS; pacote Worker ESM validado.
- Android Actions 43758573125 SUCCESS, commit a4bc7938... . 11 verificações: pacote isolado; retenção dos arquivos em atualização de fixture QA; quarentena do tenant antigo; reset com sync ocupada sem contato legado; 5 projetos e operationIds arquivados; foto idêntica; dados antigos não transferidos para nova workspace; restart; recuperação de reset interrompido; endpoint novo.
- A fixture de atualização é compilada da fonte0.3.6 com package QA37 e chave descartável de laboratório; NÃO comprova atualização do APK original com seu certificado.
- HTTP real do gateway novo validado pelo Actions e pelo navegador; resposta environment QA37/client005. A requisição Python do executor recebeu 403/1010; navegador e curl do Actions receberam resposta correta. Não inferir indisponibilidade universal.
- Emulador API29, não celular físico. Compilação receiver/manifesto/recursos não equivale a operação real do PDV.
- **PENDENTE:** atualizar PET no PC QA; gerar/autorizar código nesse PDV; instalar/abrir app QA37 e parear; enviar projeto real de teste com foto/medidas/serviço/modelo/detalhes/imagem; confirmar recebimento/ID/arquivo no PDV; reiniciar ambos; verificar persistência e não duplicação. Geração de imagem contra provedor externo e foto fim a fim não executadas nesta entrega. Não declarar homologação ponta a ponta.

### Preservação e próximo passo
GLOBAL channel/global permanece 48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1; feed de produção leal-info-pdv-updates/main permanece 6f641fc6941b573f94393a437191761243cfa69e. Casa/Loja/outros PETs, Speed Food, Alerta/Firebase/FCM e ambiente34 não alterados. Gateway antigo não alterado nem chamado para revogação. Sem backup real, migração geral ou Etapa6. Testes antigos aprovados não reexecutados; novas tentativas corrigiram apenas falhas de compilação do teste e a indisponibilidade da assinatura original.

Instrução mínima: PDV ANGRA QA → ATUALIZAR PET1.14; instalar APK QA37 em paralelo sem apagar o antigo; abrir o novo APK, conectar com código do PDV Angra e enviar teste. Etapa5B continua parcialmente pendente da homologação física. Limitação de atualização in-place permanece aberta até recuperar a assinatura original.
