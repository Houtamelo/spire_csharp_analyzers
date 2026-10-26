# Correctness fixes following the quality review

User approved all seven proposed fixes on 2026-10-26. This authorizes the
necessary diagnostic, generated inliner, validation, timeout, package, and
benchmark-input behavior changes. Preserve unrelated behavior and the existing
custom source formatting.

Starting checkpoint: `f4b1dfa` (completed indentation fix, 2,127 tests passing,
77 generated files unchanged). Worktree clean before this work began.

## Batch contracts

1. Package the standalone analyzer's PatternAnalysis dependency alongside its
   analyzer DLL. Verify both complete/incomplete switches through a real packed
   package consumer with an isolated cache and no analyzer dependencies borrowed
   from the main package.
2. Correct enum flags validation and wide enum-to-enum codefix casts. Range-check
   inputs, use underlying-type values and width-limited signed bits, cache masks,
   and preserve existing zero/named-negative acceptance rules. Test all eight
   backing types and execute fixed consumers.
3. Apply process deadlines to exit and both output readers. On timeout perform
   owned-group/tree cleanup, terminate readers, and bound cleanup itself. Test
   real descendants holding stdout/stderr after their parent exits.
4. Correct Skewed80 to choose variant zero 80% of the time and distribute the
   remainder across other variants. Fixed seeds, histogram/index validation,
   single-variant coverage, and fresh affected benchmark measurements. Preserve
   historical results and identify the corrected input generation explicitly.
5. Escape inliner identifiers, propagate supported generic constraints, and
   qualify hint names against namespace/nesting collisions. Compile and execute
   forwarders, including representative real Unity Mono/IL2CPP cases. Update
   only intentionally changed generated-output expectations.
6. Represent integral ranges exactly with BigInteger boundaries and preserve
   typed constants through parsing. Keep floating-point handling separate.
   Cover adjacent values above 2^53, extrema, and exclusive limits.
7. Recursively analyze nested property paths, preserving parent null/type tests
   and per-arm correlations. Unsupported patterns must not become wildcard
   coverage. Add finite-domain differential checks against compiled C# matching
   and keep compiler warnings visible when exhaustiveness cannot be proven.

Tests precede each implementation. Run focused checks, scoped C# formatting,
and the full SDK solution between batches; commit verified batches. Do not run
concurrent SDK builds that toggle shared portable-host assets. Final validation
includes package consumption, relevant Unity execution, generated-output review,
and updating the existing UnityShowcase local package. No publishing or pushes.

## Progress

Batch 1 complete: the standalone analyzer package now includes PatternAnalysis.
An isolated normal PackageReference consumer compiles a complete generated union
switch and rejects an incomplete one with SPIRE009, without AD0001 or analyzers
borrowed from the main package. Package bytes are checked against the build.

The full-suite gate exposed intermittent test-harness workspace application
failures. The code actions themselves returned the correct ChangedSolution;
codefix/refactoring harnesses now inspect that immutable result directly while
retaining all output and compilation assertions.

Validation: 2,128 SDK tests passed, zero skipped; all 77 fixed generated-output
controls unchanged. Evidence: `tmp/correctness-fixes-20261026/batch1-final/`.

Batch 2 complete: signed/unsigned flag inputs are range-checked, negative
members use correctly typed validation, and raw declared bits plus zero-member
metadata are cached. Enum-to-enum fixes preserve the source backing type. All
eight backing types have integration coverage; fixed consumers execute both
valid conversions and invalid-value rejection, including single-evaluation
controls. Three new native facts cover the runtime paths.

Validation: 2,152 SDK tests passed, zero skipped; all 77 fixed generated-output
controls unchanged. Evidence: `tmp/correctness-fixes-20261026/batch2-enums-final/`.
Unity execution of the new facts remains part of final validation.

Batch 3 complete: one monotonic deadline covers process exit and both redirected
streams. Cancellation stops awaiting inherited pipes independently of Mono's
reader cancellation support. Owned Linux groups are killed after parent exit,
cleanup is bounded, pending faults are observed, and log writes stop before
disposal. Eleven real-process regressions cover normal/partial output,
nonzero exit, inherited pipes, non-owned descendants, and hot stdout.

Validation: 2,163 SDK tests passed, zero skipped; all 77 fixed generated-output
controls unchanged. The portable core also builds with zero warnings/errors.
Evidence: `tmp/correctness-fixes-20261026/batch3-process-final/` and
`batch3-process-focused/`. Native host execution remains in final validation.

Batch 4 complete: Skewed80 now selects the first variant 80% of the time;
Uniform preserves its cycle and payload RNG sequence. A dedicated test project
covers all 17 fill representations, bounds, reproducibility, histograms, and
equal results across the seven match methods. Explicit no-op switch arms fix
fresh benchmark-build SPIRE015 errors while preserving the existing work.

Validation: 2,176 SDK tests passed across eight projects, zero skipped; 77 fixed
generated-output controls unchanged. All 14 affected Short measurements have
valid statistics. All six N=1,000 datasets contain 795 Idle values; the larger
control contains 79.946% Idle. Twelve historical artifacts remain byte-identical.
Results: `docs/benchmark-results/MATCH_CORRECTED_SKEWED80_2026-10-26.md`.
Evidence: `tmp/correctness-fixes-20261026/batch4-benchmark-final/` and
`batch4-match-bdn/`.

Batches 5–7 pending. Their regression matrices and staged test drafts are ready.
