[Setup]
AppId={{D9039426-FA24-4C37-91E4-B9AD30AC2026}
AppName=LEAL INFO PDV - TESTE ANGRA ETAPA4
AppVersion=10.468.0
AppPublisher=LEAL INFO
DefaultDirName={autopf}\LEAL INFO PDV - TESTE ANGRA ETAPA4
DisableDirPage=yes
DefaultGroupName=LEAL INFO PDV - TESTE ANGRA ETAPA4
OutputDir=setup-output
OutputBaseFilename=LEAL_INFO_TESTE_ANGRA_ETAPA4
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayName=LEAL INFO PDV - SOMENTE TESTE ANGRA ETAPA4
SetupLogging=yes
[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
[Files]
Source: "qa-publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
[Icons]
Name: "{autodesktop}\LEAL INFO PDV - TESTE ANGRA (Etapa 4)"; Filename: "{app}\LealInfoPDV.exe"; WorkingDir: "{app}"
[Code]
function InitializeSetup(): Boolean;
begin
 Result := not DirExists(ExpandConstant('{autopf}\LEAL INFO PDV - TESTE ANGRA ETAPA4'));
 if not Result then MsgBox('A pasta exclusiva de testes já existe. Nada será sobrescrito. Feche esta instalação para preservar esse ambiente.',mbError,MB_OK);
end;
