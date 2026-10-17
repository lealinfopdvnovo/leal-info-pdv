import subprocess,pathlib
pkg='br.com.lealinfo.vidraceiro.profissional022'
def run(*args):return subprocess.check_output(['adb',*args]).decode()
run('install','-r','mobile/vidraceiro/app/build/outputs/apk/debug/app-debug.apk')
run('install','-r','mobile/vidraceiro/app/build/outputs/apk/androidTest/debug/app-debug-androidTest.apk')
run('shell','run-as',pkg,'mkdir','-p','files')
run('push','pair-evidence/pairing.json','/data/local/tmp/pairing.json')
run('shell','run-as',pkg,'cp','/data/local/tmp/pairing.json','files/pairing.json')
out=run('shell','am','instrument','-w','-e','phase','syncPairing',pkg+'.test/br.com.lealinfo.vidraceiro.MeasureFlowInstrumentation')
pathlib.Path('pair-evidence/android-results.txt').write_text(out)
print(out)
assert 'PASS PROFESSIONAL_SYNCPAIRING' in out,out
with open('pair-evidence/proof-pairing.png','wb') as f:f.write(subprocess.check_output(['adb','exec-out','run-as',pkg,'cat','files/proof-pairing.png']))
