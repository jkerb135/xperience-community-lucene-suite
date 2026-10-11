# Enterprise Plan Audit Report

**Plan:** .paul/phases/03-rules-engine/03-05-PLAN.md
**Audited:** 2026-10-10
**Verdict:** Conditionally acceptable → acceptable after the applied upgrades

---

## 1. Executive Verdict

Conditionally acceptable. Separating a pure placement and paging function from the Lucene lookup is right, and adding pins to the match set with a zero-weight clause is the cheapest way to keep totals and facets honest (the user's choice).

One latent defect would have shipped visibly wrong results. Kentico Lucene indexes can cover several languages (`LuceneIndex.LanguageNames`), each with its own document per ItemGuid. The plan assumed one document per pinned item. In a multilingual index that gives:
- a pinned item shown twice, or a language variant left in the organic list
- an arbitrary document chosen for the pin
- a total that doesn't equal the number of hits you can page through

With that fixed, I would approve it for APPLY.

## 2. What Is Solid

- **A pure `PinMerger` with global positions,** tested without Lucene. Page boundaries can't distort placement.
- **The zero-weight, coord-disabled pinned clause,** with a test proving unpinned scores are unchanged. The plan names a fallback (a filter) if Lucene.Net normalizes differently.
- **No pins means byte-identical 03-04 behaviour and no extra queries.**
- **Lookups run inside `run(executor)`,** so 03-04's fail-open covers pin failures for free.
- **Respect-filters defaults to true,** per the research, for CMS content that may be restricted.
- **The fetch window is `page × pageSize + pins`,** which guarantees no short pages while results remain.

## 3. Enterprise Gaps Identified

1. **Several documents per ItemGuid (language variants).** The lookup cap of `pins.Count` truncates, there's no rule for choosing one, and the other variants could leak into the organic list.
2. **The total formula breaks with several documents.** "+1 per failing filter-ignoring pin" assumes one document per item, so totals drift from the pageable count.
3. **The filter check ran only for respect-filters pins,** but correct totals need the passing count whenever facet filters are applied.
4. **Zero-organic edge case untested:** a query with no organic matches but a pin.
5. **No test of the paging invariant** (total equals the hits you can page through).
6. **Analytics impact:** a search whose only hits are pins no longer counts as zero-result.
7. **Facet counts in multilingual indexes** count every language document of a pinned item.
8. **Each pinned search adds 2–3 Lucene queries.**

## 4. Upgrades Applied to Plan

### Must-Have (Release-Blocking)

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Several documents per ItemGuid (gap 1) | AC-3, Task 2 step 2–3, 5, tests | The lookup reads every document of every pinned guid. One deterministic hit per item: the organic-window document, else the lowest doc id that passes, else the lowest that exists. Every document with a pinned guid is removed from organic. Added a two-language test. |
| 2 | Totals must equal the pageable length (gaps 2, 3) | AC-3, Task 2 steps 3–4, tests | `TotalHits = run-1 total − passing pinned documents + available pins`. The filter check runs whenever facets are applied. Added an invariant test that concatenated pages hold exactly TotalHits hits. |

### Strongly Recommended

| # | Finding | Plan Section Modified | Change Applied |
|---|---------|----------------------|----------------|
| 1 | Zero-organic edge case (gap 4) | AC-3, tests | A query matching nothing plus one pin returns that pin with TotalHits = 1. |
| 2 | Limitations made explicit (gaps 6, 7) | Verification / KNOWN-LIMITATIONS | Record multilingual facet counting and the zero-result analytics semantics. |

### Deferred (Can Safely Defer)

| # | Finding | Rationale for Deferral |
|---|---------|----------------------|
| 1 | Multilingual facet counts include every language document of a pinned item (gap 7) | Requests have no language dimension yet (02-01 deferral). Totals are correct, and the facet skew is at most (languages − 1) per pinned item. |
| 2 | A search whose only hits are pins isn't zero-result (gap 6) | It matches what the visitor saw. v0.2 reports can add an "organic zero" metric if marketers want it. |
| 3 | 2–3 extra queries per pinned search (gap 8) | Covered by the extended p95 benchmark against the 10 ms budget. Pinned searches are a small share of traffic. |

## 5. Audit & Compliance Readiness

- **Evidence:** every AC clause, plus the paging invariant, maps to a test. The host checkpoint covers the real index.
- **Silent failures:** missing or filtered-out pins are dropped by design (documented); lookup failures fall back through the 03-04 path with `RulesFailed` and a throttled warning.
- **Reconstruction:** placement is deterministic for (outcome, index snapshot, request).
- **Access:** respect-filters by default keeps facet-restricted content out of pins unless a rule explicitly opts out.

## 6. Final Release Bar

- **Must be true before this ships:**
  - Pins show exactly once, at their global position.
  - TotalHits equals the pageable length in every scenario, multilingual included.
  - Unpinned scores are proven unchanged.
  - The p95 overhead with 300 pins stays under 10 ms.
  - The host checkpoint is approved.
- **Remaining risks:**
  - Multilingual facet counts are slightly inflated for pinned items.
  - Pin-only searches aren't counted as zero-result.
- **Sign-off:** I would sign this plan as amended.

---

**Summary:** Applied 2 must-have + 2 strongly-recommended upgrades. Deferred 3 items.
**Plan status:** Updated and ready for APPLY

---
*Audit performed by PAUL Enterprise Audit Workflow*
*Audit template version: 1.0*
