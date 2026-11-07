#!/usr/bin/env python3
"""Encode approved 2D scene art as small, restrained camera-drift GIF loops."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ffmpeg", required=True)
    parser.add_argument("--pack", type=Path, default=Path("content/anthology-01"))
    parser.add_argument("--output", type=Path, default=Path("media/anthology-01"))
    args = parser.parse_args()
    for source in sorted(args.pack.glob("*/story.json")):
        document = json.loads(source.read_text())
        folder = args.output / source.parent.name
        for scene in document["scenes"]:
            art = folder / "images" / (scene["key"] + "-dark.png")
            if not art.is_file():
                continue
            output = folder / "animations" / (scene["key"] + ".gif")
            proof = output.with_suffix(".json")
            art_hash = hashlib.sha256(art.read_bytes()).hexdigest()
            if output.exists() and proof.exists():
                previous = json.loads(proof.read_text())
                if (previous.get("encoderVersion") == 2 and previous.get("sourceSha256") == art_hash
                        and previous.get("sha256") == hashlib.sha256(output.read_bytes()).hexdigest()):
                    continue
            output.parent.mkdir(parents=True, exist_ok=True)
            # Start and end at the same crop. Two-pixel drift only, no invented scene action.
            filters = ("scale=644:362:force_original_aspect_ratio=increase,"
                       "crop=644:362,setsar=1,"
                       "crop=640:360:x='2-2*cos(2*PI*n/47)':y=1:exact=1,"
                       "split[a][b];[a]palettegen=max_colors=192:stats_mode=full[p];"
                       "[b][p]paletteuse=dither=bayer:bayer_scale=3")
            temporary = output.with_suffix(".tmp.gif")
            subprocess.run([args.ffmpeg, "-hide_banner", "-loglevel", "error", "-y",
                            "-loop", "1", "-framerate", "8", "-t", "6", "-i", str(art),
                            "-filter_complex", filters, "-frames:v", "48", "-loop", "0",
                            "-gifflags", "+offsetting+transdiff", str(temporary)], check=True, timeout=120)
            if not 0 < temporary.stat().st_size <= 8 * 1024 * 1024:
                raise ValueError(f"GIF size outside limits: {temporary}")
            temporary.replace(output)
            proof.write_text(json.dumps({"encoderVersion": 2, "sourceSha256": art_hash,
                "sha256": hashlib.sha256(output.read_bytes()).hexdigest(),
                "file": str(output.relative_to(args.output)), "width": 640, "height": 360,
                "frames": 48, "framesPerSecond": 8, "durationSeconds": 6,
                "movement": "two-pixel periodic camera drift; start and end crops coincide"}, indent=2) + "\n")
            print(f"Encoded {output}", flush=True)


if __name__ == "__main__":
    main()
