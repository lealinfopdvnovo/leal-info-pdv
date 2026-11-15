# ANGRA VIDROS QA 005 — PET 1.7 — três textos finais

Entrega exclusiva pela branch qa/angra-etapa4-setup-20261114, mesmo botão ATUALIZAR PET. Sem reinstalação.

## Alteração

- Login lê o plano do objeto de licença já validado: Standard, Plus ou Pro. Nenhuma alteração na licença, assinatura ou autorização.
- Título superior identifica ANGRA VIDROS e mantém LEAL INFO CONECTADO como fabricante. Sufixo técnico normalizado para PET V1.7.
- Status inferior identifica ANGRA VIDROS. A imagem aprovada também continha texto antigo no rodapé: somente o retângulo de texto (70,903,350,32) é redesenhado numa cópia em memória da imagem conhecida, sem escrever Assets nem modificar configurações. Ícone, divisórias, fabricante central e restante da arte permanecem intactos. Imagem personalizada diferente da original não é substituída.

## Evidências

Código: 826ccf850df9c46cea525298d262625673602daf.
Windows Actions: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43670415997
Capturas e JSON: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/43670415997/artifacts/14144321409
Pacote SHA-256: 941bd3f24957b459a3f2beb95ac8a299470f2d347e4182c8126e3518c9ea7fac.

Standard, Plus e Pro conferidos nas telas reais, credenciais QA utilizáveis e mesmo objeto de licença após personalização. Layout/fontes preservados; pixels amostrados fora do retângulo de texto permanecem idênticos. Capturas main-standard/plus/pro e login-standard/plus/pro inspecionáveis no artifact.

Somente AngraBrand.Apply recebeu a chamada de apresentação adicional. Todos os outros métodos e recursos originais são idênticos ao PET 1.6; inclui MP4, HTML do player e helper de transição. Núcleo e bandeirinha não recompilados. MP4 SHA-256 a2b5144645a2412fc21dc18f2685cc78ba50ae5cadbaaea22e24c88074fc9d89.

Reprodução offline completa de 9 s, transição para login opaco, 269 quadros monitorados com zero marcador de desktop e zero janela externa durante a revelação. Aplicação do pacote real e recuperação SYSTEM após interrupção restauram exatamente o PET 1.6 aprovado. O nome histórico PreviousPet15RestoredAfterSystemRecovery no JSON reutilizado não indica o baseline desta execução: test.ps1 fixa explicitamente o pacote 1.6 e compara seus hashes anteriores à instalação. Canary de dados preservado e hashes do núcleo intactos.

O job Windows passou compilação, todos os testes e publicação. Sua última checagem HTTP não confirmou a oferta dentro de 120 s. A oferta 1.7 e o SHA público foram conferidos separadamente e serão registrados por job de conferência sem recompilação. Essa distinção permanece registrada, sem ocultar a conclusão do job original.

GLOBAL channel/global: 48d8f50f00a16bc2457b5ebd04383dfaf9cf16d1. Produção updates/main: 6f641fc6941b573f94393a437191761243cfa69e. Nenhuma alteração nesses canais, em PETs de outros clientes ou em licença/bandeirinha.

## No computador de teste

ATUALIZAR → ATUALIZAR PET → PET 1.7. Confirmar visualmente os três textos no Windows físico; este computador não foi acessado. Testes acima executados em Windows descartável com fixture, não em dados de clientes reais.
