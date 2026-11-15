# ANGRA VIDROS 005 QA — PET 1.8 — somente login

Base aprovada 1.7. Branch exclusiva qa/angra-etapa4-setup-20261114. Código 90d491da010b03424a92c51850d1260c471ad873.

Título superior ANGRA VIDROS. Rodapé LEAL INFO CONECTADO — ACESSO SEGURO, preservando o sufixo técnico original. O vínculo é aplicado também a novas instâncias do login após sair da sessão. Nenhuma propriedade de layout, cor, dimensão, campo ou botão é modificada pelo novo helper.

Tela principal, barras e imagem aprovadas preservadas. Todos os recursos originais do PET 1.7 são byte a byte idênticos, incluindo helper da apresentação principal, vídeo, player e helper da transição. Somente AngraBrand.Apply e ProfileExtension.Initialize recebem a chamada do novo vínculo de texto exclusivo para LoginForm/QA005. Licença e validação preservadas; núcleo e bandeirinha reutilizados sem recompilação.

## Teste Windows

https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43672219696
Evidências: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43672219696/artifacts/14145345433

Login real pelo PetRuntime, sem Customize manual no teste. Título e fabricante conferidos após a abertura completa e em uma nova instância de login após encerrar a primeira sessão. Capturas real-login.png e login-reentry.png no artifact. O runner usa monitor 1024x768; elementos fora desse recorte não ficam visíveis nas capturas, embora seus textos sejam verificados na árvore real de controles. Layout aprovado não foi redimensionado para a evidência.

291 quadros monitorados, zero marcador de desktop e zero janelas externas durante a passagem. MP4 offline completo de 9 s; hash a2b5144645a2412fc21dc18f2685cc78ba50ae5cadbaaea22e24c88074fc9d89 preservado. Pacote real passa oferta/staging/contrato do atualizador existente, instalação e recuperação SYSTEM de interrupção com restauração exata do PET 1.7. Canary preservado e hashes do núcleo intactos.

Pacote 1.8 SHA-256: 6da4bf7a9359a77be4863f6a5ccf4340936844c9dbee7a6ce10503c945a5657e.

Compilação, testes Windows e publicação passaram. A última checagem pública do job original falhou por sintaxe de shell no script de conferência; uma execução separada somente de leitura verifica pacote público e evidências, sem recompilar.

## Instalação no QA

ATUALIZAR → ATUALIZAR PET → 1.8. Sem reinstalação. Confirmar os textos no computador físico após atualizar; este computador não foi acessado. Nenhuma publicação GLOBAL, em produção ou em PETs de outros clientes.
