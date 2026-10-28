"""Fit one continuous cloth displacement field from opposite source views.

The compact field belongs to the shared model, never to a color or a letter.
Source contours constrain projected height; the population of letters constrains
horizontal expansion inside the cloth independently of the flapping free edge.
"""
from __future__ import annotations

import numpy as np

from run_experiment import GLYPH_ROWS, split_frames


def spatial(u: float) -> np.ndarray:
    return np.array([u, u * u, u * u * u])


def harmonics(phase: int) -> np.ndarray:
    angle = phase * np.pi / 2
    return np.array([np.cos(angle), np.sin(angle), np.cos(2 * angle)])


def solve(design: list, observations: list, ridge: float = 0.08) -> tuple[np.ndarray, float]:
    matrix, target = np.asarray(design), np.asarray(observations)
    values = np.linalg.solve(matrix.T @ matrix + ridge * np.eye(matrix.shape[1]), matrix.T @ target)
    return values, float(np.sqrt(np.mean((matrix @ values - target) ** 2)))


def calibrate_motion(cloth: np.ndarray, names: list[np.ndarray], profiles: list[dict]) -> dict:
    rows, values = [], []
    for direction in (1, 5):
        scale_y = profiles[direction]["projection_scale"][1]
        cosine = float(np.cos(profiles[direction]["yaw_radians"]))
        for u in np.linspace(0.10, 0.80, 20):
            column = round(30 - 26 * u) if direction == 1 else round(28 * u)
            edges = []
            for phase in range(4):
                coverage = np.flatnonzero(cloth[direction * 4 + phase, :, column, 3] >= 128)
                edges.append([coverage.min(), coverage.max()])
            displacements = np.asarray(edges) - np.mean(edges, axis=0)
            for phase in range(4):
                basis = np.outer(harmonics(phase), spatial(float(u))).ravel()
                for edge, v in enumerate((-1, 1)):
                    rows.append(np.concatenate([scale_y * basis, scale_y * v * basis, -0.45 * cosine * basis, -0.45 * cosine * v * basis]))
                    values.append(displacements[phase, edge])
    vertical, vertical_rmse = solve(rows, values)
    coefficients = {}
    for index, field in enumerate(("lift", "stretch", "depth", "twist")):
        for harmonic, temporal in enumerate(("cos", "sin", "cos2")):
            coefficients[f"{field}_{temporal}"] = vertical[index * 9 + harmonic * 3:index * 9 + harmonic * 3 + 3].tolist()

    rows, values = [], []
    # Median temporal changes across all source letters keep this independent of 帥.
    sheets = [split_frames(sheet, 6) for sheet in names[:186]]
    for direction in (1, 5):
        cosine = float(np.cos(profiles[direction]["yaw_radians"]))
        scale_x = profiles[direction]["projection_scale"][0]
        boxes = []
        for sheet in sheets:
            edges = []
            for phase in range(4):
                _, xx = np.where(sheet[GLYPH_ROWS[direction] * 4 + phase, ..., 3] >= 128)
                if not len(xx):
                    break
                edges.append([xx.min(), xx.max()])
            if len(edges) == 4:
                boxes.append(edges)
        boxes = np.asarray(boxes)
        means = boxes.mean(axis=1)
        changes = np.median(boxes - means[:, None, :], axis=0)
        positions = np.median(means, axis=0)
        for edge in range(2):
            u = (30 - positions[edge]) / 26 if direction == 1 else positions[edge] / 28
            for phase in range(4):
                rows.append(cosine * scale_x * np.outer(harmonics(phase), spatial(float(u))).ravel())
                values.append(changes[phase, edge])
        free_edges = []
        for phase in range(4):
            _, xx = np.where(cloth[direction * 4 + phase, ..., 3] >= 128)
            free_edges.append(xx.min() if direction == 1 else xx.max())
        free_edges = np.asarray(free_edges) - np.mean(free_edges)
        for phase in range(4):
            rows.append(cosine * scale_x * np.outer(harmonics(phase), spatial(1.0)).ravel())
            values.append(free_edges[phase])
    horizontal, horizontal_rmse = solve(rows, values)
    for harmonic, temporal in enumerate(("cos", "sin", "cos2")):
        coefficients[f"length_{temporal}"] = horizontal[harmonic * 3:harmonic * 3 + 3].tolist()
    return {
        "period_seconds": 0.568,
        "model": "shared-source-calibrated-displacement",
        "coefficients": coefficients,
        "light_response": 0.06,
        "calibration": {"views": [1, 5], "phases": 4, "glyph_population": len(sheets),
                        "vertical_contour_rmse": vertical_rmse, "horizontal_landmark_rmse": horizontal_rmse,
                        "units": "source pixels; training residual, not perceptual similarity"},
    }
