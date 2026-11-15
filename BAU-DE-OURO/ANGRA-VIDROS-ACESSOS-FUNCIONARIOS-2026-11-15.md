# ANGRA VIDROS — Controle de acesso no aplicativo (15/11/2026)

**Status:** requisitos aprovados conceitualmente, ainda não implementados. Não interromper a atualização do APK e novo vínculo com PDV Angra Vidros no ambiente 37.

- **Um único APK** Android, com login e perfis Proprietário, Funcionário e Gerente (funcionário com permissões adicionais).
- **Proprietário:** acesso completo a orçamentos, histórico, relatórios, financeiro e administração. No celular, botão **Gerenciar funcionários** para cadastrar, bloquear, desbloquear, excluir e configurar permissões.
- **Funcionário:** somente tirar fotos, informar medidas e dados do orçamento, e enviar ao PDV Angra Vidros. Sem relatórios, financeiro, administração ou configurações.
- **Gerente:** proprietário seleciona permissões individuais em caixas de seleção, semelhante ao PDV (histórico, relatórios, edição, financeiro etc.). Negar por padrão; ninguém modifica as próprias permissões.
- Cadastro com matrícula fixa única e nome completo, sem CPF obrigatório. Primeiro acesso autorizado pelo proprietário, funcionário define senha própria. Matrícula sozinha não dá acesso. Não reutilizar matrículas no histórico.
- **Bloquear:** suspensão temporária. **Excluir:** revogação definitiva de conta/sessões, preservando autoria histórica. Funcionário desligado não acessa dados nem funções, mesmo mantendo APK instalado; o Android ainda pode abrir a tela de **Acesso revogado**.
- Permissões validadas no servidor/PDV, não apenas escondendo botões; revogar tokens/sessões e proteger dados offline. Cada orçamento identifica o funcionário.
- **Ordem:** primeiro gerar APK atualizado sem vínculo antigo, parear exclusivamente ao PDV Angra Vidros no ambiente 37 e testar envio real; depois implementar gestão de acessos antes da homologação final.
- Vínculo antigo da Casa do Adriano no ambiente 34 é descartado para o novo app; **não mexer no ambiente 34**. Speed Food, Alerta, Firebase, GLOBAL, outros PETs e produção intactos.

**Registro documental somente: não alterar código, não publicar, não iniciar Etapa 6.**
