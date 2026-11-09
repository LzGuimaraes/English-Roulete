#!/usr/bin/env python3
"""Transcribe the produced WAVs locally and report differences for editorial review."""
import argparse
import difflib
import hashlib
import json
from pathlib import Path
import re
import subprocess


def words(text):
    return re.findall(r"[^\W_]+", text.lower().replace("’", "'"), flags=re.UNICODE)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model", default="base.en")
    parser.add_argument("--language", choices=["en", "pt"], default="en")
    parser.add_argument("--models", type=Path, default=Path(".local/media-tools/whisper"))
    parser.add_argument("--pack", type=Path, default=Path("content/anthology-01"))
    parser.add_argument("--media", type=Path, default=Path("media/anthology-01"))
    parser.add_argument("--report", type=Path, default=Path(".local/narration-review.json"))
    parser.add_argument("--offline", action="store_true", help="Use an already downloaded model without network.")
    args = parser.parse_args()
    from faster_whisper import WhisperModel
    import imageio_ffmpeg
    import numpy as np
    model = WhisperModel(args.model, device="cpu", compute_type="int8", cpu_threads=4,
                         download_root=str(args.models), local_files_only=args.offline)
    report = json.loads(args.report.read_text()) if args.report.exists() else {}
    for source in sorted(args.pack.glob("*/story.json")):
        document = json.loads(source.read_text())
        for scene in document["scenes"]:
            identity = source.parent.name + "/" + scene["key"]
            audio = args.media / source.parent.name / "audio" / (scene["key"] + ".wav")
            checksum = hashlib.sha256(audio.read_bytes()).hexdigest()
            if report.get(identity, {}).get("sha256") == checksum:
                continue
            decoded = subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(), "-hide_banner", "-loglevel", "error",
                "-i", str(audio), "-f", "f32le", "-ac", "1", "-ar", "16000", "pipe:1"],
                check=True, stdout=subprocess.PIPE, timeout=30).stdout
            samples = np.frombuffer(decoded, dtype="<f4")
            segments, _ = model.transcribe(samples, language=args.language, beam_size=5,
                condition_on_previous_text=False, vad_filter=False)
            transcript = " ".join(segment.text.strip() for segment in segments)
            expected, actual = words(scene["narration"]), words(transcript)
            differences = []
            for tag, i, j, k, m in difflib.SequenceMatcher(None, expected, actual, autojunk=False).get_opcodes():
                if tag != "equal":
                    differences.append({"kind": tag, "source": " ".join(expected[i:j]), "heard": " ".join(actual[k:m])})
            report[identity] = {"sha256": checksum, "source": scene["narration"], "transcript": transcript,
                "differences": differences, "recognizer": args.model, "language": args.language,
                "note": "Automatic recognition can mishear names and punctuation; differences require review, not automatic text replacement."}
            temporary = args.report.with_suffix(".json.tmp")
            temporary.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
            temporary.replace(args.report)
            print(f"Transcribed {len(report):02d} {identity}; differences: {len(differences)}", flush=True)


if __name__ == "__main__":
    main()
