#define MyAppName "LEAL INFO PDV"
#define MyAppVersion "10.259"
#define MyAppPublisher "LEAL INFO CONECTADO"
#define MyAppExeName "LealInfoPDV.exe"

[Setup]
AppId={{8B1A4E75-ED29-4D54-9A63-0C1300000130}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName=C:\LEAL INFO PDV
DefaultGroupName={#MyAppName}
UsePreviousAppDir=yes
OutputDir=SETUP_PRONTO
OutputBaseFilename=Setup_LEAL_INFO_PDV_V10_259_FINAL
SetupIconFile=Assets\lealinfo.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayName={#MyAppName} V{#MyAppVersion}
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no
CloseApplicationsFilter=*.exe,*.dll

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  { Encerra PDV e LIA antes de substituir DLLs/EXEs bloqueados. }
  Exec(ExpandConstant('{cmd}'), '/C taskkill /F /T /IM LealInfoPDV.exe >nul 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{cmd}'), '/C taskkill /F /T /IM LicAi.exe >nul 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(800);
  Result := '';
end;

[InstallDelete]
Type: files; Name: "{app}\*.exe"
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.deps.json"
Type: files; Name: "{app}\*.runtimeconfig.json"

[Files]
Source: "publish_setup\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\LEAL INFO PDV"; Filename: "{app}\LealInfoPDV.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\LEAL INFO PDV"; Filename: "{app}\LealInfoPDV.exe"; WorkingDir: "{app}"
Name: "{group}\LIC AI"; Filename: "{app}\LIC-AI\LicAi.exe"; WorkingDir: "{app}\LIC-AI"
