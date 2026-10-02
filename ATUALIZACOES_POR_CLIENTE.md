# Atualizacoes por cliente

Cada licença contém um ID numérico de cliente em `ClientCode` (por exemplo `002`). O PDV consulta primeiro `clients/<ID>/version.json`; só usa o manifesto global quando não há uma atualização válida para o próprio ID.

## Publicar uma correção individual

1. Criar ou atualizar a branch `client/<ID>`, mantendo o ID numérico e os zeros à esquerda.
2. Aplicar a correção somente nessa branch e aumentar em conjunto `LealInfoPDV.csproj` e `UpdateManager.CurrentVersion`.
3. Colocar a descrição da mudança em `CLIENT_UPDATE_NOTES.txt`.
4. Enviar a branch. O workflow publica um ZIP de atualização, valida-o com o validador do PDV e atualiza somente `clients/<ID>/version.json` no repositório de atualizações.
5. O computador desse cliente baixa o ZIP pelo Atualizador do PDV. A publicação não altera `version.json` global e não produz instalador para o pacote individual.

Para disponibilizar o roteamento a instalações anteriores, publique primeiro a atualização global que contém o novo atualizador. Depois, cada correção individual fica isolada no manifesto do respectivo ID.

IDs válidos: somente dígitos, de 3 a 9 caracteres. Exemplos: `001`, `002`, `003`, `010`.
