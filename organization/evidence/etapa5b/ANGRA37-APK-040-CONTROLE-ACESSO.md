# ANGRA VIDROS QA37 — APK 0.4.0 e PET 1.15 — controle individual de acesso

Data: 2026-11-15 (horário do proprietário). Escopo exclusivo QA005; Etapa 5B. Etapa 6 não iniciada.

## Entrega efetiva
- APK instalável, release sem depuração: ANGRA_VIDROS_QA37_0.4.0.apk, versão 0.4.0-qa37, código 21.
- Pacote br.com.lealinfo.vidraceiro.ambiente37. Atualiza o QA37 0.3.8 no mesmo pacote/assinatura; o aplicativo original paralelo permanece separado.
- Download: https://chatgpt.com/library?file_id=libfile_244281a887548191a1b4392e9ce2509d&account_id=personal
- SHA256 APK: cf34605b9d11e08b413b6a8277bfab294d0456db4e6961a43ad7d36cbe5e4e81.
- Certificado SHA256, igual ao APK QA37 0.3.8: 653029f2204600e198cb11e00fb805dfcfaf731b8815239bb92628ab427a863d.
- PET 1.15 publicado exclusivamente no feed QA005 pelo workflow 43778195000.
- Commit do feed QA: de851df1c370d351321922e663b249536bb2f5dc.
- Pacote PET: https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv/qa/angra-etapa4-setup-20261114/qa/etapa4-setup/channel/ANGRA_005_PET_1_15_QA37.zip
- SHA256 PET: 2608da03c3934324c5fb2e7ce40152c6962cdac2648324b81e6b43c4ab900600.
- Identidade005, API1, GLOBAL compatível >=10.468 e <10.469; condições de licença preservadas.

## Implementação
Proprietário com administração completa; funcionário com criação de orçamento, fotos/medidas/informações, desenho do próprio projeto e envio; gerente somente com permissões selecionadas individualmente. Funções não autorizadas são ocultadas e há guardas locais. O servidor valida autorização independentemente do APK.

Cadastro por matrícula fixa e nome completo. Ativação de uso único, válida por 24h, senha criada pelo funcionário. Matrícula excluída permanece reservada e não é reutilizável; dados comerciais não são apagados. Somente o proprietário cria usuários, altera perfis/permissões, bloqueia/desbloqueia, reemite ativação ou exclui/revoga. Nenhum usuário promove a si mesmo ou cria outro proprietário.

O primeiro proprietário depende da aprovação na conta proprietária já configurada no gateway, com confirmação de origem/CSRF e vínculo ao aparelho. Não existe promoção automática do primeiro aparelho conectado. A matrícula inicial é PROPRIETARIO; após aprovação, ativação com código vazio e senha própria. Não foi criada conta proprietária real durante os testes.

O gateway atribui autoria a partir da sessão, sobrescrevendo autoria forjada, e preserva a autoria original nas alterações. Consulta AUTORIA DOS ORÇAMENTOS disponível conforme histórico/relatórios. PET preserva o campo ao reconstruir o payload local, evitando diferenças artificiais na sincronização. Projetos legados sem autoria ficam reservados ao proprietário no APK.

Servidor: senha PBKDF2-SHA256 com 100000 iterações e sal aleatório, somente hash de sessão no servidor, vínculo de sessão a dispositivo/estabelecimento/revisão, validade de 8h. Alterar permissões/bloquear/excluir/reemitir ativação revoga sessões. Capacidades de foto também são revalidadas. Cinco senhas erradas bloqueiam a conta por 15min; há limite de tentativas por dispositivo.

APK: estado e JSON de projetos/clientes/perfis, além do token persistente, cifrados com AES-GCM e chave Android Keystore. Arquivos legados são lidos e migrados por gravação atômica. Fotos permanecem em armazenamento privado do aplicativo protegido pelo Android; não foi adicionada cifragem própria aos arquivos JPEG. Backup Android desabilitado, transporte HTTPS, MainActivity não exportada, captura de conteúdo protegido bloqueada e APK entregue não depurável.

A confirmação de sessão é consultada a cada 15s; o acesso protegido exige confirmação com menos de 25s. Bloqueio no servidor vale nas próximas requisições; a interface pode levar um ciclo para refletir a mudança. Sem rede, expira a confirmação e o acesso protegido é bloqueado. Não se promete apagar remotamente um aparelho offline.

