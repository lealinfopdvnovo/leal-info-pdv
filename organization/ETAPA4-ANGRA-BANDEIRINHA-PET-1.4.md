# ANGRA VIDROS QA — bandeirinha, canal próprio e PET 1.4 — 14/11/2026

## Entrega validada
Código: 7a2ad0e3de667cbc17bc1b5a8488635126a8c278.
Publicação QA do pacote: 5a052985f1f0c3b5145edc00e03d2328682c967b.
Actions SUCCESS: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43659951700
Branch única: qa/angra-etapa4-setup-20261114. Sem merge em produção.

Arquivo: LEAL_INFO_ANGRA_QA_ATUALIZADOR.exe
Tamanho: 88.189.626 bytes.
SHA-256: 315b1b5a05de36a80c29d63a4e1354baf5552a1968f572fd55c5242994825d70.
Download direto: https://chatgpt.com/library?file_id=libfile_c55e50e542e48191acaefcbdc249dd33&account_id=personal
Artefato do instalador: 14139797359, ZIP SHA-256 a5dc7f372da314e6ba186d66ba6f2e691ef008af74dcf687fb5d3c467629836e.
Evidências: 14139443265, ZIP SHA-256 58e9d97e35da3d6a84936eb26b2ead3fd9a990499d982a1c772141027132fb2f.
Os ZIPs foram baixados, hashes recalculados e o hash do EXE comparado ao teste Windows.

## Operação para o proprietário
1. Fechar o PDV de teste e executar o arquivo de correção uma vez, no mesmo Windows/usuário do teste.
2. Abrir o atalho existente TESTE ANGRA (Etapa 4), acessar ATUALIZAR e clicar ATUALIZAR PET.
3. O PET passa de 1.3 para 1.4 e o PDV reinicia com vídeo e identidade ANGRA VIDROS.

O instalador habilita o canal e a bandeirinha no QA existente. Seu PET inicial continua sendo 005 / 1.3: o conteúdo visual/vídeo novo é recebido como PET 005 / 1.4 pelo atualizador, e não disfarçado sob o mesmo número.

## Bandeirinha e isolamento
Foi adaptado o componente nativo já existente em tools/UpdateNotifier/notifier.cpp na receita aprovada e seu UpdateNotifierHost, mantendo ícone, bandeja, menu, singleton, aviso do desktop e opção de início com Windows. Não foi criada outra bandeirinha. A recompilação foi necessária para retirar a rota fixa de produção dessa cópia QA.

A cópia QA aceita somente identidade 005, rota CLIENT e o feed da mesma branch QA. Seu notificador lê a versão PET do manifesto instalado; não compara o PET 1.4 com a versão GLOBAL 10.468. O menu abre a central original do PDV. O instalador inicia o agente para o usuário e registra apenas a chave de inicialização própria dessa instalação QA. Preferências do notificador ficam na raiz QA.

Canal: https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv/qa/angra-etapa4-setup-20261114/qa/etapa4-setup/channel/version.json
Pacote: ANGRA_005_PET_1_4.zip, SHA-256 1ee451ae66985863967212bcd973e840b7cfef1b92ba491b9b1d28d71b0a0480.
Feed público e pacote foram baixados após a execução final; SHA-256 e identidade/versão conferidos.

Não há consulta GLOBAL na central QA. O planejador, URL permitida e worker aceitam apenas PET 005. URLs de produção e identidades 001/002/003/004 foram recusadas nos testes. Não há fallback para produção.

## Identidade e personalização
- Licença de teste: identidade 005 / Standard, preservada.
- Manifesto e metadados da DLL: 005 / PET 1.4 / API 1.
- Compatibilidade: GLOBAL QA 10.468 até antes de 10.469.
- Assinatura, serial, plano, revisão, validade, integridade e contrato continuam validados. Chave pública original e fontes de licenciamento foram comparadas por hash no Windows.
- Título principal troca LOJA LEAL INFO por ANGRA VIDROS; rodapé mostra ANGRA VIDROS • PET V1.4; login mostra ANGRA / VIDROS e mantém os campos e autenticação verdadeiros.
- Dados e cadastro da empresa não são reescritos para realizar a troca visual.

O módulo usa a apresentação histórica preservada como base visual, com adaptação de procedência já existente no QA. A identidade de licença/contrato não é convertida para 001.

## Vídeo
MP4 aprovado preservado como animação base, com narração feminina pt-BR Francisca Neural:
“Bem-vindo à Angra Vidros. Na próxima tela, coloque seu usuário e sua senha.”

