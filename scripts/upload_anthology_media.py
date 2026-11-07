#!/usr/bin/env python3
"""Resume authorized media uploads through ADMIN APIs, verifying stored bytes."""
import argparse
from datetime import datetime, timezone
import getpass
import hashlib
import json
import os
from pathlib import Path
import time
from urllib.request import Request

from import_story import AdminApi, editor_payload, load_story, multipart


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save(path, document):
    temporary = path.with_suffix(".json.tmp")
    temporary.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n")
    temporary.replace(path)


def stored_hash(api, asset, kind):
    route = "media" if kind == "audio" else "visuals"
    request = Request(api.base + f"/api/admin/{route}/{asset['id']}")
    with api.http.open(request, timeout=45) as response:
        data = response.read(10 * 1024 * 1024 + 1)
    if len(data) > 10 * 1024 * 1024:
        raise ValueError("Stored media exceeds the read limit.")
    return hashlib.sha256(data).hexdigest()


def credits(kind, path, scene):
    if kind == "audio":
        return {"path": path, "file": f"audio/{scene['key']}.wav",
                "attribution": "Narração sintética original: Piper 1.8.0, en_US-ljspeech-high; modelo treinado com LJ Speech (Keith Ito / Linda Johnson, LibriVox). Texto original English Roulette.",
                "licenseEvidence": "Produzido offline para esta cena a partir do texto original autorizado pelo responsável. Repositório do modelo declara MIT: https://huggingface.co/rhasspy/piper-voices ; model card declara dataset em domínio público: https://huggingface.co/rhasspy/piper-voices/blob/main/en/en_US/ljspeech/high/MODEL_CARD ; origem: https://keithito.com/LJ-Speech-Dataset/ . Piper GPL-3.0 é ferramenta de produção, não distribuída com o jogo. Cartão e metadados do modelo arquivados no pacote local."}
    description = scene["productionNotes"]["imagePrompt"].split("SCENE: ", 1)[1].split("SETTING:", 1)[0].strip()
    return {"path": path, "file": f"animations/{scene['key']}.gif", "animated": True,
            "altText": ("Ilustração 2D sombria. " + description)[:300],
            "attribution": "Arte original assistida por IA, OpenAI image_gen; direção de animação 2D dark para English Roulette. Loop de câmera discreto codificado com FFmpeg.",
            "licenseEvidence": "Ilustração gerada especificamente para esta cena pela ferramenta image_gen, com prompts originais registrados em content/anthology-01. Produção e envio à VPS autorizados expressamente pelo responsável nesta sessão em 07/11/2026; estilo 2D dark solicitado pelo mesmo. Sem imagens de terceiros fornecidas como base. O loop deriva desta arte e mantém objetos, luzes e personagens. Arquivos e hashes preservados no pacote local; não é imagem de pessoa real."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--receipt", type=Path, default=Path("content/anthology-01/vps-import.json"))
    parser.add_argument("--media-root", type=Path, default=Path("media/anthology-01"))
    parser.add_argument("--email", default=os.environ.get("ENGLISHROULETTE_ADMIN_EMAIL"), required=False)
    parser.add_argument("--env-file", type=Path, help="Read ADMIN_PASSWORD locally without exposing it in arguments.")
    parser.add_argument("--kind", choices=["audio", "visual", "all"], default="all")
    parser.add_argument("--story", help="Only process this story folder.")
    parser.add_argument("--publish", action="store_true", help="Publish only complete, server-validated and locally reviewed stories.")
    args = parser.parse_args()
    if not args.email:
        parser.error("Provide --email or ENGLISHROULETTE_ADMIN_EMAIL.")
    values = {}
    if args.env_file:
        for line in args.env_file.read_text().splitlines():
            if "=" in line and not line.lstrip().startswith("#"):
                key, value = line.split("=", 1)
                values[key.strip()] = value.strip().strip("\"'")
    password = os.environ.get("ENGLISHROULETTE_ADMIN_PASSWORD") or values.get("ADMIN_PASSWORD") or getpass.getpass("Admin password: ")
    imported = json.loads(args.receipt.read_text())
    api = AdminApi(imported["origin"])
    api.request("POST", "/api/admin/login", {"email": args.email, "password": password})
    password = None
    values.clear()
    audit_path = args.receipt.parent / "vps-media.json"
    audit = json.loads(audit_path.read_text()) if audit_path.exists() else {"origin": api.base, "stories": {}}
    if audit["origin"] != api.base:
        raise ValueError("Media receipt origin mismatch.")
    reviewed_path = args.media_root / "review.json"
    reviewed = json.loads(reviewed_path.read_text()) if reviewed_path.exists() else {}
    for slug, imported_story in imported["stories"].items():
        if args.story and slug != args.story:
            continue
        source = args.receipt.parent / slug / "story.json"
        document = load_story(source)
        version = imported_story["versionId"]
        route = "/api/admin/stories/" + version
        draft = api.request("GET", route)
        if editor_payload(document, dict(draft, status="Draft")) != dict(draft, status="Draft"):
            raise ValueError("Server content differs from source; reconcile before uploading: " + slug)
        record = audit["stories"].setdefault(slug, {"versionId": version, "scenes": {}})
        if record["versionId"] != version:
            raise ValueError("Version mismatch: " + slug)
        for scene in document["scenes"]:
            scene_record = record["scenes"].setdefault(scene["key"], {})
            for kind in (["audio", "visual"] if args.kind == "all" else [args.kind]):
                path = args.media_root / slug / (f"audio/{scene['key']}.wav" if kind == "audio" else f"animations/{scene['key']}.gif")
                if not path.is_file():
                    continue
                expected = digest(path)
                stored_scene = next(item for item in draft["scenes"] if item["key"] == scene["key"])
                existing = stored_scene.get(kind)
                if not existing or stored_hash(api, existing, kind) != expected:
                    if draft["status"] != "Draft":
                        raise ValueError("Published media cannot be replaced: " + slug)
                    body, content_type = multipart(credits(kind, path, scene), kind)
                    # If a response is lost, stop. The next run compares stored bytes before retrying.
                    draft = api.request("POST", f"{route}/scenes/{stored_scene['id']}/{kind}?revision={draft['revision']}", body, content_type)
                    stored_scene = next(item for item in draft["scenes"] if item["key"] == scene["key"])
                    existing = stored_scene[kind]
                    time.sleep(0.6)
                if stored_hash(api, existing, kind) != expected:
                    raise ValueError("Uploaded bytes differ: " + slug + "/" + scene["key"])
                if kind == "visual" and not existing["animated"]:
                    raise ValueError("Stored visual is not animated.")
                scene_record[kind] = {"id": existing["id"], "sha256": expected,
                    "file": str(path.relative_to(args.media_root)), "verifiedAtUtc": datetime.now(timezone.utc).isoformat()}
                if kind == "audio":
                    scene_record[kind]["durationMs"] = existing["durationMs"]
                save(audit_path, audit)
                print(f"Verified VPS {slug}/{scene['key']}: {kind}", flush=True)
        validation = api.request("GET", route + "/validation")
        record.update(status=draft["status"], revision=draft["revision"], publicationIssues=validation["issues"],
                      sourceSha256=digest(source), verifiedAtUtc=datetime.now(timezone.utc).isoformat())
        complete = all(set(record["scenes"].get(scene["key"], {})) == {"audio", "visual"} for scene in document["scenes"])
        if args.publish and complete and not validation["issues"] and draft["status"] == "Draft":
            review = reviewed.get(slug, {})
            files = {f"{key}/{kind}": item["sha256"] for key, assets in record["scenes"].items() for kind, item in assets.items()}
            if review.get("sourceSha256") != digest(source) or review.get("mediaSha256") != files:
                raise ValueError("Missing or outdated local review of exact source/media: " + slug)
            api.request("POST", route + "/publish", {"revision": draft["revision"], "reviewed": True, "rightsConfirmed": True})
            draft = api.request("GET", route)
            record.update(status=draft["status"], revision=draft["revision"])
            print("Published: " + slug, flush=True)
        imported_story.update(status=draft["status"], revision=draft["revision"],
            audioAssetsUploaded=sum(bool(item.get("audio")) for item in draft["scenes"]),
            visualAssetsUploaded=sum(bool(item.get("visual")) for item in draft["scenes"]),
            publicationIssues=validation["issues"], sourceSha256=digest(source),
            verifiedAtUtc=datetime.now(timezone.utc).isoformat())
        imported["published"] = all(item["status"] == "Published" for item in imported["stories"].values())
        save(audit_path, audit)
        save(args.receipt, imported)


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError) as error:
        raise SystemExit("Media operation stopped: " + str(error)) from None
