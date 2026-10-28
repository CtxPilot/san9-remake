"""Reconstruct source-style contours, trim and continuous shade as shared layers."""
from __future__ import annotations

import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


def _simplify(points: np.ndarray, tolerance: float) -> np.ndarray:
    if len(points) <= 2:
        return points
    delta = points[-1] - points[0]
    squared = float(delta @ delta)
    if squared:
        t = np.clip((points - points[0]) @ delta / squared, 0, 1)
        distances = np.linalg.norm(points - (points[0] + t[:, None] * delta), axis=1)
    else:
        distances = np.linalg.norm(points - points[0], axis=1)
    index = int(np.argmax(distances))
    if distances[index] <= tolerance:
        return points[[0, -1]]
    return np.concatenate([_simplify(points[:index + 1], tolerance)[:-1], _simplify(points[index:], tolerance)])


def trace_contours(mask: np.ndarray, tolerance: float = 0.72) -> list[np.ndarray]:
    """Trace all four-connected components and holes, with subpixel simplification."""
    height, width = mask.shape
    edges = set()
    for y, x in np.argwhere(mask):
        if y == 0 or not mask[y - 1, x]:
            edges.add(((x, y), (x + 1, y)))
        if x == width - 1 or not mask[y, x + 1]:
            edges.add(((x + 1, y), (x + 1, y + 1)))
        if y == height - 1 or not mask[y + 1, x]:
            edges.add(((x + 1, y + 1), (x, y + 1)))
        if x == 0 or not mask[y, x - 1]:
            edges.add(((x, y + 1), (x, y)))
    outgoing = {}
    for start, end in edges:
        outgoing.setdefault(start, []).append(end)
    loops = []
    while edges:
        start, end = min(edges)
        points = [start]
        previous, current = start, end
        edges.remove((start, end))
        while current != start:
            points.append(current)
            candidates = [p for p in outgoing[current] if (current, p) in edges]
            if not candidates:
                raise ValueError("Open contour")
            incoming = np.array(current) - previous

            def turn(point):
                direction = np.array(point) - current
                return math.atan2(float(incoming[0] * direction[1] - incoming[1] * direction[0]), float(incoming @ direction))

            following = max(candidates, key=turn)
            edges.remove((current, following))
            previous, current = current, following
        points = np.asarray(points, dtype=float)
        far = int(np.argmax(np.sum((points - points[0]) ** 2, axis=1)))
        if len(points) < 4:
            continue
        first = _simplify(points[:far + 1], tolerance)
        second = _simplify(np.vstack([points[far:], points[0]]), tolerance)
        loops.append(np.vstack([first[:-1], second[:-1]]))
    return loops


def curved_path(points: np.ndarray, radius: float = 0.35) -> tuple[str, np.ndarray]:
    """Round small stair corners while preserving pointed pennon ends."""
    starts, ends = [], []
    for i, point in enumerate(points):
        before, after = points[i - 1] - point, points[(i + 1) % len(points)] - point
        lb, la = np.linalg.norm(before), np.linalg.norm(after)
        cosine = before @ after / max(lb * la, 1e-9)
        cut = 0.0 if cosine > 0.65 else min(radius, lb * 0.22, la * 0.22)
        starts.append(point + before * cut / max(lb, 1e-9))
        ends.append(point + after * cut / max(la, 1e-9))
    commands, samples = [], []
    for i, point in enumerate(points):
        a, b = starts[i], ends[i]
        commands.append(("M" if i == 0 else "L") + f" {a[0]:.4f},{a[1]:.4f}")
        commands.append(f"Q {point[0]:.4f},{point[1]:.4f} {b[0]:.4f},{b[1]:.4f}")
        for t in np.linspace(0, 1, 9):
            samples.append((1 - t) ** 2 * a + 2 * t * (1 - t) * point + t * t * b)
    return " ".join(commands) + " Z", np.asarray(samples)


def contour_layer(loops: list[np.ndarray], shape: tuple[int, int], factor: int) -> tuple[np.ndarray, list[str], np.ndarray]:
    height, width = shape
    # XOR preserves holes and disjoint ribbons irrespective of loop winding.
    coverage = np.zeros((height * factor, width * factor), dtype=bool)
    edge_image = Image.new("L", (width * factor, height * factor))
    edge_draw = ImageDraw.Draw(edge_image)
    paths = []
    for loop in loops:
        path, samples = curved_path(loop)
        paths.append(path)
        image = Image.new("1", (width * factor, height * factor))
        polygon = [(round(x * factor), round(y * factor)) for x, y in samples]
        ImageDraw.Draw(image).polygon(polygon, fill=1)
        coverage ^= np.asarray(image)
        edge_draw.line(polygon + polygon[:1], fill=255, width=max(1, round(factor * 0.25)), joint="curve")
    return coverage.astype(float), paths, np.asarray(edge_image).astype(float) / 255


