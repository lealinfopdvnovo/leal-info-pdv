# Ativação e dados locais

Um único pacote neutro contém o programa; o plano é determinado pela licença assinada. Não há pacotes com nome, logo ou dados de uma empresa.

- Standard: recursos atuais de operação, 1 computador, sem LIA e sem terminais.
- Plus e Pro: recursos atuais, LIA e rede conforme o limite contratado. Não foram inventados bloqueios adicionais entre Plus e Pro.
- Ativação: serial do Windows vinculado à assinatura RSA; código sequencial atribuído pelo gerador do proprietário; licença ativa/inativa, pagamento único ou mensal e expiração.
- O programa verifica assinatura, serial, modalidade, plano e expiração. O estado é protegido por DPAPI do usuário do Windows e salvo em `%LOCALAPPDATA%/LealInfoPDV/network.license`.
- O gerador fica separado do pacote do cliente. O cadastro administrativo reside somente no computador do vendedor, em `%LOCALAPPDATA%/LealInfoVendedor/clientes.protected`. A chave privada fica exclusivamente no kit particular do vendedor, nunca no repositório.
- A situação inativa entra em vigor quando uma licença inativa assinada é importada. Não há revogação remota instantânea: nenhum cadastro de cliente é enviado ao GitHub. Mensalidades expiram automaticamente.
- O logo pertence ao Cadastro da Empresa e fica no banco local, normalizado em PNG. Terminais recebem o mesmo dado pelo banco central. O fade Standard dura aproximadamente 1,55 s e precede o login existente. Sem logo, usa a marca padrão.
- Alteração do logo atualiza imediatamente a tela principal da instalação que o editou; demais terminais carregam o logo atualizado ao reabrir. A abertura lê o logo a cada execução.
- O atualizador mostra a edição, valida SHA256 do manifesto e só aceita arquivos do programa. Não exclui o cadastro/ativação/usuários/banco/configurações em AppData. O instalador continua usando a mesma identidade e pasta da versão anterior.
- Na migração da V10.355, uma licença antiga sem código do cliente precisa ser substituída por licença do novo gerador, usando o mesmo serial. Dados da empresa e vendas não são removidos. Servidor deve ser atualizado/ativado antes dos terminais.
- Use o mesmo usuário do Windows para manter dados e proteção DPAPI. Troca de PC ou conta requer recuperação dos dados e reemissão da licença pelo vendedor.

## Teste na loja

1. Em uma instalação nova, confirme que a ativação aparece antes do login. Copie o serial e emita licença no gerador; importe o arquivo ou cole a chave.
2. Cadastre o administrador pelo login existente e preencha o Cadastro da Empresa; selecione o logo.
3. Feche e reabra: não deve pedir novamente a chave. Standard mostra fade do logo, login e menus, sem a esfera LIA. Confirme venda e comprovante.
4. Troque o logo no Cadastro da Empresa e confirme a mudança na tela principal e na próxima abertura.
5. Plus/Pro: configure o servidor, exporte a conexão e importe no terminal com `LealInfoPDV.exe --rede`. O próximo ponto acima do limite deve receber a mensagem de contratação.
6. Gere renovação/upgrade para o cliente existente, importe e reabra o programa. Código e dados devem permanecer.
7. Importe licença inativa e confirme bloqueio; importe uma ativa mais recente para recuperar. Licença expirada/alterada/outro serial deve ser rejeitada.
8. Antes de atualizar uma instalação real, faça backup pela função existente. Atualize e confirme empresa, logo, usuários, vendas, configurações e licença.

Testes automatizados Windows verificam assinatura, ativação/persistência, edição, inativa, replay, logo local, migração, login, limites/TLS/transações e política de atualização. O roteiro real deve ser executado na máquina do cliente para validar ambiente e periféricos.
