# Remoção do botão LIA — candidata isolada validada

Estado: implementação concluída na branch, com validação Windows isolada em 28/10/2026.

- Código testado: `a9af9aa8fe88174687588ccfc335cd4aaca040d9`.
- Execução: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/40690645429
- Cinco jobs Windows aprovados: GLOBAL e cópias de fontes 001/002/003/004.
- Backup: `archive/ui/botao-lia/`, hashes e recuperação por objetos Git verificados.

## Alteração implementada

A chamada de inicialização do listener de navegação da assistente foi retirada do MainForm da candidata. Os três geradores exclusivos do botão agora não criam controle, clique/teclado, temporizadores, canal de estados ou lançador da LIA. Seus originais permanecem arquivados fora da compilação.

`remove_button.py` retira o bloco reconhecido de candidatas já montadas, recusando variantes desconhecidas antes de escrever. Preserva cópia reversível do MainForm apenas no ambiente de teste. Mantém métodos compartilhados, política de licença, dependências e arquivos dos motores; não faz limpeza no computador do cliente.

**A animação de entrada foi preservada.** LoginForm.cs, SplashForm.cs e Assets não foram alterados. Tampouco houve alteração de banco, licença, estoque, configurações, arquivos PET, atualizador, feeds, Firebase ou APK.

## Evidências

6 testes estruturais; compilação antes/depois; ausência do botão após a transformação; mesmos controles operacionais e métodos; layout em três tamanhos solicitados à janela; instanciação de login/splash; teste financeiro existente com entrada +100, saída -40, saldo final 1260 e reabertura; atualização bem-sucedida, recuperação após interrupção e rejeição de SHA inválido. Os sentinelas de dados, licença, estoque, financeiro, configurações e PET ficaram intactos nos cenários descartáveis.

O primeiro erro de validação foi a conversão CRLF do checkout Windows: corrigida com atributo Git que conserva os bytes do arquivo histórico. Depois foram corrigidos somente o contexto da receita já publicada de 002/004 e o canal dos testes de rollback. O mecanismo do atualizador permaneceu inalterado.

## Limites e congelamento

As fontes fixadas são bases históricas, não a comprovação de equivalência com todas as versões atualmente instaladas (incluindo a Casa 10.461). Não distribuir esses artefatos como atualizações. A futura publicação exige reconciliar a retirada com a receita atual exata de cada cliente e autorização separada. A variante Gemini e quaisquer variantes posteriores não reconhecidas continuam bloqueadas pelo transformador até revisão dirigida.

Não houve homologação real de vendas, Cozinha, Vidraçaria ou áudio/animação no PC do usuário nesta etapa. Suas fontes foram preservadas; os testes listados acima são a evidência disponível. Não se anunciou aprovação real nem instalação.

Nada foi publicado, instalado ou mesclado na branch operacional. Implementação e evidências congeladas no commit/execução identificados acima.
