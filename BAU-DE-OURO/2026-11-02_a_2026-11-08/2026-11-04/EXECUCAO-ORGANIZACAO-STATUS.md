# Organização GLOBAL/PET — execução autorizada em04/11/2026
Adriano autorizou organizar conforme proposta, testar e publicar somente após aprovação. Foto final deve retratar implementação real, nunca maquiar proposta como concluída.

## Trabalho executado
PR34 / branch prepare/organizacao-global-pet-20261104.
Inventário Windows dos5pacotes realmente publicados, com verificação SHA256, nomes de arquivos, cápsula PET de migração quando presente, versão do assembly e hashes dos recursos embutidos.
10testes do verificador de escopo aprovados.
Execução aprovada do inventário: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/41870106827
Artefato organizacao-inventario-real ID13343942552.

## Falha observada
Workflow legado validar_tela_venda.yml também disparou nessa branch. Compilação0erros; a etapa Capturar tela real no Windows encerrou com erro.
https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/41870112472
Não confundir essa captura legada com teste da nova migração: a integração da migração ainda não foi implementada ou homologada. Não declarar organização pronta.

## Limitação da sessão
Execução local deixou de estar disponível ao tentar abrir o artefato; retorno No usable execution environment is available. GitHub e runners continuam acessíveis. Não alegar que toda execução Windows está indisponível.

## Pendências técnicas reais
Ler inventário binário e preservar cada PET; implementar carregamento no login, atualização independente, backup/rollback por componente, migração dos canais legados e prova de GLOBAL único/mesmo hash para todos. Validar visual e regressões. Só depois publicar de acordo com autorização já concedida. Não pedir novamente autorização genérica para esse mesmo escopo; só escopo adicional exige esclarecimento.
Nenhum feed/release operacional alterado nesta execução. Não fazer rollback ou renomeação superficial para simular organização.
