"""Publish the immutable client-specific internal-layout candidate through the existing updater.

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
RELEASE = json.loads(Path('RELEASE_INTERNAL.json').read_text(encoding='utf-8'))
CLIENT = RELEASE['ClientCode']
if CLIENT not in ('001', '003'):
    raise RuntimeError('Only 001 and 003 are authorized')
QA_SOURCE = RELEASE['SourceCommit']
QA_RUN = RELEASE['QaRunId']
VERSION = '10.406'
PACKAGE = f'LEAL_INFO_PDV_{CLIENT}_V10_406_UPDATE.zip'
PACKAGE_SHA = RELEASE['PackageSha256']
PLAN_SHA = RELEASE['PlanSha256']
TAG = f'client-{CLIENT}-v{VERSION}'
ASSET_URL = f'https://github.com/{UPDATES}/releases/download/{TAG}/{PACKAGE}'
FEED_PATH = f'clients/{CLIENT}/version.json'
PROTECTED = tuple(p for p in ('version.json', 'clients/001/version.json', 'clients/002/version.json', 'clients/003/version.json', 'clients/004/version.json') if p != FEED_PATH)
BASELINES = {
    '001': ('10.405', 'd40cdbefb66f932d44c851dd2b2159fcf02a14b26b322e91f391ab74c25f5321'),
    '003': ('10.403', '4dbf912f043f2ec1c30775a62fd67f045da761d9957736725467a424eace671d'),
}
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
    check(os.environ.get('GITHUB_REF_NAME') == f'client/{CLIENT}', 'Release branch identity differs')
    check(RELEASE['Version'] == VERSION and RELEASE['Delivery'] == 'existing-installed-updater', 'Release version or delivery differs')
    check((RELEASE['BaselineVersion'], RELEASE['BaselineSha256']) == BASELINES[CLIENT], 'Client baseline differs')
    check(re.fullmatch(r'[a-f0-9]{40}', QA_SOURCE) is not None, 'Invalid QA commit')
    check(all(re.fullmatch(r'[a-f0-9]{64}', x) for x in (PACKAGE_SHA, PLAN_SHA)), 'Invalid artifact hash')
    allowed = {'.github/workflows/atualizacao_cliente.yml', 'scripts/publicar_internas.py', 'RELEASE_INTERNAL.json', 'CLIENT_UPDATE_NOTES.txt'}
    changed = subprocess.check_output(['git', 'diff', '--name-only', QA_SOURCE, 'HEAD'], text=True, encoding='utf-8').splitlines()
    check(set(changed) <= allowed, 'Production differs from approved Windows source: ' + repr(set(changed) - allowed))
    check(ET.parse('LealInfoPDV.csproj').findtext('./PropertyGroup/Version') == VERSION + '.0', 'Project version differs')
    check(re.search(r'CurrentVersion\s*=\s*"10\.406"', Path('UpdateManager.cs').read_text(encoding='utf-8-sig')), 'Updater version differs')

def package_gate():
    check(digest(PACKAGE) == PACKAGE_SHA, 'The update package differs from the tested bytes')
    plan_path = Path('verified-evidence/candidate-plan.json')
    check(digest(plan_path) == PLAN_SHA, 'Candidate plan differs')
    plan = json.loads(plan_path.read_text(encoding='utf-8-sig'))
    check(plan['SourceCommit'] == QA_SOURCE and plan['Version'] == VERSION, 'Plan identity differs')
    check(set(plan['Target']) == set(plan['Previous']) == EXPECTED_FILES, 'Plan contains unexpected files')
    with zipfile.ZipFile(PACKAGE) as archive:
        check(len(archive.namelist()) == 3 and set(archive.namelist()) == EXPECTED_FILES, 'Package is not a three-file incremental update')
        for name, expected in plan['Target'].items():
            check(hashlib.sha256(archive.read(name)).hexdigest() == expected, 'Package member differs: ' + name)
    results = json.loads(Path('verified-evidence/results.json').read_text(encoding='utf-8-sig'))
    names = {x['name'] for x in results if x['result'] == 'PASS'}
    check(len(results) >= 20 and all(x['result'] == 'PASS' for x in results), 'Windows presentation checks failed')
    check({'ProductNew', 'SelectProductFromCatalog', 'OpenSales', 'OpenProducts', 'OpenCustomers', 'OpenSuppliers', 'OpenFinance', 'OpenReports', 'OpenSettings', 'editor-original-fields-tag-values-save-handler', 'large-list-2000-refresh-search'} <= names, 'Required real modules were not exercised')
    check(Path('verified-evidence/login-desktop.png').stat().st_size > 1000, 'Real login desktop evidence missing')
    check(plan['Previous'] == RELEASE['PreviousFiles'], 'Installed client files differ from tested baseline')
    for width in (1024, 1366, 1600, 1920):
        check(Path(f'verified-evidence/OpenProducts-{width}.png').stat().st_size > 1000, 'Responsive layout evidence missing')


def verify():
    local_gate()
    run = api(f'repos/{SOURCE}/actions/runs/{QA_RUN}')
    check(run['conclusion'] == 'success' and run['head_sha'] == QA_SOURCE and run['path'] == '.github/workflows/qa_internal_layout.yml', 'Windows run is not the approved source')
    jobs = api(f'repos/{SOURCE}/actions/runs/{QA_RUN}/jobs')['jobs']
    check(len(jobs) == 1 and jobs[0]['conclusion'] == 'success', 'Windows job was not approved')
    required = {
        'Preserve approved business code and installed updater',
        'Compile Windows PDV', 'Exercise real modules and capture Windows UI',
        f"Build incremental candidate from installed {RELEASE['BaselineVersion']} dependencies",
        'Verify unchanged incremental dependencies', 'Validate candidate with installed payload policy',
    }
    passed = {x['name'] for x in jobs[0]['steps'] if x['conclusion'] == 'success'}
    check(required <= passed, 'Required Windows validations are missing')
    pins = [(x['Id'], x['Name'], x['Digest']) for x in RELEASE['Artifacts']]
    check(len(pins) == 2 and {x[1] for x in pins} == {f'candidato-{CLIENT}-{VERSION}', 'internal-layout-windows-evidence'}, 'Artifact set differs')
    for ident, name, expected in pins:
        artifact = api(f'repos/{SOURCE}/actions/artifacts/{ident}')
        check(not artifact['expired'] and artifact['name'] == name and artifact['digest'] == 'sha256:' + expected and artifact['workflow_run']['id'] == QA_RUN and artifact['workflow_run']['head_sha'] == QA_SOURCE, 'Approved artifact identity changed')
    gh('run', 'download', QA_RUN, '--repo', SOURCE, '--name', f'candidato-{CLIENT}-{VERSION}', '--dir', 'verified-package')
    gh('run', 'download', QA_RUN, '--repo', SOURCE, '--name', 'internal-layout-windows-evidence', '--dir', 'verified-evidence')
    Path(PACKAGE).write_bytes(Path('verified-package', PACKAGE).read_bytes())
    package_gate()
    print(f'PASS {CLIENT}: preserved business/updater source, Windows layouts and actions, exact compatible package; no rebuild')

def download_public(url, path):
    # No credentials are sent to release assets or raw feeds.
    request = Request(url, headers={'User-Agent': 'LEAL-INFO-InternalReleaseVerification', 'Cache-Control': 'no-cache, no-store'})
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
        'SourceCommit': QA_SOURCE, 'TargetClientCode': CLIENT,
    }.items())

def publish():
    local_gate()
    package_gate()
    check(bool(os.environ.get('GH_TOKEN')), 'Update repository publication credential is unavailable')
    EVIDENCE.mkdir(exist_ok=True)
    protected = {path: read_feed(path) for path in PROTECTED}
    before = read_feed(FEED_PATH)
    current = manifest(before)
    check(value(current, 'TargetClientCode') == CLIENT and version(value(current, 'Version')) <= version(VERSION), 'Wrong client identity or a newer version is already published')
    check(exact_manifest(current) or (value(current, 'Version'), value(current, 'Sha256')) == BASELINES[CLIENT], 'Installed baseline feed differs from verified client version')
    check(version(value(manifest(protected['version.json']), 'Version')) <= version(VERSION), 'A newer global version would override this client update')
    write_json(EVIDENCE / 'protected-before.json', protected)
    write_json(EVIDENCE / f'{CLIENT}-before.json', before)
    existing_release = api(f'repos/{UPDATES}/releases/tags/{TAG}', missing=True)
    if existing_release is None:
        gh('release', 'create', TAG, PACKAGE, '--repo', UPDATES, '--target', 'main', '--title', f'PDV {CLIENT} V{VERSION} - telas internas', '--notes-file', 'CLIENT_UPDATE_NOTES.txt', '--latest=false')
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
        'TargetClientCode': CLIENT, 'Mandatory': False,
        'Notes': Path('CLIENT_UPDATE_NOTES.txt').read_text(encoding='utf-8').strip(),
    }
    written_sha = None
    try:
        if not exact_manifest(current):
            encoded = base64.b64encode(json.dumps(new_manifest, ensure_ascii=False, indent=2).encode('utf-8')).decode()
            result = put_feed(encoded, before['sha'], f'Atualizacao exclusiva {CLIENT} V{VERSION} - layout Windows aprovado')
            written_sha = result['content']['sha']
            commit = api(f"repos/{UPDATES}/commits/{result['commit']['sha']}")
            check(len(commit['files']) == 1 and commit['files'][0]['filename'] == FEED_PATH, 'Unexpected update repository mutation')
        after = read_feed(FEED_PATH)
        check(exact_manifest(manifest(after)), 'Published client manifest differs')
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
            **new_manifest, 'QaRunId': QA_RUN, 'PublicationRunId': os.environ.get('GITHUB_RUN_ID'), 'Delivery': 'existing-installed-updater',
            'ProtectedFeeds': {path: feed['sha'] for path, feed in preserved.items()},
            'OnlyChangedFeed': FEED_PATH, 'VerifiedUtcEpoch': time.time(),
        })
        print(f'PASS PUBLISHED {CLIENT} {VERSION}: real URL, SHA-256, version and identity; all other feeds unchanged')
    except Exception:
        if written_sha:
            now = read_feed(FEED_PATH)
            if now['sha'] == written_sha:
                put_feed(before['base64'], written_sha, f'Recuperar manifesto {CLIENT} apos falha de verificacao {VERSION}')
                print(f'{CLIENT} feed recovered to its previous bytes after verification failure')
        raise

if __name__ == '__main__':
    check(len(sys.argv) == 2 and sys.argv[1] in ('verify', 'publish'), 'Expected verify or publish phase')
    {'verify': verify, 'publish': publish}[sys.argv[1]]()
