$ErrorActionPreference='Stop'
$p='LIC-AI/MainForm.cs'
$t=Get-Content $p -Raw -Encoding UTF8

# Microfones comuns de notebook/USB podem entregar RMS baixo.
$t=$t.Replace('if(rms>0.018){_speechDetected=true;_lastVoiceUtc=DateTime.UtcNow;}','if(rms>0.006){_speechDetected=true;_lastVoiceUtc=DateTime.UtcNow;}')
$t=$t.Replace('TimeSpan.FromMilliseconds(950)','TimeSpan.FromMilliseconds(450)')

# Patch robusto do catch de SendAsync, independente de CRLF/LF e pequenos espacos.
$pattern='(?s)        catch \(OperationCanceledException\)\s*\{\s*Append\("LIC", "Resposta cancelada\."\);\s*\}\s*catch \(Exception ex\)\s*\{\s*Append\("LIC", "Não consegui responder agora\. " \+ ex\.Message\);\s*\}'
$replacement=@'
        catch (OperationCanceledException)
        {
            Append("LIC", "Resposta cancelada.");
            if (speakReply)
            {
                await WriteLogAsync("Resposta GPT cancelada.");
                _processingVoice = false;
                if (_voiceMode) StartRecognition();
            }
        }
        catch (Exception ex)
        {
            Append("LIC", "Não consegui responder agora. " + ex.Message);
            if (speakReply)
            {
                await WriteLogAsync("ERRO GPT/NAVEGACAO: " + ex);
                await SendStatusAsync("ERROR");
                MessageBox.Show(this,
                    "A LIC ouviu sua fala, mas não conseguiu gerar a resposta.\n\n" + ex.Message,
                    "LIC ASSISTENTE AI - Resposta",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                _processingVoice = false;
                if (_voiceMode)
                {
                    await SendStatusAsync("LISTENING");
                    StartRecognition();
                }
            }
        }
'@
$patched=[regex]::Replace($t,$pattern,$replacement,1)
if($patched -ne $t){
    $t=$patched
} else {
    Write-Host 'Catch de SendAsync ja esta em formato diferente; mantendo implementacao atual.'
}

# Loga a passagem pelo detector de silencio sem depender do estilo de quebra de linha.
$t=[regex]::Replace($t,'_processingVoice=true;\s*BeginInvoke\(async \(\)=>await ProcessCapturedSpeechAsync\(\)\);','_processingVoice=true;'+[Environment]::NewLine+'                _ = WriteLogAsync("Silencio detectado; iniciando processamento da fala.");'+[Environment]::NewLine+'                BeginInvoke(async ()=>await ProcessCapturedSpeechAsync());',1)

Set-Content $p $t -Encoding UTF8
