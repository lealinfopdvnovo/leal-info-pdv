$ErrorActionPreference='Stop'
$p='LIC-AI/MainForm.cs'
$t=Get-Content $p -Raw -Encoding UTF8

# Microfones comuns de notebook/USB podem entregar RMS bem abaixo de 0.018.
$t=$t.Replace('if(rms>0.018){_speechDetected=true;_lastVoiceUtc=DateTime.UtcNow;}','if(rms>0.006){_speechDetected=true;_lastVoiceUtc=DateTime.UtcNow;}')
$t=$t.Replace('TimeSpan.FromMilliseconds(950)','TimeSpan.FromMilliseconds(800)')

# Em modo --voice a janela fica oculta. Antes, falhas do GPT eram apenas escritas no chat invisivel
# e o microfone nunca era reaberto. Agora o erro fica visivel, vai para o log e a conversa continua.
$old=@'
        catch (OperationCanceledException)
        {
            Append("LIC", "Resposta cancelada.");
        }
        catch (Exception ex)
        {
            Append("LIC", "Não consegui responder agora. " + ex.Message);
        }
'@
$new=@'
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
if(-not $t.Contains($old)){ throw 'Bloco SendAsync nao localizado para diagnostico V10.213' }
$t=$t.Replace($old,$new)

# Registra explicitamente que o audio ultrapassou o detector e entrou no processamento.
$t=$t.Replace('_processingVoice=true;'+[Environment]::NewLine+'                BeginInvoke(async ()=>await ProcessCapturedSpeechAsync());', '_processingVoice=true;'+[Environment]::NewLine+'                _ = WriteLogAsync("Silencio detectado; iniciando processamento da fala.");'+[Environment]::NewLine+'                BeginInvoke(async ()=>await ProcessCapturedSpeechAsync());')

Set-Content $p $t -Encoding UTF8
