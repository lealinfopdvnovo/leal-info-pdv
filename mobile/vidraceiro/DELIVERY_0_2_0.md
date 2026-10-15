# ANGRA VIDROS 0.2.0 — teste profissional

Remodulação nativa após o splash congelado. Referências e contrato técnico: `docs/PDV_INTEGRATION_V2.md`.

Sem inferência/IA e sem cota de geração no novo fluxo. Não depende de rede para projetar/calcular/documentar. Não altera o PDV.

Clientes, fornecedores e materiais são rascunhos locais identificados. Financeiro, estoque e sincronização PDV são explicitamente pendentes. PDFs comerciais e contratos usam os valores informados; nenhuma receita é criada.

Instalação de teste separada: pacote `.profissional`, preserva instalações e dados anteriores. Não há migração automática entre pacotes privados Android com assinaturas diferentes. Splash e arquivos associados são verificados byte a byte contra 0.1.5; testes de abertura fria em três proporções validam os mesmos 5s, opacidade e progressão reais.

Homologação física pendente do usuário. Evidências de emulador não são homologação física.

Plano de corte: adaptação direta de GlassCutPacking, decimal, com peças, rotação e persistência; nenhuma alteração no Windows.
