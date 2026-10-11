import os,json,urllib.request,urllib.parse,gzip,hashlib,pathlib,time
SITE='novo-91da7436';BASE='https://firebasehosting.googleapis.com/v1beta1/';token=os.environ['ACCESS_TOKEN'];report={}
def api(path,method='GET',body=None):
 data=None if body is None else json.dumps(body).encode()
 req=urllib.request.Request(BASE+path,data=data,method=method,headers={'Authorization':'Bearer '+token,'Content-Type':'application/json'})
 with urllib.request.urlopen(req,timeout=60) as r:return json.load(r)
def live():return api('sites/'+SITE+'/channels/live')['release']['version']['name']
old=live();version=api(old);report['previousVersion']=old;report['config']=version.get('config',{})
files={};page=''
while True:
 result=api(old+'/files?pageSize=1000'+('&pageToken='+urllib.parse.quote(page,safe='') if page else ''))
 for f in result.get('files',[]):
  assert f.get('hash') and f.get('status')=='ACTIVE', 'Arquivo atual sem hash ativo'
  files[f['path']]=f['hash']
 page=result.get('nextPageToken');
 if not page:break
assert files and '/track.html' in files, 'Nao e a hospedagem esperada do SpeedFood'
assert '/index.html' not in files, 'Pagina inicial ja existe; abortar'
report['preservedFiles']=files.copy()
added={}
for n in ['index.html','styles.css','app.js','firebase-config.js']:
 path='/licencas/'+n;assert path not in files, 'Colisao de caminho: '+path
 added[path]=pathlib.Path('tools/licensing-cloud/public/'+n).read_bytes()
added['/index.html']=b'<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><meta http-equiv="refresh" content="0;url=/licencas/index.html"><title>Leal Info - Licencas</title></head><body><a href="/licencas/index.html">Abrir gerador de licencas Leal Info</a></body></html>'
compressed={p:gzip.compress(b,mtime=0) for p,b in added.items()}
for p,b in compressed.items():files[p]=hashlib.sha256(b).hexdigest()
new=api('sites/'+SITE+'/versions','POST',{'config':version.get('config',{})})['name'];report['newVersion']=new
result=api(new+':populateFiles','POST',{'files':files})
required=result.get('uploadRequiredHashes',[]);byhash={hashlib.sha256(b).hexdigest():b for b in compressed.values()}
assert all(h in byhash for h in required),'Servidor exige reenvio de arquivo protegido; abortar'
for h in required:
 req=urllib.request.Request(result['uploadUrl']+'/'+h,data=byhash[h],method='POST',headers={'Authorization':'Bearer '+token,'Content-Type':'application/octet-stream'})
 with urllib.request.urlopen(req,timeout=60) as r:r.read()
api(new+'?updateMask=status','PATCH',{'status':'FINALIZED'})
assert live()==old,'Hosting mudou durante operacao; abortar antes da publicacao'
api('sites/'+SITE+'/releases?versionName='+urllib.parse.quote(new,safe=''),'POST',{'message':'Restaurar portal de licencas preservando todos os arquivos e config do SpeedFood'})
report['addedPaths']=list(added);report['released']=True
pathlib.Path('restoration-report.json').write_text(json.dumps(report,indent=2))
for p in ['/licencas/index.html','/licencas/app.js','/licencas/styles.css','/licencas/firebase-config.js','/track.html','/index.html']:
 for attempt in range(12):
  try:
   with urllib.request.urlopen('https://'+SITE+'.web.app'+p+'?restore='+str(time.time_ns()),timeout=20) as r:
    assert r.status==200
    if p in added:assert r.read()==added[p], 'Conteudo publicado diferente'
   print('PASS HTTP 200 '+p);break
  except Exception:
   if attempt==11:raise
   time.sleep(5)
print('PASS portal publicado; arquivos/config existentes preservados; sem regras/functions/dados')
