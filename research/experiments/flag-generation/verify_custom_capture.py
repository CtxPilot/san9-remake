"""Check actual GPU output for arbitrary colors and reference preservation."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image

from run_experiment import ROOT


def verify(capture: Path) -> dict:
    assert not (capture / "engine.errors.log").read_text(encoding="utf-8-sig").strip()
    assert "FLAG_CUSTOM_CAPTURE_OK" in (capture / "engine.log").read_text(encoding="utf-8-sig")
    def panel(name: str, box: tuple) -> np.ndarray:
        with Image.open(capture / f"{name}.png") as image:
            return np.asarray(image.crop(box))
    original = (120, 310, 740, 715)
    final = (860, 310, 1480, 715)
    baseline = panel("custom-baseline", original)
    for name in ("custom-dark-1", "custom-light", "custom-white", "custom-no-emblem", "custom-restored"):
        np.testing.assert_array_equal(panel(name, original), baseline)
    names = ("custom-baseline", "custom-dark-1", "custom-light", "custom-white", "custom-no-emblem")
    assert len({panel(name, final).tobytes() for name in names}) == len(names)
    assert len({panel(f"custom-dark-{view}", final).tobytes() for view in range(8)}) == 8
    np.testing.assert_array_equal(panel("custom-baseline", final), panel("custom-restored", final))
    report = {"reference_preserved": True, "dark_and_light_custom_colors_rendered": True,
              "automatic_and_manual_ink_and_no_emblem": True, "custom_views": 8, "source_palette_restored": True}
    (capture / "custom-verification.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--capture", type=Path, default=ROOT / "local/generated/research/flag-generation/custom-color-capture")
    args = parser.parse_args()
    if not args.capture.resolve().is_relative_to((ROOT / "local/generated").resolve()):
        parser.error("Report output must stay under local/generated")
    print(json.dumps(verify(args.capture), indent=2))
