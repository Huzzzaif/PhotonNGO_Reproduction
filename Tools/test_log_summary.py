"""Software tests with synthetic fixtures; these are NOT Unity experimental results."""
import csv
import tempfile
import unittest
from pathlib import Path
from summarize_logs import summarize


class SummaryTests(unittest.TestCase):
    def records(self, rows):
        folder = tempfile.TemporaryDirectory()
        self.addCleanup(folder.cleanup)
        path = Path(folder.name) / "fixture.csv"
        with path.open("w", newline="") as stream:
            writer = csv.writer(stream)
            writer.writerow(["utc", "run", "scenario", "process", "role", "event", "pass"])
            writer.writerows(rows)
        return path

    def test_failures_retained_and_unscored_not_success(self):
        path = self.records([
            ["t", "r1", "late", "a", "client", "connected", ""],
            ["t", "r1", "late", "a", "client", "placement_check", "0"],
            ["t", "r2", "late", "a", "client", "placement_check", "1"]])
        report = summarize([path])
        self.assertEqual(report["event_rows"], 3)
        self.assertEqual(sum(x["passed"] for x in report["checks"]), 1)
        self.assertEqual(sum(x["failed"] for x in report["checks"]), 1)

    def test_duplicate_checks_do_not_become_trial_count(self):
        row = ["t", "r1", "scene", "a", "client", "placement_check", "1"]
        report = summarize([self.records([row, row])])
        self.assertEqual(report["run_scenario_labels_with_checks"], 1)
        self.assertTrue(report["warnings"])

    def test_invalid_result_is_rejected(self):
        with self.assertRaises(ValueError):
            summarize([self.records([["t", "r1", "scene", "a", "client", "placement_check", "maybe"]])])

    def test_empty_logs_do_not_imply_success(self):
        report = summarize([self.records([])])
        self.assertEqual(report["checks"], [])


if __name__ == "__main__":
    unittest.main()
