---
phase: 01-foundation
plan: 01
subsystem: infra
tags: [msbuild, cpm, lock-files, kentico-lucene, lucene-48]

requires: []
provides:
  - Isolated Suite build (global.json, Directory.Build.props, Directory.Packages.props)
  - Two empty projects (XperienceCommunity.Lucene.Suite, XperienceCommunity.Lucene.Suite.Admin)
  - xbk references both (Admin conditional on SeparatedAdmin=false)
  - xbk analyzes with LUCENE_48
affects: [01-02 Search API, 01-03 Admin application]

tech-stack:
  added: [Kentico.Xperience.Core 31.9.1, Kentico.Xperience.Admin 31.9.1, Kentico.Xperience.Lucene.Core 15.0.5]
  patterns: [nearest Directory.*.props wins (isolation), CPM + lock files, 31.9.1 declared floor]

key-files:
  created: [global.json, Directory.Build.props, Directory.Packages.props, .gitignore, LICENSE, XperienceCommunity.Lucene.Suite.slnx, src/*/*.csproj, src/*/packages.lock.json]
  modified: [../xbk/xbk.csproj, ../xbk/Program.cs, ../xbk/packages.lock.json, ../XperienceCommunity.sln]

key-decisions:
  - "XPSEARCH002 added to xbk NoWarn (user decision a): pre-existing build break, not caused by this plan"
  - "Suite projects nested in the pre-existing empty LuceneSuite solution folder instead of a new Suite folder"

duration: ~6h wall clock (incl. audit, checkpoint wait)
completed: 2026-10-09T01:43:50Z
---

# Phase 1 Plan 01: Skeleton + xbk wiring Summary

**The Suite repo builds standalone with 0 warnings on its own SDK/CPM config, and xbk builds against it alongside XpSearch with Lucene resolved to 15.0.5 and analysis switched to LUCENE_48.**

## Acceptance Criteria Results

