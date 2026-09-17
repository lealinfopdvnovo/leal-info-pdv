$ErrorActionPreference = 'Stop'
$path = 'realtime\OpenAiRealtimeConnection.cs'
$c = Get-Content $path -Raw

# V10.252 - continuidade real do PCM: fila sequencial sem descarte e sem limpeza prematura.
# Preserva modelo, prompt, personalidade, VAD, reconhecimento, comandos e velocidade 1x.

# Primeiro normaliza o player para a base estabilizada da V10.249, caso o fonte ainda esteja antigo.
$c = $c.Replace('BufferDuration = TimeSpan.FromSeconds(6),','BufferDuration = TimeSpan.FromSeconds(120),')
$c = $c.Replace('DiscardOnBufferOverflow = true,','DiscardOnBufferOverflow = false,')
$c = $c.Replace('_speaker = new WaveOutEvent { DesiredLatency = 80, NumberOfBuffers = 3 };','_speaker = new WaveOutEvent { DesiredLatency = 120, NumberOfBuffers = 3 };')

$oldField = '    private int _suppressMicrophone;'
$newField = @'
    private int _suppressMicrophone;
    private readonly Channel<byte[]> _playbackQueue = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = true,
        AllowSynchronousContinuations = false
    });
    private Task? _playbackTask;
'@
if (-not $c.Contains($oldField)) { throw 'Campo de playback esperado nao encontrado.' }
$c = $c.Replace($oldField, $newField)

$oldConnect = @'
        InitializeSpeaker();
        _receiveTask = ReceiveLoopAsync(sessionCts.Token);
'@
$newConnect = @'
        InitializeSpeaker();
        _playbackTask = PlaybackLoopAsync(sessionCts.Token);
        _receiveTask = ReceiveLoopAsync(sessionCts.Token);
'@
if (-not $c.Contains($oldConnect)) { throw 'Inicializacao do receive loop nao encontrada.' }
$c = $c.Replace($oldConnect, $newConnect)

$oldDelta = @'
                SuppressMicrophoneDuringPlayback();
                _speakerBuffer?.AddSamples(pcm, 0, pcm.Length);
                Speaking?.Invoke();
                return;
'@
$newDelta = @'
                SuppressMicrophoneDuringPlayback();
                // Nunca escreve diretamente no dispositivo: cada delta entra numa fila FIFO.
                // O unico consumidor abaixo preserva exatamente a ordem recebida e evita sobreposicao.
                if (!_playbackQueue.Writer.TryWrite(pcm))
                    Error?.Invoke("Fila de audio da LIA indisponivel.");
                return;
'@
if (-not $c.Contains($oldDelta)) { throw 'Bloco de audio delta nao encontrado.' }
$c = $c.Replace($oldDelta, $newDelta)

$marker = '    private async Task HandleFunctionCallAsync(JsonElement root, CancellationToken cancellationToken)'
$playbackMethod = @'
    private async Task PlaybackLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var pcm in _playbackQueue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (pcm.Length == 0) continue;
                var buffer = _speakerBuffer;
                if (buffer == null) continue;

                // Backpressure: se a rede entregar audio mais rapido que o dispositivo toca,
                // aguarda espaco em vez de descartar qualquer trecho PCM.
                while (buffer.BufferedBytes + pcm.Length > buffer.BufferLength && !cancellationToken.IsCancellationRequested)
                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);

                buffer.AddSamples(pcm, 0, pcm.Length);
                Speaking?.Invoke();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Error?.Invoke("Reproducao da LIA: " + ex.Message); }
    }

'@
if (-not $c.Contains($marker)) { throw 'Ponto de insercao do playback loop nao encontrado.' }
$c = $c.Replace($marker, $playbackMethod + $marker)

# response.done nao limpa audio: apenas agenda retomada do microfone depois que FIFO + buffer terminarem.
$oldWait = @'
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while ((_speakerBuffer?.BufferedBytes ?? 0) > 0 && DateTime.UtcNow < deadline)
                await Task.Delay(40, guard.Token).ConfigureAwait(false);
'@
$newWait = @'
            // So considera a resposta fisicamente concluida quando nao ha mais PCM
            // aguardando na fila e o buffer do dispositivo terminou de tocar.
            var deadline = DateTime.UtcNow.AddSeconds(120);
            while ((!_playbackQueue.Reader.Completion.IsCompleted ||
                    _playbackQueue.Reader.Count > 0 ||
                    (_speakerBuffer?.BufferedBytes ?? 0) > 0) && DateTime.UtcNow < deadline)
            {
                // A Completion permanece aberta durante a sessao; para o turno corrente basta fila+buffer vazios.
                if (_playbackQueue.Reader.Count == 0 && (_speakerBuffer?.BufferedBytes ?? 0) == 0) break;
                await Task.Delay(25, guard.Token).ConfigureAwait(false);
            }
'@
if (-not $c.Contains($oldWait)) { throw 'Espera de playback esperada nao encontrada.' }
$c = $c.Replace($oldWait, $newWait)

Set-Content $path $c -Encoding UTF8
$check = Get-Content $path -Raw
if ($check -notmatch 'Channel<byte\[\]> _playbackQueue' -or
    $check -notmatch 'PlaybackLoopAsync' -or
    $check -notmatch 'DiscardOnBufferOverflow = false' -or
    $check -notmatch 'BufferDuration = TimeSpan\.FromSeconds\(120\)') {
    throw 'Correcao da fila sequencial nao foi aplicada integralmente.'
}
Write-Host 'LIA: fila FIFO sequencial aplicada; nenhum chunk PCM e descartado; ritmo 1x preservado.'
