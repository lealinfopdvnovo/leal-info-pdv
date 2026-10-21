"""Verify the offline photo archive; never opens the customer database or update feeds."""
import hashlib,json,re,zipfile
from pathlib import Path
root=Path(__file__).resolve().parents[1]
with zipfile.ZipFile(root/'ready-kitchen/photos.zip') as z:
 data=json.loads(z.read('catalog.json'));assert len(data)==258
 assert len({e['Code'] for e in data})==258 and len({e['Name'].casefold() for e in data})==258
 expected={'catalog.json'}
 for item in data:
  assert re.fullmatch(r'LEALMIX\d{3}',item['Code'])
  assert item['CommonsFile'].startswith('File:') and item['CommonsFile']!='LEAL_CATEGORY_ART'
  assert re.fullmatch(r'CC BY(?:-SA)? (?:1\.0|2\.0|2\.5|3\.0|4\.0)',item['License'],re.I) or item['License'].lower() in ('cc0','cc0 1.0','public domain','public domain mark','pdm')
  assert item['SourceUrl'].startswith('https://commons.wikimedia.org/wiki/') and item['Author'].strip()
  assert re.fullmatch(r'PHOTO_[0-9a-f]{24}\.jpg',item['ImageFile'])
  assert item['LicenseUrl'].startswith(('https://creativecommons.org/','http://creativecommons.org/'))
  raw=z.read(item['ImageFile']);assert raw.startswith(b'\xff\xd8') and len(raw)<=4_000_000
  assert hashlib.sha256(raw).hexdigest()==item['ImageSha256']
  expected.add(item['ImageFile'])
 assert set(z.namelist())==expected
 assert 'ReadyKitchen.photos.zip' in (root/'LealInfoPDV.csproj').read_text(encoding='utf-8-sig')
 print('PASS 258 attributed product photographs, verified SHA256, fully offline archive;',len(expected)-1,'distinct photos')
