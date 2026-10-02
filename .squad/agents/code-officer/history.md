# History

Learnings worth keeping across sessions (conventions discovered, pitfalls, decisions that affected this
role). Append short dated entries; do not log routine work.

- 2026-10-02 (issue 78) — SonarQube Cloud also reports MSTest analyzer rules (e.g. MSTEST0049, an async call without `TestContext.CancellationToken`) as external Roslyn issues, and they do not show up when the local build output is filtered for `RH`/`S` only. Filter the build for `MSTEST` as well and pass `TestContext.CancellationToken` to every awaited call in new tests.
- 2026-10-02 (issue 188) — The `squad-code-officer` agent type was not registered in the session although `.claude/agents/squad-code-officer.md` exists, so the launch failed. The orchestrator fell back to a general-purpose agent told to read that file and follow it; check the available agent types before step 7 instead of discovering this on launch.