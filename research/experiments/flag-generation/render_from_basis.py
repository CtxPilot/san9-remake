"""Render an arbitrary single-character flag without reading original game assets."""
from pathlib import Path
import argparse

import numpy as np
from PIL import Image

from run_experiment import ROOT, SharedFlagGenerator, backdrop, make_flat_glyph


def rgb(value: str) -> np.ndarray:
    try:
        if len(value) != 6:
            raise ValueError()
        return np.array([int(value[i:i + 2], 16) / 255 for i in (0, 2, 4)])
    except ValueError as error:
        raise argparse.ArgumentTypeError("Expected six hex RGB digits, e.g. 20A0C0") from error


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--basis-dir", type=Path, default=ROOT / "local/generated/research/flag-generation")
    parser.add_argument("--text", default="雲")
    parser.add_argument("--face", type=rgb, default=rgb("20A0C0"))
    parser.add_argument("--border", type=rgb, default=rgb("EDB847"))
    parser.add_argument("--font", type=Path, default=Path("C:/Windows/Fonts/kaiu.ttf"))
    parser.add_argument("--output", type=Path, default=ROOT / "local/generated/research/flag-generation/custom.gif")
    args = parser.parse_args()
    if len(args.text) != 1:
        parser.error("This prototype's decal layout supports one character")
    if not args.output.resolve().is_relative_to((ROOT / "local/generated").resolve()):
        parser.error("Experiment outputs must stay under local/generated")
    generator = SharedFlagGenerator(args.basis_dir / "shared-basis.npz", args.basis_dir / "base-inputs.npz")
    base = make_flat_glyph(args.text, args.font)
    frames = []
    for phase in range(4):
        frame = Image.new("RGB", (8 * 204, 156), (27, 33, 39))
        for direction in range(8):
            frame.paste(backdrop(generator.render(np.r_[args.face, args.border], base, direction, phase), 3), (direction * 204, 0))
        frames.append(frame)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(args.output, save_all=True, append_images=frames[1:], duration=140, loop=0, disposal=2)
    print(args.output)
