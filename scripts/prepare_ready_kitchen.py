"""Build an attributed ready-menu bundle from explicit Wikimedia Commons photo pages.
No store database or feeds are opened here. Any missing or unlicensed photo fails the build.
"""
import hashlib,html,json,re,time
from pathlib import Path
from urllib.request import Request,urlopen
from urllib.parse import urlencode,urlparse
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'ready-kitchen/generated'
UA='LealInfoPDV-ReadyKitchen/1.0 (https://github.com/lealinfopdvnovo/leal-info-pdv)'
def get(url):
    host=urlparse(url).hostname or ''
    if host not in ('commons.wikimedia.org','upload.wikimedia.org','thumb.wikimedia.org'):raise ValueError('Unexpected image host '+repr(host)+' URL '+repr(url))
    for attempt in range(3):
        try:
            with urlopen(Request(url,headers={'User-Agent':UA}),timeout=25) as r:
                data=r.read(4_000_001)
                if len(data)>4_000_000:raise ValueError('Photo exceeds size limit')
                return data
        except Exception:
            if attempt==2:raise
            time.sleep(2*(attempt+1))
def clean(text):return html.unescape(re.sub('<[^>]+>','',text)).strip()
def main():
    seeds=json.loads((ROOT/'ready-kitchen/seeds.json').read_text(encoding='utf-8'))
    OUT.mkdir(parents=True,exist_ok=True)
    query={'action':'query','format':'json','prop':'imageinfo','iiprop':'url|extmetadata','iiurlwidth':512,'redirects':1,'titles':'|'.join(s['CommonsFile'] for s in seeds)}
    response=json.loads(get('https://commons.wikimedia.org/w/api.php?'+urlencode(query)))['query']
    by_title={p['title']:p for p in response['pages'].values()}
    for group in ('redirects','normalized'):
        for alias in response.get(group,[]):
            if alias['to'] in by_title:by_title[alias['from']]=by_title[alias['to']]
    result=[]
    for s in seeds:
        page=by_title.get(s['CommonsFile'])
        if not page or not page.get('imageinfo'):raise ValueError('Missing photo '+s['CommonsFile'])
        info=page['imageinfo'][0];meta=info.get('extmetadata',{})
        value=lambda key:clean(meta.get(key,{}).get('value',''))
        license=value('LicenseShortName');license_url=value('LicenseUrl')
        allowed=bool(re.fullmatch(r'CC BY(?:-SA)? (?:1\.0|2\.0|2\.5|3\.0|4\.0)',license,re.I)) or license.lower() in ('cc0','cc0 1.0','public domain','public domain mark','pdm')
        if not allowed:raise ValueError('Unverified photo license '+s['CommonsFile']+' '+license+' '+license_url)
        creator=value('Artist') or value('Credit')
        if not creator:raise ValueError('Missing author '+s['CommonsFile'])
        source='https://commons.wikimedia.org/wiki/'+s['CommonsFile'].replace(' ','_')
        print('PHOTO SOURCE',s['Code'],repr(info.get('thumburl') or info['url']),flush=True)
        image=get(info.get('thumburl') or info['url'])
        if not (image.startswith(b'\xff\xd8') or image.startswith(b'\x89PNG')):raise ValueError('Not JPEG/PNG: '+s['CommonsFile'])
        filename=s['Code']+'.jpg';(OUT/filename).write_bytes(image)
        entry=dict(s,ImageFile=filename,ImageSha256=hashlib.sha256(image).hexdigest(),SourceUrl=source,Author=creator,License=license,LicenseUrl=license_url or 'https://creativecommons.org/publicdomain/mark/1.0/',Changes='Miniatura de até 512 pixels fornecida pelo Wikimedia Commons; foto ilustrativa.')
        result.append(entry)
        print('PASS PHOTO',s['Code'],s['Name'],license,creator[:100])
        time.sleep(.3)
    (OUT/'catalog.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('PASS READY MIXED CATALOG',len(result),'photos; six categories; no prices or store writes')
if __name__=='__main__':main()
