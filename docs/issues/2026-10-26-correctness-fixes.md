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

From batch 5 onward, full gates use a separate validation worktree. Live user
edits added showcase assets outside the committed export inventory, causing five
unrelated package checks in the live checkout. Validation copies the fix sources
and tests with verified SHA-256 input hashes, retaining the committed showcase
inventory. It changes neither the package checks nor the user's example/PENDING
files. Evidence records the validation working directory and input manifest.

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

Batch 5 complete: inliners escape semantic identifiers, retain generic
constraints with canonical type binding, and normalize identifier matching
without broadening alias traversal. Bounded hint names combine Roslyn method
identity with SHA-256. Public generated names and existing body formatting stay
stable. Eighteen new snapshot pairs, seven compile/execute checks, and ten native
behavioral facts cover escaping, constraints, shadowing, and collisions.

Validation: 2,211 SDK tests passed across eight projects, zero skipped, in the
isolated checkout. All 77 source controls remain byte-identical; only the 24
approved performance Hint fields changed. Their Expected paths, source hashes,
case counts, and source baseline are unchanged. Evidence:
`tmp/correctness-fixes-20261026/batch5-inliner-isolated-ready/` and
`batch5-hint-metadata/`. Native execution remains in final validation.

Batch 6 complete: the eight fixed-width integral domains use exact BigInteger
intervals and typed Roslyn constants. Closed discrete ranges preserve adjacency,
extrema, negation, and gaps above 2^53. Nullable integral partitions and overlapping
numeric/boolean rows refine to disjoint atoms. Floating representations and their
existing policies remain separate. Internal unit callers now use exact factories
rather than a lossy compatibility layer.

Validation: 2,273 SDK tests passed across eight projects, zero skipped; all 77
source controls unchanged. PatternAnalysis passes 401 tests, including 36 new
integration cases and 26 interval tests. Its portable netstandard2.1 host builds
with zero warnings/errors. Evidence:
`tmp/correctness-fixes-20261026/batch6-integral-final/` and
`batch6-integral-focused/`.

Batch 7 complete: full member paths retain parent null/type constraints,
including extended property syntax. Struct/record union payload restrictions
remain correlated with their root discriminator. Unsupported operations retain
an explicit uncertainty marker through specialization, so they cannot prove
coverage or suppress compiler warnings. MissingVariants includes only root
variant names. Numeric cross-column partitions are disjoint; negated empty
regions retain correct set algebra. Nullable and non-nullable union matrix
construction share one implementation.

The initial focused run reproduced 14 failures out of 27 cases. Final coverage
adds 27 direct checker cases and 37 real compiler/suppressor cases, including
finite-domain execution comparisons, extended nullable paths, deconstruction,
logical restrictions, and isolated package consumers. The full gate caught and
prevented a proposed change to oblivious-reference nullability; the existing
DomainResolver policy and its original regression remain intact.

Validation: 2,337 SDK tests passed across eight projects, zero skipped; all 77
source controls remain byte-identical. An independent final review found no
remaining concrete false-proof path. Evidence:
`tmp/correctness-fixes-20261026/batch7-nested-corrected-final/`.

Native runtime validation passed on Unity 6000.5.6f1 Linux x64: Editor Mono
executed 99 cases and the IL2CPP player executed 101 cases. Both executed all
93 original behavioral facts, including the new flags and inliner regressions,
with exact fact-name inventory checks and zero failures/skips. These runs used
the frozen, verified batch-6 runtime snapshot; the completed batch-7 compiler
engine will receive a separate full Unity compiler-harness run. Evidence:
`tmp/correctness-fixes-20261026/native/` (XML, logs, runtime DLL hashes).

Final compiler-host and package validation remain pending.
