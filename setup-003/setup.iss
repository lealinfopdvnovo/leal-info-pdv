[Setup]
AppId=LEAL-PDV-CORRECAO-003-10416
AppName=LEAL INFO PDV - Correcao 003
AppVersion=10.416
DefaultDirName={autopf}\LEAL INFO PDV
DisableDirPage=no
DisableProgramGroupPage=yes
CreateAppDir=no
Uninstallable=no
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=Setup_Correcao_PDV_003_10_416
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
[Files]
Source: "bundle\*"; Flags: dontcopy
[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
  Args: String;
begin
  Result := '';
  ExtractTemporaryFile('activate.ps1');
  ExtractTemporaryFile('worker.ps1');
  ExtractTemporaryFile('candidate.json');
  ExtractTemporaryFile('payload.zip');
  Args := '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{tmp}\activate.ps1') + '" -App "' + ExpandConstant('{app}') + '"';
  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Args, '', SW_HIDE, ewWaitUntilTerminated, Code) then
    Result := 'Nao foi possivel iniciar o aplicador.'
  else if Code <> 0 then
    Result := 'A atualizacao nao foi confirmada. Feche o PDV e consulte %TEMP%\LEAL_SETUP_003_ERRO.txt.';
end;
