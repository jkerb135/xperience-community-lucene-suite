---
phase: 06-widgets-package
plan: 01
subsystem: api
tags: [aspnetcore, minimal-api, rate-limiting, static-web-assets, javascript, analytics]

requires:
  - phase: 02-event-capture
    provides: ISearchInteractionLogger (sync validation, never-throwing storage), client contract + shared vectors, SuiteSearchOptions.Analytics
provides:
  - XperienceCommunity.Lucene.Suite.Widgets Razor class library (packs with _content/XperienceCommunity.Lucene.Suite.Widgets/lucene-suite.js)
  - AddLuceneSuiteWidgets + MapLuceneSuiteEvents (POST events, GET settings)
  - SuiteResultReference (sr= parameter)
  - lucene-suite.js contract client (tracker, beacons, settings, sr helpers)
affects: [06-02 tag helpers, 06-03 page builder widgets + host wiring, v0.2 reports]

tech-stack:
  added: [Microsoft.AspNetCore.TestHost 10.0.2 (tests only)]
  patterns: [package-owned PartitionedRateLimiter acquired in the handler, bounded body read, node --test against shared vectors]

key-files:
  created:
    - src/XperienceCommunity.Lucene.Suite.Widgets/Events/SuiteEventsEndpoints.cs
    - src/XperienceCommunity.Lucene.Suite.Widgets/Events/SuiteEventsRateLimiter.cs
    - src/XperienceCommunity.Lucene.Suite.Widgets/Events/SuiteResultReference.cs
    - src/XperienceCommunity.Lucene.Suite.Widgets/wwwroot/lucene-suite.js
    - tests/XperienceCommunity.Lucene.Suite.Tests/SuiteEventsEndpointTests.cs
  modified:
    - src/XperienceCommunity.Lucene.Suite/XperienceCommunity.Lucene.Suite.csproj
    - docs/analytics-client-contract.md

key-decisions:
  - "Events rate limit owned by the package (no AddRateLimiter/UseRateLimiter); EventsPerMinute 0 = off"
  - "Public API and endpoint shape approved at checkpoint: /lucenesuite/events, sr=, JS exports incl. norm/osa/typo"

patterns-established:
  - "Library endpoints never mutate host-wide middleware options"
  - "JS client tested with node --test directly against docs/analytics-test-vectors.json"

duration: ~1.5h
completed: 2026-10-11T00:00:00Z
description: "Optional Widgets package: package-rate-limited click/conversion endpoint + settings endpoint, sr= result reference, and a dependency-free contract JS client passing all shared vectors"
type: Summary
about: "XperienceCommunity.Lucene.Suite"
---

# Phase 6 Plan 01: Widgets Package Skeleton Summary

**New `XperienceCommunity.Lucene.Suite.Widgets` package: `MapLuceneSuiteEvents()` records browser clicks/conversions through `ISearchInteractionLogger` behind its own per-IP limit, serves the server's merge/attribution settings, and ships `lucene-suite.js` — the analytics contract client, proven against all 20 shared vectors.**

## Performance

| Metric | Value |
|--------|-------|
| Duration | ~1.5 h |
| Completed | 2026-10-11 |
| Tasks | 3 of 3 (2 auto + checkpoint approved) |
| Files | 9 created, 6 modified |

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Package skeleton | Pass | Razor SDK, net10.0, warnings as errors, XML docs, core-only reference; pack contains `staticwebassets/lucene-suite.js` with BasePath `_content/XperienceCommunity.Lucene.Suite.Widgets`; missing AddLuceneSuite throws; host `RateLimiterOptions` untouched (test: RejectionStatusCode 503, no IConfigureOptions). Package metadata: none to mirror (core has none) — deferred |
| AC-2: Events endpoint | Pass | 202/400/413/405/429 tests; text/plain accepted; chunked 2 KB → 413; limit works without UseRateLimiter; EventsPerMinute 0 → 100 requests all 202; eventId optional (logger receives null); no-store; no echo. The AC's "Given … app.UseRateLimiter()" is superseded by the audit |
| AC-3: Client settings | Pass | Values read from options (non-default values in test), eventsUrl, `public, max-age=300`, not rate-limited |
| AC-4: Result reference | Pass | Round trip + 15 rejection cases (incl. D-format GUID, signs, whitespace, overflow, >64 chars) |
| AC-5: JS client | Pass | 20 vector cases (ordinal + seq per step, per-case settings, click seq present), OSA, v4 GUID, sr parity with C# rejections, sendBeacon → fetch(keepalive) fallback with stable eventId; memory-only state |

## Verification Results

```
dotnet build XperienceCommunity.Lucene.Suite.slnx   → 0 Warning(s), 0 Error(s)
dotnet test (with LUCENESUITE_TEST_CONNECTIONSTRING) → Passed 536, Skipped 3 (opt-in env), Failed 0
  SuiteEventsEndpointTests                           → 34 passed
npm test (src/…Widgets/js)                           → 25 pass, 0 fail
dotnet pack src/…Widgets                             → staticwebassets/lucene-suite.js, BasePath _content/XperienceCommunity.Lucene.Suite.Widgets; dependency XperienceCommunity.Lucene.Suite
```

## Accomplishments

