"""Regression for visible glyph centering when font metrics have negative bearings."""
from pathlib import Path
import unittest

import numpy as np

from prepare_modern_preview import flat_emblem


FONT = Path("C:/Windows/Fonts/simkai.ttf")


@unittest.skipUnless(FONT.is_file(), "Requires the preview's SimKai emblem font")
class EmblemLayoutTests(unittest.TestCase):
    def test_visible_glyph_is_complete_and_centered(self):
        for text in ("帥", "雲", "龍"):
            with self.subTest(text=text):
                image = flat_emblem(text, FONT)
                left, top, right, bottom = image.getchannel("A").getbbox()
                self.assertGreaterEqual(min(left, top, image.width - right, image.height - bottom), 39)
                self.assertLessEqual(abs((left + right) / 2 - image.width / 2), 0.5)
                self.assertLessEqual(abs((top + bottom) / 2 - image.height / 2), 0.5)
                pixels = np.asarray(image)
                ink = (pixels[..., 3] > 128) & (pixels[..., :3].mean(-1) < 64)
                self.assertTrue(ink.any())
                self.assertFalse(ink[:, -1].any())

    def test_blank_text_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "no visible glyph"):
            flat_emblem("", FONT)

    def test_white_ink_keeps_the_same_complete_geometry(self):
        black = np.asarray(flat_emblem("帥", FONT))
        white = np.asarray(flat_emblem("帥", FONT, white_ink=True))
        np.testing.assert_array_equal(black[..., 3], white[..., 3])
        opaque = black[..., 3] == 255
        self.assertTrue(opaque.any())
        np.testing.assert_array_equal(black[opaque, :3].astype(int) + white[opaque, :3], 256)


if __name__ == "__main__":
    unittest.main()
