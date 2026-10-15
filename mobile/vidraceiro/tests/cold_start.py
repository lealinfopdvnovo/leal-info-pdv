"""Run against an actual booted emulator; force-stop before each independent launch."""
import io,json,re,subprocess,time
from pathlib import Path
from imageio_ffmpeg import get_ffmpeg_exe
OUT=Path('app/build/reports/splash-runtime');OUT.mkdir(parents=True,exist_ok=True)
PKG='br.com.lealinfo.vidraceiro.profissional022'
def adb(*args):return subprocess.check_output(['adb',*args])
adb('shell','wm','size','540x960')
adb('shell','wm','density','240')
adb('shell','input','keyevent','82')
time.sleep(3)
adb('install','-r','app/build/outputs/apk/debug/app-debug.apk')
adb('shell','settings','put','global','window_animation_scale','1')
adb('shell','settings','put','global','transition_animation_scale','1')
adb('shell','settings','put','global','animator_duration_scale','1')
for attempt,(screen_width,screen_height) in enumerate(((540,960),(540,1200),(800,1280)),1):
    adb('shell','wm','size',f'{screen_width}x{screen_height}')
    adb('shell','am','force-stop',PKG)
    adb('logcat','-c')
    record=subprocess.Popen(['adb','shell','screenrecord','--time-limit','9',f'/sdcard/angra-{attempt}.mp4'],stdout=subprocess.DEVNULL)
    time.sleep(1)  # Let screenrecord attach before starting the cold Activity.
    adb('shell','am','start','-n',PKG+'/br.com.lealinfo.vidraceiro.SplashActivity')
    captured={};start=time.monotonic();frames=[]
    while time.monotonic()-start<15:
        log=adb('logcat','-d','-v','brief').decode()
        frames=[(int(t),float(a),int(p)) for t,a,p in re.findall(r'FRAME ms=(\d+) alpha=([0-9.E-]+) progress=(\d+)',log)]
        if frames:
            t=frames[-1][0]
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
    assert 'ANGRA VIDROS' in ui and 'Seu trabalho, organizado.' in ui and 'LEAL INFO' not in ui,ui
    # Validate actual rendered pixels from continuous video, without blocking the
    # animation with synchronous screenshot commands on the emulator.
    video=OUT/f'{attempt}-cold-start.mp4'
    pixels=subprocess.check_output([get_ffmpeg_exe(),'-v','error','-i',str(video),
        '-vf','fps=20,crop=iw*0.75:ih*0.75:iw*0.125:ih*0.125,scale=1:1',
        '-f','rawvideo','-pix_fmt','rgb24','-'])
    brightness=[sum(pixels[i:i+3]) for i in range(0,len(pixels),3)]
    # Full artwork occupies the middle of the recording, before MainActivity.
    steady=sorted(brightness[60:112]);visible=steady[len(steady)//2]
    # A rendered frame within 15% of the fully visible artwork is dark;
    # the first frame can arrive after time zero on a busy software renderer.
    # Exact alpha zero and progress zero are independently asserted above.
    dark=next(i for i,v in enumerate(brightness[:60]) if v<visible*.15)
    full=next(i for i in range(dark+1,len(brightness)) if brightness[i]>=visible*.96)
    intermediate=[v for v in brightness[dark:full] if visible*.15<v<visible*.94]
    assert visible>80 and len(set(intermediate))>=3,(brightness,visible)
    assert all(b>=a-8 for a,b in zip(brightness[dark:full],brightness[dark+1:full+1])),brightness
    early=brightness[dark]
    (OUT/f'{attempt}-result.json').write_text(json.dumps({'passed':True,'frames':frames,'brightness_initial':early,'brightness_visible':visible,'video_brightness_20fps':brightness,'main_screen':True},indent=2))
    print(f'PASS cold launch {attempt}: dark -> opacity -> 5000ms/100% -> main; brightness {early:.1f} -> {visible:.1f}')
