# Botão LIA — arquivo histórico, sem compilação

Os três scripts originais estão preservados byte a byte com extensão `.source`.
O manifesto registra commit de origem e SHA-256. Eles implementam aparência,
posicionamento, pintura, pulso, clique, estados e encerramento do botão.
Não executar esses scripts para recuperar somente o visual: o clique original
inicia a assistente. Nenhum serviço de IA é necessário para guardar o arquivo.

Recuperação: obter o commit do manifesto no GitHub e comparar os arquivos
`original/*.source` com `git show COMMIT:CAMINHO_DE_ORIGEM` e seus hashes.
A extração e comparação são verificadas por `qa/lia-removal/test_remove_button.py`.
Este arquivo preserva a implementação histórica identificada; não comprova
equivalência visual de variantes posteriores montadas para cada cliente.

A animação de entrada é independente deste bloco. LoginForm.cs, SplashForm.cs
e Assets não são modificados pelo transformador. Não apagar System.Speech,
WebView2, arquivos multimídia, licenças nem dependências compartilhadas.

Para restaurar uma candidata transformada, usar a cópia byte a byte
`lia-removal-evidence/MainForm.cs.original.source` daquela mesma candidata,
conferir o hash `before_sha256` e restaurar somente em ambiente isolado.
Reverter esta branch não constitui publicação ou atualização de clientes.
