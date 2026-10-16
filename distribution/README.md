# Publicação independente do PDV

A publicação usa somente `Distribuir PDV - canais independentes` em GitHub Actions.
O operador da loja continua usando ATUALIZAÇÕES DO SISTEMA → ATUALIZAR.

- GLOBAL usa `channel/global`. Clientes usam `client/001` a `client/004`.
- Cada pacote parte da própria árvore do canal e das dependências do pacote instalado.
- O cliente compilado recusa outro ID, feed inválido/indisponível e substituição por GLOBAL.
- Um canal individual válido tem prioridade independentemente da versão global.
- Uma publicação GLOBAL reserva automaticamente a mesma versão nos canais individuais que estiverem abaixo, recompilando suas próprias bases sem transferir código GLOBAL. Isso protege instalações antigas que ainda escolhem a maior versão.
- Todos os candidatos passam no Windows antes de qualquer manifesto ser alterado. Os pacotes publicados são os mesmos bytes testados.
- Os manifestos de uma publicação GLOBAL são gravados num único commit. Publicação individual altera somente seu manifesto.
- A política real do PDV verifica o payload incremental de três arquivos. Banco, licença, configurações, imagens e dependências locais não entram no pacote.
- O publicador verifica download público, SHA-256, versão e URLs usadas pelo PDV. Falha de verificação restaura os manifestos se nenhum outro publicador tiver alterado a referência.
- As rotinas de cópia/aplicação/reinicialização existentes não foram alteradas. Esta etapa não declara homologação de rollback integral dos executáveis.

Os workflows antigos de publicação com versões fixas foram arquivados em `legacy-workflows` para impedir concorrência e regressão de manifestos. Seus testes e histórico permanecem disponíveis.
