# LEAL INFO — Diagnóstico portátil 1.0

Aplicativo Windows 10/11 x64 independente. Não instala nem atualiza o PDV e não precisa de .NET instalado. Código isolado em qa/pdv-diagnostics, excluído da compilação do PDV. Nenhum canal de atualização é alterado.

## Uso

1. Abra LEAL_INFO_DIAGNOSTICO.exe, escolha o processo e, opcionalmente, a pasta do PDV.
2. Clique em INICIAR ANÁLISE. Use o PDV normalmente, marcando Repouso, Venda, Produtos ou Cozinha.
3. Clique em ENCERRAR COLETA ou aguarde o tempo selecionado.
4. SALVAR RELATÓRIO grava HTML + JSON em uma pasta escolhida fora da instalação do PDV. O HTML abre no navegador e pode ser impresso.

Colete alguns minutos de repouso e dos fluxos que apresentam lentidão. A ferramenta não realiza vendas ou impressão e não altera banco, licença, configurações, arquivos ou prioridades do processo monitorado. Não pede administração. Se o Windows negar acesso, informa a limitação.

## Medições e limites

CPU normalizada pelos processadores lógicos, RAM física disponível, memória privada/conjunto de trabalho, E/S lógica cumulativa do processo e conexões TCP IPv4/IPv6 estabelecidas pertencentes ao PID. Tráfego em bytes pertence aos adaptadores ativos do computador; não é atribuído ao PDV nem ao Firebase. Não coleta endpoints, conteúdo de rede, credenciais ou conteúdo de bancos/licenças/configurações. O inventário observa metadados, ignora pontos de nova análise/junções e é limitado a 50 mil entradas e cerca de 20 segundos de enumeração; acessos individuais lentos podem causar relatório parcial. Não confirma duplicação por tamanho. Não identifica SSD/HDD nem mede latência física do disco.

Não mede automaticamente tempo de abertura, resposta das telas ou vazamento de memória. Marcas de etapa são informadas pelo operador. As recomendações são triagem, não autorização de remoção. A ferramenta também consome recursos e seu impacto deve ser considerado.

Dados permanecem locais. Revise os nomes de arquivos antes de compartilhar o relatório. O relatório não é enviado automaticamente. O runtime do aplicativo portátil pode usar o cache temporário do próprio Windows; não grava na pasta do PDV.
