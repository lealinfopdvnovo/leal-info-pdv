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

As fontes fixadas são bases históricas, não a comprovação de equivalência com todas as versões atualmente instaladas (incluindo a Casa 10.461). Não distribuir esses artefatos como atualizações. A futura publicação exige reconciliar a retirada com a receita atual exata de cada cliente e autorização separada. A variante Gemini da Casa10.461 foi acrescentada como padrão exato e validada separadamente pela receita publicada. Quaisquer variantes posteriores não reconhecidas continuam bloqueadas pelo transformador até revisão dirigida.

Não houve homologação real de vendas, Cozinha, Vidraçaria ou áudio/animação no PC do usuário nesta etapa. Suas fontes foram preservadas; os testes listados acima são a evidência disponível. Não se anunciou aprovação real nem instalação.

Nada foi publicado, instalado ou mesclado na branch operacional. Implementação e evidências congeladas no commit/execução identificados acima.

## Validação adicional da Casa10.461 atual — concluída

Código: `6d3a84f5d5e1aae378092c8db66c9f4c3dc2351b`.
Execução aprovada: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/40691698125

O pacote público10.461 foi recuperado e conferido pelo SHA-256 `d0e78ae6e9baef1aad05b4ce4847d156940cd8a9322a7d8b909aa2f0ac1a58fc`. Sua receita fixada em `be7c9adb6ca04b88637eb323d96b344a660d07a5` foi recomposta, reutilizando payload de voz e notificador incorporados, sem modificar esses componentes nem chamar IA. Isto comprova a origem da receita/payload; não se anunciou igualdade binária entre recompilações.

Compilação antes/depois, botão ausente, mesmos controles/métodos operacionais, login/splash, fonte e assets da animação intactos, teste financeiro e três cenários de atualização/recuperação aprovados no Windows isolado. O ramo anterior de cinco bases foi reaproveitado sem repetir os testes já aprovados.

Artefato de evidência: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/40691698125/artifacts/12816559465
SHA-256 do ZIP de evidência: `1fa7289b05034ff657f14af220763cfc5e16f82575ea426d972b351223c3a1ec`.

A candidata mantém a versão da base e NÃO é uma atualização publicada. Não há instalação, incremento de versão comercial, promoção GLOBAL, alteração de feed ou homologação real. Antes de futura entrega autorizada, definir versão crescente e validar a distribuição exata; não utilizar artefatos históricos nos clientes.
