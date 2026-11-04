# Casa — correção do selo para 10.464
O usuário mostrou o PDV real na versão10.463 sem o selo. A publicação anterior Casa10.463 não seria oferecida a uma instalação com o mesmo número. Além disso, a primeira URL usava tag client-003-v, rejeitada pelo contrato do atualizador Casa; a rota foi corrigida para v10.463-casa-selo-original sem modificar os bytes.

Para superar a colisão, foi compilada, validada e publicada Casa10.464, exclusivamente no feed clients/003/global/version.json.
Fonte: 54ec32ebe9bc2f9b126df3995a46d037352b2b64
Execução: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/41745520275
Compilação antes/depois, probes Windows, regressão de caixa, integridade e19cenários de recuperação aprovados. Pacote republicado somente após teste. URL aceita pelo atualizador existente: releases/download/v10.464-casa-selo-original/. Feed relido e confirmado10.464.
GLOBAL raiz, Loja e demais clientes não foram modificados por esta correção. Instalação e aprovação visual no computador real ainda aguardam confirmação do usuário.
