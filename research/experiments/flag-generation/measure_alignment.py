"""Compare temporal contour changes in the actual GPU front-view captures.

Remove each column's mean height so the result measures motion rather than the
different painted rest silhouettes. This is a narrow metric, not visual similarity.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image

from run_experiment import ROOT


def contour_motion(files: list[Path], columns: list[float]) -> np.ndarray:
    frames = []
    for file in files:
        with Image.open(file) as image:
            rgb = np.asarray(image.convert("RGB")).astype(int)
        frame = []
        for column in columns:
            x = round(column)
            mask = np.max(np.abs(rgb[350:660, x - 1:x + 2] - [32, 43, 48]), axis=-1) > 40
            yy = np.flatnonzero(mask.any(axis=1))
            if not len(yy):
                raise AssertionError(f"No cloth at column {x}: {file}")
            frame.append([yy.min(), yy.max()])
        frames.append(frame)
    positions = np.asarray(frames) / 8.0
    return positions - positions.mean(axis=0)


def measure(capture: Path, baseline: Path, settings_path: Path) -> dict:
    settings = json.loads(settings_path.read_text(encoding="utf-8"))
    profile = settings["directions"][1]
    stations = np.linspace(0.15, 0.65, 6)
    original_columns = [166 + (1 + 30 - 26 * u) * 8 for u in stations]
    final_columns = [906 + (profile["attachment"][0] - profile["projection_scale"][0] * 26 * u) * 8 for u in stations]
    old_files = [baseline / f"frame-{phase * 9:03d}.png" for phase in range(4)]
    new_files = [capture / f"alignment-1-{phase}.png" for phase in range(4)]
    source = contour_motion(new_files, original_columns)
    before = contour_motion(old_files, final_columns)
    after = contour_motion(new_files, final_columns)
    # Source is unchanged between the captures, independently of final geometry.
    np.testing.assert_array_equal(source, contour_motion(old_files, original_columns))
    before_rmse = float(np.sqrt(np.mean((before - source) ** 2)))
    after_rmse = float(np.sqrt(np.mean((after - source) ** 2)))
    report = {"view": 1, "phase_count": 4, "stations_from_hoist": stations.tolist(),
              "before_temporal_contour_rmse": before_rmse, "after_temporal_contour_rmse": after_rmse,
              "source_motion": source.tolist(), "before_motion": before.tolist(), "after_motion": after.tolist(),
              "units": "source display pixels; mean-subtracted upper/lower contours, excluding tabs and pole",
              "limitation": "front-view contour motion only; not a similarity score for lettering, folds or all views"}
    (capture / "alignment-verification.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not after_rmse < before_rmse:
        raise AssertionError(f"Contour motion did not improve: {before_rmse} -> {after_rmse}")
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--capture", type=Path, default=ROOT / "local/generated/research/flag-generation/alignment-capture")
    parser.add_argument("--baseline", type=Path, default=ROOT / "local/generated/research/flag-generation/color-capture")
    parser.add_argument("--settings", type=Path, default=ROOT / "local/generated/research/flag-generation/modern-godot/assets/generated-material.json")
    args = parser.parse_args()
    if not args.capture.resolve().is_relative_to((ROOT / "local/generated").resolve()):
        parser.error("Report output must stay under local/generated")
    report = measure(args.capture, args.baseline, args.settings)
    print(json.dumps({key: report[key] for key in ("before_temporal_contour_rmse", "after_temporal_contour_rmse", "units")}, indent=2))
