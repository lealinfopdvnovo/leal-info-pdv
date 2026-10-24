$ErrorActionPreference = 'Stop'
$path = 'realtime\OpenAiRealtimeConnection.cs'
$c = Get-Content $path -Raw

# Correcao cirurgica: preserva Realtime, prompt, modelo, VAD, navegacao e conversa.
# O buffer anterior tinha somente 6 s e descartava PCM ao transbordar. Em respostas longas,
# isso podia eliminar trechos do fluxo e produzir a percepcao de fala em modo corrida.
$old = @'
        _speakerBuffer = new BufferedWaveProvider(new WaveFormat(24000, 16, 1))
        {
            BufferDuration = TimeSpan.FromSeconds(6),
            DiscardOnBufferOverflow = true,
            ReadFully = true
        };
        _speaker = new WaveOutEvent { DesiredLatency = 80, NumberOfBuffers = 3 };
'@
$new = @'
        _speakerBuffer = new BufferedWaveProvider(new WaveFormat(24000, 16, 1))
        {
            // Mantem os chunks PCM na ordem de chegada e com relogio fixo de 24 kHz.
            // A margem cobre respostas longas sem descartar audio para "alcancar" o streaming.
            BufferDuration = TimeSpan.FromSeconds(120),
            DiscardOnBufferOverflow = false,
            ReadFully = true
        };
        _speaker = new WaveOutEvent { DesiredLatency = 120, NumberOfBuffers = 3 };
'@
if (-not $c.Contains($old)) { throw 'Bloco de playback esperado nao encontrado; abortando para nao alterar outra logica.' }
$c = $c.Replace($old, $new)
Set-Content $path $c -Encoding UTF8

# Validacao defensiva: nao permitir regressao para descarte de audio.
$check = Get-Content $path -Raw
if ($check -notmatch 'BufferDuration = TimeSpan\.FromSeconds\(120\)' -or $check -notmatch 'DiscardOnBufferOverflow = false') {
    throw 'Correcao do streaming nao foi aplicada.'
}
Write-Host 'LIA: playback PCM estabilizado em 24 kHz, sem descarte de chunks.'
