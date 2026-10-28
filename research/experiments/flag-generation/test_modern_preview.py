"""Coverage reconstruction must not reintroduce a transparency key as green fringes."""
import unittest

import numpy as np

from prepare_modern_preview import upscale_style


class ModernPreviewTests(unittest.TestCase):
    def test_key_colors_do_not_contaminate_visible_high_resolution_edges(self):
        pixels = np.full((8, 8, 4), [0, 255, 0, 0], dtype=np.uint8)
        pixels[2:6, 2:6] = [40, 70, 120, 255]
        actual = np.asarray(upscale_style(pixels, 8))
        visible = actual[..., 3] > 0
        np.testing.assert_array_equal(actual[..., :3][visible], np.tile([40, 70, 120], (visible.sum(), 1)))
        self.assertTrue(np.any((actual[..., 3] > 0) & (actual[..., 3] < 255)))

    def test_separate_pennons_survive_contour_reconstruction(self):
        pixels = np.zeros((12, 12, 4), dtype=np.uint8)
        pixels[2:10, 5:10] = [30, 50, 90, 255]
        pixels[2:3, 1:6] = [30, 50, 90, 255]
        pixels[6:7, 1:6] = [30, 50, 90, 255]
        actual = np.asarray(upscale_style(pixels, 16))
        self.assertGreater(actual[2 * 16 + 8, 2 * 16 + 8, 3], 240)
        self.assertGreater(actual[6 * 16 + 8, 2 * 16 + 8, 3], 240)
        self.assertEqual(actual[4 * 16 + 8, 2 * 16 + 8, 3], 0)


if __name__ == "__main__":
    unittest.main()
