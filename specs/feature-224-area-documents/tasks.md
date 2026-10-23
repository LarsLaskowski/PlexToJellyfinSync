# Tasks: Introduce area documents

Plan: [plan.md](plan.md) — Status: Draft

Each task names its criteria, the tasks it waits for and its group (one launch per member in steps 4–6; rows
consecutive). Every group is owned by the Lead alone (mode `docs`) and has no new API. Each documentation edit is
in one row. The rows of G1 are independent and may run as parallel Lead launches.

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Write the Sync pipeline area document (no new API) | `docs/areas/sync-pipeline.md` | AC1, AC5, AC6, AC10, AC12 | — | Lead | [ ] |
| 2 | G1 | Write the Path mapping and NFO area document | `docs/areas/path-mapping-and-nfo.md` | AC1, AC5, AC6, AC10, AC12 | — | Lead | [ ] |
| 3 | G1 | Write the Dashboard and auth area document, forwarded-header wording per plan *Approach* 1 | `docs/areas/dashboard-and-auth.md` | AC1, AC5, AC6, AC10, AC12 | — | Lead | [ ] |
| 4 | G1 | Write the Configuration area document with the settings table and container contract | `docs/areas/configuration.md` | AC1, AC5, AC6, AC10, AC12 | — | Lead | [ ] |
| 5 | G2 | Record 0021 and its index row `Proposed` (written in step 2 with the plan; no new API) | `docs/decisions/0021-area-documents-and-area-assignment.md`, `docs/decisions/README.md` | AC3, AC4, AC11, AC12 | — | Lead | [x] |
| 6 | G2 | `Area:` lines in 0015–0019, area links in 0015–0018, value bullets of 0016 cut to a link | `docs/decisions/0015-*.md` … `0019-*.md` | AC2, AC4, AC10, AC12 | 1–4 | Lead | [ ] |
| 7 | G2 | Four rows in the `area-index` block (last, as it switches the area check on) | `docs/areas/README.md` | AC1, AC2, AC11, AC12 | 1–4, 6 | Lead | [ ] |
| 8 | G3 | Areas paragraph and links in the `architecture` project block, restated behavior replaced by links (no new API) | `docs/ARCHITECTURE.md` | AC7, AC10, AC11, AC12 | 1–4 | Lead | [ ] |
| 9 | G3 | Stability bullets re-linked to the area documents | `docs/CONTRIBUTING.md` | AC9, AC10, AC11, AC12 | 1–4 | Lead | [ ] |
| 10 | G3 | *Development* paragraph names `docs/areas/README.md` | `README.md` | AC9, AC10, AC12 | 7 | Lead | [ ] |
| 11 | G3 | *Guarantees* intro and links, *Integration surface* items | `.squad/project.md` | AC8, AC10, AC12 | 1–4 | Lead | [ ] |