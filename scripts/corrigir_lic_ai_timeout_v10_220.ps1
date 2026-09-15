$ErrorActionPreference='Stop'
$p='LIC-AI/MainForm.cs'
$t=Get-Content $p -Raw -Encoding UTF8

# Nenhuma chamada de resposta pode prender a instancia invisivel indefinidamente.
$t=$t.Replace('_cts = new CancellationTokenSource();','_cts = new CancellationTokenSource(TimeSpan.FromSeconds(35));')
$t=$t.Replace('            var reply = await _engine.SendNavigationAsync(text, _cts.Token);','            await WriteLogAsync("Enviando texto para a OpenAI: " + text);'+[Environment]::NewLine+'            var reply = await _engine.SendNavigationAsync(text, _cts.Token);'+[Environment]::NewLine+'            await WriteLogAsync("Resposta da OpenAI recebida.");')

# Em modo invisivel, qualquer falha encerra o processo e devolve o botao ao repouso.
$oldCancel=@'
                _processingVoice = false;
                if (_voiceMode) StartRecognition();
'@
$newCancel=@'
                _processingVoice = false;
                if (_voiceOnly)
                {
                    _voiceMode = false;
                    await SendStatusAsync("IDLE");
                    Close();
                }
                else if (_voiceMode) StartRecognition();
'@
$t=$t.Replace($oldCancel,$newCancel)

$oldError=@'
                _processingVoice = false;
                if (_voiceMode)
                {
                    await SendStatusAsync("LISTENING");
                    StartRecognition();
                }
'@
$newError=@'
                _processingVoice = false;
                if (_voiceOnly)
                {
                    _voiceMode = false;
                    await SendStatusAsync("IDLE");
                    Close();
                }
                else if (_voiceMode)
                {
                    await SendStatusAsync("LISTENING");
                    StartRecognition();
                }
'@
$t=$t.Replace($oldError,$newError)

Set-Content $p $t -Encoding UTF8
Write-Host 'Timeout e encerramento seguro da LIA V10.220 aplicados.'
