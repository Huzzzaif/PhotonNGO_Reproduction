#!/usr/bin/env python3
"""Summarize actual recorded checks; never interpret event counts as independent trials."""
import argparse
import csv
import json
from collections import defaultdict
from pathlib import Path


def summarize(paths):
    groups = defaultdict(lambda: {"recorded_checks": 0, "passed": 0, "failed": 0})
    warnings, processes, records, checked_runs = [], set(), 0, set()
    for path in paths:
        with Path(path).open(newline="", encoding="utf-8-sig") as stream:
            reader = csv.DictReader(stream)
            required = {"utc", "run", "scenario", "process", "role", "event", "pass"}
            if not required.issubset(reader.fieldnames or []):
                raise ValueError(f"Missing CSV columns in {path}: {required - set(reader.fieldnames or [])}")
            for row in reader:
                records += 1
                processes.add(row["process"])
                result = row["pass"].strip()
                if result == "":
                    continue  # Unscored events are not successes.
                if result not in {"0", "1"}:
                    raise ValueError(f"Invalid pass value in {path}: {result!r}")
                key = (row["run"], row["scenario"], row["role"], row["event"])
                g = groups[key]
                g["recorded_checks"] += 1
                g["passed" if result == "1" else "failed"] += 1
                checked_runs.add((row["run"], row["scenario"]))
                if row["run"] in {"", "unassigned"}:
                    warnings.append(f"Check has no meaningful run label in {path}")
    result = []
    for key, values in sorted(groups.items()):
        if values["recorded_checks"] > 1:
            warnings.append("Multiple check records for " + "/".join(key) +
                            "; do not count these as independent trial repetitions.")
        result.append(dict(zip(("run", "scenario", "role", "event"), key), **values))
    return {"source_files": [str(p) for p in paths], "event_rows": records,
            "process_count": len(processes), "run_scenario_labels_with_checks": len(checked_runs),
            "checks": result, "warnings": sorted(set(warnings)),
            "interpretation": "Counts describe recorded check events, not independent trials or reliability estimates."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("logs", nargs="+", help="Actual CSV logs; folders are scanned recursively")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--strict", action="store_true", help="Nonzero exit if failures, warnings, or no scored checks")
    args = parser.parse_args()
    files = []
    for name in args.logs:
        path = Path(name)
        files.extend(sorted(path.rglob("*.csv")) if path.is_dir() else [path])
    files = sorted(set(files))
    if not files:
        parser.error("No CSV files found")
    try:
        report = summarize(files)
    except (ValueError, OSError) as error:
        parser.error(str(error))
    text = json.dumps(report, indent=2)
    if args.output:
        args.output.write_text(text + "\n", encoding="utf-8")
    print(text)
    if args.strict and (not report["checks"] or report["warnings"] or any(g["failed"] for g in report["checks"])):
        raise SystemExit(1)


if __name__ == "__main__":
    main()
