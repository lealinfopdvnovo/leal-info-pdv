[Setup]
AppId={{A8005005-FA24-4C37-91E4-B9AD30AC2026}
AppName=LEAL INFO PDV - TESTE ANGRA ETAPA4 CORRIGIDO
AppVersion=10.468.1
AppPublisher=LEAL INFO
DefaultDirName={autopf}\LEAL INFO PDV - TESTE ANGRA ETAPA4 CORRIGIDO
DisableDirPage=yes
UsePreviousAppDir=no
MinVersion=10.0.17763
DefaultGroupName=LEAL INFO PDV - TESTE ANGRA ETAPA4 CORRIGIDO
OutputDir=setup-output
OutputBaseFilename=LEAL_INFO_TESTE_ANGRA_ETAPA4_CORRIGIDO
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
 Result := not DirExists(ExpandConstant('{autopf}\LEAL INFO PDV - TESTE ANGRA ETAPA4 CORRIGIDO'));
 if not Result then SuppressibleMsgBox('A pasta exclusiva de testes já existe. Nada será sobrescrito. Feche esta instalação para preservar esse ambiente.',mbError,MB_OK,IDOK);
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
var Expected: String;
begin
 Expected := ExpandConstant('{autopf}\LEAL INFO PDV - TESTE ANGRA ETAPA4 CORRIGIDO');
 Result := '';
 if CompareText(ExpandFileName(WizardDirValue), Expected) <> 0 then
  Result := 'Pasta recusada. Este setup só instala no diretório exclusivo de testes ANGRA ETAPA4.'
 else if DirExists(Expected) or FileExists(Expected) then
  Result := 'A pasta de testes já existe. Nada será sobrescrito.';
end;
