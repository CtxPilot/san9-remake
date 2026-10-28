"""Check the reconstruction boundaries that affect flag identity and recoloring."""
import unittest

import numpy as np

from rebuild_flag_material import contour_layer, evaluate_shade, fit_shade, reconstruct_trim, trace_contours


class RebuiltMaterialTests(unittest.TestCase):
    def test_holes_and_separate_pennons_survive_vectorization(self):
        mask = np.zeros((14, 14), dtype=bool)
        mask[2:12, 5:12] = True
        mask[2:4, 1:6] = True
        mask[9:11, 1:6] = True
        mask[6:8, 8:10] = False
        coverage, paths, _ = contour_layer(trace_contours(mask), mask.shape, 16)
        self.assertEqual(coverage[3 * 16, 2 * 16], 1)
        self.assertEqual(coverage[10 * 16, 2 * 16], 1)
        self.assertEqual(coverage[6 * 16, 2 * 16], 0)
        self.assertEqual(coverage[7 * 16, 9 * 16], 0)
        self.assertEqual(len(paths), 2)

    def test_spline_preserves_a_smooth_illumination_plane(self):
        yy, xx = np.mgrid[:20, :24]
        reference = 0.4 + (xx + 0.5) * 0.013 + (yy + 0.5) * 0.007
        mask = np.ones_like(reference, dtype=bool)
        controls = fit_shade(reference, mask, 8, 8)
        rendered = evaluate_shade(controls, reference.shape, 3)
        expected = 0.4 + (np.arange(72)[None] + 0.5) / 3 * 0.013 + (np.arange(60)[:, None] + 0.5) / 3 * 0.007
        self.assertLess(float(np.abs(rendered - expected).max()), 0.003)

    def test_missing_trim_samples_do_not_draw_a_band_across_the_face(self):
        silhouette = np.zeros((40, 32), dtype=bool)
        silhouette[8:32, 8:29] = True
        trim = np.zeros_like(silhouette)
        trim[8:32, 8] = True
        trim[8, 8:29] = True
        trim[31, 8:29] = True
        trim[8:17, 28] = True
        trim[24:32, 28] = True
        pixels, path = reconstruct_trim(trim, silhouette, 16)
        self.assertTrue(path.endswith(" Z"))
        self.assertEqual(pixels[20 * 16, 18 * 16], 0)
        self.assertGreater(pixels[20 * 16, 8 * 16:10 * 16].sum(), 0)
        self.assertGreater(pixels[20 * 16, 27 * 16:30 * 16].sum(), 0)


if __name__ == "__main__":
    unittest.main()
