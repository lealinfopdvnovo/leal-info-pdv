# ANGRA VIDROS QA 005 — PET 1.6

Correção exclusiva da transição: a abertura permanece modeless sobre o monitor enquanto o LoginForm real é preparado. O fade de 650 ms começa somente com login pronto e opaco, na passagem final do vídeo aprovado. O player permanece vivo até o evento ended e a drenagem original de áudio. O proprietário modal nativo permanece invisível até o fechamento do login para evitar encerramento involuntário do diálogo.

- Branch: qa/angra-etapa4-setup-20261114.
- Código final: 4f89b8c5b066caab0eb456fa1ace56e44d4c5161 (produto em 29f53a2; último commit acrescenta evidência ao teste).
- Publicação QA: 8933bc4c73192aad5b07de5ea41d1f2028969dac.
- Windows Actions: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43667012870
- Evidências Windows: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43667012870/artifacts/14142781434
- Pacote SHA-256: 25ab1b7ed0ffbb5b6bf8278d644384707fd11ced20ae6ef3e379ee3a0668c21b.
- MP4 original preservado: a2b5144645a2412fc21dc18f2685cc78ba50ae5cadbaaea22e24c88074fc9d89.

## Verificação automática

Windows descartável, host QA aprovado reutilizado sem recompilar. Instalador usado somente para montar a máquina de teste; entrega ao proprietário exclusivamente pelo PET existente.

282 capturas monitoradas, zero quadros com marcador do desktop e zero janelas externas detectadas durante a revelação. Login verdadeiro preparado em 4,866 s, antes do fim de mídia em 14,510 s (tempos incluem inicialização do WebView). Login opaco, habilitado, cobrindo monitor 1024x768. Fala/animação completas: 9 s, arquivo e HTML do player idênticos aos aprovados. Demora simulada de 600 ms com UI ocupada e mais 700 ms antes de construir login. Janela proprietária temporária removida ao fechar o login.

Atualizador existente oferece 1.6 a 005/1.5 e valida staging/contrato. Aplicação do pacote e recuperação SYSTEM após interrupção restauram exatamente PET 1.5. Canary de dados preservado; núcleo original intacto. Somente manifest e DLL PET no pacote. Licença, bandeirinha e outros métodos/recursos originais não alterados.

O job Windows passou compilação, todos os testes e publicação. A última conferência pública não confirmou a oferta no limite de 120 s; a oferta pública e SHA foram conferidos separadamente após a publicação. Um job de conferência sem recompilação registra novamente essas verificações. Não ocultar essa distinção ao consultar o histórico.

GLOBAL channel/global permanece 48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1. Produção updates/main permanece 6f641fc6941b573f94393a437191761243cfa69e. Nenhum envio para produção.

## Uso e limite

No PDV TESTE ANGRA: ATUALIZAR → ATUALIZAR PET → aceitar PET 1.6. Sem reinstalação. Confirmação visual final no Windows físico do proprietário ainda necessária; não foi acessado. As capturas amostradas no runner não equivalem a garantia em todo hardware/DPI.
