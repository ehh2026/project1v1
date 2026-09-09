"""Regression tests for scripts/prepare_web_assets.py.

    py -3 -m unittest scripts.prepare_web_assets_tests -v

Covers the CodeRabbit PR #35 findings: coordinate validation/fallback,
collision-proof derivative filenames, and destructive-output path guards.
"""

import os
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import prepare_web_assets as pwa


class WebSafeNameTests(unittest.TestCase):
    def test_clean_name_passes_through(self):
        self.assertEqual(pwa.web_safe_name("photo 1.jpg"), "photo 1.jpg")

    def test_unsafe_chars_get_digest(self):
        out = pwa.web_safe_name("4-stamp_demo - Copy (4).png")
        self.assertNotIn("(", out)
        self.assertTrue(out.endswith(".png"))
        self.assertIn("4-stamp_demo - Copy _4_", out)

    def test_distinct_sources_never_collide(self):
        a = pwa.web_safe_name("scan (front).jpg")
        b = pwa.web_safe_name("scan _front_.jpg")
        self.assertNotEqual(a, b)

    def test_digest_is_stable(self):
        self.assertEqual(pwa.web_safe_name("a(b).png"), pwa.web_safe_name("a(b).png"))

    def test_unicode_basename_is_normalized(self):
        out = pwa.web_safe_name("café.jpg")
        self.assertTrue(out.endswith(".jpg"))
        self.assertTrue(out.startswith("caf_"))
        self.assertNotIn("é", out)

    def test_ascii_digest_length(self):
        out = pwa.web_safe_name("scan (front).jpg")
        stem, ext = out.rsplit(".", 1)
        digest = stem.rsplit(".", 1)[-1]
        self.assertEqual(len(digest), 12)


class ValidatedCoordsTests(unittest.TestCase):
    def test_primary_frame_used_when_valid(self):
        nx, ny = pwa._validated_coords("4000", "2000", 8198, 5542, "", "", 16397, 11085, "X")
        self.assertAlmostEqual(nx, 4000 / 8198)
        self.assertAlmostEqual(ny, 2000 / 5542)

    def test_out_of_frame_falls_back_to_secondary(self):
        nx, ny = pwa._validated_coords("99999", "2000", 8198, 5542, "8000", "4000", 16397, 11085, "X")
        self.assertAlmostEqual(nx, 8000 / 16397)

    def test_both_bogus_returns_none(self):
        self.assertIsNone(pwa._validated_coords("99999", "x", 8198, 5542, "", "", 16397, 11085, "X"))

    def test_empty_is_rejected_not_zero(self):
        self.assertIsNone(pwa._validated_coords("", "", 8198, 5542, "", "", 16397, 11085, "X"))


class PathSafetyTests(unittest.TestCase):
    def test_filesystem_root_is_rejected_before_cleanup(self):
        with tempfile.TemporaryDirectory() as content_dir:
            filesystem_root = os.path.realpath(os.path.abspath(os.path.sep))
            with mock.patch.object(sys, "argv", ["prepare_web_assets.py", "--content-set", content_dir,
                                                   "--out", filesystem_root]):
                self.assertEqual(pwa.main(), 3)

    def test_location_folder_must_be_strict_descendant(self):
        with tempfile.TemporaryDirectory() as content_dir:
            content_real = os.path.realpath(content_dir)
            nested = os.path.join(content_real, "nested")
            sibling = os.path.join(os.path.dirname(content_real), "sibling")
            self.assertTrue(pwa.is_strict_descendant(nested, content_real))
            self.assertFalse(pwa.is_strict_descendant(content_real, content_real))
            self.assertFalse(pwa.is_strict_descendant(sibling, content_real))


class _Loc(dict):
    """Tiny location holder so tests read like prepare_web_assets data."""
    def __init__(self, nx, ny):
        super().__init__(nx=nx, ny=ny)


class CropBudgetTests(unittest.TestCase):
    def test_crop_bounds_pad_each_axis_with_own_master_dimension(self):
        nx0, ny0, nx1, ny1 = pwa._crop_bounds([_Loc(0.5, 0.5)])
        self.assertAlmostEqual(nx1 - nx0, 2 * pwa.CROP_PAD_X / pwa.MASTER_W)
        self.assertAlmostEqual(ny1 - ny0, 2 * pwa.CROP_PAD_Y / pwa.MASTER_H)
        self.assertAlmostEqual(nx0, 0.5 - pwa.CROP_PAD_X / pwa.MASTER_W)
        self.assertAlmostEqual(ny1, 0.5 + pwa.CROP_PAD_Y / pwa.MASTER_H)

    def test_crop_bounds_clamp_to_map_unit_square(self):
        nx0, ny0, nx1, ny1 = pwa._crop_bounds([_Loc(0.0, 1.0)])
        self.assertEqual(nx0, 0.0)
        self.assertEqual(ny1, 1.0)

    def test_split_group_halves_along_longer_axis(self):
        wide = [_Loc(0.1, 0.5), _Loc(0.2, 0.5), _Loc(0.3, 0.5), _Loc(0.4, 0.5)]
        a, b = pwa._split_group(wide)
        self.assertTrue(a and b)
        self.assertEqual(len(a) + len(b), len(wide))

    def test_split_group_single_pin_cannot_split(self):
        self.assertEqual(pwa._split_group([_Loc(0.5, 0.5)]), [])

    def test_split_reduces_bounding_box_area(self):
        wide = [_Loc(i * 0.01, i * 0.01) for i in range(60, 0, -1)]
        a, b = pwa._split_group(wide)
        area = lambda g, pad=pwa.CROP_PAD_X: (max(x["nx"] for x in g) - min(x["nx"] for x in g) + 2 * pad / pwa.MASTER_W)
        self.assertLess(max(area(a), area(b)), area(wide))

    def test_save_crop_for_budget_small_bytes_passes_through(self):
        try:
            from PIL import Image
        except ImportError:
            self.skipTest("Pillow not installed")
        import tempfile as _tmp
        with _tmp.TemporaryDirectory() as d:
            path = os.path.join(d, "x.jpg")
            img = Image.new("RGB", (64, 64), "navy")
            self.assertTrue(pwa._save_crop_for_budget(img, path, 10_000_000))

    def test_save_crop_for_budget_tiny_budget_shrinks_to_fit(self):
        try:
            from PIL import Image
        except ImportError:
            self.skipTest("Pillow not installed")
        import tempfile as _tmp
        with _tmp.TemporaryDirectory() as d:
            path = os.path.join(d, "x.jpg")
            img = Image.new("RGB", (1200, 1200), "white")
            img.save(path, "JPEG", quality=95)
            self.assertGreater(os.path.getsize(path), 2000)
            os.remove(path)
            pwa._save_crop_for_budget(img, path, 2000)
            self.assertTrue(os.path.isfile(path))
            self.assertLessEqual(os.path.getsize(path), 2000)


if __name__ == "__main__":
    unittest.main()
