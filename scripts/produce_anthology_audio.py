#!/usr/bin/env python3
"""Produce offline neural WAV narration; generated media stays outside Git."""
import argparse
import hashlib
import json
from pathlib import Path
import wave


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--pack", type=Path, default=Path("content/anthology-01"))
    parser.add_argument("--output", type=Path, default=Path("media/anthology-01"))
    args = parser.parse_args()
    # Optional production dependency, never imported by the application/runtime.
    import numpy as np
    from piper import PiperVoice, SynthesisConfig

    voice = PiperVoice.load(args.model)
    model_hash = sha256(args.model)
    receipt_path = args.output / "audio-production.json"
    args.output.mkdir(parents=True, exist_ok=True)
    receipt = json.loads(receipt_path.read_text()) if receipt_path.exists() else {"scenes": {}}
    for source in sorted(args.pack.glob("*/story.json")):
        story = json.loads(source.read_text())
        slug = source.parent.name
        pace = {"A1": 1.22, "A2": 1.10, "B1": 1.03}[story["metadata"]["difficulty"]]
        for scene in story["scenes"]:
            text = scene["productionNotes"]["audioText"]
            if text != scene["narration"]:
                raise ValueError(f"Narration mismatch: {slug}/{scene['key']}")
            identity = slug + "/" + scene["key"]
            output = args.output / slug / "audio" / (scene["key"] + ".wav")
            text_hash = hashlib.sha256(text.encode()).hexdigest()
            previous = receipt["scenes"].get(identity, {})
            reuse = (output.exists() and previous.get("textSha256") == text_hash
                     and previous.get("modelSha256") == model_hash and previous.get("lengthScale") == pace
                     and previous.get("sha256") == sha256(output))
            if not reuse:
                output.parent.mkdir(parents=True, exist_ok=True)
                temporary = output.with_suffix(".wav.tmp")
                with wave.open(str(temporary), "wb") as wav:
                    voice.synthesize_wav(text, wav, syn_config=SynthesisConfig(
                        length_scale=pace, noise_scale=0.45, noise_w_scale=0.6, volume=0.86))
                temporary.replace(output)
            with wave.open(str(output), "rb") as wav:
                if (wav.getsampwidth(), wav.getnchannels(), wav.getcomptype()) != (2, 1, "NONE"):
                    raise ValueError(f"Invalid PCM: {identity}")
                duration = wav.getnframes() / wav.getframerate()
                samples = np.frombuffer(wav.readframes(wav.getnframes()), dtype="<i2").astype(np.float64)
            rms = float(np.sqrt(np.mean(samples ** 2)))
            peak = float(np.max(np.abs(samples)))
            if not 1 < duration <= 120 or not 0 < output.stat().st_size <= 10 * 1024 * 1024 or rms < 100 or peak >= 32767:
                raise ValueError(f"Invalid duration, size, silence or clipping: {identity}")
            receipt["scenes"][identity] = {
                "file": str(output.relative_to(args.output)), "sha256": sha256(output),
                "textSha256": text_hash, "model": "en_US-ljspeech-high", "modelSha256": model_hash,
                "lengthScale": pace, "durationMs": int(duration * 1000), "rms": round(rms, 2), "peak": peak,
                "sampleRate": voice.config.sample_rate, "engine": "piper-tts 1.8.0",
                "modelCard": "https://huggingface.co/rhasspy/piper-voices/blob/main/en/en_US/ljspeech/high/MODEL_CARD",
                "dataset": "https://keithito.com/LJ-Speech-Dataset/",
            }
            temporary_receipt = receipt_path.with_suffix(".json.tmp")
            temporary_receipt.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n")
            temporary_receipt.replace(receipt_path)
            print(f"{len(receipt['scenes']):02d}/60 {identity}: {duration:.1f}s, PCM valid", flush=True)


if __name__ == "__main__":
    main()
