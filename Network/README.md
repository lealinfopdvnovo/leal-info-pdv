# PDV em rede e licenciamento por computador

Pacote inicial: **2 computadores no total, incluindo o servidor**. O servidor também opera o PDV. Os terminais usam o mesmo banco do servidor; cada nova identidade permanece cadastrada, mesmo quando desconectada. O fechamento de um PDV não libera uma vaga para outro computador.

## Configuração

1. Atualize todas as máquinas para a mesma versão.
2. Na máquina que já possui os dados da loja, entre como administrador em **Configurações > Rede e licença** e escolha **Usar este computador como servidor**.
3. Reinicie o PDV. Permita o aplicativo no Firewall do Windows apenas na rede privada; a porta padrão é TCP 47821. Mantenha o servidor ligado e o PDV aberto.
4. Exporte o arquivo `.lealrede`, informando o nome da máquina ou seu IP fixo/reservado no roteador. Transporte o arquivo por meio privado ao terminal.
5. No terminal, importe a conexão e reinicie. Se ainda não puder abrir as configurações, execute `LealInfoPDV.exe --rede`.
6. O terminal usa os usuários, produtos, vendas e estoque existentes no servidor. Não há mesclagem automática de bancos de máquinas que já possuíam dados próprios.

A conexão verifica o certificado do servidor e utiliza TLS. Cada terminal possui uma chave de identidade protegida pelo Windows. Copiar o arquivo de conexão não libera computadores adicionais. O cadastro de dispositivos e a licença ficam fora do banco comercial, protegidos pelo Windows; alterar uma configuração comum não aumenta a quantidade contratada.

## Aviso automático

Ao tentar cadastrar o terceiro computador com a licença base, o servidor recusa o acesso e mostra:

> Esta licença permite o uso do sistema em somente 2 computadores (servidor incluído).
>
> Para utilizar mais computadores, contrate um ponto adicional. Entre em contato com o vendedor.

Depois de contratar pontos adicionais, o aviso acompanha o limite da nova licença. O contato do vendedor aparece quando preenchido na licença.

## Modalidades do vendedor

- **Pagamento único (`unico`)**: libera a quantidade contratada sem vencimento.
- **Mensalidade (`mensal`)**: libera a quantidade contratada até a data/hora da validade; renovar significa importar uma nova licença assinada.

Os valores e a modalidade comercial serão decididos pelo vendedor. O programa não debita pagamentos automaticamente. O cliente pode importar uma licença assinada, mas não emitir licenças nem editar o limite.

O kit privado do vendedor contém `Emitir-Licenca.ps1` e a chave que corresponde à chave pública do PDV. A chave privada nunca deve acompanhar a instalação do cliente nem ser incluída no repositório. Veja os exemplos em `tools/licencas/README.md`.

## Operação e recuperação

- Se o servidor parar, os terminais interrompem operações e avisam sobre a perda da conexão. Não existe modo offline com sincronização nesta implementação.
- Gravações não são repetidas automaticamente após uma falha: antes de repetir, confira se a venda já foi registrada.
- Backups e restauração são executados no servidor. Para restaurar, feche servidor e terminais, abra `LealInfoPDV.exe --manutencao`, restaure e reinicie normalmente.
- Para renovar uma licença vencida, abra `LealInfoPDV.exe --rede` e importe a licença do vendedor.
- Não apague arquivos `network.*` para reinstalar. A identidade e o cadastro de pontos ficam vinculados ao usuário Windows. Reinstalação completa, troca de computador, substituição de terminal e migração de servidor exigem atendimento do vendedor; a remoção de pontos não é exposta ao cliente.
- Fotos de produtos e equipamentos continuam sendo recursos locais: impressoras são configuradas por máquina. Caminhos de fotos existentes no servidor podem não existir no terminal; nesse caso vale o fallback visual existente.

## Validação

O workflow `validar_rede_licencas.yml` compila no Windows e testa limite base, reconnect, persistência, concorrência, assinatura inválida, licença para outro servidor, pagamento único, mensalidade, vencimento, renovação, relógio monotônico, leitor de dados, TLS com certificado fixado e transações SQLite no servidor (commit, rollback e desconexão).

Ainda é necessário executar o teste de instalação em duas máquinas Windows na rede real da loja, incluindo Firewall, impressoras locais e operação simultânea. O PDV servidor precisa permanecer aberto; não foi instalado um serviço do Windows.

Não há promessa de proteção absoluta contra quem controla o Windows, altera o executável ou clona integralmente as identidades das máquinas. A validação impede o uso normal de um terceiro computador e alterações comuns de configuração/licença.
