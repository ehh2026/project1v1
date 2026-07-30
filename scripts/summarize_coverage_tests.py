#!/usr/bin/env python3
import tempfile
import unittest
from pathlib import Path

import summarize_coverage


COBERTURA = """<?xml version="1.0" ?>
<coverage line-rate="{line_rate}" branch-rate="{branch_rate}" lines-covered="1" lines-valid="2" />
"""


class SummarizeCoverageTests(unittest.TestCase):
    def test_results_directory_alias_sets_path(self):
        with tempfile.TemporaryDirectory() as tmp:
            args = summarize_coverage.parse_args(["--results-directory", tmp])

        self.assertEqual(Path(tmp), args.path)

    def test_rejects_positional_path_with_results_directory(self):
        with self.assertRaises(SystemExit):
            summarize_coverage.parse_args(["TestResults", "--results-directory", "OtherResults"])

    def test_threshold_failure_returns_one(self):
        with tempfile.TemporaryDirectory() as tmp:
            coverage = Path(tmp) / "coverage.cobertura.xml"
            coverage.write_text(COBERTURA.format(line_rate="0.41", branch_rate="0.36"), encoding="utf-8")
            args = summarize_coverage.parse_args(
                [tmp, "--min-line-coverage", "42", "--min-branch-coverage", "37"])

            self.assertEqual(1, summarize_coverage.check_thresholds(args, [coverage]))

    def test_missing_coverage_with_threshold_returns_one(self):
        args = summarize_coverage.parse_args(["--min-line-coverage", "42"])

        self.assertEqual(1, summarize_coverage.check_thresholds(args, []))


if __name__ == "__main__":
    unittest.main()
