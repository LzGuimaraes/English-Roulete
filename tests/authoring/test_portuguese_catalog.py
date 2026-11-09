import json
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]
PACK = ROOT / "content/portuguese-01"


class PortugueseCatalogTests(unittest.TestCase):
    def test_all_twenty_two_originals_have_one_separate_complete_translation(self):
        manifest = json.loads((PACK / "source-catalog.json").read_text())["stories"]
        paths = sorted(PACK.glob("*/story.json"))
        self.assertEqual(len(paths), 22)
        self.assertEqual({p.parent.name for p in paths}, set(manifest))
        self.assertEqual(len({m["sourceVersionId"] for m in manifest.values()}), 22)
        originals = {json.loads(p.read_text())["metadata"]["title"]: json.loads(p.read_text())
                     for p in (ROOT / "content").glob("anthology-*/*/story.json")}
        self.assertTrue(set(originals).issubset(m["sourceTitle"] for m in manifest.values()))
        self.assertEqual(sum(len(json.loads(p.read_text())["scenes"]) for p in paths), 108)
        for path in paths:
            document = json.loads(path.read_text())
            record = manifest[path.parent.name]
            with self.subTest(story=path.parent.name):
                self.assertEqual(document["metadata"]["language"], "pt-BR")
                self.assertEqual(document["authoringNotes"]["sourceVersionId"], record["sourceVersionId"])
                self.assertTrue(document["metadata"]["synopsis"])
                self.assertNotIn(document["metadata"]["title"], originals)
                self.assertEqual([s["key"] for s in document["scenes"]], record["sceneKeys"])
                self.assertEqual(len(document["authoringNotes"]["routes"]), 2)
                original = originals.get(record["sourceTitle"])
                if original:
                    for translated, source in zip(document["scenes"], original["scenes"]):
                        self.assertNotEqual(translated["narration"], source["narration"])
                        self.assertNotEqual(translated["prompt"], source["prompt"])
                        self.assertEqual(translated["correctOptionKey"], source["correctOptionKey"])
                        self.assertEqual([(t["key"], t["targetSceneKey"]) for t in translated["transitions"]],
                                         [(t["key"], t["targetSceneKey"]) for t in source["transitions"]])

    def test_every_scene_has_portuguese_audio_script_evidence_and_clickable_definitions(self):
        for path in sorted(PACK.glob("*/story.json")):
            document = json.loads(path.read_text())
            for scene in document["scenes"]:
                with self.subTest(story=path.parent.name, scene=scene["key"]):
                    notes = scene["productionNotes"]
                    self.assertEqual(notes["audioLanguage"], "pt-BR")
                    self.assertEqual(notes["audioText"], scene["narration"])
                    self.assertEqual(" ".join(c["text"] for c in scene["dialogue"]), scene["narration"])
                    self.assertIn(notes["questionEvidence"], scene["narration"])
                    self.assertEqual(len(scene["vocabulary"]), 2)
                    self.assertTrue(notes["reusedVisualId"])
                    self.assertTrue(notes["altText"])
                    self.assertEqual(scene["visualDescription"], notes["altText"])
                    self.assertLessEqual(len(scene["visualDescription"]), 300)
                    self.assertTrue(scene["prompt"].endswith("?"))
                    self.assertTrue(scene["explanation"])
                    for hint in scene["vocabulary"]:
                        self.assertRegex(scene["narration"].casefold(),
                                         r"(?<!\w)" + re.escape(hint["term"].casefold()) + r"(?!\w)")
                        self.assertTrue(hint["translation"])
                    self.assertNotIn("Continue the story", [t["label"] for t in scene["transitions"]])
                    for previous, following in zip(scene["dialogue"], scene["dialogue"][1:]):
                        self.assertLessEqual(previous["endMilliseconds"], following["startMilliseconds"])


if __name__ == "__main__":
    unittest.main()
