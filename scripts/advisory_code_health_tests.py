#!/usr/bin/env python3
"""
Unit checks for advisory_code_health.py.

These are stdlib-only so they can run anywhere the harness scripts run.
"""

from __future__ import annotations

import tempfile
import textwrap
import unittest
from pathlib import Path

import advisory_code_health


class AdvisoryCodeHealthTests(unittest.TestCase):
    def test_collects_largest_files_without_excluded_directories(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            app_file = root / "Services" / "ContentService.cs"
            app_file.parent.mkdir()
            app_file.write_text("class ContentService\n{\n}\n", encoding="utf-8")

            excluded_file = root / "backups" / "Old.cs"
            excluded_file.parent.mkdir()
            excluded_file.write_text("\n".join(["class Old"] * 50), encoding="utf-8")

            report = advisory_code_health.analyze_repository(root)

        self.assertEqual([item.relative_path for item in report.largest_files], ["Services/ContentService.cs"])

    def test_reports_long_and_complex_methods(self) -> None:
        source = textwrap.dedent(
            """
            class Example
            {
                void Busy()
                {
                    if (true) { }
                    for (var i = 0; i < 3; i++) { }
                    foreach (var item in items) { }
                    while (false) { }
                    switch (value)
                    {
                        case 1: break;
                        case 2: break;
                    }
                    try { } catch { }
                    var flag = a && b || c ? true : false;
                }
            }
            """
        )

        metrics = advisory_code_health.analyze_csharp_methods("Example.cs", source, max_method_lines=8, max_complexity=8)

        self.assertEqual(len(metrics), 1)
        self.assertEqual(metrics[0].method_name, "Busy")
        self.assertGreater(metrics[0].line_count, 8)
        self.assertGreater(metrics[0].complexity, 8)

    def test_ignores_control_blocks_that_look_method_like(self) -> None:
        source = textwrap.dedent(
            """
            class Example
            {
                void Wrapper()
                {
                    foreach (var item in items)
                    {
                        if (item.Enabled)
                        {
                            Run(item);
                        }
                    }
                }
            }
            """
        )

        metrics = advisory_code_health.analyze_csharp_methods("Example.cs", source, max_method_lines=1, max_complexity=1)

        self.assertEqual([item.method_name for item in metrics], ["Wrapper"])


if __name__ == "__main__":
    unittest.main()
