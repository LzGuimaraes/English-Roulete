#!/usr/bin/env python3
"""Generate prerecorded development WAV assets offline; never runs inside the API."""
import ctypes
import ctypes.util
import hashlib
import io
import json
import math
from pathlib import Path
import sys
import wave

ROOT = Path(__file__).resolve().parents[1]
MEDIA = ROOT / "media"


def main():
    library = ctypes.util.find_library("espeak-ng")
    if not library:
        raise SystemExit("Install libespeak-ng1 and espeak-ng-data to generate the development narration.")
    engine = ctypes.CDLL(library)
    engine.espeak_Initialize.argtypes = [ctypes.c_int, ctypes.c_int, ctypes.c_char_p, ctypes.c_int]
    sample_rate = engine.espeak_Initialize(2, 0, None, 0x8000)
    if sample_rate <= 0:
        raise SystemExit("eSpeak NG voice data is unavailable.")
    engine.espeak_SetVoiceByName.argtypes = [ctypes.c_char_p]
    if engine.espeak_SetVoiceByName(b"en-us") != 0:
        raise SystemExit("The en-us voice is unavailable.")
    engine.espeak_SetParameter(1, 150, 0)
    chunks = []
    callback_type = ctypes.CFUNCTYPE(ctypes.c_int, ctypes.POINTER(ctypes.c_short), ctypes.c_int, ctypes.c_void_p)

    @callback_type
    def collect(samples, count, events):
        if samples and count:
            chunks.append(ctypes.string_at(samples, count * 2))
        return 0

    engine.espeak_SetSynthCallback.argtypes = [callback_type]
    engine.espeak_SetSynthCallback(collect)
    engine.espeak_Synth.argtypes = [ctypes.c_void_p, ctypes.c_size_t, ctypes.c_uint, ctypes.c_int,
                                   ctypes.c_uint, ctypes.c_uint, ctypes.POINTER(ctypes.c_uint), ctypes.c_void_p]
    story = json.loads((ROOT / "src/EnglishRoulette.Api/Demo/story.json").read_text())
    manifest_path = MEDIA / "manifest.json"
    assets = json.loads(manifest_path.read_text()) if manifest_path.exists() else []
    (MEDIA / "audio").mkdir(parents=True, exist_ok=True)
    for scene in story["scenes"]:
        text_hash = hashlib.sha256(scene["narration"].encode()).hexdigest()
        existing = next((asset for asset in assets if asset["storyVersionId"] == story["id"]
                         and asset["sceneKey"] == scene["key"]), None)
        if existing:
            file = MEDIA / "audio" / (existing["sha256"] + ".wav")
            if existing["textSha256"] != text_hash:
                raise SystemExit("Published narration changed: create a new story version before regenerating audio.")
            if file.exists() and hashlib.sha256(file.read_bytes()).hexdigest() == existing["sha256"]:
                print(scene["key"] + ": existing immutable narration verified")
                continue
        chunks.clear()
        text = ctypes.create_string_buffer(scene["narration"].encode("utf-8"))
        identifier = ctypes.c_uint()
        if engine.espeak_Synth(text, len(text), 0, 1, 0, 1, ctypes.byref(identifier), None) != 0:
            raise SystemExit("Narration synthesis failed.")
        pcm = b"".join(chunks)
        buffer = io.BytesIO()
        with wave.open(buffer, "wb") as output:
            output.setnchannels(1)
            output.setsampwidth(2)
            output.setframerate(sample_rate)
            output.writeframes(pcm)
        content = buffer.getvalue()
        sha = hashlib.sha256(content).hexdigest()
        if existing and existing["sha256"] != sha:
            raise SystemExit("The voice output changed. Restore the original asset or publish a new story version.")
        file = MEDIA / "audio" / (sha + ".wav")
        file.write_bytes(content)
        asset = dict(storyVersionId=story["id"], sceneKey=scene["key"], sha256=sha, textSha256=text_hash,
                     durationMs=math.ceil(len(pcm) / 2 * 1000 / sample_rate), sizeBytes=len(content),
                     voice="eSpeak NG en-us, 150 words/minute", attribution="Original English Roulette demo story; synthetic development narration.")
        if not existing:
            assets.append(asset)
        print(scene["key"] + ": " + str(asset["durationMs"]) + " ms prerecorded WAV generated")
    manifest_path.write_text(json.dumps(assets, indent=2) + "\n")
    engine.espeak_Terminate()


if __name__ == "__main__":
    main()
