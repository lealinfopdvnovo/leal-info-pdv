# Estado da execução GLOBAL/PET — 04/11/2026

Último código integralmente aprovado: `c7aa2df9ea8b58a881ff639aa452a79fd6fe5222`.
Execução: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/41888293183
PR de trabalho: https://github.com/lealinfopdvnovo/leal-info-pdv/pull/34

## Resultado comprovado

A execução passou em todos os passos: reconstrução da GLOBAL atual 10.463, preservação de todos os recursos embutidos publicados, compilação da central e dos aplicadores, 45 verificações de contrato comum, três confirmações de inicialização, dez cenários PET, compatibilidade e bootstrap atômico dos quatro perfis com estoque/caixa/configuração preservados e backups validados.

O aplicador original da Loja, SHA256 `A760F78DF6B2D3D11B037727CA642BE8E3D5EFEF802D0AC6898EA54F9D819968`, executou sobre os binários publicados reais da Loja. Foram aprovados os quatro cenários `success`, `bad-sha`, `interruption-system` e `unqualified-startup-rollback`. Licença, banco e extensões de QA conservaram seus hashes. A recuperação ocorreu por tarefa SYSTEM. O último cenário é um teste negativo e não comprova inicialização ativada bem-sucedida.

O artefato `integracao-componentes-Windows` dessa execução contém `legacy-loja-transition.json` e todas as provas anteriores. Os resultados e capturas já preservados na pasta `evidencias-perfis` continuam válidos para as etapas anteriores. As capturas `CENTRAL-LOJA-INTEGRADA-QA.png` e `CENTRAL-CASA-INTEGRADA-QA.png` são reais, de ambiente Windows descartável.

## Publicação: não realizada

- `positiveActivatedStartupQualified`: false.
- `migrationVerified`: false.
- Não foi demonstrada transferência completa das personalizações embutidas antigas, incluindo o selo, para os PETs independentes.
- Nenhum feed, release ou computador de cliente foi modificado.
- Nenhuma atualização nova deve ser anunciada como disponível no botão ATUALIZAR.
- Não mesclar sobre o main antigo. A candidata é montada sobre a fonte e a receita da distribuição atual.

## Continuação necessária

1. Aproveitar a candidata e os testes aprovados, sem recomeçar a auditoria.
2. Completar a preservação e transferência das personalizações específicas, conferindo o selo aprovado já extraído e os demais módulos embutidos.
3. Qualificar as transições restantes e a inicialização positiva em ambiente licenciado, sem bypass de licença e sem alterar dados de cliente.
4. Somente então preparar as versões e publicar a atualização interna nos canais autorizados.

Este registro não declara a entrega completa. A regra do Adriano permanece: resultado verificável, sem confirmação redundante, sem ZIP/setup/comandos manuais e sem publicar alteração incompleta ou arriscada.
