# PROMPT PARA WORK — Tutorial interativo de cadastro de funcionários (ANGRA VIDROS)

**Estado:** solicitação documentada; NÃO executar, publicar ou atualizar sem autorização expressa do proprietário do projeto.
**Escopo futuro:** ambiente Vidraçaria do Leal Info PDV. Preservar GLOBAL e personalizações PET, APK ANGRA VIDROS 5B e integrações existentes.

## Objetivo
Adicionar no PDV um botão **Como cadastrar funcionários** que abra um tutorial guiado e interativo, reutilizando o recurso já existente de círculos de destaque e setas. Não duplicar componentes se houver implementação reutilizável.

## Experiência
- Destacar na tela real os controles com círculo luminoso e seta, escurecendo discretamente o restante sem impedir acessibilidade.
- Exibir texto curto por etapa e botões **Próximo**, **Voltar**, **Sair** e **Ver novamente**.
- Adaptar a indicação à posição real dos controles, à resolução e à escala; quando um controle não estiver visível, orientar a navegação de modo seguro.
- O tutorial apenas instrui: não deve criar contas, emitir códigos, conceder permissões ou alterar registros automaticamente.

## Roteiro
1. Explicar a primeira ativação do proprietário, sem solicitar ou expor senhas no tutorial.
2. Indicar como abrir o gerenciamento e cadastrar um funcionário.
3. Demonstrar a definição de permissões conforme os controles existentes.
4. Mostrar a geração do código único de ativação e recomendar guardá-lo em segurança.
5. Explicar como o funcionário insere o código no aplicativo em seu próprio aparelho.
6. Incluir **Perdeu o código?**: mostrar onde entrar em **Gerenciar Funcionários** e solicitar a emissão de novo código. Verificar no sistema se o anterior é invalidado; caso não seja, tratar como requisito de segurança a validar, sem afirmar que já funciona.
7. Mostrar como revisar permissões e revogar o acesso de funcionário, apenas se tais funções existirem; sinalizar lacunas.

## Regras de segurança e implementação
- Antes de programar, inspecionar os fluxos e rótulos reais do PDV e do APK 5B, inclusive a recuperação do código, sem presumir telas inexistentes.
- Não alterar a vinculação do proprietário, a lógica de ativação, o cadastro de funcionários, os dados ou a integração PDV–APK como efeito deste tutorial.
- Não modificar GLOBAL, PETs, APK 5B, instaladores, publicações nem versões sem autorização específica.
- Se autorizado a implementar posteriormente, trabalhar em branch isolada, demonstrar telas, testar navegação, responsividade, retorno e saída do tutorial; entregar relatório e evidências para aprovação antes de qualquer merge ou publicação.

## Entrega esperada quando autorizado
Tutorial funcional no PDV, reutilizando os destaques existentes, acompanhado de testes e evidências. **Este arquivo é apenas um pedido para execução futura.**
