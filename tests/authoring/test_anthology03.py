"""New thematic coverage and evidence-based questions, without media or network."""
from collections import Counter
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
PACK = ROOT / "content/anthology-03"


class NewThemesTests(unittest.TestCase):
    def test_each_story_adds_a_genre_and_title_not_present_in_the_previous_packs(self):
        current = [json.loads(path.read_text()) for path in sorted(PACK.glob("*/story.json"))]
        previous = [json.loads(path.read_text()) for pack in ["anthology-01", "anthology-02"]
                    for path in (ROOT / "content" / pack).glob("*/story.json")]
        genres = {story["metadata"]["genre"].casefold() for story in current}
        titles = {story["metadata"]["title"].casefold() for story in current}
        self.assertEqual(len(current), 4)
        self.assertEqual(len(genres), 4)
        self.assertTrue(genres.isdisjoint(story["metadata"]["genre"].casefold() for story in previous))
        self.assertEqual(len(titles), 4)
        self.assertTrue(titles.isdisjoint(story["metadata"]["title"].casefold() for story in previous))
        self.assertEqual(Counter(story["metadata"]["difficulty"] for story in current), {"A1": 1, "A2": 2, "B1": 1})

    def test_questions_audio_scripts_and_dialogue_use_the_same_current_scene(self):
        positions = Counter()
        for path in sorted(PACK.glob("*/story.json")):
            story = json.loads(path.read_text())
            lower, upper = {"A1": (30, 50), "A2": (45, 75), "B1": (65, 95)}[story["metadata"]["difficulty"]]
            for scene in story["scenes"]:
                with self.subTest(story=path.parent.name, scene=scene["key"]):
                    notes = scene["productionNotes"]
                    self.assertEqual(scene["narration"], notes["audioText"])
                    self.assertEqual(scene["narration"], " ".join(cue["text"] for cue in scene["dialogue"]))
                    self.assertTrue(notes["questionEvidence"].strip())
                    self.assertIn(notes["questionEvidence"].casefold(), scene["narration"].casefold())
                    self.assertTrue(lower <= len(scene["narration"].split()) <= upper)
                    self.assertEqual([option["key"] for option in scene["options"]], ["a", "b", "c"])
                    self.assertEqual(len({option["text"].casefold() for option in scene["options"]}), 3)
                    self.assertIn(scene["correctOptionKey"], ["a", "b", "c"])
                    self.assertTrue(scene["prompt"].endswith("?"))
                    positions[scene["correctOptionKey"]] += 1
                    self.assertEqual(len(scene["vocabulary"]), 2)
                    self.assertIn("2D animated-feature", notes["imagePrompt"])
        self.assertEqual(positions, {"a": 7, "b": 7, "c": 6})


if __name__ == "__main__":
    unittest.main()
