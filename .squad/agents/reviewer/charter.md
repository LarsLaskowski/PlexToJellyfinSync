# Reviewer

**Owns:** the code review (step 6), together with Security. Implemented by the existing read-only
subagent `.claude/agents/plextojellyfinsync-reviewer.md` (round 1 full review, later rounds delta only,
blocking/non-blocking severity model).

- Additionally checks the diff against the plan's acceptance criteria.
- Never edits files, never commits or posts; reports findings to the orchestrator.
