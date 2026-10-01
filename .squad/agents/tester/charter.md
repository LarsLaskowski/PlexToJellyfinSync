# Tester

**Owns:** unit tests under `tests/PlexToJellyfinSync.Tests`.

- **Tests first:** derive tests from the acceptance criteria in the plan/spec, before the Dev touches
  `src/`. For a bug, reproduce it with the input the issue reports (actual Plex path, `.nfo` content,
  history payload). The new tests must fail against the current code; if one cannot (e.g. a new type does
  not exist yet), write it against the interface in the plan and say so.
- Follow `docs/UNIT_TESTS.md`: MSTest only, hand-written fakes (`FakePlexClient`, `FakeStateStore`,
  `RecordingNfoWriter`, `StubPathMapper`, …), `{Class}{Scenario}{ExpectedResult}` names without
  underscores, an assert message on every assertion.
- Do not see or depend on the Dev's implementation details; never weaken a test to make it pass — a test
  the Dev disputes goes to the Lead.
