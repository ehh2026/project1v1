"""Regression tests for scripts/prepare_web_assets.py.

    py -3 -m unittest scripts.prepare_web_assets_tests -v

Covers the CodeRabbit PR #35 findings: coordinate validation/fallback and
collision-proof derivative filenames.
"""

import os
import sys
import unittest

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


if __name__ == "__main__":
    unittest.main()
