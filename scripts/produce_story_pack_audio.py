#!/usr/bin/env python3
"""Produce resumable offline dialogue WAVs for an authored story pack in its own language."""
import argparse
import hashlib
import json
from pathlib import Path

from produce_dialogue_audio import produce


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pack", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--pace", type=float, default=1.04)
    args = parser.parse_args()
    if not 0.8 <= args.pace <= 1.6:
        parser.error("Use a length scale between 0.8 and 1.6.")
    from piper import PiperVoice
    voice = PiperVoice.load(args.model)
    model_hash = digest(args.model)
    for source in sorted(args.pack.glob("*/story.json")):
        document = json.loads(source.read_text())
        for scene in document["scenes"]:
            output = args.output / source.parent.name / "audio" / (scene["key"] + ".wav")
            sidecar = output.with_suffix(".json")
            previous = json.loads(sidecar.read_text()) if sidecar.exists() else {}
            text_hash = hashlib.sha256(scene["narration"].encode()).hexdigest()
            reusable = (output.exists() and previous.get("sha256") == digest(output)
                        and previous.get("textSha256") == text_hash and previous.get("modelSha256") == model_hash
                        and previous.get("lengthScale") == args.pace
                        and [(cue["speaker"], cue["text"]) for cue in previous.get("dialogue", [])]
                        == [(cue["speaker"], cue["text"]) for cue in scene["dialogue"]])
            if reusable:
                scene["dialogue"] = previous["dialogue"]
                receipt = previous
            else:
                receipt = produce(document, scene["key"], args.model, output, args.pace, voice=voice)
            receipt.update(language=document["metadata"].get("language", "en"), model=args.model.stem)
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
