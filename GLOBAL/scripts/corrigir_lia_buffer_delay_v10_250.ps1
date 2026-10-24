$ErrorActionPreference = 'Stop'
$path = 'realtime\OpenAiRealtimeConnection.cs'
$c = Get-Content $path -Raw

# V10.250 - jitter buffer de 1,8 s para impedir soluços entre chunks.
# Preserva sample rate 24 kHz, ritmo 1x, Realtime, prompt, VAD e logica de conversa.

$oldField = '    private int _suppressMicrophone;'
$newField = @'
    private int _suppressMicrophone;
    private int _playbackStarted;
    private static readonly TimeSpan PlaybackPrebuffer = TimeSpan.FromMilliseconds(1800);
'@
if (-not $c.Contains($oldField)) { throw 'Campo de playback esperado nao encontrado.' }
$c = $c.Replace($oldField, $newField)

$oldDelta = @'
                SuppressMicrophoneDuringPlayback();
                _speakerBuffer?.AddSamples(pcm, 0, pcm.Length);
                Speaking?.Invoke();
'@
$newDelta = @'
                SuppressMicrophoneDuringPlayback();
                _speakerBuffer?.AddSamples(pcm, 0, pcm.Length);

                // Jitter buffer: acumula 1,8 s antes de iniciar. Assim pequenas variacoes
                // na chegada dos proximos chunks nao deixam o dispositivo sem PCM no meio da fala.
                if (Volatile.Read(ref _playbackStarted) == 0 &&
                    (_speakerBuffer?.BufferedDuration ?? TimeSpan.Zero) >= PlaybackPrebuffer)
                {
                    _speaker?.Play();
                    Interlocked.Exchange(ref _playbackStarted, 1);
                }
                Speaking?.Invoke();
'@
if (-not $c.Contains($oldDelta)) { throw 'Bloco de delta esperado nao encontrado.' }
$c = $c.Replace($oldDelta, $newDelta)

$oldCreated = @'
                case "response.created":
                    Interlocked.Exchange(ref _responseActive, 1);
                    SuppressMicrophoneDuringPlayback();
                    break;
'@
$newCreated = @'
                case "response.created":
                    Interlocked.Exchange(ref _responseActive, 1);
                    Interlocked.Exchange(ref _playbackStarted, 0);
                    try { _speaker?.Pause(); } catch { }
                    SuppressMicrophoneDuringPlayback();
                    break;
'@
if (-not $c.Contains($oldCreated)) { throw 'Bloco response.created esperado nao encontrado.' }
$c = $c.Replace($oldCreated, $newCreated)

$oldDone = @'
                case "response.done":
                case "response.cancelled":
                    Interlocked.Exchange(ref _responseActive, 0);
                    ScheduleMicrophoneResume();
                    break;
'@
$newDone = @'
                case "response.done":
                    Interlocked.Exchange(ref _responseActive, 0);
                    // Respostas curtas podem terminar antes de atingir 1,8 s: toca o restante imediatamente.
                    if (Volatile.Read(ref _playbackStarted) == 0 && (_speakerBuffer?.BufferedBytes ?? 0) > 0)
                    {
                        _speaker?.Play();
                        Interlocked.Exchange(ref _playbackStarted, 1);
                    }
                    ScheduleMicrophoneResume();
                    break;
                case "response.cancelled":
                    Interlocked.Exchange(ref _responseActive, 0);
                    ScheduleMicrophoneResume();
                    break;
'@
if (-not $c.Contains($oldDone)) { throw 'Bloco response.done esperado nao encontrado.' }
$c = $c.Replace($oldDone, $newDone)

$oldSpeaker = @'
        _speaker = new WaveOutEvent { DesiredLatency = 120, NumberOfBuffers = 3 };
        _speaker.Init(_speakerBuffer);
        _speaker.Play();
'@
$newSpeaker = @'
        _speaker = new WaveOutEvent { DesiredLatency = 120, NumberOfBuffers = 3 };
        _speaker.Init(_speakerBuffer);
        // O Play e iniciado somente apos o prebuffer de streaming.
        Interlocked.Exchange(ref _playbackStarted, 0);
'@
if (-not $c.Contains($oldSpeaker)) { throw 'Bloco de inicializacao V10.249 nao encontrado.' }
$c = $c.Replace($oldSpeaker, $newSpeaker)

$oldClear = @'
    private void ClearLocalPlayback()
    {
        try { _speakerBuffer?.ClearBuffer(); } catch { }
    }
'@
$newClear = @'
    private void ClearLocalPlayback()
    {
        try { _speaker?.Pause(); } catch { }
        try { _speakerBuffer?.ClearBuffer(); } catch { }
        Interlocked.Exchange(ref _playbackStarted, 0);
    }
'@
if (-not $c.Contains($oldClear)) { throw 'Bloco ClearLocalPlayback esperado nao encontrado.' }
$c = $c.Replace($oldClear, $newClear)

Set-Content $path $c -Encoding UTF8
$check = Get-Content $path -Raw
if ($check -notmatch 'PlaybackPrebuffer = TimeSpan\.FromMilliseconds\(1800\)' -or $check -notmatch '_speaker\?\.Pause\(\)') {
    throw 'Buffer delay V10.250 nao foi aplicado.'
}
Write-Host 'LIA: jitter buffer de 1,8 s aplicado; playback permanece PCM 24 kHz em 1x.'