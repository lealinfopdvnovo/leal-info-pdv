"""Publish only the immutable, already-tested 001 candidate after local protection.

No build, SQLite access, licensing changes, or writes to another update feed.
"""
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time
from urllib.request import Request, urlopen
from urllib.error import HTTPError
import xml.etree.ElementTree as ET
import zipfile

SOURCE = 'lealinfopdvnovo/leal-info-pdv'
UPDATES = 'lealinfopdvnovo/leal-info-pdv-updates'
QA_SOURCE = '4ece3ee51754539d71d6a913e29760d03c4e3835'
QA_RUN = 38939192999
VERSION = '10.404'
PACKAGE = 'LEAL_INFO_PDV_001_V10_404_UPDATE.zip'
PACKAGE_SHA = 'a6dc443af7c8a26c191487744ce3039441363ec6670bc4759002dcefbd5b967c'
PLAN_SHA = 'fbdc405a10ba0ebf18ba5f269964019dbef035583902b7d8208a178122f478fa'
TAG = 'client-001-v10.404'
ASSET_URL = f'https://github.com/{UPDATES}/releases/download/{TAG}/{PACKAGE}'
FEED_PATH = 'clients/001/version.json'
PROTECTED = ('version.json', 'clients/002/version.json', 'clients/003/version.json', 'clients/004/version.json')
EVIDENCE = Path('publication-evidence')
EXPECTED_FILES = {'LealInfoPDV.exe', 'LealInfoPDV.dll', 'LealInfoPDV.deps.json'}

def check(condition, why):
    if not condition:
        raise RuntimeError(why)

def gh(*args, missing=False):
    result = subprocess.run(['gh', *map(str, args)], capture_output=True, text=True, encoding='utf-8')
    if result.returncode:
        if missing and re.search(r'\bHTTP 404\b', result.stderr):
            return None
        raise RuntimeError('GitHub operation failed: ' + result.stderr.strip()[:600])
    return result.stdout

def api(path, missing=False):
    result = gh('api', path, missing=missing)
    return json.loads(result) if result is not None else None

