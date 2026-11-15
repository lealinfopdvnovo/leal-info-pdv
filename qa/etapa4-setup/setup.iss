[Setup]
AppId={{A8005005-FA24-4C37-91E4-B9AD30AC2026}
AppName=LEAL INFO PDV - TESTE ANGRA ETAPA4 CORRIGIDO
AppVersion=10.468.2
AppPublisher=LEAL INFO
DefaultDirName={autopf}\LEAL INFO PDV - TESTE ANGRA ETAPA4 CORRIGIDO
DisableDirPage=yes
UsePreviousAppDir=no
MinVersion=10.0.17763
DefaultGroupName=LEAL INFO PDV - TESTE ANGRA ETAPA4 CORRIGIDO
OutputDir=setup-output
OutputBaseFilename=LEAL_INFO_ANGRA_QA_ATUALIZADOR
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
[Run]
Filename: "{app}\LealInfoPDV.exe"; Parameters: "--qa-start-notifier"; Flags: nowait runasoriginaluser
[Code]
function ApprovedExistingQA(): Boolean;
var P: String;
begin
 P := ExpandConstant('{autopf}\LEAL INFO PDV - TESTE ANGRA ETAPA4 CORRIGIDO');
 Result := FileExists(P+'\.qa-test-only') and FileExists(P+'\LealInfoPDV.dll') and FileExists(P+'\Extensions\PET\manifest.json');
 if Result then Result := (GetSHA256OfFile(P+'\LealInfoPDV.dll') = '165f58c122a511a09d4ccd39465bdfddf9b935635d1bbd6ccc5b4da2c717184f') and (GetSHA256OfFile(P+'\Extensions\PET\manifest.json') = 'fd37bbd88ae30633b15215ff519f1d87c5a5a621e6e55605bd465ac3ba1cdd14');
end;
procedure CurStepChanged(CurStep: TSetupStep);
var P, B: String;
begin
 if (CurStep=ssInstall) and ApprovedExistingQA() then begin
  P:=ExpandConstant('{app}'); B:=P+'\.qa-bridge-backup';
  ForceDirectories(B+'\Extensions\PET');
  if not FileCopy(P+'\LealInfoPDV.exe',B+'\LealInfoPDV.exe',False) then RaiseException('Falha no backup QA.');
  if not FileCopy(P+'\LealInfoPDV.dll',B+'\LealInfoPDV.dll',False) then RaiseException('Falha no backup QA.');
  if not FileCopy(P+'\LealInfoPDV.deps.json',B+'\LealInfoPDV.deps.json',False) then RaiseException('Falha no backup QA.');
  if not FileCopy(P+'\Extensions\PET\Pet.dll',B+'\Extensions\PET\Pet.dll',False) then RaiseException('Falha no backup QA.');
  if not FileCopy(P+'\Extensions\PET\manifest.json',B+'\Extensions\PET\manifest.json',False) then RaiseException('Falha no backup QA.');
 end;
end;
function InitializeSetup(): Boolean;
begin
 Result := not DirExists(ExpandConstant('{autopf}\LEAL INFO PDV - TESTE ANGRA ETAPA4 CORRIGIDO')) or ApprovedExistingQA();
 if not Result then SuppressibleMsgBox('A pasta exclusiva de testes já existe. Nada será sobrescrito. Feche esta instalação para preservar esse ambiente.',mbError,MB_OK,IDOK);
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
var Expected: String;
begin
 Expected := ExpandConstant('{autopf}\LEAL INFO PDV - TESTE ANGRA ETAPA4 CORRIGIDO');
 Result := '';
 if CompareText(ExpandFileName(WizardDirValue), Expected) <> 0 then
  Result := 'Pasta recusada. Este setup só instala no diretório exclusivo de testes ANGRA ETAPA4.'
 else if (DirExists(Expected) or FileExists(Expected)) and not ApprovedExistingQA() then
  Result := 'A pasta de testes já existe. Nada será sobrescrito.';
end;
