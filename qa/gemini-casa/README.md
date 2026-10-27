# LIA Gemini Live — experimento exclusivo Casa do Adriano

Estado: experimento separado, não publicado. Não é atualização do PDV. Não altera nenhum arquivo operacional, PET, banco, licença, Firebase ou APK. O executável original LicAi e o núcleo PDV são preservados byte a byte. Encerrar este experimento e abrir a LIA original é o retorno seguro; não existe transação de substituição a desfazer.

## Auditoria em 27/10/2026

Referência examinada: c19a69f50616c652dd89341fa4a07fa311750b61; realtime/OpenAiRealtimeConnection.cs configura gpt-realtime-2.1, voz marin, entrada/saída PCM24kHz. A branch release/lia-mobile-001-456-457 usa gpt-realtime-2.1-mini. Não confundir branch com versão instalada: a correção Casa10.458 continha somente os três arquivos do PDV, sem LicAi. Modelo efetivamente instalado na Casa ainda depende de evidência local.

realtime/MainForm.cs já tem fallback Gemini HTTP generateContent gemini-3.5-flash-lite e TTS gemini-3.1-flash-tts-preview; isto não é Gemini Live. Também existe rota de voz local. A base histórica LIC-AI (f0e3f579...) referencia gpt-5.6-sol em OpenAiClient e gpt-4o-mini em NavigationAssistantClient; os scripts de montagem podem substituí-los. Não foram autenticados nem testados esses modelos nesta conta.

A lista OpenAI realtime tem 14 telas; não contém Vidraçaria/Cozinha/Entregas. O receptor de navegação do PDV inclui Entregas, mas não possui casos VIDRACARIA ou COZINHA. Há ainda uma resposta genérica de sucesso do receptor mesmo para comandos desconhecidos; o experimento bloqueia alvos desconhecidos antes de enviar. Não altera o receptor para contornar essa limitação. Produtos/Vendas e demais 15 alvos já implementados são mantidos. Vidraçaria e Cozinha são reconhecidas nas instruções, mas sua abertura por voz continua bloqueada até existir receptor compatível autorizado. Nenhum teste de protocolo prova abertura real de telas.

## Modelos e limites oficiais

https://ai.google.dev/gemini-api/docs/models/gemini-3.8-live
https://ai.google.dev/gemini-api/docs/models/gemini-3.8-live-extended-thinking
https://ai.google.dev/gemini-api/docs/pricing
https://ai.google.dev/gemini-api/docs/rate-limits
https://ai.google.dev/gemini-api/terms
https://ai.google.dev/gemini-api/docs/live-api/get-started-websocket
https://ai.google.dev/gemini-api/docs/live-api/session-management

3.8 Live é o padrão recomendado para baixa latência. Extended Thinking é a variante para mais raciocínio em segundo plano; não há prova de superioridade para este PDV. Ambos constam da tabela gratuita, com entrada/saída sem custo. A disponibilidade efetiva e a cota são por projeto e precisam ser vistas em AI Studio; não se garante número fixo universal. Não ativar billing para testar. A aplicação não pode comprovar o plano a partir de uma chave; exige confirmação do operador antes de transmitir. Não há fallback pago automático, nem busca Google ativada. Preços pagos documentados: texto $0.75 entrada/$4.50 saída por milhão; áudio $3 entrada/$12 saída por milhão. Não aplicáveis se o projeto permanecer gratuito e tiver cota disponível.

Plano gratuito pode usar entradas/saídas para melhoria dos produtos Google e revisão humana, conforme termos; somente dados fictícios no teste, nenhum cadastro de cliente, valores ou dados do PDV são coletados automaticamente. Microfone e conversa são enviados após início explícito. Transcrições ficam só na tela, sem log em disco. Chave é local e mascarada; leitura opcional da chave Gemini já protegida por DPAPI, sem alterá-la ou salvá-la. Nenhuma chave embutida. Produção distribuída exigiria avaliar tokens efêmeros; não foi criada infraestrutura nem billing.

Protocolo WSS v1beta, áudio entrada PCM16 mono16kHz/saída PCM16 mono24kHz. Aoede é uma voz Gemini configurada com instrução pt-BR; não se promete o mesmo timbre marin da OpenAI. Extended Thinking usa ferramentas NON_BLOCKING e não encerra no turnComplete. Não há reconexão automática: limite local5min, GoAway encerra teste; cotas/erros exigem intervenção. Fila de microfone limitada e reprodução ordenada com falha explícita ao exceder buffer. Estabilidade de interrupções, cancelamento remoto de ferramentas e navegação simultânea com áudio ainda requer validação; não considerar pronta para produção.

## Execução e comparação pendentes

A candidata deve permanecer separada, numa pasta LIC-AI-EXPERIMENTAL diretamente dentro da instalação Casa. O guard lê Extensions/PET/manifest.json Identity003 e consulta --check-lia (licença Plus/Pro). Não instalar nem mover arquivos para a máquina real sem autorização. Para estes testes automatizados, não é necessário chave, microfone ou PDV real. Não executar lado a lado com a LIA atual, pois o microfone seria disputado.

A interface oferece Conversa/Navegação; cada comando de navegação requer confirmação local. Não executa pagamentos ou alterações de dados. Interromper pelo botão Parar e reabrir a LIA original pelo PDV; nenhuma configuração foi alterada.

Comparar com dados fictícios, mesmos dispositivos/rede: 20 frases pt-BR por provedor; 10 solicitações de Produtos/Vendas; 5 explicações de Cozinha/Vidraçaria; 10 interrupções; sessões de5min; medir tempo fim da fala→primeiro áudio audível, transcrição correta, ação correta, erros/quota e avaliação humana da voz. Registrar mediana/p95 e taxa de sucesso, com amostras sem nomes/chaves. Ainda não há resultados reais desses ensaios.

## Entrega bloqueada pelo atualizador existente

O botão ATUALIZAR da Casa10.458 aceita somente LealInfoPDV.exe/dll/deps.json. Este experimento é somente LIA separada e não cabe nessa política. Não foi alterado o núcleo, mecanismo ou feed para contornar. Um pacote de atualização pelo botão não foi preparado; esta candidata é apenas de laboratório, aguardando validação de API e decisão de entrega autorizada.
