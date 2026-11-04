# Integração GLOBAL/PET — evidência real de Windows

Commit testado: `5c86c2f827f64838472d829ff5fbc2f2e46dffbe`.
Execução aprovada: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/41883953915
PR candidato: https://github.com/lealinfopdvnovo/leal-info-pdv/pull/34

## O que passou

- Reconstrução da base comum atual 10.463, a partir da mesma fonte e receita das funcionalidades aprovadas 10.453, com remoção LIA preservada.
- Hash do pacote publicado conferido; fontes de autenticação, banco e licença preservadas; worker GLOBAL preservado byte a byte.
- Compilação do PDV real com a central de componentes e aplicador PET integrados.
- 45 verificações do contrato comum: um feed GLOBAL para todas as identidades, recusa de payloads locais e integridade/identidade PET, aplicação única da extensão, preservação dos bytes e versão PET ao selecionar GLOBAL.
- Três verificações independentes de confirmação de inicialização GLOBAL/PET, incluindo ausência do diário do outro componente.
- Dez cenários do aplicador PET: sucesso, hash inválido, identidade errada, manifesto errado, downgrade, travessia, transação GLOBAL pendente, staging alterado, interrupção com recuperação externa SYSTEM e falha de inicialização com rollback.
- Os testes do aplicador verificaram que os três binários da base, banco e licença de QA conservaram os hashes.

## Limites que não podem ser omitidos

- Executado em Windows descartável do GitHub Actions, sem instalação operacional de cliente.
- O PET 1.27 do teste de contrato é uma fixture; não é uma versão entregue ao Adriano.
- A migração dos PETs reais ainda está em validação. Pacotes antigos incluem personalizações embutidas que precisam ser preservadas.
- Nenhum feed de produção, release ou instalação foi alterado nesta etapa.
- Não mesclar o PR sobre o main antigo e não publicar a candidata enquanto migração e regressões dos perfis não forem aprovadas.

Os JSONs anexos são os arquivos da execução, sem reclassificar testes isolados como homologação de cliente.
