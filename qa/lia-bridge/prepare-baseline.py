import json,hashlib,re
from pathlib import Path
p=Path('controller/qa/global-453/prepare.py')
s=p.read_text().replace("('10.452','d3deeefb63aa41f54b4a355daaa52b3f15ed2f6efe38281842896b8c2cdce6e1')","('10.453','2b18cfdd5437ce9e0b1f901e22f655f1c6640ac15198bb92d3317dbc3d3877dd')")
p.write_text(s)
