"""Regression tests for scripts/audit_unused_assets.py — run with stdlib unittest:

    py -3 -m unittest scripts.audit_unused_assets_tests -v

Covers the CodeRabbit PR #34 finding: path separators must survive token
extraction, and duplicate basenames must not suppress real candidates.
"""

import unittest

import audit_unused_assets as audit


class ExtractTokensTests(unittest.TestCase):
    def test_backslash_paths_are_preserved(self):
        tokens = audit.extract_reference_tokens('MapPath = "Assets\\World Map Extra Large.jpg"')
        self.assertIn("assets/world map extra large.jpg", tokens)

    def test_forward_slash_paths_are_preserved(self):
        tokens = audit.extract_reference_tokens('"Assets/pins_v2/pin_01.png"')
        self.assertIn("assets/pins_v2/pin_01.png", tokens)

    def test_bare_basename_extracted(self):
        tokens = audit.extract_reference_tokens('"stamp.png"')
        self.assertIn("stamp.png", tokens)


class IsReferencedTests(unittest.TestCase):
    def test_duplicate_basename_not_suppressed_by_other_dir_reference(self):
        tokens = audit.extract_reference_tokens('"Assets/foo.png"')
        basename_counts = {"foo.png": 2}  # exists in both Assets/ and Extras/
        self.assertTrue(audit.is_referenced("assets/foo.png", tokens, basename_counts))
        self.assertFalse(audit.is_referenced("extras/foo.png", tokens, basename_counts))

    def test_unique_basename_matches_without_path(self):
        tokens = audit.extract_reference_tokens('"logo.png"')
        basename_counts = {"logo.png": 1}
        self.assertTrue(audit.is_referenced("assets/img/logo.png", tokens, basename_counts))

    def test_similar_name_does_not_match(self):
        tokens = audit.extract_reference_tokens('"old-map.png"')
        basename_counts = {"old-map.png": 1, "map.png": 1}
        self.assertFalse(audit.is_referenced("assets/map.png", tokens, basename_counts))

    def test_partial_path_suffix_matches(self):
        tokens = audit.extract_reference_tokens('"pins_v2/pin_01.png"')
        basename_counts = {"pin_01.png": 1}
        self.assertTrue(audit.is_referenced("assets/pins_v2/pin_01.png", tokens, basename_counts))


if __name__ == "__main__":
    unittest.main()
