# Enterprise Plan Audit Report

**Plan:** .paul/phases/06-widgets-package/06-01-PLAN.md
**Audited:** 2026-10-11
**Verdict:** Conditionally acceptable → acceptable after the applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. This is the first anonymous, public write endpoint in the suite. The plan gets the shape right: validation through the logger, short 400 responses, size limits, no echo, no identity stored, and a rate limit.

The way it planned to rate-limit would misbehave inside a host application. Calling `AddRateLimiter` from a library and setting a rejection status code changes the host's own `RateLimiterOptions`. That silently alters how the host's other limiters respond. It also makes the limit depend on the host remembering `UseRateLimiter()`. If the host forgets, enforcement silently disappears.

A widget package must not reconfigure its host. With a self-contained limiter, I would approve the plan.

## 2. What Is Solid

- **The logger is the single validator.** It throws ArgumentException synchronously, which the endpoint maps to 400, and storage never throws (02-02). The endpoint adds no second set of rules.
- **Accepts both text/plain and JSON.** `sendBeacon` with a string works same-origin without a CORS preflight.
- **Nothing visitor-identifying is stored.** The IP is used only as a partition key in memory.
- **The JS client is tested against the same vectors** as the C# minter and the rollup. One source of truth.
- **The settings endpoint publishes only non-sensitive merge and attribution values.** Clients stay in step with server configuration.
- **sr= parsing never throws and bounds its input length.**

## 3. Enterprise Gaps Identified

1. **Host configuration mutated.** `AddRateLimiter` and the global rejection status code would change behaviour outside the package. The limit also depends on the host calling `UseRateLimiter`.
2. **Proxy/CDN deployments.** Without forwarded headers every visitor shares one IP, so legitimate events are dropped at 60 per minute with no visible signal.
3. **Body limit enforced only by Content-Length.** Chunked bodies would bypass it.
4. **eventId optional or required was ambiguous** against the contract.
5. **Forged events.** Anyone can post well-formed events; only rollup validation and the rate limit bound them.
6. **No fingerprinting on the static JS asset.**
7. **Rate limit is per process,** not across a web farm (already in the plan's KNOWN-LIMITATIONS).

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Host rate-limiter configuration mutated (gap 1) | AC-1, Task 1 action + tests | The endpoint owns a singleton `PartitionedRateLimiter` and acquires a lease in its handler. No `AddRateLimiter`, no `RateLimiterOptions` changes, no `UseRateLimiter` dependency. Tests: the limit works without `UseRateLimiter`, and the host's options stay at their defaults. |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Proxy/CDN shared IP (gap 2) | AC-2, Task 1 | `EventsPerMinute = 0` disables the limiter. A throttled warning on rejections names the forwarded-headers configuration. |
| 2 | Chunked bodies (gap 3) | AC-2, Task 1, tests | Bounded buffered read (at most 1 024 bytes), with a chunked 2 KB test returning 413. |
| 3 | eventId semantics (gap 4) | AC-2, tests | Optional, as the contract says; the logger mints one when it's missing. Tested. |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Forged events (gap 5) | The rollup validates search existence, the attribution window and the position on the page. Damage is bounded to plausible events within the rate limit. Recorded in KNOWN-LIMITATIONS. |
| 2 | Asset fingerprinting (gap 6) | Host concern (static assets mapping). Recorded. |
| 3 | Per-process limit (gap 7) | Already in the plan's KNOWN-LIMITATIONS; a distributed limiter would need shared state the suite avoids. |

## 5. Audit & Compliance Readiness

- **Evidence:**
  - In-process endpoint tests: status codes, limits, the host-isolation proof, chunked bodies, settings.
  - Node vector tests.
  - Pack output proving the static asset ships.
- **Privacy:** no identity is stored, the IP stays in memory only, and responses never echo input.
- **Silent failures:** rate-limit rejections log a throttled warning instead of dropping events invisibly.
- **Host safety:** the package adds endpoints and services only and leaves host-wide middleware configuration alone.

## 6. Final Release Bar

- **Must be true before shipping:**
  - The host's rate-limiter configuration is untouched.
  - The limit is enforced without host middleware.
  - Bodies are bounded in all cases.
  - The JS passes every vector.
  - The API names are reviewed at the checkpoint.
- **Remaining risks:**
  - Plausible forged events within the limit.
  - The limit is per process.
  - The asset is not fingerprinted.
- **Sign-off:** I would sign this plan as amended.

---

**Summary:** Applied 1 must-have + 3 strongly-recommended upgrades. Deferred 3 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
