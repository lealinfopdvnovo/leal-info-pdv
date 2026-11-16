# PROMPT PARA WORK — Tutorial interativo de cadastro de funcionários (ANGRA VIDROS)

**Estado:** IMPLEMENTADO E ENTREGUE no QA em 16/11/2026 — APK0.4.2 e PET1.18. Testes automatizados aprovados; homologação física pendente. Evidências e instruções em [ENTREGA_2026-11-16.md](ENTREGA_2026-11-16.md).
**Escopo de implementação:** ambiente Vidraçaria do Leal Info PDV. Preservar GLOBAL e personalizações PET e as funcionalidades homologadas da APK ANGRA VIDROS 5B; incorporar a melhoria na entrega em andamento, sem regressões.

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
- Não modificar o GLOBAL ou PETs fora do escopo. A implementação e a entrega da melhoria na APK/fluxo ANGRA VIDROS em andamento estão autorizadas, com testes de regressão e versão rastreável. Não publicar mudanças globais não relacionadas.
- Implementar agora na branch de trabalho apropriada, demonstrar telas, testar navegação, responsividade, retorno e saída do tutorial; entregar o artefato instalável atualizado quando aplicável, com relatório e evidências. Evitar substituir versões homologadas antes de validar os testes.

## Entrega esperada agora
Tutorial funcional, reutilizando os destaques existentes, integrado à entrega ANGRA VIDROS em andamento, acompanhado de testes, evidências, identificação da versão e artefato atualizado. **Não encerrar a tarefa apenas com planejamento.**


## Complemento entregue — 16/11/2026
Guia visual com imagens e explicações publicado no PET1.19 exclusivo QA005; APK0.4.2 preservado. Testes automáticos aprovados, validação visual no Windows do proprietário pendente. Ver [GUIA_VISUAL_PET_1_19_2026-11-16.md](GUIA_VISUAL_PET_1_19_2026-11-16.md).
