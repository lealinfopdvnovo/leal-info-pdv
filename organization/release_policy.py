import re
from pathlib import PurePosixPath

CLIENTS = {"001": "Loja Leal Info", "002": "Gelamaq", "003": "Casa do Adriano", "004": "Casa do Genilson"}

def validate_release(manifest, payload_paths, changed_feeds, recipients):
    """Offline publication preflight. Does not change feeds or customer machines."""
    def require(ok, message):
        if not ok:
            raise ValueError(message)
    component = manifest.get("Component")
    require(component in {"GLOBAL", "PET"}, "Component must be GLOBAL or PET")
    require(bool(re.fullmatch(r"[0-9a-fA-F]{64}", manifest.get("Sha256", ""))), "Invalid SHA256")
    paths = list(payload_paths)
    require(bool(paths), "Empty payload")
    normalized = []
    for raw in paths:
        p = PurePosixPath(raw)
        require(raw == p.as_posix() and not p.is_absolute() and ".." not in p.parts
                and "\\" not in raw and ":" not in raw, "Unsafe package path")
        normalized.append(raw.casefold())
    require(len(set(normalized)) == len(normalized), "Duplicate Windows package paths")
    if component == "PET":
        client = manifest.get("TargetClientCode")
        require(client in CLIENTS, "Unknown PET identity")
        require(bool(re.fullmatch(r"1\.\d+(?:\.\d+)*", manifest.get("Version", ""))), "PET requires independent 1.x version")
        require(set(recipients) == {client}, "PET must target exactly its owner")
        require(set(changed_feeds) == {f"clients/{client}/pet/version.json"}, "PET cannot write GLOBAL or another feed")
        for raw in paths:
            p = PurePosixPath(raw)
            require(p.parts[:2] == ("Extensions", "PET") and len(p.parts) >= 3, "PET cannot replace application base files")
            require(p.suffix.casefold() not in {".exe", ".ps1", ".bat", ".cmd", ".db", ".sqlite"}, "Forbidden PET payload")
        require("extensions/pet/manifest.json" in normalized, "Missing PET integrity/identity manifest")
        require(any(p.endswith(".dll") for p in normalized), "Missing PET assembly")
    else:
        require(not manifest.get("TargetClientCode"), "GLOBAL cannot be exclusive to a client")
        require(bool(re.fullmatch(r"10\.\d+(?:\.\d+)*", manifest.get("Version", ""))), "Invalid GLOBAL version")
        require(set(recipients) == set(CLIENTS), "GLOBAL must be available to every registered client")
        require(set(changed_feeds) == {"version.json"}, "GLOBAL must use one common feed")
        require(all(not p.startswith("extensions/pet/") for p in normalized), "GLOBAL cannot replace PET payloads")
    return True
