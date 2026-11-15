# 2026-11-15 — LEAL INFO

## Registro do dia — planejamento e Etapa 5
- Reconstrução provisória do plano de sete etapas; etapas 1–4 homologadas segundo o proprietário.
- Regra aprovada: migração seletiva e progressiva do ambiente 34 para o 37, preservando GLOBAL/PET e funcionalidades já aprovadas.
- **Etapa 5:** registro técnico no [Plano de Etapas](../../../PLANO-DE-ETAPAS-LEAL-INFO.md) e no [Diário de Continuidade](../../../DIARIO-DE-CONTINUIDADE.md). Foram comparados 28 componentes; 252 arquivos do 34 preservados; Windows Actions 43735576962 registrado como SUCCESS. Etapa tecnicamente concluída conforme relatório, **aguardando aceite do proprietário**. Não houve transbordo de clientes.

## Decisões posteriores do proprietário — Etapa 5B e destino do 34
- Criar uma **Etapa 5B de auditoria obrigatória antes da Etapa 6**, cobrindo GitHub, Firebase, configurações e dependências existentes, APK ANGRA VIDROS, Speed Food e LEAL INFO Alerta. Verificar o Salão MJ como projeto independente, sem misturá-lo ao GLOBAL/PET.
- Verificar origem do código, capacidade de recompilar APKs e integrações, sem copiar segredos para o repositório nem modificar produção. A existência de APK não comprova funcionamento ponta a ponta.
- O **37 será o ambiente definitivo e autônomo**. O 34 será mantido durante migração/homologação, mas **desativado como ambiente operacional após aceite final**, com backup histórico organizado, privado, íntegro e restaurável.
- Backup do 34: arquivos/pacotes por categoria e inventário para localizar e recuperar componentes pontuais em necessidade excepcional. Não copiar indiscriminadamente o 34 para o 37; não guardar dados sensíveis, credenciais ou bancos de produção em ZIP público.
- **Sem autorização para apagar o 34 agora**, alterar GLOBAL/PET de produção ou executar a Etapa 6. Encerramento condicionado a testes, integridade/restauração do backup e autorização expressa.
- Manter a organização semanal/diária do Baú de Ouro; não criar estrutura paralela.

## Estado e próximo passo
- Etapa 5: resultado técnico registrado; homologação do proprietário pendente.
- Etapa 5B: **escopo aprovado em conversa, execução não confirmada**.
- Etapas 6 e 7: não autorizadas a iniciar.
- Conferir com o Work os relatórios e evidências antes de mudar status. Este README não substitui testes nem acesso ao Firebase.
