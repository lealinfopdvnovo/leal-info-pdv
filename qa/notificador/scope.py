import subprocess
from pathlib import Path
base='e249c200747f0298df916f25bb19b537ae973bae'
changed=subprocess.check_output(['git','diff','--name-only',base,'HEAD'],text=True,encoding='utf-8').splitlines()
allowed={'LealInfoPDV.csproj','UpdateManager.cs','MainForm.cs','UpdateNotificationForm.cs','MainForm.Notificador.cs','UpdateNotifierHost.cs','.github/workflows/notificador_casa_isolado.yml','docs/notificador-windows.md'}
assert all(p in allowed or p.startswith('qa/notificador/') or p.startswith('tools/UpdateNotifier/') for p in changed),changed
old=subprocess.check_output(['git','show',base+':UpdateManager.cs'],text=True,encoding='utf-8')
assert Path('UpdateManager.cs').read_text(encoding='utf-8')==old.replace('CurrentVersion="10.446"','CurrentVersion="10.447"')
old=subprocess.check_output(['git','show',base+':MainForm.cs'],text=True,encoding='utf-8')
assert Path('MainForm.cs').read_text(encoding='utf-8')==old.replace('            _ = UpdateManager.CheckForUpdatesAsync(this, true);','            if (Environment.GetCommandLineArgs().Contains("--central-atualizacoes"))\n                _ = UpdateManager.ShowUpdateCenterAsync(this);\n            else _ = UpdateManager.CheckForUpdatesAsync(this, true);',1)
native=Path('tools/UpdateNotifier/notifier.cpp').read_text(encoding='utf-8')
for forbidden in ('sqlite3','Stop-Process','TerminateProcess','URLDownloadToFile','WinHttpWriteData','network.license','cash_movements'):
 assert forbidden not in native,forbidden
assert 'WINHTTP_FLAG_SECURE' in native and 'WINHTTP_OPTION_REDIRECT_POLICY_NEVER' in native
print('PASS source scope: business database licensing finance stock and transactional worker unchanged; native agent never installs or downloads packages')
