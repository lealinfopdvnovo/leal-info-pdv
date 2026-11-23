"""Promote the validated Windows artifact exclusively to the Loja 001 feed."""
import base64
import hashlib
import json
import os
from pathlib import Path
import subprocess
import urllib.request

REPO = "lealinfopdvnovo/leal-info-pdv-updates"
FEED = "clients/001/version.json"
BASELINE_BLOB = "872d9a92f99e91cb886a22925633610c55cf4c50"
VERSION = "10.469"
TAG = f"client-001-v{VERSION}-transbordo"
NAME = "LEAL_INFO_PDV_001_V10_469_UPDATE.zip"

def api(path, data=None, method="POST"):
    command = ["gh", "api", f"repos/{REPO}/{path}"]
    if data is not None:
        command += ["--method", method, "--input", "-"]
    return json.loads(subprocess.check_output(command, input=None if data is None else json.dumps(data).encode()))

def sha(data):
    return hashlib.sha256(data).hexdigest()

def main():
    artifact = Path("loja-artifact")
    package = artifact / "destination-10469/initial-transition.zip"
    evidence = json.loads((artifact / "completion-evidence/destination.json").read_text(encoding="utf-8-sig"))
    recovery = json.loads((artifact / "completion-evidence/joint-recovery.json").read_text(encoding="utf-8-sig"))
    package_hash = sha(package.read_bytes())
    assert evidence["Pass"] and evidence["Target"] == VERSION and evidence["Pet"] == "1.3"
    assert evidence["Origins"] == ["10.467", "10.468"] and evidence["PackageSha256"] == package_hash
    assert {row["Origin"] for row in recovery if row["Pass"] and row["Scenario"] == "after-first-pet"} == {"10.467", "10.468"}
    assert any(row["Pass"] and row["Origin"] == "10.468" and row["Scenario"] == "interrupted-joint-recovery-SYSTEM" for row in recovery)
    ref = api("git/ref/heads/main")["object"]["sha"]
    feed = api(f"contents/{FEED}?ref={ref}")
    assert feed["sha"] == BASELINE_BLOB, "Live Loja 001 feed changed"
    manifest = json.loads(base64.b64decode(feed["content"]))
    assert manifest["Version"] == "10.468" and manifest["TargetClientCode"] == "001"
    for item in api("git/matching-refs/tags/client-001"):
        assert VERSION not in item["ref"], f"Version occupied: {item['ref']}"
    published_asset = Path(NAME)
    published_asset.write_bytes(package.read_bytes())
    subprocess.run(
        ["gh", "release", "create", TAG, str(published_asset), "--repo", REPO,
         "--target", ref, "--title", "Loja Leal Info 001 10.469 — Transbordo",
         "--notes", "Transbordo GLOBAL/PET exclusivo da Loja 001. Inclui PET 1.3 e barra de backup.",
         "--latest=false"], check=True)
    url = f"https://github.com/{REPO}/releases/download/{TAG}/{NAME}"
    with urllib.request.urlopen(url + "?verification=" + os.environ["GITHUB_RUN_ID"], timeout=90) as response:
        assert sha(response.read(128 * 1024 * 1024 + 1)) == package_hash, "Hosted package SHA mismatch"
    assert api("git/ref/heads/main")["object"]["sha"] == ref, "Update repository advanced during release creation"
    manifest.update(Version=VERSION, PackageUrl=url, Sha256=package_hash,
                    SourceCommit=os.environ["VALIDATED_COMMIT"],
                    BuildRunId=os.environ["VALIDATED_RUN"], ValidationRunId=os.environ["VALIDATED_RUN"],
                    PublicationRunId=os.environ["GITHUB_RUN_ID"], PetVersion="1.3",
                    PublishedBaselineVersion="10.468", MinimumInstalledVersion="10.467",
                    PackagesVerifiedBeforeFeed=True, InstalledOnRealComputers=False,
                    Notes="Transbordo GLOBAL/PET Loja 001 com barra de backup. Origens validadas: 10.467 e 10.468.")
    published = api(f"contents/{FEED}", {
        "message": f"Loja 001: publicar transbordo {VERSION}",
        "branch": "main", "sha": BASELINE_BLOB,
        "content": base64.b64encode((json.dumps(manifest, ensure_ascii=False, indent=2) + "\n").encode()).decode(),
    }, method="PUT")
    print(json.dumps({"FeedCommit": published["commit"]["sha"], "FeedBlob": published["content"]["sha"],
                      "Version": VERSION, "PackageSha256": package_hash, "PackageUrl": url}, ensure_ascii=False))

if __name__ == "__main__":
    main()