Duração final 8,042 s. Música reduzida para 16%, voz iniciada a 250 ms, fade-out audiovisual nos 0,6 s finais. Sem legendas ou login falso. MP4 com áudio AAC embutido no PET; nenhuma síntese ou serviço de voz é chamado pelo PDV em execução. A LIA não foi reintroduzida.

SHA-256 do vídeo: 3841b67fe8285afca01610a72f9b89f6ca69f41be85d92d05246f160458c85f9.
Extração no cache QA com verificação de hash, reprodução pelo WebView2 já utilizado no produto, recursos locais e restrição de rede. A apresentação antiga de abertura foi removida somente do fluxo QA para não aparecer outro vídeo entre a abertura ANGRA e o login verdadeiro.

O teste registrou Played=true, Ended=true, LastSeconds=8.042, áudio não silenciado/volume positivo e Error=null. Isso comprova o fluxo de mídia no Windows do runner; a qualidade percebida e a saída nas caixas de som do proprietário dependem da conferência física.

## Instalação sobre o QA anterior
O teste recuperou o instalador anterior já aprovado pelo seu hash, instalou-o e depois aplicou a correção sobre essa instalação exata. O instalador só aceita atualização do QA anterior conhecido: DLL 165f58c122a511a09d4ccd39465bdfddf9b935635d1bbd6ccc5b4da2c717184f e manifesto fd37bbd88ae30633b15215ff519f1d87c5a5a621e6e55605bd465ac3ba1cdd14. Destino alternativo, diretório desconhecido e repetição sobre a nova versão são recusados.

Backup dos binários/PET anteriores em .qa-bridge-backup. Raiz de dados/configuração/licença permanece %LOCALAPPDATA%\\LEAL-INFO-QA\\ANGRA-ETAPA4-20261114, sem apagar, renomear ou importar nova licença. Banco, usuários e configurações não entram no payload. O antigo QA sem CORRIGIDO também permaneceu intacto no ensaio.

## Testes efetivamente aprovados
- Instalação da correção sobre o QA anterior; backup e canários de dados preservados; destinos indevidos recusados.
- Agente nativo real em execução, configuração 005/CLIENT e registro HKCU de início com Windows apontando exclusivamente para a raiz QA.
- Planejador e staging do pacote real aprovados; URLs de produção e outros quatro clientes recusados.
- Pacote real PET 1.4 aplicado aos binários instalados pelo worker existente em modo de fixture isolada; núcleo preservado por hash.
- Licenças de fixture Standard 005 assinadas com chave efêmera, assinatura/serial/ativação/persistência DPAPI reais e recusa de chave ou serial incorretos. O parâmetro de chave de verificação já existente na API foi utilizado somente no probe; não houve troca da chave pública do EXE entregue.
- Identidade anterior 001, API divergente, hash adulterado e metadados incompatíveis recusados.
- Módulo instalado 005/1.4 carregado com as validações ativas; vídeo offline concluído; login nativo, autenticação da fixture, cabeçalho e rodapé conferidos.
- Sete cenários do worker: sucesso, hash incorreto, identidade incorreta, manifesto incorreto, estágio adulterado, interrupção com recuperação SYSTEM e falha de saúde. Núcleo, dados e licença sintéticos preservados por hash.
- Central verdadeira aberta contra o feed QA público: ATUALIZAR PET habilitado com disponível 1.4, GLOBAL desabilitado; screenshot e live-feed.json no artefato.

O teste da central observa a oferta real; a aplicação do pacote é verificada separadamente pelo worker. Não se afirma um clique completo com UAC, licença do proprietário e reinício autenticado no computador real. O início após reiniciar Windows também não foi observado: foram conferidos o registro e o processo real do agente.

## Limites e preservação
Confirmar no PC do proprietário: abertura após clicar ATUALIZAR, mesma licença/usuário/dados, áudio físico e início da bandeirinha no próximo logon do Windows. Não houve acesso remoto a esse computador, uso da licença real no runner ou envio da licença/chaves ao GitHub. Não é homologação de migração 10.467→10.468 nem de todas as operações comerciais.

GLOBAL de produção reconferido: 48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1.
Distribuição de produção reconferida: 6f641fc6941b573f94393a437191761243cfa69e.
Alterações limitadas à receita/workflow/ativos/canal QA e este registro. PETs canônicos dos demais clientes, feeds e instalações de produção preservados.

Histórico da validação: duas execuções iniciais foram canceladas durante o ajuste da espera pelo processo persistente do tray e preparação da fixture. A execução 43659335500 passou instalação/vídeo/contrato/rollback, mas falhou na espera curta pela primeira propagação do feed público; não foi declarada aprovação integral. A execução final acima concluiu também a verificação online e terminou SUCCESS.
