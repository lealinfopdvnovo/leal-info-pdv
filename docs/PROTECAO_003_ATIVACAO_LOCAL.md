# Proteção 003 — pronta para ativação local, sem publicação do feed
Data: 20/10/2026.
Fonte do ativador: e18798bb47f34c42b73007804fda176dfcbb812e.
Windows: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/39508203139.
Verificação do EXE portátil: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/39508898075.
Download: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/39508898075/artifacts/12304772379.
SHA256 EXE: 379ddf94876a545f2b8559154565206d9f6e813b710b851c7940e464a38958f5.
SHA256 artefato completo: ab61672b37010c00714ff708aa72e57b511b35fdd2cf6a5821c0e57e4b95f6a3.

## Resultado
O ativador independente não utiliza o aplicador defeituoso da 10.415. Valida licença assinada/ativa, serial e cliente 003, os três hashes da 10.415 e o pacote original 10.422. Antes de permitir qualquer substituição cria backup verificado, staging, ACL restrita e tarefa SYSTEM de recuperação periódica e na inicialização. O segundo botão verifica novamente a proteção e entrega a primeira troca ao mesmo worker transacional aprovado, que oculta o EXE antigo e publica o novo somente após trocar o conjunto completo.
A 10.422 já contém o atualizador interno corrigido para as próximas atualizações.
O ativador não escreve banco, licença ou configurações. Não é setup. Usar a pasta inteira extraída, com suas dependências.

## Evidências sem repetir testes aprovados
Cozinha/UI/pacote: 39504222643, reaproveitados.
19 cenários do worker: 39504754135, reaproveitados.
20 novos cenários de identidade/licença/integridade/backup/interrupção da preparação: passaram em 39506906458; esse run falhou somente no cenário seguinte, então ainda pendente.
Quatro cenários finais passaram em 39508203139: recuperação pelo disparo periódico SYSTEM real, interrupção durante troca de DLL, aplicação protegida em Program Files com saúde de host sintético e rollback automático após falha nativa de abertura.
A lógica protegida dos 20 primeiros permaneceu idêntica; a única diferença de Core.cs foi telemetria do PID do worker para aguardar seu encerramento real no teste.
39508898075 consolidou os 24 cenários sem repeti-los, verificou os bytes e abriu o EXE portátil real: o botão de preparação recusou instalação inexistente e a aplicação permaneceu desabilitada.
Todos os cinco feeds permaneceram idênticos.
Não houve ativação no PC real, queda física de energia, reboot físico ou homologação completa do PDV operacional. A saúde foi testada com host sintético.

## Procedimento mínimo no computador do 003
1. Fechar PDV no servidor e terminais.
2. Baixar o artefato e usar Extrair Tudo.
3. Abrir LealInfoProtecao003.exe dentro da pasta extraída, na mesma conta Windows do PDV; confirmar administrador.
4. Clicar em 1 — VERIFICAR E PREPARAR PROTEÇÃO.
5. Após PREPARAÇÃO VALIDADA, clicar em 2 — CONCLUIR TRANSIÇÃO PROTEGIDA. Essa primeira troca usa o ativador externo protegido, não ATUALIZAR da 10.415.
6. Confirmar por print a versão que abriu. Se houver recusa, enviar print; não improvisar comandos.
A etapa 1 sozinha não protege permanentemente o atualizador legado; a preparação pode expirar com cancelamento seguro. Não orientar uso do botão antigo após somente preparar.
Instalação esperada: Program Files/LEAL INFO PDV. Licença local/servidor 003 assinada e ativa; terminais são recusados.
Registros: .pdv-update/journal.json e .pdv-update/activation003-*/protection-proof.json.

## Publicação
003 continua 10.415 no feed; 001 continua 10.422; 002/004/GLOBAL continuam 10.408. Nada publicado neste trabalho.
O pacote 10.422 exclusivo do 003 está embarcado no ativador com aplicação condicionada à proteção local; não foi oferecido ao atualizador legado.
Antes de liberar o feed 003, confirmar a transição real e verificar qualquer terminal legado que também possa consumir esse feed.
