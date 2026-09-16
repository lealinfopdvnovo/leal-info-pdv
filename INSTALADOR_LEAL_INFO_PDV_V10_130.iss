#define MyAppName "LEAL INFO PDV"
#define MyAppVersion "10.239"
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
OutputBaseFilename=Setup_LEAL_INFO_PDV_V10_239_FINAL
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
Name: "{group}\LIC AI"; Filename: "{app}\LIC-AI\LicAi.exe"
Name: "{autodesktop}\LIC AI"; Filename: "{app}\LIC-AI\LicAi.exe"
