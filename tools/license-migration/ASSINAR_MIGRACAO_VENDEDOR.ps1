$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
function Select-File($title,$filter){
    $d=[Windows.Forms.OpenFileDialog]::new();$d.Title=$title;$d.Filter=$filter
    try{if($d.ShowDialog() -ne [Windows.Forms.DialogResult]::OK){return $null};return $d.FileName}finally{$d.Dispose()}
}
try {
    $request=Select-File 'Solicitacao de migracao do computador correto' 'Solicitacao|*.json'
    if(-not $request){exit}
    $r=Get-Content -Raw -LiteralPath $request | ConvertFrom-Json
    $info="Cliente: $($r.ClientCode)`nHardware destino: $($r.ToHardwareSerial)`nRevisao: $($r.Revision)`nCheckpoint: $($r.StateAvailability)`n`nConfirme nos seus registros que este e o MESMO computador e que esta e a revisao atual.`nSem checkpoint, a rede permanecera bloqueada.`n`nAutorizar sem nova compra ou consumo de pontos?"
    if([Windows.Forms.MessageBox]::Show($info,'SOMENTE VENDEDOR — CONFIRMACAO OBRIGATORIA','YesNo','Warning') -ne 'Yes'){exit}
    $key=Select-File 'Chave privada ORIGINAL do vendedor — NUNCA enviar ao cliente' 'Chave do vendedor|*.pem'
    if(-not $key){exit}
    $output=[Windows.Forms.SaveFileDialog]::new();$output.Filter='Autorizacao|*.lealmigracao';$output.FileName='Autorizacao.lealmigracao'
    try{if($output.ShowDialog() -ne [Windows.Forms.DialogResult]::OK){exit};$dest=$output.FileName}finally{$output.Dispose()}
    & "$PSScriptRoot/Autorizar_Migracao.ps1" -Solicitacao $request -ChavePrivada $key -Saida $dest -ConfirmoMesmoComputador -ConfirmoRevisaoAtual
    [void][Windows.Forms.MessageBox]::Show('Autorizacao criada. Envie somente .lealmigracao, nunca o PEM.','Concluido')
} catch { [void][Windows.Forms.MessageBox]::Show('Nao autorizado. Verifique a solicitacao e a chave original. Nenhuma chave privada sera exibida.','Falha');exit 1 }
