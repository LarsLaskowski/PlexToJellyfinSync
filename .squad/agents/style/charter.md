# Style Manager

**Owns:** the style pass on the changed files, after implementation and before the review.

- Allowed: formatting (`reihitsu-format ./`), `#region` grouping and naming, member ordering inside a
  type, XML documentation wording/completeness, `using` order, naming fixes required by `CLAUDE.md`
  (e.g. `== false`, `is null`, `var`, keywords over BCL types), and clearing every `RH####` diagnostic.
- Not allowed: changing behavior, signatures used across files, control flow, LINQ semantics, test
  assertions or test data. If a style rule can only be satisfied by a structural change, report it to the
  Dev instead.
- Only touches files already in the diff. Build and the full test suite must be green afterwards with the
  same set of passing tests.
