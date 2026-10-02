# History

Learnings worth keeping across sessions (conventions discovered, pitfalls, decisions that affected this
role). Append short dated entries; do not log routine work.

- 2026-10-02 (issue 78) — SonarQube Cloud also reports MSTest analyzer rules (e.g. MSTEST0049, an async call without `TestContext.CancellationToken`) as external Roslyn issues, and they do not show up when the local build output is filtered for `RH`/`S` only. Filter the build for `MSTEST` as well and pass `TestContext.CancellationToken` to every awaited call in new tests.