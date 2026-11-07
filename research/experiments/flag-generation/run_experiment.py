"""Offline shared-material/shared-deformation experiment; never edits game content."""
from __future__ import annotations

import argparse
import hashlib
import json
import platform
import struct
import sys
from pathlib import Path

import numpy as np
import PIL
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "research/scripts"))
from square_map.graphics import parse_palette_image_archive

GLYPH_ROWS = (0, 1, 2, -1, 3, 4, 5, -1)
CANONICAL = 4  # direction 1, phase 0, in direction-major order
SIZE = (68, 52)


def load_archive(path: Path, palette_slots: list[int], alpha: bool) -> list[np.ndarray]:
    _, records = parse_palette_image_archive(path)
    raw = path.read_bytes()
    decoded = []
    for record, slot in zip(records, palette_slots, strict=True):
        start = 4 + slot * 1064 + 40
        palette = np.frombuffer(raw[start:start + 1024], dtype=np.uint8).reshape(256, 4)
        indices = np.frombuffer(record.pixels, dtype=np.uint8).reshape(record.height, record.width)
        rgba = palette[indices].copy()
        visible = np.any(rgba != palette[0], axis=-1)
        rgba[..., 3] = np.where(visible, 255, 0)
        if alpha:
            rgba[..., 3] = np.where(visible, (np.minimum(palette[indices, 3].astype(int), 128) * 255 + 64) // 128, 0)
        decoded.append(rgba)
    return decoded


def split_frames(sheet: np.ndarray, rows: int) -> np.ndarray:
    return sheet.reshape(rows, 40, 4, 32, 4).transpose(0, 2, 1, 3, 4).reshape(rows * 4, 40, 32, 4)


def premultiply(rgba: np.ndarray) -> np.ndarray:
    result = rgba.astype(np.float32) / 255
    result[..., :3] *= result[..., 3:4]
    return result


def unpremultiply(pixels: np.ndarray) -> np.ndarray:
    pixels = np.clip(pixels, 0, 1)
    rgb = np.divide(pixels[..., :3], pixels[..., 3:4], out=np.zeros_like(pixels[..., :3]), where=pixels[..., 3:4] > 1e-7)
    return np.rint(np.concatenate([np.clip(rgb, 0, 1), pixels[..., 3:4]], axis=-1) * 255).astype(np.uint8)


def material_parameters(cloths: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    # A fixed face patch and trim region identified on training color 2 (red).
    face = np.median(cloths[:, CANONICAL, 14:25, 12:23, :3], axis=(1, 2)) / 255
    reference = cloths[2, CANONICAL]
    trim_mask = (reference[..., 0] > 100) & (reference[..., 1] > 70) & (reference[..., 2] < 100) & (reference[..., 3] > 0)
    trim = np.median(cloths[:, CANONICAL, :, :, :3][:, trim_mask], axis=1) / 255
    return np.c_[face, trim, np.ones(len(face))], trim_mask


def fit_material(cloths: np.ndarray, parameters: np.ndarray, training: list[int]) -> tuple[np.ndarray, np.ndarray]:
    # One spatial response to base RGB + border RGB. No per-flag image survives.
    x = parameters[training]
    y = cloths[training, ..., :3].astype(np.float32) / 255
    mask = cloths[training, ..., 3] > 0
    coverage = np.mean(mask, axis=0) >= 0.5
    # Invisible key-green pixels must never participate in the color fit.
    weights = mask.astype(np.float64)
    gram = np.einsum("na,nfh,nb->fhab", x, weights.reshape(len(x), 32, -1), x)
    rhs = np.einsum("na,nfh,nfhc->fhac", x, weights.reshape(len(x), 32, -1), y.reshape(len(x), 32, -1, 3))
    gram += np.eye(7) * 1e-5
    coefficients = np.linalg.solve(gram, rhs).transpose(2, 0, 1, 3).reshape(7, 32, 40, 32, 3).astype(np.float32)
    return coefficients, coverage


def render_cloth(parameters: np.ndarray, coefficients: np.ndarray, coverage: np.ndarray) -> np.ndarray:
    rgb = np.clip(np.einsum("a,afhwc->fhwc", parameters, coefficients), 0, 1)
    return np.rint(np.concatenate([rgb, coverage[..., None]], axis=-1) * 255).astype(np.uint8)


def fit_warp(glyphs: np.ndarray, training: list[int]) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    """Fit one bilinear inverse map per frame, shared by all glyphs and inks.

    Descriptors concatenate training glyph coverage and premultiplied luminance.
    Matching only searches local bilinear patches of the canonical source frame;
    the output contains sampling positions/gain, never glyph-specific pixels.
    """
    samples = premultiply(glyphs[:, training]).reshape(2 * len(training), 24, 40, 32, 4)
    descriptors = np.concatenate([samples[..., 3], samples[..., :3].mean(-1)], axis=0)
    base = descriptors[:, CANONICAL].reshape(-1, 1280).T
    active = np.flatnonzero(np.mean(base ** 2, axis=1) > 0.00005)
    candidates = base[active]
    norm = np.sum(candidates ** 2, axis=1)
    uv = np.zeros((24, 1280, 2), dtype=np.float32)
    gain = np.zeros((24, 1280), dtype=np.float32)
    residual = np.zeros((24, 1280), dtype=np.float32)
    yy, xx = np.mgrid[:40, :32]
    for frame in range(24):
        if frame == CANONICAL:
            uv[frame] = np.c_[xx.ravel(), yy.ravel()]
            gain[frame] = 1
            continue
        target = descriptors[:, frame].reshape(-1, 1280).T
        targets = np.flatnonzero(np.mean(target ** 2, axis=1) > 0.00005)
        t = target[targets]
        dot = t @ candidates.T
        scales = np.clip(dot / (norm[None] + 1e-10), 0, 1.8)
        errors = np.sum(t ** 2, axis=1)[:, None] - 2 * scales * dot + scales ** 2 * norm[None]
        seeds = active[np.argpartition(errors, 5, axis=1)[:, :6]]
        best_error = np.full(len(targets), np.inf)
        best_uv = np.zeros((len(targets), 2), dtype=np.float32)
        best_gain = np.zeros(len(targets), dtype=np.float32)
        # Coarse fractional inverse sampling, then deterministic subpixel refinement.
        for step, offsets in [(0.5, range(-2, 3)), (0.125, range(-2, 3))]:
            centers = np.stack([seeds % 32, seeds // 32], axis=-1) if step == 0.5 else best_uv[:, None]
            for dx in offsets:
                for dy in offsets:
                    positions = np.clip(centers + np.array([dx, dy]) * step, [0, 0], [31, 39])
                    vectors = sample_bilinear(base.reshape(40, 32, -1), positions)
                    dot = np.einsum("nkd,nd->nk", vectors, t)
                    norms = np.sum(vectors ** 2, axis=-1)
                    scales = np.clip(dot / (norms + 1e-10), 0, 1.8)
                    errors = np.sum(t ** 2, axis=1)[:, None] - 2 * scales * dot + scales ** 2 * norms
                    choice = np.argmin(errors, axis=1)
                    error = errors[np.arange(len(targets)), choice]
                    better = error < best_error
                    best_error[better] = error[better]
                    best_uv[better] = positions[np.arange(len(targets)), choice][better]
                    best_gain[better] = scales[np.arange(len(targets)), choice][better]
        uv[frame, targets] = best_uv
        gain[frame, targets] = best_gain
        residual[frame, targets] = best_error / descriptors.shape[0]
    return uv.reshape(24, 40, 32, 2), gain.reshape(24, 40, 32), residual.reshape(24, 40, 32)


def sample_bilinear(image: np.ndarray, positions: np.ndarray) -> np.ndarray:
    x = np.clip(positions[..., 0], 0, image.shape[1] - 1)
    y = np.clip(positions[..., 1], 0, image.shape[0] - 1)
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    x1, y1 = np.minimum(x0 + 1, image.shape[1] - 1), np.minimum(y0 + 1, image.shape[0] - 1)
    fx, fy = (x - x0)[..., None], (y - y0)[..., None]
    return (image[y0, x0] * (1 - fx) + image[y0, x1] * fx) * (1 - fy) + (image[y1, x0] * (1 - fx) + image[y1, x1] * fx) * fy


def render_glyph(base: np.ndarray, uv: np.ndarray, gain: np.ndarray) -> np.ndarray:
    return unpremultiply(sample_bilinear(premultiply(base), uv) * gain[..., None])


class SharedFlagGenerator:
    """The generation stage uses only shared fields and flat per-flag inputs."""

    def __init__(self, basis: Path, inputs: Path):
        with np.load(basis, allow_pickle=False) as data:
            self.coefficients = data["cloth_coefficients"]
            self.coverage = data["cloth_coverage"]
            self.uv = data["glyph_uv"]
            self.gain = data["glyph_gain"]
        with np.load(inputs, allow_pickle=False) as data:
            self.materials = data["material_parameters"]
            self.glyph_bases = data["glyph_bases"]
            self.pole = data["pole"]
            self.offsets = data["offsets"]
            self.front = data["pole_front"]

    def render(self, material: np.ndarray, base: np.ndarray, direction: int, phase: int) -> Image.Image:
        if not 0 <= direction < 8 or not 0 <= phase < 4:
            raise ValueError("Direction must be 0..7; phase must be 0..3")
        frame = direction * 4 + phase
        cloth = render_cloth(np.r_[material, 1], self.coefficients[:, frame:frame + 1], self.coverage[frame:frame + 1])[0]
        row = GLYPH_ROWS[direction]
        glyph = None if row < 0 else render_glyph(base, self.uv[row * 4 + phase], self.gain[row * 4 + phase])
        return compose(self.pole, cloth, glyph, tuple(self.offsets[direction]), self.front[direction])


def compose(pole: np.ndarray, cloth: np.ndarray, glyph: np.ndarray | None, offset: tuple[int, int], front: bool) -> Image.Image:
    canvas = Image.new("RGBA", SIZE)
    pole_image = Image.fromarray(pole)
    if not front:
        canvas.alpha_composite(pole_image, (31, 8))
    canvas.alpha_composite(Image.fromarray(cloth), offset)
    if glyph is not None:
        canvas.alpha_composite(Image.fromarray(glyph), offset)
    if front:
        canvas.alpha_composite(pole_image, (31, 8))
    return canvas


def metrics(expected: np.ndarray, actual: np.ndarray) -> dict:
    ep, ap = premultiply(expected), premultiply(actual)
    union = (expected[..., 3] > 0) | (actual[..., 3] > 0)
    intersection = (expected[..., 3] > 0) & (actual[..., 3] > 0)
    strong_union = (expected[..., 3] >= 128) | (actual[..., 3] >= 128)
    strong_intersection = (expected[..., 3] >= 128) & (actual[..., 3] >= 128)
    return {
        "visible_union_pixels": int(union.sum()),
        "silhouette_iou": float(intersection.sum() / max(1, union.sum())),
        "coverage_50pct_iou": float(strong_intersection.sum() / max(1, strong_union.sum())),
        "alpha_mae_255": float(np.abs(ep[..., 3] - ap[..., 3])[union].mean() * 255),
        "premultiplied_rgb_mae_255": float(np.abs(ep[..., :3] - ap[..., :3])[union].mean() * 255),
    }


def backdrop(image: Image.Image, scale: int = 1) -> Image.Image:
    bg = Image.new("RGBA", image.size, (65, 79, 66, 255))
    draw = ImageDraw.Draw(bg)
    for y in range(0, image.height, 8):
        for x in range(0, image.width, 8):
            if (x // 8 + y // 8) % 2:
                draw.rectangle((x, y, x + 7, y + 7), fill=(79, 91, 77, 255))
    bg.alpha_composite(image)
    return bg.convert("RGB").resize((image.width * scale, image.height * scale), Image.Resampling.NEAREST)


def make_flat_glyph(text: str, font: Path) -> np.ndarray:
    # One authored 2-D decal in the canonical face, independent of direction/phase.
    canvas = Image.new("RGBA", (32 * 4, 40 * 4))
    f = ImageFont.truetype(str(font), 15 * 4)
    ImageDraw.Draw(canvas).text((18 * 4, 20 * 4), text, font=f, anchor="mm", fill="black", stroke_width=4, stroke_fill="white")
    return np.asarray(canvas.resize((32, 40), Image.Resampling.LANCZOS))


def run(data_dir: Path, output: Path, font: Path) -> dict:
    output.mkdir(parents=True, exist_ok=True)
    exe = (data_dir / "San9PK.exe").read_bytes()
    if hashlib.sha256(exe).hexdigest() != "d20794aeff67301ec2bf8c3becb1e9944c68c6c0588fbfd4bf04e8597f0e5028":
        raise ValueError("Expected the registered SRC-SAN9PK-TC-101 executable")
    bindings = {struct.unpack_from("<I", exe, 0x24ea90 + i * 8)[0] & 65535: struct.unpack_from("<I", exe, 0x24ea94 + i * 8)[0] for i in range(1425)}
    flags = load_archive(data_dir / "G_Flag.s9", [bindings[491 + i] - 285 for i in range(52)], False)
    names = load_archive(data_dir / "G_Name.s9", [i // 186 for i in range(372)], True)
    cloths = np.array([split_frames(sheet, 8) for sheet in flags[1:]])
    glyphs = np.array([split_frames(sheet, 6) for sheet in names]).reshape(2, 186, 24, 40, 32, 4)
    color_test = list(range(0, 50, 5)) + [50]
    color_train = [i for i in range(51) if i not in color_test]
    glyph_test = list(range(0, 186, 4))
    glyph_train = [i for i in range(186) if i not in glyph_test]
    parameters, trim_mask = material_parameters(cloths)
    coefficients, coverage = fit_material(cloths, parameters, color_train)
    print("Fitted shared cloth material", flush=True)
    uv, gain, _ = fit_warp(glyphs, glyph_train)
    print("Fitted shared glyph deformation", flush=True)
    generated_cloths = np.array([render_cloth(p, coefficients, coverage) for p in parameters])
    generated_glyphs = np.array([[render_glyph(g[CANONICAL], uv, gain) for g in ink] for ink in glyphs])
    offsets = [struct.unpack_from("<2i", exe, 0x24a368 + d * 8) for d in range(8)]
    front = [bool(struct.unpack_from("<i", exe, 0x24a3d8 + d * 4)[0]) for d in range(8)]

    def flag(c: int, g: int, ink: int, d: int, phase: int, generated: bool) -> Image.Image:
        cloth = (generated_cloths if generated else cloths)[c, d * 4 + phase]
        row = GLYPH_ROWS[d]
        glyph = None if row < 0 else (generated_glyphs if generated else glyphs)[ink, g, row * 4 + phase]
        return compose(flags[0], cloth, glyph, offsets[d], front[d])

    # Separate cloth and glyph errors; canonical identity is excluded from glyph scores.
    novel_frames = [i for i in range(24) if i != CANONICAL]
    report = {
        "experiment": "NOTE-PROCEDURAL-FLAGS-20261028",
        "environment": {"python": platform.python_version(), "pillow": PIL.__version__, "numpy": np.__version__, "platform": platform.platform()},
        "inputs": {name: {"bytes": (data_dir / name).stat().st_size, "sha256": hashlib.sha256((data_dir / name).read_bytes()).hexdigest()} for name in ("San9PK.exe", "G_Flag.s9", "G_Name.s9")},
        "font": {"name": font.name, "sha256": hashlib.sha256(font.read_bytes()).hexdigest()},
        "split": {"color_training": color_train, "color_holdout": color_test, "glyph_training": glyph_train, "glyph_holdout": glyph_test, "canonical_glyph_frame_excluded": CANONICAL},
        "cloth_all_51": metrics(cloths, generated_cloths),
        "cloth_holdout": metrics(cloths[color_test], generated_cloths[color_test]),
        "cloth_holdout_by_color": {str(i): metrics(cloths[i], generated_cloths[i]) for i in color_test},
        "glyph_holdout": metrics(glyphs[:, glyph_test][:, :, novel_frames], generated_glyphs[:, glyph_test][:, :, novel_frames]),
        "glyph_holdout_by_row": {str(row): metrics(glyphs[:, glyph_test][:, :, [f for f in novel_frames if f // 4 == row]], generated_glyphs[:, glyph_test][:, :, [f for f in novel_frames if f // 4 == row]]) for row in range(6)},
        "counts": {"cloth_direction_phases": 32, "glyph_direction_phases": 24, "cloth_base_parameters_per_color": 6, "glyph_base_rgba_pixels_per_ink": 1280},
    }
    np.savez_compressed(output / "shared-basis.npz", cloth_coefficients=coefficients, cloth_coverage=coverage, glyph_uv=uv, glyph_gain=gain)
    # Persist only flat decals/parameters as per-flag inputs, never animated sheets.
    np.savez_compressed(output / "base-inputs.npz", material_parameters=parameters[:, :6], glyph_bases=glyphs[:, :, CANONICAL], pole=flags[0], offsets=offsets, pole_front=front)
    generator = SharedFlagGenerator(output / "shared-basis.npz", output / "base-inputs.npz")
    independently_verified = 0
    composed_pairs = []
    for c, g in zip(color_test, glyph_test[:len(color_test)]):
        for ink in range(2):
            for d in range(8):
                for phase in range(4):
                    generated = generator.render(generator.materials[c], generator.glyph_bases[ink, g], d, phase)
                    if generated.tobytes() != flag(c, g, ink, d, phase, True).tobytes():
                        raise AssertionError("Serialized basis failed independent reconstruction")
                    composed_pairs.append((np.asarray(flag(c, g, ink, d, phase, False)), np.asarray(generated)))
                    independently_verified += 1
    report["independent_basis_roundtrip_frames"] = independently_verified
    report["composed_holdout_pairs"] = metrics(np.array([p[0] for p in composed_pairs]), np.array([p[1] for p in composed_pairs]))
    Image.fromarray((coverage[CANONICAL] * 255).astype(np.uint8)).save(output / "shared-cloth-mask.png")
    Image.fromarray((trim_mask * 255).astype(np.uint8)).save(output / "trim-sampling-mask.png")
    colors = [0, 5, 10, 20, 35, 50]
    selected_glyphs = [0, 16, 40, 80, 120, 184]
    sheet = Image.new("RGB", (1250, 90 + len(colors) * 177), (27, 33, 39))
    draw = ImageDraw.Draw(sheet)
    draw.text((14, 12), "HOLDOUT FLAGS | original / generated / difference amplified x3 | zoom x2 + native scale", fill="white")
    for row, (c, g) in enumerate(zip(colors, selected_glyphs)):
        y = 58 + row * 177
        draw.text((14, y), f"color {c}, glyph {g} (both held out); directions 1 / 2 / 5", fill="white")
        for col, direction in enumerate([1, 2, 5]):
            phase = (row + col) % 4
            original, generated = flag(c, g, 0, direction, phase, False), flag(c, g, 0, direction, phase, True)
            # Difference on the same background includes coverage and pole occlusion.
            diff = np.abs(np.asarray(backdrop(original)).astype(int) - np.asarray(backdrop(generated)).astype(int))
            diff_image = Image.fromarray(np.clip(diff * 3, 0, 255).astype(np.uint8))
            for k, image in enumerate([backdrop(original), backdrop(generated), diff_image]):
                sheet.paste(image.resize((136, 104), Image.Resampling.NEAREST), (14 + col * 408 + k * 136, y + 22))
            # Native scale beside the enlargement, to judge actual display readability.
            sheet.paste(backdrop(original), (14 + col * 408, y + 128))
            sheet.paste(backdrop(generated), (90 + col * 408, y + 128))
    sheet.save(output / "comparison.png")
    # Complete contact sheet covers all 8 directions and 4 phases for a holdout pair.
    sheet = Image.new("RGB", (32 * 136, 2 * 130), (27, 33, 39))
    draw = ImageDraw.Draw(sheet)
    for d in range(8):
        for p in range(4):
            x = (d * 4 + p) * 136
            for row, generated in enumerate([False, True]):
                draw.text((x, row * 130), f"d{d} p{p} {'gen' if generated else 'src'}", fill="white")
                sheet.paste(backdrop(flag(0, 80, 0, d, p, generated), 2), (x, row * 130 + 20))
    sheet.save(output / "all-directions.png")
    new_cases = [("雲", (0.10, 0.65, 0.77), (0.93, 0.72, 0.28)), ("龍", (0.65, 0.20, 0.80), (0.95, 0.80, 0.32)), ("新", (0.90, 0.40, 0.25), (0.80, 0.85, 0.90))]
    new_bases = [make_flat_glyph(text, font) for text, _, _ in new_cases]
    for index, base in enumerate(new_bases):
        Image.fromarray(base).save(output / f"new-glyph-{index}-base.png")

    def panel(phase: int) -> Image.Image:
        canvas = Image.new("RGB", (1136, 800), (27, 33, 39))
        draw = ImageDraw.Draw(canvas)
        draw.text((16, 14), "SHARED FLAG MOTION | top: original / generated | bottom: new color + font decal", fill="white")
        draw.text((16, 36), "4 source phases, 142 ms/phase; new flags have one flat decal and six color values", fill="white")
        for column, d in enumerate([0, 1, 2, 4, 5, 6]):
            x = 16 + column * 184
            draw.text((x, 68), f"direction {d}", fill="white")
            for row, generated in enumerate([False, True]):
                canvas.paste(backdrop(flag(0, 80, 0, d, phase, generated), 2), (x, 90 + row * 116))
            for row in range(3):
                _, face, border = new_cases[row]
                image = generator.render(np.r_[face, border], new_bases[row], d, phase)
                canvas.paste(backdrop(image, 2), (x, 360 + row * 132))
        return canvas

    animation = [panel(p) for p in range(4)]
    animation[0].save(output / "animation.gif", save_all=True, append_images=animation[1:], duration=140, loop=0, disposal=2)
    animation[0].save(output / "animation.png", save_all=True, append_images=animation[1:], duration=142, loop=0, disposal=1, blend=0)
    animation[0].save(output / "extensions.png")
    report["artifacts"] = {name: {"bytes": (output / name).stat().st_size, "sha256": hashlib.sha256((output / name).read_bytes()).hexdigest()} for name in ("shared-basis.npz", "base-inputs.npz", "comparison.png", "animation.gif", "animation.png", "extensions.png")}
    (output / "metrics.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: report[key] for key in ("cloth_holdout", "glyph_holdout")}, indent=2), flush=True)
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--data-dir", type=Path, default=ROOT / "private/game-files")
    parser.add_argument("--output", type=Path, default=ROOT / "local/generated/research/flag-generation")
    parser.add_argument("--font", type=Path, default=Path("C:/Windows/Fonts/kaiu.ttf"))
    args = parser.parse_args()
    if not args.output.resolve().is_relative_to((ROOT / "local/generated").resolve()):
        parser.error("Experiment outputs must stay under local/generated")
    run(args.data_dir, args.output, args.font)
