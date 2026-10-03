# Gerador de licenças na nuvem

Painel web privado para emitir as mesmas licenças RSA-PSS que o PDV já valida. Os arquivos de licença permanecem compatíveis com `.leallicenca` e com a chave `LEAL1-...`.

## Preparação única do Firebase

1. Crie um projeto Firebase no plano Spark e registre um app Web. Habilite **Authentication > E-mail/senha** e crie o banco do **Cloud Firestore**.
2. Copie a configuração do app Web para `public/firebase-config.js`. Esse arquivo contém configuração pública do Firebase; as regras abaixo protegem os dados.
3. Publique `firestore.rules` após substituir `SUBSTITUA_PELO_EMAIL_DO_VENDEDOR` pelo e-mail proprietário usado no Firebase Authentication. Crie a conta pelo painel, confirme o e-mail e use sempre esse endereço. Todo outro usuário autenticado fica sem acesso aos documentos.
4. Publique o site e as regras a partir desta pasta:

   ```powershell
   npm install --global firebase-tools
   firebase login
   firebase deploy --project SEU_PROJECT_ID --only hosting,firestore:rules
   ```

5. Entre no painel e importe uma vez os cadastros existentes informando manualmente os códigos atuais (001 e 002) e os dados que aparecem no gerador antigo. O código existente é preservado; o próximo novo passa a ser 003. Depois dessa carga inicial, o número é reservado em uma transação do Firestore, então dois computadores não emitem o mesmo código.
6. Importe a chave `chave-vendedor.pem` do kit original. O navegador confere se ela corresponde à chave pública embutida no PDV e a criptografa com AES-256-GCM/PBKDF2 antes de guardar o conteúdo cifrado. Escolha uma frase longa e exclusiva e use a mesma para desbloquear em outro computador. A chave privada não é enviada em texto aberto.

Os cadastros, documentos, seriais, licenças emitidas e a cópia criptografada da chave ficam em coleções protegidas por Authentication e regras do Firestore. Não coloque dados de clientes no código, em planilhas públicas, ou em commits.

## Atualizações automáticas do painel

O workflow `Deploy gerador de licenças` acompanha mudanças nesta pasta na branch `main`. Para ativá-lo depois da primeira publicação, cadastre no GitHub:

- variável de repositório `LEAL_FIREBASE_PROJECT_ID`;
- segredo de repositório `LEAL_FIREBASE_SERVICE_ACCOUNT`, com uma chave JSON de uma conta de serviço limitada às permissões de Firebase Hosting e publicação de regras do Firestore.

Sem esses valores, o workflow pula a publicação sem falhar. Depois de configurados, cada atualização do painel é publicada automaticamente. O código público não contém a conta de serviço, os dados dos clientes ou a chave privada do vendedor.

## Uso

- Para migrar, use o campo **Código do cliente** e grave os números já existentes. Não emita outra licença durante a migração.
- Para clientes novos, deixe o código vazio e salve: o Firestore reserva o próximo número sequencial automaticamente, com no mínimo três dígitos.
- Abra um cadastro e use **Gerar / renovar licença**. Para pagamento único a licença fica sem vencimento. Para mensalidade é obrigatório escolher a data final.
- Baixe `.leallicenca` ou `.chave.txt` e envie ao cliente pelo canal habitual. Desativação passa a valer depois que uma licença inativa atualizada for importada no PDV, de acordo com o fluxo atual do programa.

## Segurança e limites

- A regra de proprietário usa e-mail verificado e igualdade exata do e-mail configurado.
- A chave privada fica descriptografada apenas em memória enquanto o painel está aberto e desbloqueado; sair da conta apaga a referência em memória.
- Guarde a frase secreta separadamente. Perder a frase significa precisar importar de novo a chave privada original.
- O painel não processa pagamentos, não revoga licenças instantaneamente e não altera o banco de vendas do PDV.
- O projeto precisa ser criado/configurado na conta Firebase do proprietário antes de o painel ter um endereço online.
