#!/usr/bin/env python3
"""Produce one scene offline, joining authored speech cues and recording exact PCM intervals."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import wave


def produce(document, scene_key, model, output, pace):
    from piper import PiperVoice, SynthesisConfig
    scene = next(scene for scene in document["scenes"] if scene["key"] == scene_key)
    cues = scene.get("dialogue", [])
    normalize = lambda text: " ".join(text.split())
    if not cues or normalize(" ".join(cue["text"] for cue in cues)) != normalize(scene["narration"]):
        raise ValueError("Dialogue must reproduce the entire narration in order.")
    voice = PiperVoice.load(model)
    chunks, frames, sample_rate = [], 0, voice.config.sample_rate
    for cue in cues:
        buffer = io.BytesIO()
        with wave.open(buffer, "wb") as wav:
            voice.synthesize_wav(cue["text"].replace('"', ''), wav, syn_config=SynthesisConfig(
                length_scale=pace, noise_scale=0.45, noise_w_scale=0.6, volume=0.86))
        buffer.seek(0)
        with wave.open(buffer, "rb") as wav:
            if (wav.getsampwidth(), wav.getnchannels(), wav.getframerate()) != (2, 1, sample_rate):
                raise ValueError("All cues must use the same PCM format.")
            count = wav.getnframes()
            cue["startMilliseconds"] = frames * 1000 // sample_rate
            frames += count
            cue["endMilliseconds"] = frames * 1000 // sample_rate
            chunks.append(wav.readframes(count))
    if not 0 < frames / sample_rate <= 120:
        raise ValueError("Scene exceeds the supported duration.")
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_suffix(".wav.tmp")
    with wave.open(str(temporary), "wb") as wav:
        wav.setnchannels(1); wav.setsampwidth(2); wav.setframerate(sample_rate)
        wav.writeframes(b"".join(chunks))
    if temporary.stat().st_size > 10 * 1024 * 1024:
        temporary.unlink(); raise ValueError("Scene exceeds the upload limit.")
    temporary.replace(output)
    receipt = {"scene": scene_key, "sha256": hashlib.sha256(output.read_bytes()).hexdigest(),
        "textSha256": hashlib.sha256(scene["narration"].encode()).hexdigest(),
        "modelSha256": hashlib.sha256(model.read_bytes()).hexdigest(), "lengthScale": pace,
        "durationMs": frames * 1000 // sample_rate, "dialogue": cues,
        "note": "One synthetic voice; speaker names are editorial labels. Cue boundaries are measured from concatenated PCM, not estimated word timestamps."}
    output.with_suffix(".json").write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n")
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--story", type=Path, required=True)
    parser.add_argument("--scene", required=True)
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--pace", type=float, default=1.1)
    args = parser.parse_args()
    document = json.loads(args.story.read_text())
    receipt = produce(document, args.scene, args.model, args.output, args.pace)
    args.story.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n")
    print(f"{args.scene}: {receipt['durationMs']} ms; {len(receipt['dialogue'])} measured cues, one synthetic voice.")


if __name__ == "__main__":
    main()
