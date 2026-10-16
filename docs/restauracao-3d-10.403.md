# Restauração do acesso original 2D/3D

Bases reais dirigidas: 001/10.402 em 3a2ecce364305b4a1d1e6ad039223c73db417fb1; 003/10.402 em 2630a738470c8a2c0badc25c184f9369fcba4df8. Pacotes, hashes e manifestos são validados antes do build de cada cliente.

## Histórico comprovado

- 57ad0f9 (10.382): implementação original `GlassBox3DView`, GDI+ local sob demanda, rotação por mouse, zoom, reset e fallback 2D.
- 39ad935 / ce8f73808e1a79d2c5f73c48c852aafb0b30d795 (10.383): duas folhas e dimensões fixas, seletor 2D/3D visível na janela de projeto anterior à Central.
- fc6d456c0f2cb6bbcbfc49a0cbedbfb40b8818f7 / 6905057d9d26c4dd95c18816e990eb2880128ac3 (10.386): `GlassProjectForm.Mount` passou a hospedar campos/superfície na Central e abriu 3D automaticamente pela aba Visualização. Reparentou RESETAR VISÃO, mas não o contêiner `ViewModes` com `Mode2D` e `Mode3D`; estes ficaram na janela de projeto não mostrada. É a perda do seletor explícito original, anterior à 10.402.
- Evoluções 10.388/10.389: `Glass3DTechnicalOverlay` e guarda de legibilidade das cotas. Blob `GlassBox3DView.cs` de 56638a311a88d2708fe382de4b48b9c625ed4326 (10.389) é exatamente 9f401c10f640dbf61191b53937c93161358334de, o mesmo da 10.402.

O 3D não foi excluído na 10.402, e a implementação está presente nesta própria versão. A seleção da aba Visualização ainda o aciona; o acesso explícito 2D/3D original não foi montado na Central. Os testes antigos exercitavam a aba e o renderizador, sem exigir os botões na árvore visível da Central.

## Correção seletiva

Somente `GlassProjectForm.Mount`: reparenta o mesmo `ViewModes` e os mesmos botões para Desenho/Visualização, junto à superfície compartilhada. Cliques selecionam as abas já existentes; callbacks e motores originais continuam intactos. Ordem Dock mantém o seletor fora da superfície gráfica. Nenhum novo 3D, controle de câmera, banco, schema, cálculo ou funcionalidade comercial.

`GlassBox3DView`, `Glass3DTechnicalOverlay`, desenho 2D, impressão, cotas, persistência, materiais, orçamento, Plano de Corte, Financeiro, Agenda, Recibos, Contratos e particularidades dos clientes são preservados byte a byte.

## Verificação Windows

Teste da base 10.402 reproduz ausência dos botões na Central e confirma que o motor ainda funciona automaticamente pela aba. Teste corrigido usa a Central real em nove layouts (1366×768/1600×900/1920×1080; 100/125/150%), exige botões visíveis e contidos, alterna 2D/3D, confere mesma fonte/mm, rotação, zoom, reset, cotas, atualização de medida, projeto salvo e novo processo. Todas as regressões01–08 são executadas por cliente antes da publicação dirigida.
