"""Check the original editorial pack without credentials, network, or generated media."""
from collections import Counter
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
PACK = ROOT / "content/anthology-01"
STORIES = sorted(PACK.glob("*/story.json"))
spec = importlib.util.spec_from_file_location("import_story", ROOT / "scripts/import_story.py")
importer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(importer)
LIMITS = {"A1": (30, 50), "A2": (45, 75), "B1": (65, 95)}


class AnthologyTests(unittest.TestCase):
    def test_current_anthologies_have_contextual_words_in_every_scene(self):
        for path in sorted((ROOT / "content").glob("anthology-*/*/story.json")):
            document = importer.load_story(path)
            for scene in document["scenes"]:
                with self.subTest(story=path.parent.name, scene=scene["key"]):
                    words = scene.get("vocabulary", [])
                    self.assertTrue(words, "Missing editorial hints makes assistance disappear between stories.")
                    self.assertLessEqual(len(words), 12)
                    self.assertEqual(len({word["key"] for word in words}), len(words))
                    for word in words:
                        self.assertIn(word["term"].casefold(), scene["narration"].casefold())
                        self.assertTrue(word["translation"].strip())

    def test_pack_covers_four_genres_and_three_levels_with_distinct_stories(self):
        self.assertEqual(len(STORIES), 12)
        documents = [importer.load_story(path) for path in STORIES]
        self.assertEqual(Counter(document["metadata"]["difficulty"] for document in documents),
                         {"A1": 4, "A2": 4, "B1": 4})
        self.assertEqual(Counter(document["metadata"]["genre"] for document in documents),
                         {"Mistério": 3, "Terror psicológico": 3, "Aventura": 3, "Ficção científica": 3})
        self.assertEqual(len({document["metadata"]["title"] for document in documents}), 12)
        self.assertEqual(len({scene["narration"] for document in documents for scene in document["scenes"]}), 60)

    def test_every_route_is_complete_acyclic_and_matches_the_editorial_route_list(self):
        for path in STORIES:
            with self.subTest(story=path.parent.name):
                document = importer.load_story(path)
                scenes = {scene["key"]: scene for scene in document["scenes"]}
                self.assertEqual(len(scenes), 5)
                reached, routes = set(), []

                def visit(key, prefix):
                    self.assertIn(key, scenes)
                    self.assertNotIn(key, prefix, "The publication graph must not cycle.")
                    reached.add(key)
                    scene = scenes[key]
                    route = prefix + [key]
                    self.assertRegex(key, r"^[a-z0-9][a-z0-9-]{0,79}$")
                    self.assertEqual(scene["isEnding"], not scene["transitions"])
                    if scene["isEnding"]:
                        routes.append(route)
                    else:
                        for transition in scene["transitions"]:
                            self.assertRegex(transition["key"], r"^[a-z0-9][a-z0-9-]{0,79}$")
                            self.assertTrue(transition["label"].strip())
                            visit(transition["targetSceneKey"], route)

                visit(document["initialSceneKey"], [])
                self.assertEqual(reached, set(scenes))
                self.assertEqual(len(routes), 2)
                self.assertTrue(all(len(route) == 4 for route in routes))
                self.assertEqual(sorted(routes), sorted(document["authoringNotes"]["routes"]))

    def test_questions_have_evidence_audio_is_exact_and_answer_positions_vary(self):
        positions = Counter()
        for path in STORIES:
            document = importer.load_story(path)
            lower, upper = LIMITS[document["metadata"]["difficulty"]]
            for scene in document["scenes"]:
                with self.subTest(story=path.parent.name, scene=scene["key"]):
                    count = len(scene["narration"].split())
                    self.assertTrue(lower <= count <= upper, f"{count} words outside editorial target")
                    self.assertEqual([option["key"] for option in scene["options"]], ["a", "b", "c"])
                    self.assertEqual(len({option["text"].casefold() for option in scene["options"]}), 3)
                    self.assertIn(scene["correctOptionKey"], {option["key"] for option in scene["options"]})
                    positions[scene["correctOptionKey"]] += 1
                    self.assertTrue(scene["prompt"].endswith("?"))
                    self.assertTrue(scene["explanation"].strip())
                    notes = scene["productionNotes"]
                    self.assertIn(notes["questionEvidence"].casefold(), scene["narration"].casefold())
                    self.assertEqual(notes["audioText"], scene["narration"])
                    self.assertTrue(notes["cluePurpose"].strip())
        self.assertEqual(positions, {"a": 20, "b": 20, "c": 20})

    def test_media_is_honestly_planned_and_editorial_hints_stay_outside_import_payload(self):
        for path in STORIES:
            with self.subTest(story=path.parent.name):
                document = importer.load_story(path)
                self.assertIn("autorizou expressamente o envio", document["metadata"]["licenseEvidence"])
                self.assertFalse(document["authoringNotes"]["uploadAuthorization"]["additionalApprovalRequired"])
                self.assertNotIn("status", document)
                for kind, status in document["authoringNotes"]["mediaStatus"].items():
                    self.assertIn(status, {"not-produced", "produced"})
                    if status == "produced":
                        receipt = json.loads((PACK / "vps-media.json").read_text())
                        uploaded = receipt["stories"][path.parent.name]["scenes"]
                        for scene in document["scenes"]:
                            self.assertRegex(uploaded[scene["key"]][kind]["sha256"], r"^[a-f0-9]{64}$")
                            self.assertTrue(uploaded[scene["key"]][kind]["id"])
                manifest = json.loads((path.parent / "media.example.json").read_text())
                self.assertEqual(set(manifest), {scene["key"] for scene in document["scenes"]})
                draft = {"id": "server-version", "revision": 1, "status": "Draft", "metadata": {}, "scenes": []}
                payload = importer.editor_payload(document, draft)
                self.assertNotIn("authoringNotes", payload)
                for scene in payload["scenes"]:
                    self.assertNotIn("productionNotes", scene)
                    self.assertIsNone(scene["audio"])
                    self.assertIsNone(scene["visual"])
                for scene in document["scenes"]:
                    notes = scene["productionNotes"]
                    for label in ["SCENE:", "SETTING:", "CHARACTERS:", "VISUAL CLUES:",
                                  "MOOD AND HORROR:", "CINEMATOGRAPHY:", "LIGHTING:",
                                  "CONTINUITY:", "DO NOT INCLUDE:", "AVOID:"]:
                        self.assertIn(label, notes["imagePrompt"])
                    self.assertIn("approved base image", notes["animationPrompt"])
                    self.assertEqual(notes["animationPlan"]["durationSeconds"], 6)
                    self.assertEqual(notes["animationPlan"]["framesPerSecond"], 8)
                    for kind in ["audio", "visual"]:
                        asset = manifest[scene["key"]][kind]
                        relative = Path(asset["file"])
                        self.assertFalse(relative.is_absolute())
                        self.assertNotIn("..", relative.parts)
                        self.assertEqual(asset["licenseEvidence"], "")
                        self.assertFalse((path.parent / relative).exists())

    def test_art_direction_is_drawn_animation_without_positive_photorealism(self):
        for path in STORIES:
            document = importer.load_story(path)
            with self.subTest(story=path.parent.name):
                self.assertEqual(document["authoringNotes"]["visualStyle"]["style"], "2d-animated-feature")
                for scene in document["scenes"]:
                    notes = scene["productionNotes"]
                    positive = notes["imagePrompt"].split("DO NOT INCLUDE:")[0].lower()
                    self.assertIn("2d animated-feature keyframe", positive)
                    self.assertIn("hand-drawn adult characters", positive)
                    self.assertIn("painted backgrounds", positive)
                    self.assertIn("dark, shadowy", positive)
                    self.assertNotIn("photorealistic live-action film still", positive)
                    self.assertNotIn("believable live-action", positive)
                    self.assertIn("2d animated-feature style", notes["animationPrompt"].lower())
                    self.assertIn("real actors", notes["imagePrompt"].split("AVOID:")[1].lower())

    def test_palettes_preserve_readable_text_contrast(self):
        def luminance(color):
            self.assertRegex(color, r"^#[a-fA-F0-9]{6}$")
            channels = [int(color[index:index + 2], 16) / 255 for index in (1, 3, 5)]
            linear = [value / 12.92 if value <= .04045 else ((value + .055) / 1.055) ** 2.4
                      for value in channels]
            return sum(value * weight for value, weight in zip(linear, (.2126, .7152, .0722)))

        for path in STORIES:
            with self.subTest(story=path.parent.name):
                theme = importer.load_story(path)["metadata"]["appearance"]
                for key in ["primary", "accent", "background", "surface", "text"]:
                    self.assertRegex(theme[key], r"^#[a-fA-F0-9]{6}$")
                for background in ["background", "surface"]:
                    values = sorted([luminance(theme["text"]), luminance(theme[background])])
                    self.assertGreaterEqual((values[1] + .05) / (values[0] + .05), 4.5)
                self.assertIn(theme["font"], {"classic", "rounded", "modern"})
                self.assertIn(theme["motion"], {"gentle", "none"})


if __name__ == "__main__":
    unittest.main()
