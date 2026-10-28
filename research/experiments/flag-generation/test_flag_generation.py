"""Small numerical checks for the experiment's actual failure boundaries."""
import unittest

import numpy as np
from PIL import Image

from run_experiment import compose, fit_material, metrics, render_cloth, render_glyph, sample_bilinear


class FlagGenerationTests(unittest.TestCase):
    def test_bilinear_subpixel_and_edges(self):
        image = np.array([[[0.], [2.]], [[4.], [6.]]])
        actual = sample_bilinear(image, np.array([[0.5, 0.5], [-4, 0], [10, 10]]))
        np.testing.assert_allclose(actual[:, 0], [3, 0, 6])

    def test_identity_preserves_coverage_and_blank_decal_stays_blank(self):
        base = np.array([[[25, 70, 125, 128], [100, 200, 150, 255]], [[0, 0, 0, 0], [5, 20, 80, 50]]], dtype=np.uint8)
        yy, xx = np.mgrid[:2, :2]
        uv = np.stack([xx, yy], axis=-1)
        np.testing.assert_array_equal(render_glyph(base, uv, np.ones((2, 2))), base)
        np.testing.assert_array_equal(render_glyph(np.zeros_like(base), uv, np.ones((2, 2)) * 1.8), 0)

    def test_material_training_ignores_holdout_and_transparent_rgb(self):
        rng = np.random.default_rng(42)
        parameters = np.c_[rng.uniform(0.2, 0.8, (9, 6)), np.ones(9)]
        cloths = np.zeros((9, 32, 40, 32, 4), dtype=np.uint8)
        cloths[..., :3] = np.rint(parameters[:, None, None, None, :3] * 255)
        cloths[:, :, 1:3, 1:3, 3] = 255
        training = list(range(8))
        before = fit_material(cloths, parameters, training)
        cloths[8] = 123
        cloths[:8, :, 0, 0, :3] = 255
        after = fit_material(cloths, parameters, training)
        for a, b in zip(before, after):
            np.testing.assert_array_equal(a, b)
        generated = render_cloth(parameters[0], *before)
        self.assertEqual(generated[0, 0, 0, 3], 0)

    def test_pole_occlusion_and_hidden_rgb_metrics(self):
        pole = np.full((36, 4, 4), [255, 0, 0, 255], dtype=np.uint8)
        cloth = np.full((40, 32, 4), [0, 0, 255, 255], dtype=np.uint8)
        back = compose(pole, cloth, None, (1, 3), False)
        front = compose(pole, cloth, None, (1, 3), True)
        self.assertEqual(back.getpixel((31, 8)), (0, 0, 255, 255))
        self.assertEqual(front.getpixel((31, 8)), (255, 0, 0, 255))
        a = np.array(Image.new("RGBA", (2, 1)))
        b = a.copy()
        a[0, 0] = [20, 40, 60, 255]
        b[0, 0] = a[0, 0]
        b[0, 1, :3] = 255
        self.assertEqual(metrics(a, b)["premultiplied_rgb_mae_255"], 0)


if __name__ == "__main__":
    unittest.main()
