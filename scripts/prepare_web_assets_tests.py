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

        def box_area(group):
            nx0, ny0, nx1, ny1 = pwa._crop_bounds(group)
            return (nx1 - nx0) * (ny1 - ny0)

        self.assertLess(max(box_area(a), box_area(b)), box_area(wide))

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
            self.assertTrue(pwa._save_crop_for_budget(img, path, 2000))
            self.assertTrue(os.path.isfile(path))
            self.assertLessEqual(os.path.getsize(path), 2000)


class _FakePILImage:
    """Stand-in for PIL.Image so cut_crops' split loop is testable offline."""
    MAX_IMAGE_PIXELS = 200_000_000
    LANCZOS = 1

    class _FakeMaster:
        def __init__(self, w, h):
            self.width, self.height = w, h

        def __enter__(self):
            return self

        def __exit__(self, *exc):
            return False

        def crop(self, box):
            return _FakePILImage._FakeCrop(box)

    class _FakeCrop:
        def __init__(self, box):
            self.box = box

        def convert(self, mode):
            return self

        def save(self, *args, **kwargs):
            return None

        def resize(self, size, resample):
            self._size = size
            return self

        @property
        def width(self):
            return self._size[0] if getattr(self, "_size", None) else self.box[2] - self.box[0]

        @property
        def height(self):
            return self._size[1] if getattr(self, "_size", None) else self.box[3] - self.box[1]

    @classmethod
    def open(cls, path):
        return cls._FakeMaster(16397, 11085)


class CutCropsIntegrationTests(unittest.TestCase):
    def _clusters(self, count=10):
        return [[{"name": f"p{i}", "nx": 0.2 + i * 0.03, "ny": 0.3 + i * 0.02} for i in range(count)]]

    def _run_cut_crops(self, clusters, max_pixels):
        import types
        with tempfile.TemporaryDirectory() as out:
            saved = []
            fake_modules = {"PIL.Image": _FakePILImage, "PIL": types.ModuleType("PIL")}
            with mock.patch.dict(sys.modules, fake_modules) as _md:
                with mock.patch.object(pwa, "CROP_MAX_PIXELS", max_pixels):
                    with mock.patch.object(pwa, "_save_crop_for_budget",
                                   side_effect=lambda img, path, mb: (saved.append(path) or open(path, "wb").close() or True)):
                        crops = pwa.cut_crops(clusters, out)
            return crops, saved

    def test_oversized_groups_split_and_numbering_stays_gapless(self):
        clusters = self._clusters()
        crops, saved = self._run_cut_crops(clusters, max_pixels=6_000_000)
        self.assertGreater(len(crops), 1)
        files = [c["file"] for c in crops]
        self.assertEqual(files, sorted(files))
        expected = [f"images/crops/crop_{i + 1:02d}.jpg" for i in range(len(crops))]
        self.assertEqual(files, expected)
        self.assertEqual(len(saved), len(crops))
        for c in crops:
            self.assertLessEqual(c["nx0"], c["nx1"])
            self.assertLessEqual(c["ny0"], c["ny1"])
            self.assertTrue(c["members"])

    def test_singleton_over_pixel_budget_still_emits_fallback_crop(self):
        clusters = [[{"name": "solo", "nx": 0.5, "ny": 0.5}]]
        crops, saved = self._run_cut_crops(clusters, max_pixels=1_000_000)
        self.assertEqual(len(crops), 1)
        self.assertEqual(crops[0]["members"], ["solo"])
        self.assertEqual(len(saved), 1)


if __name__ == "__main__":
    unittest.main()
