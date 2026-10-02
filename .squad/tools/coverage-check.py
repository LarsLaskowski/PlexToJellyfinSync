#!/usr/bin/env python3
"""Coverage gate for the squad: line coverage of new/changed production lines (like SonarQube's
"coverage on new code") and overall line coverage, read from the newest coverlet Cobertura report.

The base (origin/main) and the report directory (./TestResults) are fixed on purpose: the script takes
no paths or refs from the command line, so nothing user-supplied reaches git or the filesystem.

Usage, from the repository root:
    dotnet test PlexToJellyfinSync.slnx -c Release --no-build \
        --collect:"XPlat Code Coverage" --results-directory ./TestResults
    python3 .squad/tools/coverage-check.py [--threshold 80]

Exit code 0 when both values reach the threshold, 1 otherwise.
"""
import argparse
import glob
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

BASE_REF = "origin/main"
RESULTS_DIR = "TestResults"
PATHSPECS = ["src/*.cs", "src/**/*.cs", "src/*.razor", "src/**/*.razor"]


def changed_lines():
    """Return {repo-relative path: set(line numbers)} of lines added or changed since the merge base with
    origin/main (working tree included) in src/**/*.cs and src/**/*.razor."""
    merge_base = subprocess.run(
        ["git", "merge-base", BASE_REF, "HEAD"], capture_output=True, text=True, check=True).stdout.strip()
    diff = subprocess.run(
        ["git", "diff", "-U0", merge_base, "--", *PATHSPECS],
        capture_output=True, text=True, check=True).stdout
    result, current = {}, None
    for line in diff.splitlines():
        if line.startswith("+++ "):
            path = line[4:]
            current = path[2:] if path.startswith("b/") else None
            if current:
                result.setdefault(current, set())
        elif line.startswith("@@") and current:
            match = re.search(r"\+(\d+)(?:,(\d+))?", line)
            start, count = int(match.group(1)), int(match.group(2) or "1")
            result[current].update(range(start, start + count))
    return result


def load_report():
    reports = glob.glob(os.path.join(RESULTS_DIR, "**", "coverage.cobertura.xml"), recursive=True)
    if not reports:
        sys.exit(f"No coverage.cobertura.xml under ./{RESULTS_DIR} - run dotnet test with coverage first")
    root = ET.parse(max(reports, key=os.path.getmtime)).getroot()
    sources = [s.text.rstrip("/\\") for s in root.iter("source") if s.text]
    hits = {}
    for cls in root.iter("class"):
        filename = cls.get("filename")
        for src in sources:
            candidate = os.path.join(src, filename)
            if os.path.exists(candidate):
                filename = candidate
                break
        path = os.path.relpath(os.path.abspath(filename)).replace(os.sep, "/")
        lines = hits.setdefault(path, {})
        for ln in cls.iter("line"):
            number, count = int(ln.get("number")), int(ln.get("hits"))
            lines[number] = max(lines.get(number, 0), count)
    return float(root.get("line-rate")) * 100, hits


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--threshold", type=float, default=80.0)
    args = parser.parse_args()

    overall, hits = load_report()
    covered = coverable = 0
    print("Changed production files (coverable changed lines):")
    for path, lines in sorted(changed_lines().items()):
        file_hits = hits.get(path, {})
        relevant = [n for n in lines if n in file_hits]
        hit = sum(1 for n in relevant if file_hits[n] > 0)
        covered, coverable = covered + hit, coverable + len(relevant)
        missed = sorted(n for n in relevant if file_hits[n] == 0)
        rate = f"{hit / len(relevant) * 100:5.1f}%" if relevant else "  n/a "
        print(f"  {rate}  {hit}/{len(relevant)}  {path}" + (f"  uncovered: {missed}" if missed else ""))

    new_code = covered / coverable * 100 if coverable else 100.0
    print(f"\nNew/changed code: {new_code:.1f}% ({covered}/{coverable} lines)")
    print(f"Overall:          {overall:.1f}%")
    ok = new_code >= args.threshold and overall >= args.threshold
    print(f"Threshold {args.threshold:.0f}%: {'PASS' if ok else 'FAIL'}")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
