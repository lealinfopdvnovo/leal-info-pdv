# Baú de Ouro — 13/11/2026
## Proposta futura: catálogos por segmento no LEAL INFO PDV

**Status:** ideia aprovada para registro e implementação futura; nenhuma mudança de código ou publicação autorizada.

- Catálogos JSON por segmento comercial (roupas, hortifruti, papelaria, motopeças, autopeças etc.), hospedados na nuvem e baixados sob demanda, evitando sobrecarregar o PDV.
- Botão futuro **Importar Catálogo por Segmento** no Cadastro de Produtos. Usuário seleciona somente produtos efetivamente vendidos; preços, estoque e códigos de barras são conferidos/ajustados. Importação transacional, sem duplicações ou sobrescrita de dados existentes. Produtos importados ficam disponíveis offline.
- Para motopeças, duas pesquisas sobre a mesma base: **Buscar por peça** (nome/código) e **Buscar por moto** (marca/modelo/ano/versão). Alternativa complementar: busca online de dados técnicos, sem assumir que resultados de internet garantem compatibilidade.
- Compatibilidade com estados **confirmada**, **não verificada** e **incompatível**. Nunca inferir compatibilidade sem evidência técnica confiável, considerando código e aplicação do fabricante.
- Ao selecionar uma peça de qualquer origem, mostrar balão flutuante central: **ATENÇÃO — VERIFIQUE A COMPATIBILIDADE. Confirme marca, modelo, ano, versão e código da peça; consulte o catálogo do fabricante. Resultados online não garantem compatibilidade.** Botões **Voltar e conferir** e **Confirmei a verificação**. Confirmação não altera estado não verificado para confirmado.
- Na finalização de venda de peça não verificada, mostrar **segundo alerta explícito**, para ciência do vendedor, preservando indicação de compatibilidade não verificada.
- Usar biblioteca técnica verificada e, quando possível/licenciado, dados oficiais de fabricantes; não apresentar resultados de busca genérica como prova de aplicação.
- **Regra de proteção:** GLOBAL intocável e PETs preservados; desenvolver isoladamente, testar antes de qualquer publicação e só integrar/publicar mediante autorização expressa do proprietário.
