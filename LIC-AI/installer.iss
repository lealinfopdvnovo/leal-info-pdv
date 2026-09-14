#define MyAppName "LIC AI"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Leal Info Conectado"
#define MyAppExeName "LIC AI.exe"

[Setup]
AppId={{A7FBB1C6-7C1A-4D31-9F27-6A42B073A8D1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\LIC AI
DefaultGroupName=LIC AI
OutputDir=SETUP_PRONTO
OutputBaseFilename=Setup_LIC_AI_V1_0_0
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\LIC AI"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\LIC AI"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na area de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir LIC AI"; Flags: nowait postinstall skipifsilent