def reconstruct_trim(mask: np.ndarray, silhouette: np.ndarray, factor: int) -> tuple[np.ndarray, str]:
    """Recover the body border separately from the narrow pennons."""
    from PIL import ImageFilter

    height, width = mask.shape
    seam_column = int(np.argmax(mask.sum(axis=0)))
    center = np.argwhere(silhouette)[:, 1].mean()
    free_edge_left = seam_column < center
    columns = np.arange(width)[None]
    body_trim = mask & ((columns >= seam_column - 1) if free_edge_left else (columns <= seam_column + 1))
    opened = np.asarray(Image.fromarray(silhouette.astype('uint8') * 255).filter(ImageFilter.MinFilter(5)).filter(ImageFilter.MaxFilter(5))) > 0
    top, bottom = np.flatnonzero(body_trim.any(axis=1))[[0, -1]]
    left, right = [], []
    for y in range(top, bottom + 1):
        visible = np.flatnonzero(silhouette[y])
        gold = np.flatnonzero(body_trim[y])
        core = np.flatnonzero(opened[y])
        if not len(visible):
            continue
        near = gold[np.abs(gold - seam_column) <= 3]
        if free_edge_left:
            edge = near[0] if len(near) else core[0] - 1 if len(core) else gold[0] if len(gold) else visible[0]
            left.append((edge + 0.5, y + 0.5))
            right.append((visible[-1] + 0.5, y + 0.5))
        else:
            edge = near[-1] if len(near) else core[-1] + 1 if len(core) else gold[-1] if len(gold) else visible[-1]
            left.append((visible[0] + 0.5, y + 0.5))
            right.append((edge + 0.5, y + 0.5))
    # A sparse one-dimensional fit removes isolated pixel kinks at ribbon roots.
    for side in (left, right):
        points = np.asarray(side)
        count = min(8, len(points))
        if count < 4:
            continue
        design = spline_basis(points[:, 1] - top, count, bottom - top + 1)
        weights = np.ones(len(points))
        weights[[0, -1]] = 6
        curvature = np.diff(np.eye(count), n=2, axis=0)
        controls = np.linalg.solve(design.T @ (design * weights[:, None]) + 0.05 * curvature.T @ curvature, design.T @ (points[:, 0] * weights))
        prediction = design @ controls
        side[:] = [(x, p[1]) for x, p in zip(prediction, points)]
    polygon = np.array(left + right[::-1])
    polygon = polygon[np.r_[True, np.any(np.diff(polygon, axis=0), axis=1)]]
    if np.array_equal(polygon[0], polygon[-1]):
        polygon = polygon[:-1]
    far = int(np.argmax(np.sum((polygon - polygon[0]) ** 2, axis=1)))
    polygon = np.vstack([_simplify(polygon[:far + 1], 0.72)[:-1], _simplify(np.vstack([polygon[far:], polygon[0]]), 0.72)[:-1]])
    path, samples = curved_path(polygon, radius=0.5)
    image = Image.new("L", (width * factor, height * factor))
    pixels = [(round(p[0] * factor), round(p[1] * factor)) for p in samples]
    ImageDraw.Draw(image).line(pixels + pixels[:1], fill=255, width=round(factor * 1.05), joint="curve")
    return np.asarray(image).astype(float) / 255, path


def spline_basis(coordinates: np.ndarray, count: int, extent: float) -> np.ndarray:
    degree = 3
    knots = np.r_[np.zeros(degree + 1), np.linspace(0, extent, count - degree + 1)[1:-1], np.full(degree + 1, extent)]
    x = np.minimum(coordinates, np.nextafter(extent, 0))
    basis = ((x[:, None] >= knots[:-1]) & (x[:, None] < knots[1:])).astype(float)
    for order in range(1, degree + 1):
        result = np.zeros((len(x), len(knots) - order - 1))
        for i in range(result.shape[1]):
            left, right = knots[i + order] - knots[i], knots[i + order + 1] - knots[i + 1]
            if left:
                result[:, i] += (x - knots[i]) / left * basis[:, i]
            if right:
                result[:, i] += (knots[i + order + 1] - x) / right * basis[:, i + 1]
        basis = result
    return basis


