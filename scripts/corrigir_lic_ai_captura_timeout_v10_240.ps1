$ErrorActionPreference='Stop'
$p='LIC-AI/MainForm.cs'
$t=Get-Content $p -Raw -Encoding UTF8

# Cada captura recebe um identificador. Assim um temporizador antigo nunca processa
# o áudio de uma nova captura.
if($t -notmatch '_voiceCaptureSession')
{
    $field='    private DateTime _lastVoiceUtc;'
    if(!$t.Contains($field)){throw 'Campo de controle de voz nao localizado'}
    $fields=$field+[Environment]::NewLine+
        '    private int _voiceCaptureSession;'+[Environment]::NewLine+
        '    private int _emptyVoiceCaptures;'
    $t=$t.Replace($field,$fields)
}

# Arma um limite de quatro segundos assim que o NAudio começa a gravar. O detector
# de silêncio continua sendo a via normal e mais rápida.
$startRecording='            _microphone.StartRecording();'
if(!$t.Contains($startRecording)){throw 'Inicio da gravacao NAudio nao localizado'}
if($t -notmatch 'ForceVoiceCaptureAfterTimeoutAsync\(captureSession\)')
{
    $armed=$startRecording+[Environment]::NewLine+
        '            var captureSession = System.Threading.Interlocked.Increment(ref _voiceCaptureSession);'+[Environment]::NewLine+
        '            _ = ForceVoiceCaptureAfterTimeoutAsync(captureSession);'
    $t=$t.Replace($startRecording,$armed)
}

$processAnchor='    private async Task ProcessCapturedSpeechAsync()'
if(!$t.Contains($processAnchor)){throw 'Processamento da fala nao localizado'}
if($t -notmatch 'private async Task ForceVoiceCaptureAfterTimeoutAsync')
{
    $timeout=@'
    private async Task ForceVoiceCaptureAfterTimeoutAsync(int captureSession)
    {
        await Task.Delay(TimeSpan.FromSeconds(4));
        if (captureSession != _voiceCaptureSession || !_voiceMode || !_recognizing || _processingVoice)
            return;

        await WriteLogAsync("Tempo maximo de captura atingido; processando o audio automaticamente.");
        _processingVoice = true;
        if (!IsDisposed && IsHandleCreated)
            BeginInvoke(async () => await ProcessCapturedSpeechAsync());
    }

'@
    $t=$t.Replace($processAnchor,$timeout+$processAnchor)
}

# Se o Whisper não reconhecer nada duas vezes seguidas, exibe um diagnóstico
# visível em vez de deixar a esfera azul indefinidamente.
$empty='            if(string.IsNullOrWhiteSpace(heard)){_processingVoice=false;if(_voiceMode)StartRecognition();return;}'
if(!$t.Contains($empty)){throw 'Tratamento de transcricao vazia nao localizado'}
$emptyReplacement=@'
            if(string.IsNullOrWhiteSpace(heard))
            {
                _emptyVoiceCaptures++;
                _processingVoice=false;
                if(_emptyVoiceCaptures >= 2)
                {
                    _voiceMode=false;
                    await SendStatusAsync("ERROR");
                    await WriteLogAsync("O microfone gravou, mas o Whisper nao reconheceu voz em duas tentativas.");
                    MessageBox.Show(
                        "O microfone foi aberto, mas nenhuma voz foi reconhecida em duas tentativas.\n\n"+
                        "Confira se o microfone correto esta selecionado no Windows e fale perto dele.",
                        "LIC ASSISTENTE AI - Microfone",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    if(_voiceOnly) Close();
                    return;
                }
                if(_voiceMode) StartRecognition();
                return;
            }
            _emptyVoiceCaptures=0;
'@
$t=$t.Replace($empty,$emptyReplacement.TrimEnd())

Set-Content $p $t -Encoding UTF8
Write-Host 'Timeout seguro da captura de voz V10.240 aplicado.'
