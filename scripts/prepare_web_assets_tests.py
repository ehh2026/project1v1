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


if __name__ == "__main__":
    unittest.main()