O gateway continua exclusivo QA37, sem Firebase nem gateway antigo. Identidade, recursos visuais, abertura, áudio, animação AJUDA, licenciamento e bandeirinha do PET preservados. Financeiro/estoque mantêm a disponibilidade anterior: módulos oficiais no PDV, sem inventar módulo Android novo a partir de uma caixa de permissão.

## Testes comprovados
| Componente | Execução | Resultado e alcance |
|---|---|---|
| APK release final | https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43762805202 | Sucesso; 17 verificações em emulador Android29. Atualização sobre fixture0.3.8 com mesma assinatura temporária preserva arquivo; login inicial, perfis, filtros, editor fornecedor, autoria, cifragem/reabertura, adulteração, expiração e limpeza local aprovados. |
| Segurança do gateway | https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43762096329 | 7 grupos aprovados em armazenamento QA simulado com criptografia real: proprietário/CSRF, ativação única, privilégio indevido, revogações/capacidades, matrícula não reutilizável, autoria, isolamento, expiração, edição após recebimento e tentativas abusivas. |
| Receptor Windows | https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43761367253 | Compilação, helper e ponte real do PET executados no Windows; autoria preservada, payload nulo/cliente não alterados. |
| Escopo do PET | Mesmo run Windows | Todos os demais métodos originais e recursos embutidos idênticos à base aprovada; mídia de abertura preservada. Core e bandeirinha não recompilados. |
| Assinatura final | Verificação local apksigner | Assinatura válida e certificado idêntico ao QA37 0.3.8. Todos os itens não-META-INF do ZIP idênticos ao release testado no emulador; apenas assinatura substituída pela chave QA preservada. |
| Publicação PET | https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43778195000 | Sucesso. Comparação do feed 6607edf... para de851df... altera somente version.json e novo pacote QA005. |

A compilação 43762558126 falhou por separação de instruções Groovy na configuração release; corrigida no commit final e sucedida por 43762805202. Não se trata de teste funcional aprovado. Receptor Windows e gateway aprovados foram reaproveitados nos builds Android finais.

## Código, hospedagem e proteção
- Branch do código: qa/angra37-access-20261115, commit final e9cd7e12a61b52b75c932f0d7b092a0bf4d7c436.
- PR laboratório #39 (draft): https://github.com/lealinfopdvnovo/leal-info-pdv/pull/39 — não mesclar em GLOBAL.
- Branch de entrega isolada: qa/angra37-access-delivery-20261115, commit41740967f8f07a2ae72bac216d25bd5144a33048.
- Gateway fonte: a7e3c3a522353802bd947b88136b251e5c1557ea; versão salva3, deployment appgdep_6afa204d25248191806e701ca315493c com sucesso.
- URL QA: https://angra-vidros-qa37.adriansantosangra.chatgpt.site.
- GLOBAL permanece 48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1.
- Feed produção permanece 6f641fc6941b573f94393a437191761243cfa69e.
- Nenhuma alteração no ambiente34, outros clientes, Firebase, Speed Food ou Alerta. Nenhum backup real iniciado.

## Pendente — NÃO HOMOLOGADO nos equipamentos reais
A autorização inicial do proprietário, ativação de funcionário em segundo celular, bloqueio com APK aberto, pareamento persistente, foto/medidas/serviço/modelo/imagem e recebimento no PDV real ainda precisam de teste físico. Não foi declarada homologação ponta a ponta nem conclusão integral da Etapa5B.

## Instruções mínimas
1. PDV ANGRA QA37: ATUALIZAR PET para1.15.
2. Instalar APK0.4.0 sobre o QA37 0.3.8, sem desinstalar.
3. Conectar ao PDV se necessário; autorizar primeiro proprietário na conta proprietária do gateway; voltar e ativar PROPRIETARIO, código vazio, senha própria.
4. GERENCIAR FUNCIONÁRIOS: cadastrar matrícula/nome/perfil; pessoa ativa no próprio aparelho com código de uso único e senha própria.
5. Executar teste real de envio e confirmar autoria/recebimento antes de homologar.
