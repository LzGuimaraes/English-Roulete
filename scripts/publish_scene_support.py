#!/usr/bin/env python3
"""Publish reviewed vocabulary/dialogue as new immutable versions; reuse existing art/audio."""
import argparse
import getpass
import json
import os
from pathlib import Path
from import_story import AdminApi, load_story, multipart


def publish(api, story_path, media_root, publish_reviewed):
    document = load_story(story_path)
    versions = []
    for page in range(1, 10001):
        batch = api.request("GET", f"/api/admin/stories?page={page}")
        versions.extend(batch)
        if len(batch) < 25:
            break
    original = next((row for row in versions if row["title"] == document["metadata"]["title"] and row["status"] == "Published"), None)
    if not original:
        raise ValueError("Publish the original story/media first; this tool only annotates existing content.")
    original = api.request("GET", "/api/admin/stories/" + original["id"])
    sources = {scene["key"]: scene for scene in document["scenes"]}
    if set(sources) != {scene["key"] for scene in original["scenes"]} or any(
            scene["narration"] != sources[scene["key"]]["narration"] for scene in original["scenes"]):
        raise ValueError("Published narration/scene keys differ from reviewed source. Review before replacing.")
    if all(scene.get("vocabulary", []) == sources[scene["key"]].get("vocabulary", []) and
           scene.get("dialogue", []) == sources[scene["key"]].get("dialogue", []) for scene in original["scenes"]):
        return {"slug": story_path.parent.name, "id": original["id"], "status": "already-published"}
    draft = api.request("POST", "/api/admin/stories/" + original["id"] + "/duplicate", {})
    replacements = {}
    for scene in draft["scenes"]:
        source = sources[scene["key"]]
        scene["vocabulary"] = source.get("vocabulary", [])
        scene["dialogue"] = source.get("dialogue", [])
        if scene["dialogue"]:
            asset = (media_root / story_path.parent.name / "dialogue-audio" / (scene["key"] + ".wav")).resolve()
            if not asset.is_relative_to(media_root.resolve()):
                raise ValueError("Dialogue media must stay inside the media package.")
            receipt = json.loads(asset.with_suffix(".json").read_text())
            import hashlib
            if receipt["sha256"] != hashlib.sha256(asset.read_bytes()).hexdigest() or receipt["dialogue"] != scene["dialogue"]:
                raise ValueError("Dialogue audio/cues do not match their local production receipt.")
            old = scene["audio"]
            note = (" Gravação original preservada; intervalos editoriais alinhados ao áudio." if receipt.get("method")
                    else " Falas concatenadas com intervalos PCM medidos; uma única voz sintética.")
            replacements[scene["key"]] = {"path": asset, "attribution": old["attribution"] + note, "licenseEvidence": old["licenseEvidence"]}
            # New intervals may exceed the old recording; upload only to the new draft.
            scene["audio"] = None
    route = "/api/admin/stories/" + draft["id"]
    draft = api.request("PUT", route, draft)
    for key, asset in replacements.items():
        scene = next(scene for scene in draft["scenes"] if scene["key"] == key)
        body, content_type = multipart(asset, "audio")
        draft = api.request("POST", f"{route}/scenes/{scene['id']}/audio?revision={draft['revision']}", body, content_type)
    validation = api.request("GET", route + "/validation")
    if validation["issues"]:
        raise ValueError("New draft failed publication validation: " + "; ".join(validation["issues"]))
    if publish_reviewed:
        api.request("POST", route + "/publish", {"revision": draft["revision"], "reviewed": True, "rightsConfirmed": True})
    return {"slug": story_path.parent.name, "previousId": original["id"], "id": draft["id"], "status": "Published" if publish_reviewed else "Draft"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--origin", required=True)
    parser.add_argument("--email", required=True)
    parser.add_argument("--env-file", type=Path)
    parser.add_argument("--media", type=Path, default=Path("media/anthology-01"))
    parser.add_argument("--publish-reviewed", action="store_true")
    parser.add_argument("stories", nargs="+", type=Path)
    args = parser.parse_args()
    values = {}
    if args.env_file:
        for line in args.env_file.read_text().splitlines():
            if "=" in line and not line.lstrip().startswith("#"):
                key, value = line.split("=", 1); values[key.strip()] = value.strip().strip("\"'")
    password = os.environ.get("ENGLISHROULETTE_ADMIN_PASSWORD") or values.get("ADMIN_PASSWORD") or getpass.getpass("Admin password: ")
    api = AdminApi(args.origin)
    api.request("POST", "/api/admin/login", {"email": args.email, "password": password})
    password = None; values.clear()
    for story in args.stories:
        print(json.dumps(publish(api, story, args.media, args.publish_reviewed), ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
