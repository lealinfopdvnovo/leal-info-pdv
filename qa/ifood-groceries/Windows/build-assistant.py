from pathlib import Path
root=Path(__file__).resolve().parent.parent
code=(root/'Core/Authentication.cs').read_text().replace('#nullable disable\n','')+'\n'+(root/'Windows/AssistantForm.cs').read_text()
assert "\n'@" not in code
ps='''# LEAL INFO: diagnostico isolado do aplicativo iFood Teste (D).
# Nao requer administrador, SDK, instalacao ou alteracao do PDV.
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Este assistente requer Windows.' }
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$source = @'
'''+code+'''\n'@
try {
    Add-Type -TypeDefinition $source -ReferencedAssemblies @('System.dll','System.Core.dll','System.Net.Http.dll','System.Runtime.Serialization.dll','System.Xml.dll','System.Windows.Forms.dll','System.Drawing.dll') -ErrorAction Stop
    [LealInfo.IFood.Assistant.AssistantForm]::Run()
} catch {
    Write-Host 'Nao foi possivel iniciar o assistente. Nenhuma credencial foi salva.'
    Write-Host 'Informe ao Work: falha local ao iniciar assistente Windows.'
    Read-Host 'Pressione Enter para fechar'
}
'''
(root/'Windows/Conectar_iFood_Teste.ps1').write_text(ps,encoding='utf-8-sig')
(root/'Windows/ABRIR_IFOOD_TESTE.cmd').write_bytes(b'@echo off\r\ncd /d "%~dp0"\r\npowershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0Conectar_iFood_Teste.ps1"\r\n')
print('Assistant generated from the same Authentication.cs tested by the .NET suite.')
