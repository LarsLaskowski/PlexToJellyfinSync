#!/usr/bin/env python3
"""Analyzer gate: every Roslyn diagnostic in a changed file, at any severity, as SonarQube Cloud sees it.

SonarQube Cloud imports *all* Roslyn diagnostics from the build's SARIF error log - including info-level
ones such as the MSTest analyzer rules (MSTEST####), which never appear as build warnings. A console
build therefore hides issues that the quality gate later reports. This script runs a full, non-incremental
Release build with an SARIF error log per project and reports every non-suppressed diagnostic (RH####,
S####, MSTEST####, CA####, ...) located in a file changed since the merge base with origin/main
(working tree and untracked files included).

The build command, the base (origin/main) and the log location are fixed on purpose: the script takes no
arguments, so nothing user-supplied reaches the shell, git or the filesystem.

Usage, from the repository root (after `dotnet restore PlexToJellyfinSync.slnx`):
    python3 .squad/tools/analyzer-check.py

Exit code 0 when no changed file has a diagnostic, 1 otherwise (or when the build fails).
"""
import glob
import json
import os
import subprocess
import sys
from urllib.parse import unquote, urlparse

BASE_REF = "origin/main"
SARIF_NAME = os.path.join("obj", "roslyn.sarif")
BUILD = ["dotnet", "build", "PlexToJellyfinSync.slnx", "-c", "Release", "--no-restore", "--no-incremental",
         "-p:ErrorLog=" + SARIF_NAME + "%2Cversion=2.1"]


def git(*args):
    return subprocess.run(["git", *args], capture_output=True, text=True, check=True).stdout


def changed_files():
    merge_base = git("merge-base", BASE_REF, "HEAD").strip()
    names = git("diff", "--name-only", merge_base).splitlines()
    names += git("ls-files", "--others", "--exclude-standard").splitlines()
    return {name.strip() for name in names if name.strip()}


def to_repo_path(uri, root):
    parsed = urlparse(uri)
    path = unquote(parsed.path) if parsed.scheme == "file" else unquote(uri)
    return os.path.relpath(os.path.realpath(path), root).replace(os.sep, "/")


def diagnostics(root):
    for log in glob.glob(os.path.join("**", SARIF_NAME), recursive=True):
        with open(log, encoding="utf-8-sig") as handle:
            data = json.load(handle)
        for run in data.get("runs", []):
            for result in run.get("results", []):
                if result.get("suppressions"):
                    continue
                locations = result.get("locations") or []
                if not locations:
                    continue
                physical = locations[0].get("physicalLocation", {})
                path = to_repo_path(physical.get("artifactLocation", {}).get("uri", ""), root)
                line = physical.get("region", {}).get("startLine", 0)
                yield path, line, result.get("ruleId", "?"), result.get("level", "warning"), \
                    result.get("message", {}).get("text", "")


def main():
    root = os.path.realpath(os.getcwd())
    for stale in glob.glob(os.path.join("**", SARIF_NAME), recursive=True):
        os.remove(stale)
    build = subprocess.run(BUILD, capture_output=True, text=True, check=False)
    if build.returncode != 0:
        print(build.stdout[-4000:])
        print("Build failed.")
        return 1

    changed = changed_files()
    found = sorted(set(diagnostics(root)))
    in_changed = [d for d in found if d[0] in changed]
    elsewhere = len(found) - len(in_changed)

    for path, line, rule, level, message in in_changed:
        print(f"{path}({line}): {level} {rule}: {message}")
    print(f"\nDiagnostics in changed files: {len(in_changed)}")
    print(f"Diagnostics in unchanged files (not gating): {elsewhere}")
    print("PASS" if not in_changed else "FAIL")
    return 0 if not in_changed else 1


if __name__ == "__main__":
    sys.exit(main())
