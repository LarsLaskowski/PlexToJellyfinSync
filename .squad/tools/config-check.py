#!/usr/bin/env python3
"""Config gate for the squad: agent and skill definitions must load, and skill mirrors must match.

Claude Code silently drops an agent or skill whose YAML front matter does not parse (for example an
unquoted description containing ": "), so a broken file only shows up when a squad run tries to launch
it. This script checks, without arguments:

- every `.claude/agents/*.md`, `.claude/skills/*/SKILL.md` and `.github/skills/*/SKILL.md` has front
  matter that parses as YAML, with a non-empty `name` and `description`;
- an agent's `name` equals its file name, a skill's `name` equals its folder name;
- `.claude/skills/` and `.github/skills/` contain the same skills with identical content.

Usage, from the repository root:
    python3 .squad/tools/config-check.py

Exit code 0 when everything is valid, 1 otherwise. Requires PyYAML (`pip install pyyaml`).
"""
import glob
import os
import sys

try:
    import yaml
except ImportError:
    sys.exit("PyYAML is required: pip install pyyaml")


def front_matter(path):
    with open(path, encoding="utf-8-sig") as handle:
        text = handle.read().replace("\r\n", "\n")
    if not text.startswith("---\n"):
        raise ValueError("no front matter")
    end = text.find("\n---", 4)
    if end < 0:
        raise ValueError("unterminated front matter")
    data = yaml.safe_load(text[4:end])
    if not isinstance(data, dict):
        raise ValueError("front matter is not a mapping")
    return data


def check(path, expected_name, errors):
    try:
        data = front_matter(path)
    except (ValueError, yaml.YAMLError) as error:
        errors.append(f"{path}: {str(error).splitlines()[0]}")
        return
    for key in ("name", "description"):
        if not str(data.get(key) or "").strip():
            errors.append(f"{path}: missing '{key}'")
    if data.get("name") and data["name"] != expected_name:
        errors.append(f"{path}: name '{data['name']}' does not match '{expected_name}'")


def main():
    # Resolve paths from the repository root, whatever the current directory is.
    os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    errors = []
    agents = sorted(glob.glob(os.path.join(".claude", "agents", "*.md")))
    for path in agents:
        check(path, os.path.splitext(os.path.basename(path))[0], errors)
    skills = {}
    for root in (os.path.join(".claude", "skills"), os.path.join(".github", "skills")):
        found = {}
        for path in sorted(glob.glob(os.path.join(root, "*", "SKILL.md"))):
            name = os.path.basename(os.path.dirname(path))
            check(path, name, errors)
            with open(path, "rb") as handle:
                found[name] = handle.read()
        skills[root] = found
    claude, github = skills[os.path.join(".claude", "skills")], skills[os.path.join(".github", "skills")]
    for name in sorted(set(claude) | set(github)):
        if name not in claude or name not in github:
            errors.append(f"skill '{name}' exists in only one of .claude/skills and .github/skills")
        elif claude[name] != github[name]:
            errors.append(f"skill '{name}' differs between .claude/skills and .github/skills")

    if not agents or not claude:
        errors.append("no agents or skills found - is this the PlexToJellyfinSync repository?")
    for error in errors:
        print(error)
    print(f"\nChecked {len(agents)} agents and {len(claude)} skills: {'PASS' if not errors else 'FAIL'}")
    return 0 if not errors else 1


if __name__ == "__main__":
    sys.exit(main())