def digest(path):
    with open(path, 'rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def write_json(path, data):
    Path(path).write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

def read_feed(path):
    item = api(f'repos/{UPDATES}/contents/{path}?ref=main', missing=True)
    if item is None:
        return {'exists': False, 'sha': None, 'content': None}
    raw = base64.b64decode(item['content'])
    return {'exists': True, 'sha': item['sha'], 'content': raw.decode('utf-8-sig'), 'base64': base64.b64encode(raw).decode()}

def manifest(feed):
    check(feed['exists'], 'Required update feed is missing')
    return json.loads(feed['content'])

def value(obj, name, default=None):
    return next((v for k, v in obj.items() if k.lower() == name.lower()), default)

def version(text):
    check(isinstance(text, str) and re.fullmatch(r'\d+\.\d+(?:\.\d+)?', text), 'Invalid version')
    parts = tuple(map(int, text.split('.')))
    return parts + (0,) * (3 - len(parts))

def local_gate():
    check(os.environ.get('GITHUB_REF_NAME') == 'client/001', 'Publication is authorized only on client/001')
    proof = json.loads(Path('PROTECTION_001_VERIFIED.json').read_text(encoding='utf-8'))
    expected = {
        'ClientCode': '001', 'InstalledVersion': '10.403', 'TargetVersion': VERSION,
        'ProtectionSourceCommit': QA_SOURCE, 'QaRunId': QA_RUN,
        'PackageSha256': PACKAGE_SHA, 'BridgePlanSha256': PLAN_SHA,
        'ProtectionExeSha256': 'ab5b6b3490744dd1ce1a068ecdf364cde596a2da89600de7a05c6904d5d8c55f',
        'EvidenceScreenshotSha256': '6b1ceec5f5a39c167ea8841e458aaada1ac519436d75fed1b5b7a7cddf2c2c27',
        'SafetyCopyVerifiedByAuxiliary': True, 'ServiceRunningConfirmedByAuxiliary': True,
        'AutomaticWindowsStartupConfirmedByAuxiliary': True,
    }
    check(all(proof.get(k) == v for k, v in expected.items()), 'Local protection evidence differs from reviewed activation')
    allowed = {'.github/workflows/atualizacao_cliente.yml', 'scripts/publicar_restauracao_001.py', 'PROTECTION_001_VERIFIED.json', 'CLIENT_UPDATE_NOTES.txt'}
    changed = subprocess.check_output(['git', 'diff', '--name-only', QA_SOURCE, 'HEAD'], text=True).splitlines()
    check(set(changed) <= allowed, 'Production sources differ from tested checkpoint: ' + repr(set(changed) - allowed))
    check(ET.parse('LealInfoPDV.csproj').findtext('./PropertyGroup/Version') == '10.404.0', 'Project version differs')
    check(re.search(r'CurrentVersion\s*=\s*"10\.404"', Path('UpdateManager.cs').read_text(encoding='utf-8')), 'Updater version differs')

def package_gate():
    check(digest(PACKAGE) == PACKAGE_SHA, 'The update package differs from the tested bytes')
    plan_path = Path('verified-evidence/bridge-plan.json')
    check(digest(plan_path) == PLAN_SHA, 'Protection plan differs')
    plan = json.loads(plan_path.read_text(encoding='utf-8-sig'))
    check(plan['SourceCommit'] == QA_SOURCE and plan['Version'] == VERSION, 'Plan identity differs')
    check(set(plan['Target']) == set(plan['Previous']) == EXPECTED_FILES, 'Plan contains unexpected files')
    with zipfile.ZipFile(PACKAGE) as archive:
        check(len(archive.namelist()) == 3 and set(archive.namelist()) == EXPECTED_FILES, 'Package is not a three-file incremental update')
        for name, expected in plan['Target'].items():
            check(hashlib.sha256(archive.read(name)).hexdigest() == expected, 'Package member differs: ' + name)
    for file, field, minimum in [('results.json', 'scenarios', 14), ('bridge.json', 'results', 13)]:
        results = json.loads(Path('verified-evidence', file).read_text(encoding='utf-8'))[field]
        check(len(results) >= minimum and all(x['result'] == 'PASS' for x in results), 'Essential Windows evidence failed: ' + file)

def verify():
    local_gate()
    run = api(f'repos/{SOURCE}/actions/runs/{QA_RUN}')
    check(run['conclusion'] == 'success' and run['head_sha'] == QA_SOURCE and run['path'] == '.github/workflows/qa_restore_001.yml', 'Windows run is not the approved source')
    jobs = api(f'repos/{SOURCE}/actions/runs/{QA_RUN}/jobs')['jobs']
    check(len(jobs) == 1 and jobs[0]['conclusion'] == 'success', 'Windows job was not approved')
    required = {
        'Executar motor real de restauracao com falhas e processos Windows',
        'Executar recuperacao externa real de executaveis',
        'Executar regressao de rede real', 'Preservar dependencias do pacote incremental',
        'Validar candidato pelo validador instalado',
    }
    passed = {x['name'] for x in jobs[0]['steps'] if x['conclusion'] == 'success'}
    check(required <= passed, 'Required Windows validations are missing')
    pins = [
        (12062800818, 'candidato-001-10.404', 'c7698bb86303430abcb97504034401ac3585a0c61da5ef1f13c08adbc1c4b582'),
        (12062222556, 'protecao-001-validada', '09361eded555854352d86005c8c8f4005c2c7f1752d0cd0776f069e23006be8a'),
        (12061729865, 'restore-real-windows-evidence', '7fe9c9d84ffe0c990bbc5582183d664949a20a16be391a33a860f3b0bf972cae'),
    ]
    for ident, name, expected in pins:
        artifact = api(f'repos/{SOURCE}/actions/artifacts/{ident}')
        check(not artifact['expired'] and artifact['name'] == name and artifact['digest'] == 'sha256:' + expected and artifact['workflow_run']['id'] == QA_RUN and artifact['workflow_run']['head_sha'] == QA_SOURCE, 'Approved artifact identity changed')
    gh('run', 'download', QA_RUN, '--repo', SOURCE, '--name', 'candidato-001-10.404', '--dir', 'verified-package')
    gh('run', 'download', QA_RUN, '--repo', SOURCE, '--name', 'restore-real-windows-evidence', '--dir', 'verified-evidence')
    Path(PACKAGE).write_bytes(Path('verified-package', PACKAGE).read_bytes())
    package_gate()
    print('PASS local protection evidence, approved Windows run, exact incremental package and dependencies; no rebuild')

def download_public(url, path):
    # No credentials are sent to release assets or raw feeds.
    request = Request(url, headers={'User-Agent': 'LEAL-INFO-001-ReleaseVerification', 'Cache-Control': 'no-cache, no-store'})
    with urlopen(request, timeout=30) as response, open(path, 'wb') as output:
        while chunk := response.read(1024 * 1024):
            output.write(chunk)

def put_feed(raw_base64, expected_sha, message):
    payload = {'message': message, 'content': raw_base64, 'sha': expected_sha, 'branch': 'main'}
    request_path = EVIDENCE / 'feed-request.json'
    write_json(request_path, payload)
    return json.loads(gh('api', '--method', 'PUT', f'repos/{UPDATES}/contents/{FEED_PATH}', '--input', request_path))

def exact_manifest(obj):
    return all(value(obj, key) == expected for key, expected in {
        'Version': VERSION, 'PackageUrl': ASSET_URL, 'Sha256': PACKAGE_SHA,
        'SourceCommit': QA_SOURCE, 'TargetClientCode': '001',
    }.items())

def publish():
    local_gate()
    package_gate()
    check(bool(os.environ.get('GH_TOKEN')), 'Update repository publication credential is unavailable')
    EVIDENCE.mkdir(exist_ok=True)
    protected = {path: read_feed(path) for path in PROTECTED}
    before = read_feed(FEED_PATH)
    current = manifest(before)
    check(value(current, 'TargetClientCode') == '001' and version(value(current, 'Version')) <= version(VERSION), 'Wrong client identity or a newer version is already published')
    check(version(value(manifest(protected['version.json']), 'Version')) <= version(VERSION), 'A newer global version would override this client update')
    write_json(EVIDENCE / 'protected-before.json', protected)
    write_json(EVIDENCE / '001-before.json', before)
    existing_release = api(f'repos/{UPDATES}/releases/tags/{TAG}', missing=True)
    if existing_release is None:
        gh('release', 'create', TAG, PACKAGE, '--repo', UPDATES, '--target', 'main', '--title', 'PDV 001 V10.404 - restauracao automatica', '--notes-file', 'CLIENT_UPDATE_NOTES.txt')
    downloaded = EVIDENCE / 'downloaded-update.zip'
    for attempt in range(6):
        try:
            download_public(ASSET_URL, downloaded)
            break
        except HTTPError as error:
            if error.code != 404 or attempt == 5:
                raise
            time.sleep(2)
    check(digest(downloaded) == PACKAGE_SHA, 'Remote release package differs; client feed has not been changed')
    new_manifest = {
        'Version': VERSION, 'PackageUrl': ASSET_URL, 'Sha256': PACKAGE_SHA, 'SourceCommit': QA_SOURCE,
        'TargetClientCode': '001', 'Mandatory': False,
        'Notes': Path('CLIENT_UPDATE_NOTES.txt').read_text(encoding='utf-8').strip(),
    }
    written_sha = None
    try:
        if not exact_manifest(current):
            encoded = base64.b64encode(json.dumps(new_manifest, ensure_ascii=False, indent=2).encode('utf-8')).decode()
            result = put_feed(encoded, before['sha'], 'Atualizacao exclusiva 001 V10.404 - pacote Windows aprovado')
            written_sha = result['content']['sha']
            commit = api(f"repos/{UPDATES}/commits/{result['commit']['sha']}")
            check(len(commit['files']) == 1 and commit['files'][0]['filename'] == FEED_PATH, 'Unexpected update repository mutation')
        after = read_feed(FEED_PATH)
        check(exact_manifest(manifest(after)), 'Published 001 manifest differs')
        live = None
        for attempt in range(6):
            try:
                url = f'https://raw.githubusercontent.com/{UPDATES}/main/{FEED_PATH}?t={time.time_ns() // 1_000_000}'
                request = Request(url, headers={'Cache-Control': 'no-cache, no-store'})
                with urlopen(request, timeout=15) as response:
                    live = json.load(response)
                if exact_manifest(live):
                    break
            except (OSError, ValueError):
                pass
            time.sleep(5)
        check(live is not None and exact_manifest(live), 'Manifest is not available through the installed updater URL')
        preserved = {path: read_feed(path) for path in PROTECTED}
        check(all(protected[path]['sha'] == preserved[path]['sha'] and protected[path]['exists'] == preserved[path]['exists'] for path in PROTECTED), 'A protected feed changed during publication')
        write_json(EVIDENCE / 'published.json', {
            **new_manifest, 'QaRunId': QA_RUN, 'PublicationRunId': os.environ.get('GITHUB_RUN_ID'),
            'ProtectedFeeds': {path: feed['sha'] for path, feed in preserved.items()},
            'OnlyChangedFeed': FEED_PATH, 'VerifiedUtcEpoch': time.time(),
        })
        print('PASS PUBLISHED 001 10.404: real URL, SHA-256, version, identity; global/002/003/004 unchanged')
    except Exception:
        if written_sha:
            now = read_feed(FEED_PATH)
            if now['sha'] == written_sha:
                put_feed(before['base64'], written_sha, 'Recuperar manifesto 001 apos falha de verificacao 10.404')
                print('001 feed recovered to its previous bytes after verification failure')
        raise

if __name__ == '__main__':
    check(len(sys.argv) == 2 and sys.argv[1] in ('verify', 'publish'), 'Expected verify or publish phase')
    {'verify': verify, 'publish': publish}[sys.argv[1]]()
