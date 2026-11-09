#!/usr/bin/env python3
"""Produce resumable offline dialogue WAVs for an authored story pack in its own language."""
import argparse
import hashlib
import json
from pathlib import Path
import unicodedata

from produce_dialogue_audio import produce


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def portuguese_rhotic_compatibility(phonemes):
    """Keep intervocalic taps; use the model's r ID in clusters and syllable codas."""
    vowels = set("aeiouyɐɑɒæəɛɜɞɪɯɔœɵʊʌøɶ")
    consonants = set("ptkbdɡgfvszʃʒθðmnŋɲlrɹɾɻʁxɣçhʎwɥjʂʐɕʑʔβʋ")
    result = []
    for sentence in phonemes:
        output = list(sentence)
        significant = [i for i, phone in enumerate(sentence)
                       if phone not in {"ˈ", "ˌ", "ː"} and not unicodedata.combining(phone)]
        for position, index in enumerate(significant):
            if sentence[index] != "ɾ":
                continue
            previous = sentence[significant[position - 1]] if position else None
            following = sentence[significant[position + 1]] if position + 1 < len(significant) else None
            if previous in consonants or following not in vowels:
                output[index] = "r"
        result.append(output)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pack", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--pace", type=float, default=1.04)
    parser.add_argument("--phoneme-alias", action="append", default=[], metavar="SOURCE=TARGET",
                        help="Optional production-only phoneme ID alias, recorded in every audio receipt.")
    parser.add_argument("--portuguese-rhotic-compat", action="store_true",
                        help="Preserve Brazilian intervocalic taps while clarifying clusters/codas in older Portuguese voices.")
    args = parser.parse_args()
    if not 0.8 <= args.pace <= 1.6:
        parser.error("Use a length scale between 0.8 and 1.6.")
    from piper import PiperVoice
    voice = PiperVoice.load(args.model)
    if args.portuguese_rhotic_compat:
        if args.phoneme_alias or voice.config.espeak_voice != "pt-br":
            parser.error("Portuguese rhotic compatibility requires a pt-br voice without global aliases.")
        phonemize = voice.phonemize
        voice.phonemize = lambda text: portuguese_rhotic_compatibility(phonemize(text))
    aliases = {}
    for definition in args.phoneme_alias:
        pair = definition.split("=")
        if len(pair) != 2 or any(part not in voice.config.phoneme_id_map for part in pair):
            parser.error("Each phoneme alias must name two known model phonemes, separated by '='.")
        aliases[pair[0]] = pair[1]
    original_map = dict(voice.config.phoneme_id_map)
    for source_phoneme, target_phoneme in aliases.items():
        voice.config.phoneme_id_map[source_phoneme] = original_map[target_phoneme]
    model_hash = digest(args.model)
    for source in sorted(args.pack.glob("*/story.json")):
        document = json.loads(source.read_text())
        language = document["metadata"].get("language", "en")
        voice_language = voice.config.espeak_voice.lower().replace("_", "-")
        if not (language == "pt-BR" and voice_language == "pt-br" or language == "en" and voice_language.startswith("en")):
            raise ValueError(f"Voice language does not match the story: {source.parent.name}")
        for scene in document["scenes"]:
            output = args.output / source.parent.name / "audio" / (scene["key"] + ".wav")
            sidecar = output.with_suffix(".json")
            previous = json.loads(sidecar.read_text()) if sidecar.exists() else {}
            text_hash = hashlib.sha256(scene["narration"].encode()).hexdigest()
            reusable = (output.exists() and previous.get("sha256") == digest(output)
                        and previous.get("textSha256") == text_hash and previous.get("modelSha256") == model_hash
                        and previous.get("lengthScale") == args.pace
                        and previous.get("phonemeAliases", {}) == aliases
                        and previous.get("portugueseRhoticCompatibility", False) == args.portuguese_rhotic_compat
                        and [(cue["speaker"], cue["text"]) for cue in previous.get("dialogue", [])]
                        == [(cue["speaker"], cue["text"]) for cue in scene["dialogue"]])
            if reusable:
                scene["dialogue"] = previous["dialogue"]
                receipt = previous
            else:
                receipt = produce(document, scene["key"], args.model, output, args.pace, voice=voice)
            receipt.update(language=document["metadata"].get("language", "en"), model=args.model.stem, phonemeAliases=aliases)
            receipt["portugueseRhoticCompatibility"] = args.portuguese_rhotic_compat
            sidecar.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n")
            # Save each completed scene so interruption never loses measured intervals.
            temporary = source.with_suffix(".json.tmp")
            temporary.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n")
            temporary.replace(source)
            print(f"{source.parent.name}/{scene['key']}: {receipt['durationMs']} ms", flush=True)
        document["authoringNotes"]["mediaStatus"]["audio"] = "produced"
        document["authoringNotes"]["dialogueTimingStatus"] = "Measured from concatenated PCM frames; one synthetic voice for all speakers."
        source.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n")


if __name__ == "__main__":
    main()
