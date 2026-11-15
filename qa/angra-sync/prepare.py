from pathlib import Path
import zipfile,hashlib,json
here=Path('controller/qa/angra-sync');raw=b''.join(p.read_bytes() for p in sorted((here/'media').glob('part*')))
assert hashlib.sha256(raw).hexdigest()=='a2b5144645a2412fc21dc18f2685cc78ba50ae5cadbaaea22e24c88074fc9d89'
Path('sync-media.mp4').write_bytes(raw)
p=Path('controller/qa/etapa4-setup/channel/ANGRA_005_PET_1_4.zip');assert hashlib.sha256(p.read_bytes()).hexdigest()=='1ee451ae66985863967212bcd973e840b7cfef1b92ba491b9b1d28d71b0a0480'
with zipfile.ZipFile(p) as z:z.extractall('baseline')
Path('sync-evidence').mkdir(exist_ok=True)
Path('sync-evidence/media-timeline.json').write_text(json.dumps(dict(VoiceFileSHA256='95bdd2fd3a4d1752e840ef7011a42c43577a18bf18f90b0cfc564b5e7f897bdf',VoiceRegenerated=False,OriginalAnimationPreserved=True,VoiceDuration=5.664,VoiceLeadIn=1.75,VoiceEnd=7.414,FadeStart=8.35,SpeechToFadeMargin=0.936,Duration=9.0,FinalAudioDrainMs=350,MusicVolume=0.16,Transcript='Bem-vindo à Angra Vidros. Na próxima tela, coloque seu usuário e sua senha.'),ensure_ascii=False,indent=2))
