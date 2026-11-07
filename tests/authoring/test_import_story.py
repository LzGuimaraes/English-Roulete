"""Authoring format and import boundaries; no AI service or production database is used."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("import_story", ROOT / "scripts/import_story.py")
importer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(importer)
SAMPLE = ROOT / "content/examples/green-ribbon-a2.json"


class AuthoringTests(unittest.TestCase):
    def test_example_is_complete_and_speech_script_matches_narration(self):
        document = importer.load_story(SAMPLE)
        self.assertEqual(len(document["scenes"]), 4)
        self.assertEqual(sum(scene["isEnding"] for scene in document["scenes"]), 2)
        for scene in document["scenes"]:
            self.assertEqual(scene["narration"], scene["productionNotes"]["audioText"])
            self.assertIn(scene["correctOptionKey"], {option["key"] for option in scene["options"]})
            self.assertTrue(scene["productionNotes"]["animationPrompt"])

    def test_payload_ignores_ai_ids_media_urls_and_notes_and_preserves_existing_media(self):
        document = importer.load_story(SAMPLE)
        document["id"] = "untrusted-id"; document["status"] = "Published"
        document["scenes"][0].update(id="untrusted-scene", audio={"id": "untrusted-audio"})
        draft = {"id": "server-version", "status": "Draft", "revision": 4, "metadata": {},
                 "scenes": [{"key": "quiet-room", "id": "server-scene", "audio": {"id": "server-audio"}}]}
        result = importer.editor_payload(document, draft)
        self.assertEqual(result["id"], "server-version"); self.assertEqual(result["revision"], 4)
        self.assertEqual(result["scenes"][0]["id"], "server-scene")
        self.assertEqual(result["scenes"][0]["audio"]["id"], "server-audio")
        self.assertEqual(result["scenes"][1]["id"], importer.EMPTY_ID)
        self.assertNotIn("productionNotes", result["scenes"][0])
        self.assertEqual(draft["scenes"][0]["id"], "server-scene")
        with self.assertRaises(ValueError):
            importer.editor_payload(document, dict(draft, status="Published"))

    def test_media_requires_complete_package_and_cannot_read_outside_its_directory(self):
        document = importer.load_story(SAMPLE)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); package = root / "package"; package.mkdir()
            (package / "audio.wav").write_bytes(b"fixture-wave")
            (package / "visual.gif").write_bytes(b"fixture-gif")
            (root / "outside.wav").write_bytes(b"outside-package")
            manifest = {scene["key"]: {
                "audio": {"file": "audio.wav", "attribution": "Test", "licenseEvidence": "Fixture"},
                "visual": {"file": "visual.gif", "altText": "Test scene", "attribution": "Test", "licenseEvidence": "Fixture", "animated": True}
            } for scene in document["scenes"]}
            path = package / "media.json"; path.write_text(json.dumps(manifest))
            loaded = importer.load_media(path, document)
            self.assertEqual(len(loaded), 4)
            content, content_type = importer.multipart(loaded["quiet-room"]["visual"], "visual")
            self.assertIn(b'filename="asset"', content); self.assertIn(b"fixture-gif", content)
            self.assertTrue(content_type.startswith("multipart/form-data; boundary="))
            manifest["quiet-room"]["audio"]["file"] = "../outside.wav"
            path.write_text(json.dumps(manifest))
            with self.assertRaises(ValueError):
                importer.load_media(path, document)

    def test_remote_connection_requires_https_and_redirects_are_refused(self):
        for url in ["http://vps.example", "https://user:password@example.com", "https://example.com/path"]:
            with self.assertRaises(ValueError):
                importer.AdminApi(url)
        self.assertEqual(importer.AdminApi("http://localhost:5080").base, "http://localhost:5080")
        self.assertIsNone(importer.NoRedirect().redirect_request(None, None, 302, "", {}, "https://other.example"))

    def test_missing_scene_key_is_rejected_before_connecting(self):
        document = importer.load_story(SAMPLE); document["initialSceneKey"] = "missing"
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "story.json"; path.write_text(json.dumps(document))
            with self.assertRaises(ValueError):
                importer.load_story(path)


if __name__ == "__main__":
    unittest.main()
