#define MyAppName "LEAL INFO PDV"
#define MyAppVersion "10.165"
#define MyAppPublisher "LEAL INFO CONECTADO"
#define MyAppExeName "LealInfoPDV.exe"

[Setup]
AppId={{8B1A4E75-ED29-4D54-9A63-0C1300000130}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\LEAL INFO PDV
DefaultGroupName={#MyAppName}
UsePreviousAppDir=yes
OutputDir=SETUP_PRONTO
OutputBaseFilename=Setup_LEAL_INFO_PDV_V10_165_LIA_FINAL
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

[InstallDelete]
Type: files; Name: "{app}\*.exe"
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.deps.json"
Type: files; Name: "{app}\*.runtimeconfig.json"

[Files]
Source: "publish_setup\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na area de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir {#MyAppName}"; Flags: nowait postinstall skipifsilent
