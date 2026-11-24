[Setup]
AppId=LealInfoPDV-Loja001-Recuperacao-10469
AppName=LEAL INFO PDV - Recuperação Loja 001
AppVersion=10.469
AppPublisher=LEAL INFO
DefaultDirName={autopf}\LEAL INFO PDV - Loja 001 Recuperacao
DisableDirPage=yes
UsePreviousAppDir=no
MinVersion=10.0.17763
OutputDir=.
OutputBaseFilename=LEAL_INFO_001_RECUPERACAO_10_469
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
Uninstallable=no
[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
[Files]
Source: "..\..\..\loja-publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
[Icons]
Name: "{autodesktop}\LEAL INFO PDV - Loja 001 Recuperacao"; Filename: "{app}\LealInfoPDV.exe"; WorkingDir: "{app}"
[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var Expected: String;
begin
 Expected := ExpandConstant('{autopf}\LEAL INFO PDV - Loja 001 Recuperacao');
 Result := '';
 if CompareText(ExpandFileName(WizardDirValue), Expected) <> 0 then
  Result := 'Selecione o diretório exclusivo de recuperação.'
 else if DirExists(Expected) or FileExists(Expected) then
  Result := 'O diretório exclusivo de recuperação já existe.';
end;
