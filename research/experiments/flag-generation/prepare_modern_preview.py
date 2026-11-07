"""Prepare an isolated Godot flag prototype; generated resources stay local."""
from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import struct
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from run_experiment import ROOT, GLYPH_ROWS, compose, load_archive, material_parameters, split_frames
from calibrate_alignment import calibrate_motion


def flat_emblem(text: str, font_path: Path, size: int = 512, white_ink: bool = False) -> Image.Image:
    """Author a flat, high-resolution decal; no animated or low-res source frame."""
    # Font metrics can contain large side bearings that do not match visible ink.
    # Render with ample guard space, then measure actual coverage before centering.
    image = Image.new("RGBA", (size * 4, size * 4))
    font = ImageFont.truetype(str(font_path), round(size * 0.76))
    draw = ImageDraw.Draw(image)
    x, y = size * 1.5, size * 1.5
    outline, ink = ((8, 8, 8, 255), (248, 248, 248, 255)) if white_ink else ((248, 248, 248, 255), (8, 8, 8, 255))
    draw.text((x, y), text, font=font, fill=outline, stroke_width=round(size * 0.05), stroke_fill=outline)
    draw.text((x, y), text, font=font, fill=ink, stroke_width=round(size * 0.012), stroke_fill=ink)
    bounds = image.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("The text produced no visible glyph")
    glyph = image.crop(bounds)
    available = size - round(size * 0.16)
    ratio = min(1.0, available / glyph.width, available / glyph.height)
    if ratio < 1:
        glyph = glyph.resize((round(glyph.width * ratio), round(glyph.height * ratio)), Image.Resampling.LANCZOS)
    result = Image.new("RGBA", (size, size))
    result.alpha_composite(glyph, ((size - glyph.width) // 2, (size - glyph.height) // 2))
    return result


def upscale_style(rgba: np.ndarray, factor: int = 16) -> Image.Image:
    """Keep the source silhouette/crease colors, reconstruct a smooth coverage edge.

    This is a contour reconstruction, not newly invented high-frequency fabric art.
    Fill invisible RGB before filtering so the original green key cannot bleed in.
    """
    rgb = rgba[..., :3].copy()
    visible = rgba[..., 3] > 0
    known = np.argwhere(visible)
    empty = np.argwhere(~visible)
    if len(known) and len(empty):
        nearest = ((empty[:, None] - known[None]) ** 2).sum(-1).argmin(-1)
        rgb[empty[:, 0], empty[:, 1]] = rgb[known[nearest, 0], known[nearest, 1]]
    size = (rgba.shape[1] * factor, rgba.shape[0] * factor)
    colors = np.array(Image.fromarray(rgb).resize(size, Image.Resampling.BICUBIC))
    coverage = np.array(Image.fromarray(rgba[..., 3]).resize(size, Image.Resampling.BICUBIC)) / 255
    alpha = np.rint(np.clip((coverage - 0.5) * factor + 0.5, 0, 1) * 255).astype(np.uint8)
    return Image.fromarray(np.dstack([colors, alpha]))


def direction_profiles(cloth: np.ndarray, exe: bytes, plane_points: np.ndarray) -> list[dict]:
    """Shared projected views; source bounds calibrate their displayed proportions."""
    # These are visual projection angles for source direction IDs, not world bearings.
    angles = [-25, 0, 58, 86, 155, 180, 238, 266]
    profiles = []
    dx, dy = plane_points[:, 0] - 30, plane_points[:, 1] - 20.5
    for direction, degrees in enumerate(angles):
        theta = float(np.deg2rad(degrees))
        y, x = np.where(cloth[direction * 4, ..., 3] > 0)
        projected_x = np.cos(theta) * dx
        projected_y = dy + np.sin(theta) * 0.45 * dx
        scale_x = float((x.max() - x.min() + 1) / np.ptp(projected_x))
        scale_y = float((y.max() - y.min() + 1) / np.ptp(projected_y))
        offset = struct.unpack_from("<2i", exe, 0x24a368 + direction * 8)
        profiles.append({
            "id": direction,
            "yaw_radians": theta,
            "projection_scale": [scale_x, scale_y],
            "attachment": [float(np.clip(x.mean() + offset[0] - projected_x.mean() * scale_x, 31, 35)), float(y.mean() + offset[1] - projected_y.mean() * scale_y)],
            "pole_front": bool(struct.unpack_from("<i", exe, 0x24a3d8 + direction * 4)[0]),
            "show_emblem": GLYPH_ROWS[direction] >= 0,
        })
    return profiles


def prepare(output: Path, data_dir: Path, font: Path, generated_master: Path, emblem_font: Path) -> dict:
    if not generated_master.is_file():
        raise FileNotFoundError(f"Provide the selected generated master with --generated-master: {generated_master}")
    generated_root = (ROOT / "local/generated").resolve()
    if not output.resolve().is_relative_to(generated_root):
        raise ValueError("Preview output must stay under local/generated")
    assets = output / "assets"
    assets.mkdir(parents=True, exist_ok=True)
    # Remove only owned, obsolete preview derivatives; historical captures stay intact.
    obsolete = [f"original-{phase}.png" for phase in range(4)]
    obsolete += [f"original-{direction}-{phase}.png" for direction in range(8) for phase in range(4)]
    obsolete += [f"{name}-{profile}{variant}.png" for name in ("commander", "cloud", "dragon", "new") for profile in ("left", "right") for variant in ("", "-rebuilt")]
    obsolete += ["cloud.png", "dragon.png", "new.png"]
    obsolete += [f"original-{style}-{direction}-{phase}.png" for style in ("gold", "dark") for direction in range(8) for phase in range(4)]
    for name in obsolete:
        for suffix in ("", ".import"):
            path = (assets / (name + suffix)).resolve()
            if not path.is_relative_to(assets.resolve()):
                raise ValueError("Invalid obsolete asset path")
            path.unlink(missing_ok=True)
    layer_root = output / "layers"
    for profile in ("left", "right"):
        for name in ("silhouette.png", "trim.png", "shade.png", "gold-shade.png", "contours.svg", "model.json"):
            for suffix in ("", ".import"):
                path = (layer_root / f"{profile}-{name}{suffix}").resolve()
                if not path.is_relative_to(output.resolve()):
                    raise ValueError("Invalid obsolete layer path")
                path.unlink(missing_ok=True)
    for source in (Path(__file__).parent / "godot").iterdir():
        if source.is_file():
            target = output / source.name
            if not target.exists() or target.read_bytes() != source.read_bytes():
                shutil.copyfile(source, target)
    shutil.copyfile(font, assets / "preview-font.ttf")
    flat_emblem("帥", emblem_font).save(assets / "commander.png")
    flat_emblem("帥", emblem_font, white_ink=True).save(assets / "commander-white.png")
    shutil.copyfile(generated_master, assets / "cloth-generated-v1.png")
    with Image.open(generated_master) as image:
        pixels = np.asarray(image.convert("RGBA"))
        yy, xx = np.where(pixels[..., 3] >= 128)
        if not len(xx):
            raise ValueError("The generated master has no opaque cloth")
        width, height = image.size
        left, top = max(0, int(xx.min()) - 4), max(0, int(yy.min()) - 4)
        right, bottom = min(width, int(xx.max()) + 5), min(height, int(yy.max()) + 5)
    plane_points = np.column_stack([3 + (xx + 0.5 - left) / (right - left) * 28, 8 + (yy + 0.5 - top) / (bottom - top) * 23])
    exe = (data_dir / "San9PK.exe").read_bytes()
    if hashlib.sha256(exe).hexdigest() != "d20794aeff67301ec2bf8c3becb1e9944c68c6c0588fbfd4bf04e8597f0e5028":
        raise ValueError("Expected SRC-SAN9PK-TC-101")
    bindings = {struct.unpack_from("<I", exe, 0x24ea90 + i * 8)[0] & 65535: struct.unpack_from("<I", exe, 0x24ea94 + i * 8)[0] for i in range(1425)}
    flags = load_archive(data_dir / "G_Flag.s9", [bindings[491 + i] - 285 for i in range(52)], False)
    names = load_archive(data_dir / "G_Name.s9", [i // 186 for i in range(372)], True)
    cloths = np.array([split_frames(sheet, 8) for sheet in flags[1:]])
    parameters, _ = material_parameters(cloths)
    glyphs = [split_frames(names[ink * 186], 6) for ink in range(2)]
    profiles = direction_profiles(cloths[0], exe, plane_points)
    # Original contrast rule, also documented by Rendering.FlagVisuals.GlyphInk.
    white_ink_colors = {3, 6, 7, 8, 11, 16, 18, 21, 22, 23, 24, 26, 27, 29, 31, 33, 38, 39, 40, 46, 49, 50}
    palette = []
    for source_color in range(len(cloths)):
        glyph_ink = int(source_color in white_ink_colors)
        has_emblem = source_color != 50
        atlas = Image.new("RGBA", (4 * 68, 8 * 52))
        for profile in profiles:
            direction = profile["id"]
            offset = struct.unpack_from("<2i", exe, 0x24a368 + direction * 8)
            for phase in range(4):
                row = GLYPH_ROWS[direction]
                letter = None if row < 0 or not has_emblem else glyphs[glyph_ink][row * 4 + phase]
                frame = compose(flags[0], cloths[source_color, direction * 4 + phase], letter, offset, profile["pole_front"])
                atlas.paste(frame, (phase * 68, direction * 52))
        name = f"original-color-{source_color:02d}.png"
        atlas.save(assets / name)
        label = f"旗色 {source_color:02d}" + (" · 中性无字" if not has_emblem else " · 白字" if glyph_ink else " · 黑字")
        palette.append({"id": source_color, "label": label, "glyph_ink": glyph_ink, "has_emblem": has_emblem,
                        "material_parameters": parameters[source_color, :6].tolist(), "original_atlas": name})
    upscale_style(flags[0]).save(assets / "pole.png")
    generated_settings = {
        "sha256": hashlib.sha256(generated_master.read_bytes()).hexdigest(),
        "image_size": [width, height],
        "art_region": [left / width, top / height, (right - left) / width, (bottom - top) / height],
        "palette": palette,
        "directions": profiles,
        "original_frame_seconds": 0.142,
        "motion": calibrate_motion(cloths[0], names, profiles),
    }
    (assets / "generated-material.json").write_text(json.dumps(generated_settings, indent=2) + "\n", encoding="utf-8")
    manifest = {
        "experiment": "original-final-eight-direction-flags",
        "font": {"name": font.name, "sha256": hashlib.sha256(font.read_bytes()).hexdigest()},
        "emblem_font": {"name": emblem_font.name, "sha256": hashlib.sha256(emblem_font.read_bytes()).hexdigest()},
        "decal_size": 512,
        "generated_master": generated_settings,
        "source_style_reference": "SRC-SAN9PK-TC-101; all 51 flag colors, glyph 0 with both ink palettes, all eight directions",
        "files": {str(p.relative_to(output)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(output.rglob('*')) if p.is_file() and '.godot' not in p.parts and p.name != 'manifest.json' and not p.name.endswith('.import')},
    }
    (output / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return manifest


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "local/generated/research/flag-generation/modern-godot")
    parser.add_argument("--data-dir", type=Path, default=ROOT / "private/game-files")
    parser.add_argument("--font", type=Path, default=Path("C:/Windows/Fonts/kaiu.ttf"))
    parser.add_argument("--emblem-font", type=Path, default=Path("C:/Windows/Fonts/simkai.ttf"))
    parser.add_argument("--generated-master", type=Path, default=ROOT / "local/generated/research/flag-generation/imagegen/cloth-neutral-v1.png")
    args = parser.parse_args()
    if not args.output.resolve().is_relative_to((ROOT / "local/generated").resolve()):
        parser.error("Experiment output must stay under local/generated")
    prepare(args.output, args.data_dir, args.font, args.generated_master, args.emblem_font)
    print(args.output)