| Criterion | Status | Evidence |
|-----------|--------|----------|
| AC-1: Suite builds in isolation | Pass | `dotnet build XperienceCommunity.Lucene.Suite.slnx -c Release` → 0 warnings, 0 errors, net10.0; Kentico.Xperience.Core 31.9.1, Kentico.Xperience.Lucene.Core 15.0.5; both packages.lock.json committed (4a69ad5); `dotnet restore --locked-mode` passed. NU1901–NU1904: none reported. |
| AC-2: Coexists with XpSearch in xbk | Pass (after user decision a) | No NU1107/NU1608/NU1605. Baseline build already failed (1 error XPSEARCH002, pre-existing). After NoWarn += XPSEARCH002: exit 0, 0 errors, 279 warnings = baseline 279 (normal baseline's extra 131 were MSB3061 file-lock warnings from a running xbk.exe, not code warnings). Distinct codes identical (CS86xx/CS9107). Evidence: project-sidecar/backups/01-01/baseline.txt. |
| AC-3: SeparatedAdmin excludes Admin | Pass | SeparatedAdmin=true build exit 0, 279 warnings; output contains Suite.dll, no Suite.Admin.dll; `-getItem:ProjectReference` with SeparatedAdmin=true lists no Suite.Admin. Baseline for this mode also failed pre-change on XPSEARCH002 (recorded). |
| AC-4: LUCENE_48 and search works | Pass | `.SetAnalyzerLuceneVersion(LuceneVersion.LUCENE_48)` added; XpSearch had no existing call (no duplicate). User approved checkpoint: boot OK, dancinggoat index rebuilt, search returns results. |
| AC-5: Untracked host edits reversible | Pass | Byte-identical backups of xbk.csproj, Program.cs, packages.lock.json, XperienceCommunity.sln in project-sidecar/backups/01-01/ taken before edits; diffs below. |

## Task Results

| Task | Result | Commit |
|------|--------|--------|
| 0: Baseline + backups | Done; found both xbk builds already broken (XPSEARCH002) | n/a (outside repo) |
| 1: Isolated Suite build + two projects | Done as specified | `4a69ad5` |
| 2: xbk references + LUCENE_48 | Done with 2 deviations (below) + user-approved XPSEARCH002 NoWarn | n/a (outside repo) |
| 3: Human verify | Approved | n/a |

## Deviations from Plan

| # | Deviation | Why | Impact |
|---|-----------|-----|--------|
| 1 | xbk.csproj NoWarn += XPSEARCH002 | Baseline showed xbk already failed on XpSearch's [Experimental] `UseMcp()`; user chose option (a) | One line outside plan scope in xbk.csproj; opts xbk into an experimental XpSearch API |
| 2 | Suite projects nested under existing empty "LuceneSuite" solution folder, not a new "Suite" folder | Folder already existed for this purpose | Cosmetic |
| 3 | `dotnet sln` re-sorted 8 existing NestedProjects lines in XperienceCommunity.sln | Tool behavior | Noise only; same 8 mappings |
| 4 | `--no-incremental` baseline builds deleted unlocked files from xbk/bin/Debug/net10.0 while the user's xbk.exe was running | Environment | User told to restart xbk; no source affected |

Skill audit: /dotnet:csharp-refactoring (required for the Program.cs edit) — invocation not evidenced in the APPLY record; gap logged in STATE.md. The edit is a one-call builder-chain addition.

## Untracked File Diffs (outside the Suite repo)

### xbk/xbk.csproj
```diff
@@ -4,7 +4,7 @@
-    <NoWarn>$(NoWarn);KXE0004;KXE0006;XPSEARCH001</NoWarn>
+    <NoWarn>$(NoWarn);KXE0004;KXE0006;XPSEARCH001;XPSEARCH002</NoWarn>
@@ -27,6 +27,8 @@
     <ProjectReference Include="..\..\..\Personal\xperience-community-lucene-instant-search\src\XperienceCommunity.Lucene.Search.Widgets\XperienceCommunity.Lucene.Search.Widgets.csproj" />
+    <ProjectReference Include="..\XperienceCommunity.Lucene.Suite\src\XperienceCommunity.Lucene.Suite\XperienceCommunity.Lucene.Suite.csproj" />
+    <ProjectReference Include="..\XperienceCommunity.Lucene.Suite\src\XperienceCommunity.Lucene.Suite.Admin\XperienceCommunity.Lucene.Suite.Admin.csproj" Condition="'$(SeparatedAdmin)' == 'false'" />
   </ItemGroup>
```

### xbk/Program.cs
```diff
@@ -14,6 +14,7 @@
 using Kentico.Xperience.Mjml;
+using Lucene.Net.Util;
 using Microsoft.AspNetCore.Identity;
@@ -67,7 +68,8 @@
 builder.Services
     .AddKenticoLucene(lucene => lucene
-        .RegisterStrategy<MySearchIndexingStrategy>(nameof(MySearchIndexingStrategy)), builder.Configuration)
+        .RegisterStrategy<MySearchIndexingStrategy>(nameof(MySearchIndexingStrategy))
+        .SetAnalyzerLuceneVersion(LuceneVersion.LUCENE_48), builder.Configuration)
     .AddXpSearch(features =>
```

### XperienceCommunity.sln
```diff
@@ -41,6 +41,10 @@
 Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "LuceneSuite", "LuceneSuite", "{899AB3C5-6467-49EE-AFBD-C36E96E28203}"
 EndProject
+Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "XperienceCommunity.Lucene.Suite", "XperienceCommunity.Lucene.Suite\src\XperienceCommunity.Lucene.Suite\XperienceCommunity.Lucene.Suite.csproj", "{D4C4CC59-BBC4-4C70-AE69-8846A6518390}"
+EndProject
+Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "XperienceCommunity.Lucene.Suite.Admin", "XperienceCommunity.Lucene.Suite\src\XperienceCommunity.Lucene.Suite.Admin\XperienceCommunity.Lucene.Suite.Admin.csproj", "{28CCAB19-834F-4FC6-826A-9882C67C21B2}"
+EndProject
@@ -207,6 +211,30 @@
+		(24 ProjectConfigurationPlatforms lines: Debug/Release x Any CPU/x64/x86, ActiveCfg + Build.0, for D4C4CC59 and 28CCAB19, all mapped to Any CPU)
@@ -218,12 +246,14 @@  NestedProjects
-		{7E7D5C8D-040A-416D-9619-F3063A27F77B} = {FBA66846-B2DA-4601-97DA-BFE96530C751}
-		{28087E9B-A36F-4237-8B7B-7D82316A15F8} = {7E7D5C8D-040A-416D-9619-F3063A27F77B}
-		{48AE39BA-588B-4A24-9F47-2CB19EBD240C} = {7E7D5C8D-040A-416D-9619-F3063A27F77B}
 		{D841DC29-F5D3-4798-928C-724DA61E0DF9} = {7E7D5C8D-040A-416D-9619-F3063A27F77B}
+		{FC3E93A1-33BE-4421-847E-E86661252327} = {7E7D5C8D-040A-416D-9619-F3063A27F77B}
+		{28087E9B-A36F-4237-8B7B-7D82316A15F8} = {7E7D5C8D-040A-416D-9619-F3063A27F77B}
 		{B390A871-C185-4B4E-940A-988126847BA5} = {7E7D5C8D-040A-416D-9619-F3063A27F77B}
+		{48AE39BA-588B-4A24-9F47-2CB19EBD240C} = {7E7D5C8D-040A-416D-9619-F3063A27F77B}
 		{8133EC1F-0B28-4A3E-ACBF-015F3B297AE1} = {7E7D5C8D-040A-416D-9619-F3063A27F77B}
-		{FC3E93A1-33BE-4421-847E-E86661252327} = {7E7D5C8D-040A-416D-9619-F3063A27F77B}
+		{7E7D5C8D-040A-416D-9619-F3063A27F77B} = {FBA66846-B2DA-4601-97DA-BFE96530C751}
+		{D4C4CC59-BBC4-4C70-AE69-8846A6518390} = {899AB3C5-6467-49EE-AFBD-C36E96E28203}
+		{28CCAB19-834F-4FC6-826A-9882C67C21B2} = {899AB3C5-6467-49EE-AFBD-C36E96E28203}
```
The NestedProjects removals/additions of existing GUIDs are a re-sort (same mappings); the only new mappings are the last two lines.

### xbk/packages.lock.json (summary)
The file on disk (last written 2026-10-08 21:00, by the SeparatedAdmin=true verification build) reflects the **SeparatedAdmin=true** graph, not the default one:
- Added: `xperiencecommunity.lucene.suite` Project entry (Kentico.Xperience.Core [31.9.1, ), Kentico.Xperience.Lucene.Core [15.0.5, )).
- `Kentico.Xperience.Admin` moved from `Direct` to `CentralTransitive` (same 31.9.1, same hash) because the conditional admin reference is off in that mode.
- No `xperiencecommunity.lucene.suite.admin` entry (excluded in that mode).
- No other package version changed.
The next default (`SeparatedAdmin=false`) restore rewrites it back to Admin `Direct` + both Suite project entries. Harmless unless something restores xbk with `--locked-mode` in the default mode before that happens. See open item 3.

## Open Items (need user action)

1. **LICENSE holder unconfirmed.** LICENSE copied from XperienceCommunity.GEO: "Copyright (c) 2026 Citro Digital". User approved the checkpoint but did not explicitly confirm the holder.
2. **Root Directory.build.targets still applies to the Suite.** `.targets` resolve independently of `.props`, so the Suite's own Directory.Build.props does not stop the solution-root Directory.build.targets from importing. Harmless today (only Content Update rules for lock/package.json), but a Suite-only clone (e.g. CI) will not have it, so local and CI builds can diverge later. Fix candidate: empty Suite `Directory.Build.targets` in a later plan.
3. **xbk/packages.lock.json is in SeparatedAdmin=true shape.** Run a plain `dotnet restore xbk/xbk.csproj` (or normal build) to regenerate the default-mode lock file. Two build modes sharing one lock file will keep flipping it; worth a decision if xbk ever restores in locked mode.

## Deferred (from audit)

| Item | Revisit |
|------|---------|
| NuGet PackageId / prefix availability | Release phase |
| CI pipeline | Release phase |
| Hard-coded Management API secret, xbk/Program.cs:65 | Outside Suite; user to move to user secrets |
| Root global.json pins 8.0.101 | Outside Suite; Suite global.json makes it irrelevant |

## Next

Plan 01-02: Search API over `ILuceneSearchService` + test project. Phase 1 progress: 1 of 3 plans.

---
*Completed: 2026-10-08 (local)*
