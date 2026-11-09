import copy
import json
from pathlib import Path
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))
from import_story import editor_payload, load_story
from upload_anthology_media import credits
from verify_anthology_narration import words


class LanguageToolsTests(unittest.TestCase):
    def test_import_keeps_explicit_language_and_rejects_unsupported_language(self):
        source = ROOT / "content/anthology-03/a-very-important-rehearsal-a1/story.json"
        document = load_story(source)
        self.assertNotIn("language", document["metadata"])
        portuguese = copy.deepcopy(document)
        portuguese["metadata"]["language"] = "pt-BR"
        draft = {"status": "Draft", "metadata": {}, "scenes": []}
        result = editor_payload(portuguese, draft)
        self.assertEqual(result["metadata"]["language"], "pt-BR")
        self.assertNotIn("language", document["metadata"])
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "story.json"
            path.write_text(json.dumps(portuguese))
            self.assertEqual(load_story(path)["metadata"]["language"], "pt-BR")
            portuguese["metadata"]["language"] = "es"
            path.write_text(json.dumps(portuguese))
            with self.assertRaises(ValueError):
                load_story(path)

    def test_portuguese_audio_credits_identify_its_actual_voice_and_dataset(self):
        scene = {"key": "first", "productionNotes": {"audioLanguage": "pt-BR"}}
        result = credits("audio", Path("first.wav"), scene)
        self.assertIn("pt_BR-cadu-medium", result["attribution"])
        self.assertIn("CC0", result["licenseEvidence"])
        self.assertNotIn("LJ Speech", result["attribution"])
        self.assertIn("en_US-ljspeech-high", credits("audio", Path("first.wav"), {"key": "first"})["attribution"])

    def test_speech_review_keeps_portuguese_accents_and_complete_words(self):
        self.assertEqual(words("A narração: três opções, coração e mão."),
                         ["a", "narração", "três", "opções", "coração", "e", "mão"])
