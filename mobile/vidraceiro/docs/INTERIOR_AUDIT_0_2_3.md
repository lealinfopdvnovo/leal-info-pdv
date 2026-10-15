# Auditoria e padronização interna — 0.2.3

Base real: a427fba14f8072955ab45e5998c70fd061447a8b, ANGRA VIDROS 0.2.2. Mesmo applicationId de teste profissional022 e mesma assinatura retida. Atualização direta desta linha, sem pacote paralelo novo.

## Telas encontradas

| Entrada / tela | Controles e funções reais |
|---|---|
| Orçamentos / projetos | Lista pesquisável, abrir orçamento de projeto, criar projeto |
| Projetos de vidro e esquadria | Lista, pesquisa, cadastro/edição de projeto, tipo aberto, modelo, observações, cliente, medidas individuais, anexos e ações existentes |
| Clientes e Fornecedores | Lista, pesquisa, formulário nome/documento/telefone/e-mail/endereço, salvar; diálogo explícito de vínculo de cliente |
| Projeto para têmpera | Especificações editáveis, salvar, prévia/PDF |
| Contratos | Texto editável, salvar revisão, lista de revisões e documento |
| Financeiro / Estoque | Estados pendentes de integração oficial; nenhuma operação financeira/estoque implementada nesta etapa |
| Calculadora | Largura/altura, unidade, área e perímetro |
| Materiais | Lista, cadastro/edição, seleção no projeto |
| Plano de corte | Chapa, unidades, rotação, lista de peças; adicionar/editar/excluir peça; resultado, representação e prévia de documento |
| Projetos 3D | Visualizador existente, 2D/3D, medidas, reset, zoom, divisões informadas |
| Foto do local | Miniatura, ampliação, câmera/importação, substituir/remover com confirmação |
| Integração | Estado real de transporte preparado, exportação JSON e projeto atual |
| Documentos / diálogos | Prévia selecionável, PDF/impressão; mensagens, confirmações, cliente e revisões |

Não existem abas próprias ou agenda/recibos operacionais adicionais neste Android; não foram inventados. Câmera, seletor de arquivos, compartilhamento e impressão são interfaces do Android e mantêm o visual do sistema.

## Design system

InteriorVisuals reutiliza o ícone nativo de módulo HomeVisuals.Icon e o vetor AV, mantendo HomeVisuals e todos os assets aprovados sem alterações. Azul-marinho, painéis graduais, ciano, bordas, raios e tipografia derivam da Home, com decoração reduzida nos formulários.

Cabeçalho compacto: ação INÍCIO já existente (mesmo destino), assinatura AV/ANGRA VIDROS, ícone e título. Não há nova navegação ou fachada gigante nos formulários.

Campos nativos recebem fundo por estado (foco/normal/desabilitado), borda e contraste. Seletores usam layout TextView próprio, preservando adaptadores/valores/seleções e handlers. Botões mantêm textos e callbacks; salvar/calcular recebem destaque, navegação tom secundário e remover/excluir tom destrutivo discreto. Checkboxes preservam seleção/listeners. Resultados e dimensões usam painéis. Diálogos próprios recebem o mesmo tema por subclasse de Builder com show() decorado; conteúdo, ações e geração de documentos ficam intactos.

O decorador visita controles novos após layout, sem reparentear campos, trocar listeners ou acessar dados do projeto. Não formata/parsa valores e não cria estado comercial. Cada tela recebe seu próprio observador, sem tocar a Home.

## Congelamento verificável

A CI exige Home byte-idêntica à base, HomeVisuals e recursos intactos. Em MainActivity, exclui somente screen() e normaliza exclusivamente o nome do Builder visual e o recurso de layout de dropdown antes de comparar todo o restante à base. Isso mantém todos os callbacks e regras comerciais/foto/3D/persistência como estavam. As classes de domínio, armazenamento e 3D não foram modificadas. Nenhum arquivo PDV, canal ou updater foi alterado.

## Teclado: causa real encontrada em execução

O listener original de insets aplicava a altura completa do teclado como padding inferior do decor, ao mesmo tempo em que o Android já redimensionava a área de conteúdo. Isso eliminava a área útil e deixava o formulário invisível com o teclado. Em screen(), somente as telas internas passam a aplicar o inset estável da barra do sistema no padding inferior. O redimensionamento do IME continua a cargo do Android. Ao voltar à Home, restaura-se o tratamento original, preservando sua composição. Não há mudança nos campos, listeners de edição, valores ou regras.
