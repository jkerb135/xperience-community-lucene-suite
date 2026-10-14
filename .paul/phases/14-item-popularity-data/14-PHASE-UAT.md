# Phase 14 — Item Popularity Data: Acceptance & Validation

**Plans:**
- 14-01: per-item daily popularity (`ItemDaily`, schema v8) from the rollup's attribution pass; same transaction and retention; seeded history.
- 14-02: **Top clicked items** card + **All clicked items** panel on Search reports; Pin / Hide in a new rule via one-time handoff; no-conditions warning in the rule form.

**Commits:** 4f4b212..8782193 on main (14-01: 4f4b212, 65dc845, 9a54f77 · 14-02: 7e42481, 6e67cc5, 53505b4, d9cf945 · security: 899ef28, 8782193)
**Tests:**
- Before Phase 14 (end of 14-01, DB set): 868 passed / 6 skipped. Client: 103.
- After Phase 14 (2026-10-13, DB unset): 877 total, 792 passed, 84 skipped (every Database test), 1 failed (`RulesOverhead_StaysUnderTenMillisecondsAtP95`, environmental). Client: 108/108. tsc clean.

**Build warnings:** 0 (Debug and Release).

## 1. Acceptance criteria — every AC in the phase

| Plan | AC | Status | Evidence | Notes |
|------|----|--------|----------|-------|
| 14-01 | AC-1 Schema v8 | PASS | `Version7Database_UpgradesToVersion8_CreatingItemDaily_LeavingVersion7ObjectsUntouched`, `ItemDaily_RejectsADuplicateDayIndexAndItem`; host installer at v8 | DB tests ran at 14-01 (connection string set then) |
| 14-01 | AC-2 Item rows from the same pass | PASS | Invariant tests per index; `65dc845` | |
| 14-01 | AC-3 Same transaction and retention | PASS | Transaction failure test covering both inserts; batched retention | No backfill (KNOWN-LIMITATIONS) |
| 14-01 | AC-4 Seeded history | PASS | Host re-seed: totals 7,725 / 2,344 / 203 unchanged, 1,027 item rows, 0 mismatching days (APPROVAL-CHECKS #22) | |
| 14-01 | AC-5 Identity-free, within budget | PASS | Schema has no identity columns; 0 warnings; 868/6 skipped | |
| 14-02 | AC-1 Top items data | DEFERRED | Code read in review (constant ORDER BY, nulls-last average, GUID tiebreak, OUTER APPLY totals, one `Describe` per page) | New tests in `SearchReportDatabaseTests` and `SearchReportsPageDatabaseTests` were **not executed** (connection string unset; risk accepted by Josh). They become PASS when those tests pass. |
| 14-02 | AC-2 Card on Search reports | DEFERRED | Code read in review; `53505b4` fixes a table rendering before the first successful load | Host checks in APPROVAL-CHECKS #23 |
| 14-02 | AC-3 Pin or hide through a new rule | DEFERRED | `handoff.test.ts`, `state.test.ts` (`conditionsWarning`); code read in review | Host check of the pre-fill and the warning in #23 |
| 14-02 | AC-4 No regressions | DEFERRED | 0 warnings; client 108/108; tsc clean; PublicAPI.Unshipped updated | Width checks (≈670 / 1269 px) in #23; DB suite not run |

## 2. Needs Josh — human actions the loop routed around

| Action | Why it is needed | What is unblocked when done | How to do it |
|--------|------------------|-----------------------------|--------------|
| Run the DB test suite | 84 Database tests skipped in this session, including the new TopItems SQL tests | 14-02 AC-1 → PASS | Add `"env": { "LUCENESUITE_TEST_CONNECTIONSTRING": "<comm_projects_test>" }` to `.claude/settings.local.json` (or your user env), then run `dotnet test` |
| Re-run the rules-overhead performance test | p95 was 10.8–16.3 ms (budget 10 ms) with 48% background CPU; 14-02 changes no search code | Confirms no search regression | `dotnet test -c Release --filter "FullyQualifiedName~RulesOverhead"` on an idle machine |
| APPROVAL-CHECKS #23 host checks | No human-verify step was done in APPLY | 14-02 AC-2, AC-3, AC-4 → PASS | See section 4 |
| P14-08: Pkcs High advisory (pre-existing) | `System.Security.Cryptography.Pkcs` 10.0.0, transitive via Kentico.Xperience.Core 31.9.1 | Clean dependency audit before v0.3.0 | Choose between a central pin in `Directory.Packages.props` and a Kentico update |
| Confirm two decisions | 14-02 landed as one bundled commit (`6e67cc5`); a range change in the All clicked items panel fires two requests | Closes #23 item 4 | Approve or ask for a fix |

## 3. Deferred issues opened this phase

| Issue | Owner |
|-------|-------|
| No backfill of `ItemDaily` before v8 | 14-01, KNOWN-LIMITATIONS (by design) |
| 14-02 DB tests not executed | 14-02 → before the v0.3.0 release |
| Rules-overhead performance test over budget under load | 14-02 → quiet-machine re-run |
| All clicked items: two requests on a range change | 14-02, minor |
| P14-01: visitor events can skew item popularity | Phase 19, before the popularity boost uses `ItemDaily` |
| P14-08: Pkcs advisory | Josh, before the v0.3.0 release |

## 4. End-user validation — for Josh to perform

**Pre-flight:**
- Host CommunityProjects at http://localhost:27340, restarted on main @ 8782193.
- Admin client: run `npm start` in `src/XperienceCommunity.Lucene.Suite.Admin/Client` (Debug).
- Signed in as an admin with Site search → Manage rules.

**Top clicked items card** (Site search → index 4 → Search reports, last 30 days, then the seeded range):
1. The card shows "Popular content / Top clicked items", a summary line, and at most 10 rows.
   - Expected: the rows match `SELECT TOP 10 ItemGuid, SUM(ClickedSearches) … FROM LuceneSuite_ItemDaily WHERE IndexName = … AND Day BETWEEN … GROUP BY ItemGuid ORDER BY 2 DESC, ItemGuid`.
2. Click the Conversions, Avg. position and Clicked searches headers, using the keyboard (Enter) for each.
   - Expected: the order changes, focus stays on the header, and aria-sort is set. Avg. position sorts ascending with "—" last.
3. Pick a range with no data.
   - Expected: "No clicked items in this period." and no Show all button.

**All clicked items panel:**
1. Click **Show all**.
   - Expected: the panel opens with 25 per page; 10/25/50 and paging work.
2. Close the panel.
   - Expected: focus returns to **Show all**.

**Pin / Hide in a new rule:**
1. On an indexed row, click **Pin in a new rule**.
   - Expected: rule create for the same index with the item in Pinned items at position 1, empty conditions, and the warning "No conditions: this rule applies to every search." Leave without saving, then check the Rule table: no new row.
2. Repeat with **Hide in a new rule**.
   - Expected: the item is in Hidden items.
3. Go back and reload the create page.
   - Expected: it opens empty, because the handoff is single-use.

**Edge cases:**
- **Item not in the index:** a row whose item was removed from the index shows its GUID with a "Not in this index" tag, and both actions are disabled with the reason in their title.
- **Without Manage rules:** both actions are disabled with "Requires Site Search → Manage rules".
- **Failure:** stop the host mid-session and change the sort. Expected: the card fades, shows the alert and **Retry**, and works again after a restart. On a first load that fails, no empty table appears.

**Visual check:**
- At about 670 and 1269 px content width: no horizontal overflow, rows stay on one line, the Actions column fits, and the card matches the other report cards (Paper, heading level 2, tokens).

## 5. Security — AEGIS phase audit

Report: [14-SECURITY-AUDIT.md](14-SECURITY-AUDIT.md) (899ef28, triage 8782193).

| Outcome | Count | Findings |
|---------|-------|----------|
| Confirmed and fixed | 0 | No SEC-FIX plan needed |
| Rejected | 4 | P14-02 test-only SQL helpers; P14-03 ORDER BY is not injectable; P14-05 handoff is bounded; P14-06 no PII |
| Accepted risk | 5 | P14-01 (Phase 19); P14-04 and P14-07 (existing report posture); P14-08 (Josh, pre-existing); P14-09 (dev dependencies only) |
| Still open | 1 | P14-08, also listed in section 2 |

**Tools that did not run:**
- **SonarQube:** the token cannot create this project, and C# needs the MSBuild scanner.
- **Checkov:** no infrastructure-as-code in the repo.
- **Semgrep:** ran, but parsed only part of `SearchReportService.cs`, so the SQL conclusion rests on reading the code.
- **AEGIS:** ran as a single session; the specialist and devil's-advocate sessions were not run.

**Per-plan review:**
- 14-02 `/code-review` (high), 5 findings:
  - Task 3 not done in APPLY → docs were already in 7e42481; host checks moved to #23.
  - DB tests not run → accepted risk.
  - First-load failure rendered an empty table → fixed in 53505b4.
  - Double request on a range change → deferred.
  - Bundled commit → recorded.
- `/security-review` was not required by either plan's skills.

## 6. Decisions auto-approved this phase

| Decision |
|----------|
| 2026-10-13: Enterprise audit on 14-01-PLAN.md. 1 must-have and 1 strongly-recommended applied: the rollup gate is `installed != Version`. |
| 2026-10-13: Enterprise audit on 14-02-PLAN.md. 1 must-have and 1 strongly-recommended applied: the no-conditions warning; TopItems sort mapped to fixed ORDER BY expressions. |
| 2026-10-13: 14-02 DB tests not executed in review (connection string unset); risk accepted by Josh, re-run before release. |
| 2026-10-13: 14-02 review signed off with one fix (53505b4). APPLY was done by Josh on main, so there was no agent report. |
| 2026-10-13: Phase 14 AEGIS audit triaged: 0 confirmed, 4 rejected, 5 accepted risk. P14-01 assigned to Phase 19; P14-08 goes to Josh. |
