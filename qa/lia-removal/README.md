# Remoção do botão LIA — preparação isolada

## Escopo e estado

PENDENTE de compilação e regressão em Windows. Não pronta para homologação.
A candidata desativa a chamada de inicialização de navegação LIA no MainForm e os três geradores exclusivos do botão. Um workflow novo de validação tem somente contents:read; os workflows existentes e feeds permanecem intactos. Sem publicação ou produção.
Base do controlador: release/lia-casa-10-458, preservada em manifest.json.
Não usar esta base antiga como atualização de clientes.

O botão não existe diretamente no MainForm da base organizada consultada:
é injetado pelos scripts históricos na montagem das candidatas. Por isso,
não se substituiu a aplicação por uma base mais antiga para retirar o botão.

O transformador recebe exclusivamente uma cópia já recomposta da versão de
cada cliente. A cópia deve conter `.lia-removal-isolated` com o conteúdo
`ISOLATED-NOT-PRODUCTION`. Não executar em instalações reais.

Remove o bloco conhecido de criação do botão, clique, teclado, pintura,
timer, canal de estados e encerramento da assistente, mais a chamada de
inicialização do listener de navegação. Mantém os métodos de navegação,
licenciamento e dependências para não excluir funções compartilhadas.
Variantes desconhecidas ou referências fora do bloco são rejeitadas.
O arquivo original completo é guardado apenas na candidata local para reversão;
não enviar essa cópia automaticamente ao GitHub.

## Verificação disponível

`python -m unittest discover -s qa/lia-removal -v`

Testes estruturais Python: arquivo histórico e recuperação via objetos Git,
três terminadores históricos, LF/CRLF, BOM, rejeição de código compartilhado,
rejeição de variante desconhecida, proteção de arquivos e reversão byte a byte.
Fixtures estruturais não comprovam compilação, interface Windows nem vendas.

## Próximo gate

Recompor a candidata atual exata de cada canal com suas receitas existentes;
aplicar somente após reconhecer o bloco completo, compilar e executar regressão
Windows, inclusive login/animação, módulos comerciais e atualização/rollback.
A versão 10.461/Gemini pode ter variantes adicionais: não presumir compatibilidade.
Este ambiente não possui dotnet, PowerShell ou Windows GUI disponíveis.
Nenhum workflow foi alterado ou disparado para contornar o limite autorizado.

Sem publicação, mudança de versão, instalador, acesso a banco, licença,
chaves, Firebase, APK ou configuração de cliente. A futura distribuição
continua dependendo de autorização separada.

## Continuação autorizada

A remoção foi implementada no MainForm da candidata e nos três geradores; a animação de entrada não é alterada. O workflow novo compila cópias de fontes fixadas GLOBAL/001/002/003/004 antes/depois da transformação e verifica login, splash, controles operacionais, métodos, hashes das demais fontes e recuperação do aplicador existente. Estes commits fixados são bases históricas, não reconstruções garantidas das versões atualmente instaladas. Não usar artefatos como atualização de clientes. A publicação continua proibida.
