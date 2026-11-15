from pathlib import Path
import subprocess
out=Path('integration-evidence');out.mkdir(exist_ok=True)
root=Path('android-source/mobile/vidraceiro/app/build/outputs/apk')
for p in [root/'debug/app-debug.apk',root/'androidTest/debug/app-debug-androidTest.apk']:subprocess.run(['adb','install','-r',str(p)],check=True)
r=subprocess.run(['adb','shell','am','instrument','-w','br.com.lealinfo.vidraceiro.angraqa005.test/br.com.lealinfo.vidraceiro.QaIntegrationInstrumentation'],capture_output=True,text=True,timeout=180)
(out/'android.txt').write_text(r.stdout+'\n'+r.stderr)
print(r.stdout)
assert r.returncode==0 and 'PASS ANDROID_QA005_INTEGRATION' in r.stdout and 'FAIL ' not in r.stdout
for name in ['qa-integration.png','qa-integration.txt']:
 r=subprocess.run(['adb','exec-out','run-as','br.com.lealinfo.vidraceiro.angraqa005','cat','files/'+name],capture_output=True,check=True);(out/name).write_bytes(r.stdout)