- One-line host wiring for browser analytics (`AddLuceneSuiteWidgets()` + `MapLuceneSuiteEvents()`), safe to drop into a site with its own rate limiters.
- Contract client with no dependencies or build step, held to the same vectors as the C# reference minter and the rollup engine.
- `sr=` parsing on both sides with identical rules, ready for 06-02 result links and conversion pages.

## Files Created/Modified

| File | Change | Purpose |
|------|--------|---------|
| `src/XperienceCommunity.Lucene.Suite.Widgets/XperienceCommunity.Lucene.Suite.Widgets.csproj` | Created (18) | Razor class library; js/ excluded from content |
| `…Widgets/LuceneSuiteWidgetsServiceCollectionExtensions.cs` | Created (40) | SuiteWidgetsOptions + AddLuceneSuiteWidgets |
| `…Widgets/Events/SuiteEventsEndpoints.cs` | Created (98) | POST events / GET settings, bounded body read |
| `…Widgets/Events/SuiteEventsRateLimiter.cs` | Created (50) | Package-owned per-IP fixed window + throttled proxy warning |
| `…Widgets/Events/SuiteResultReference.cs` | Created (47) | sr= Format/TryParse |
| `…Widgets/Events/SuiteClientSettings.cs` | Created (32) | Settings payload from SuiteSearchOptions |
| `…Widgets/wwwroot/lucene-suite.js` | Created (134) | Contract JS client |
| `…Widgets/js/lucene-suite.test.mjs`, `js/package.json` | Created (81, 6) | Node tests on shared vectors |
| `…Widgets/packages.lock.json` | Created | Lock file (repo uses lock files) |
| `tests/…/SuiteEventsEndpointTests.cs` | Created (253) | In-process endpoint + sr tests |
| `src/XperienceCommunity.Lucene.Suite/XperienceCommunity.Lucene.Suite.csproj` | Modified | InternalsVisibleTo Widgets |
| `tests/…/XperienceCommunity.Lucene.Suite.Tests.csproj`, `packages.lock.json` | Modified | TestHost + Widgets reference |
| `Directory.Packages.props`, `XperienceCommunity.Lucene.Suite.slnx` | Modified | TestHost version; project added |
| `docs/analytics-client-contract.md` | Modified | Widgets package section; settings endpoint; EventsPerMinute |

## Decisions Made

| Decision | Rationale | Impact |
|----------|-----------|--------|
| Reuse core `LogThrottle` and `SearchExecutor.MaxResultWindow` via InternalsVisibleTo | No duplicate throttle/limit logic | Core csproj gains one line |
| Endpoint accepts any content type that parses as JSON | AC requires JSON + text/plain; rejecting others adds code without protection (curl can send anything) | — |
| Click without position → 400 in the endpoint (logger's click signature requires it) | Only rule the logger can't express | — |
| JS exports `norm`, `osa`, `typo` | Reusable by custom UIs; approved at checkpoint | Public JS surface |

## Deviations from Plan

### Summary

| Type | Count | Impact |
|------|-------|--------|
| Auto-fixed | 2 | Essential |
| Scope additions | 1 | Core csproj InternalsVisibleTo (not in files_modified) |
| Deferred | 1 | Package metadata |

**Total impact:** Essential fixes, no scope creep.

### Auto-fixed Issues

**1. Oversized body read as empty (400 instead of 413)**
- **Found during:** Task 1 (chunked-body test)
- **Issue:** `length > Max ? null : buffer.AsMemory(...)` — `null` converted to `Memory<byte>` through the implicit `byte[]` operator, giving an empty body instead of null.
- **Fix:** explicit `if … return null;` branch.
- **Verification:** chunked 2 KB → 413; probe confirmed on real Kestrel before the fix that it was the endpoint, not TestServer.

**2. Node test hang**
- **Found during:** Task 2
- **Issue:** `t.mock.property(globalThis, "navigator", …)` on Node 24's navigator getter never let the test finish.
- **Fix:** define/restore the property via `Object.defineProperty` + `t.after`.
- **Verification:** `npm test` 25/25 in < 100 ms.

### Deferred Items

- NuGet metadata (license, description, repository, readme) for core and Widgets — KNOWN-LIMITATIONS; release phase.

## Issues Encountered

| Issue | Resolution |
|-------|------------|
| First `dotnet pack -o /c/tmp/...` resolved to `F:\c\tmp\widgetspack` | Deleted that folder (only `F:\c\tmp`, pre-existing `F:\c\Users` untouched); repacked to `C:/tmp` |

## Skill Audit

/dotnet:csharp-refactoring (required): invoked this session ✓. No admin UI (design-screen n/a).

## Next Phase Readiness

**Ready:**
- 06-02 tag helpers can emit `SuiteResultReference` links, include `_content/XperienceCommunity.Lucene.Suite.Widgets/lucene-suite.js` and post click beacons to the mapped events URL (`SuiteClientSettings.EventsUrl`).

**Concerns:**
- Per-process limit and forgeable events (KNOWN-LIMITATIONS); asset not fingerprinted — use the host's static-assets versioning in 06-02.
- Host must configure forwarded headers behind a proxy (warning logged otherwise) — note for 06-03 host wiring.

**Blockers:** None

---
*Phase: 06-widgets-package, Plan: 01*
*Completed: 2026-10-11*
