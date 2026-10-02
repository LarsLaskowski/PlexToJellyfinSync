# History

Learnings worth keeping across sessions (conventions discovered, pitfalls, decisions that affected this
role). Append short dated entries; do not log routine work.

- 2026-10-02 (issue 78) — New async tests take a `TestContext` constructor parameter and pass `_testContext.CancellationToken` to `Task.Run`/`Task.Delay` (pattern in NfoWriterTests/PlexClientTests); otherwise SonarQube Cloud raises MSTEST0049 on the PR.