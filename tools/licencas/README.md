# Emissor de licenças — uso exclusivo do vendedor

Use PowerShell 7 (`pwsh`). Guarde a chave privada `chave-vendedor.pem` junto do script, somente no computador do vendedor.

Copie o serial do servidor na tela **Rede e licença**. `Computadores` sempre inclui o servidor. Para três máquinas no total, use 3.

Pagamento único:

```powershell
pwsh -File .\Emitir-Licenca.ps1 -Servidor 'LI-XXXX-XXXX-XXXX-XXXX' -Computadores 3 -Modalidade unico -Contato 'WhatsApp do vendedor' -Saida '.\Cliente.leallicenca'
```

Mensalidade:

```powershell
pwsh -File .\Emitir-Licenca.ps1 -Servidor 'LI-XXXX-XXXX-XXXX-XXXX' -Computadores 3 -Modalidade mensal -Validade '2026-11-01T23:59:59-03:00' -Contato 'WhatsApp do vendedor' -Saida '.\Cliente.leallicenca'
```

Para renovar, gere outro arquivo com a nova validade. Envie **somente o `.leallicenca`** ao cliente e importe-o no servidor. O script atribui uma revisão crescente baseada no horário; mantenha o relógio do vendedor correto.

Não envie a chave privada, este kit ou o script ao cliente. Não inclua a chave no GitHub. Não existe cobrança financeira automática: o pagamento é administrado pelo vendedor, que emite a licença após a contratação.

Troca de computador e migração devem preservar os arquivos protegidos do servidor e a identidade do Windows. A primeira versão não disponibiliza revogação/substituição de dispositivos pelo cliente.
