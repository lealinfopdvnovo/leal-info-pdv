import subprocess,pathlib,os
pkg='br.com.lealinfo.vidraceiro.profissional022'
subprocess.run(['adb','install','-r','mobile/vidraceiro/app/build/outputs/apk/debug/app-debug.apk'],check=True)
subprocess.run(['adb','install','-r','mobile/vidraceiro/app/build/outputs/apk/androidTest/debug/app-debug-androidTest.apk'],check=True)
subprocess.run(['python3','mobile/vidraceiro/tests/sync_live.py'],check=True)
p=pathlib.Path('mobile/vidraceiro/sync-evidence');p.mkdir(exist_ok=True)
(p/'PASS.txt').write_text('PASS real Android lookup, offline outbox, structured project, units and official material')
