"""Run against an actual booted emulator; force-stop before each independent launch."""
import io,json,re,subprocess,time
from pathlib import Path
from PIL import Image,ImageStat
OUT=Path('app/build/reports/splash-runtime');OUT.mkdir(parents=True,exist_ok=True)
PKG='br.com.lealinfo.vidraceiro'
def adb(*args):return subprocess.check_output(['adb',*args])
adb('shell','wm','size','540x960')
adb('shell','wm','density','240')
adb('shell','input','keyevent','82')
time.sleep(3)
adb('install','-r','app/build/outputs/apk/debug/app-debug.apk')
adb('shell','settings','put','global','window_animation_scale','1')
adb('shell','settings','put','global','transition_animation_scale','1')
adb('shell','settings','put','global','animator_duration_scale','1')
for attempt in (1,2):
    adb('shell','am','force-stop',PKG)
    adb('logcat','-c')
    record=subprocess.Popen(['adb','shell','screenrecord','--time-limit','9',f'/sdcard/angra-{attempt}.mp4'],stdout=subprocess.DEVNULL)
    adb('shell','am','start','-n',PKG+'/.SplashActivity')
    captured={};start=time.monotonic();frames=[]
    while time.monotonic()-start<15:
        log=adb('logcat','-d','-v','brief').decode()
        frames=[(int(t),float(a),int(p)) for t,a,p in re.findall(r'FRAME ms=(\d+) alpha=([0-9.E-]+) progress=(\d+)',log)]
        if frames:
            t=frames[-1][0]
            key=next((name for threshold,name in [(0,'initial'),(500,'fade'),(1600,'visible'),(3000,'progress'),(5000,'complete')] if t>=threshold and name not in captured),None)
            if key:
                data=adb('exec-out','screencap','-p');(OUT/f'{attempt}-{key}.png').write_bytes(data);captured[key]=(t,data)
            if t>=5000:break
        time.sleep(.06)
    (OUT/f'{attempt}-log.txt').write_text(log)
    record.wait(timeout=12)
    adb('pull',f'/sdcard/angra-{attempt}.mp4',str(OUT/f'{attempt}-cold-start.mp4'))
    print('Observed animation frames:',frames,flush=True)
    assert frames and frames[0]==(0,0.,0),frames
    assert frames[-1]==(5000,1.,10000),frames[-1:]
    assert all(b[0]>=a[0] and b[1]>=a[1] and b[2]>=a[2] for a,b in zip(frames,frames[1:])),frames
    assert any(250<=t<=1000 and 0<a<1 for t,a,p in frames),frames
    assert all(abs(p-t*2)<=2 for t,a,p in frames),frames
    for t,a,p in frames:
        if t>=1500:assert a==1.,(t,a)
    time.sleep(.5)
    adb('shell','uiautomator','dump','/sdcard/angra.xml')
    ui=adb('shell','cat','/sdcard/angra.xml').decode()
    assert 'ANGRA VIDROS' in ui and 'Nome do projeto' in ui and 'LEAL INFO' not in ui,ui
    def brightness(data):
        image=Image.open(io.BytesIO(data)).convert('RGB');w,h=image.size
        return sum(ImageStat.Stat(image.crop((w//8,h//8,w*7//8,h*7//8))).mean)
    assert 'initial' in captured and 'visible' in captured,captured.keys()
    early=brightness(captured['initial'][1]);visible=brightness(captured['visible'][1])
    assert visible>early+12,(early,visible,captured['initial'][0])
    (OUT/f'{attempt}-result.json').write_text(json.dumps({'passed':True,'frames':frames,'brightness_initial':early,'brightness_visible':visible,'main_screen':True},indent=2))
    print(f'PASS cold launch {attempt}: dark -> opacity -> 5000ms/100% -> main; brightness {early:.1f} -> {visible:.1f}')