def fit_shade(values: np.ndarray, mask: np.ndarray, columns: int = 18, rows: int = 20) -> np.ndarray:
    height, width = mask.shape
    bx = spline_basis(np.arange(width) + 0.5, columns, width)
    by = spline_basis(np.arange(height) + 0.5, rows, height)
    design = np.einsum("yi,xj->yxij", by, bx).reshape(height, width, -1)[mask]
    # Curvature regularization fits a continuous low-frequency field, not texel noise.
    horizontal = np.kron(np.eye(rows), np.diff(np.eye(columns), n=2, axis=0))
    vertical = np.kron(np.diff(np.eye(rows), n=2, axis=0), np.eye(columns))
    gram = design.T @ design + 0.003 * (horizontal.T @ horizontal + vertical.T @ vertical) + np.eye(rows * columns) * 1e-5
    return np.linalg.solve(gram, design.T @ values[mask]).reshape(rows, columns)


def evaluate_shade(coefficients: np.ndarray, shape: tuple[int, int], factor: int) -> np.ndarray:
    height, width = shape
    by = spline_basis((np.arange(height * factor) + 0.5) / factor, coefficients.shape[0], height)
    bx = spline_basis((np.arange(width * factor) + 0.5) / factor, coefficients.shape[1], width)
    return by @ coefficients @ bx.T


class RebuiltMaterial:
    def __init__(self, source: np.ndarray, source_face: np.ndarray, source_border: np.ndarray, factor: int = 32):
        self.factor = factor
        self.shape = source.shape[:2]
        visible = source[..., 3] > 0
        r, g, b = source[..., :3].astype(float).transpose(2, 0, 1)
        trim = visible & (r > b * 1.4) & (g > b * 1.1) & (r > 20) & (g > r * 0.35)
        self.silhouette, self.outline_paths, self.edge = contour_layer(trace_contours(visible), self.shape, factor)
        self.trim, self.trim_path = reconstruct_trim(trim, visible, factor)
        self.trim *= self.silhouette
        face_values = b / max(255 * source_face[2], 1)
        self.face_coefficients = fit_shade(face_values, visible & ~trim)
        gold_luminance = r * 0.30 + g * 0.59 + b * 0.11
        base_luminance = float(source_border @ np.array([0.30, 0.59, 0.11]) * 255)
        self.gold_coefficients = fit_shade(gold_luminance / max(base_luminance, 1), trim, 8, 10)
        self.shade = np.clip(evaluate_shade(self.face_coefficients, self.shape, factor), 0.06, 1.45)
        self.gold_shade = np.clip(evaluate_shade(self.gold_coefficients, self.shape, factor), 0.25, 1.55)

    def render(self, face: np.ndarray, border: np.ndarray) -> Image.Image:
        body = face * self.shade[..., None]
        gold = border * self.gold_shade[..., None]
        colors = body * (1 - self.trim[..., None]) + gold * self.trim[..., None]
        colors *= 1 - self.edge[..., None] * 0.38
        rgba = np.rint(np.clip(np.dstack([colors, self.silhouette]), 0, 1) * 255).astype(np.uint8)
        # A final 2x downsample anti-aliases vector edges without blurring the layers.
        return Image.fromarray(rgba).resize((self.shape[1] * self.factor // 2, self.shape[0] * self.factor // 2), Image.Resampling.LANCZOS)

    def save_layers(self, directory: Path, profile: str) -> dict:
        directory.mkdir(parents=True, exist_ok=True)
        for name, pixels in [("silhouette", self.silhouette), ("trim", self.trim), ("shade", self.shade / 1.45), ("gold-shade", self.gold_shade / 1.55)]:
            Image.fromarray(np.rint(np.clip(pixels, 0, 1) * 255).astype(np.uint8)).save(directory / f"{profile}-{name}.png")
        paths = " ".join(self.outline_paths)
        svg = f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {self.shape[1]} {self.shape[0]}">\n<path fill="#18348a" fill-rule="evenodd" d="{paths}"/>\n<path fill="none" stroke="#c79d21" stroke-width="1.05" stroke-linejoin="round" d="{self.trim_path}"/>\n</svg>\n'
        (directory / f"{profile}-contours.svg").write_text(svg, encoding="utf-8")
        model = {"source_shape": list(self.shape), "silhouette_paths": self.outline_paths, "trim_path": self.trim_path, "trim_width": 1.05, "face_spline": self.face_coefficients.tolist(), "gold_spline": self.gold_coefficients.tolist()}
        (directory / f"{profile}-model.json").write_text(json.dumps(model, indent=2) + "\n", encoding="utf-8")
        return {"silhouette_contours": len(self.outline_paths), "trim_contours": 1, "face_controls": int(self.face_coefficients.size), "trim_controls": int(self.gold_coefficients.size)}
