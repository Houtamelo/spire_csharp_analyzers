# Full code-quality review

Review baseline: `e51f2b5c465e477d7ba7d0390a5d92e90ec77002` (2026-10-25).
The baseline commit preserves all pre-existing changes; `git status --porcelain`
was empty before review began.

## Request and boundaries

Review the entire first-party codebase and integration tests for brittle code,
duplication, missing integration use-cases, and opportunities to simplify
maintenance. Complete the read-only review and record findings before making
implementation changes. Then implement worthwhile improvements autonomously.

Public behavior must remain unchanged, including diagnostics, code fixes,
generated source, serialization, and editor interaction. Run the test suite
between refactor batches. Existing defects requiring behavior changes are
report-only. Preserve generated output exactly, rather than relying solely on
syntax-equivalent snapshots.

The first-party scope includes `src/`, `tools/`, `tests/`, `benchmarks/`,
`examples/UnityShowcase/`, packaging/build configuration, and CI. Third-party
sample submodules (`examples/BossRoom`, `examples/ChopChop`) are dependency
boundaries; their integration is reviewed without refactoring upstream code.
Ignored build outputs, `tmp/`, and historical plans are not production source.

## Status

Read-only review completed before implementation. The findings below refer to
the baseline, including its line numbers. Implementation decisions are explicit;
the validation log records the subsequent batches separately.

All six accepted cleanup batches are implemented and committed. The final SDK
gate passes 2,115 tests with zero skipped; 77 fixed generated files remain
byte-identical. Behavior-changing defects below remain report-only, as required.

## Findings

## Highest-priority findings

1. **Q01 — The portable Unity test suite does not compile.**
   `tests/Houtamelo.Spire.SourceGenerators.Tests/Unity/UnitySerializationStateTests.cs:7,264-294`
   unconditionally uses `System.Runtime.Loader.AssemblyLoadContext`. The test
   project also targets `netstandard2.1` with `SpireUnityTestHost=true`, where
   this API is unavailable. The first full solution run reproduced seven failed
   `UnityHarnessRunnerTests`, all with CS0234/CS0246 from this file. The existing
   `UnityGeneratorTestHarness.cs:489-530` already separates host and CoreCLR
   loading. **Implement:** use that host boundary consistently; retain all tests.

2. **Q02 — Nested property patterns can be falsely considered exhaustive.**
   `src/Houtamelo.Spire.PatternAnalysis/Algorithm/PatternMatrix.cs:684-690,1064-1067`
   converts a nested recursive property pattern to a wildcard. A real Roslyn
   compilation of `x switch { { Child: { Flag: true } } => 1 }`, with nonnullable
   struct properties, produced zero missing regions despite `Flag=false` being
   uncovered. Input had no compiler errors. **Report-only:** repairing this
   changes analyzer results. Missing integration cases: nested leaf omission,
   complementary leaves, and nested nullability.

3. **Q03 — Numeric exhaustiveness loses integer precision.**
   `Domains/NumericDomain.cs:102-117` and `Algorithm/PatternMatrix.cs:1184-1192`
   represent integral boundaries as `double`. A real compilation of
   `long x` matched by `<= 9007199254740992L` and `>= 9007199254740994L`
   produced zero missing regions, missing the gap at `9007199254740993L`.
   **Report-only:** an exact numeric representation changes diagnostics and
   requires its own design. Add future differential cases for `long`, `ulong`,
   decimal, infinity, and adjacent endpoints above 2^53.

4. **Q04 — Valid inliner inputs can generate uncompilable source.**
   `SourceGenerators/Performance/Emit/InlinerStructEmitter.cs:117-146` does not
   escape method names. `[InlinerStruct] public static int @class(int x) => x;`
   compiled before generation and produced CS1001/CS1002 afterward. Separately,
   `Performance/Parsing/InlinerStructParser.cs:58-59` leaves constraints empty:
   `Echo<T>(T x) where T : unmanaged => x` compiled before generation and
   produced CS8377 afterward. **Report-only:** both fixes change generated code.
   Cover escaped declaring/member/parameter names and constrained methods in a
   future compile-and-execute matrix.
   The performance generators also construct hints from declaring-type and
   method simple names only (`InlinerStructGenerator.cs:33`,
   `InlinableTwinGenerator.cs:47`), unlike the DU generator's qualified hints.
   Same-named hosts in different namespaces/enclosing types need collision
   integration coverage; this collision risk is source-inspected, not reproduced
   in the execution harness above.

5. **Q05 — Signed flags can throw from the public Try API.**
   `src/Houtamelo.Spire/SpireEnum.cs:275-300`: for
   `[Flags] enum E { One=1, All=-1 }`, `TryFromFlags(-1, out _)` threw
   `ArgumentException` because `Enum.IsDefined` receives boxed `long` for an
   int-backed enum. `TryFromFlags(1, out _)` threw `OverflowException` while
   converting the negative named member to `ulong`. Both reproduced against
   the actual library. **Report-only:** returning a bool instead would change
   public behavior. Future coverage needs all eight backing types and signed
   high-bit members, alongside zero-member and unnamed-combination policies.

6. **Q06 — Process timeouts stop protecting output draining.**
   `tools/Spire.Tool.Core/ProcessRunner.cs:57-75` stops checking the deadline
   when the parent exits, then awaits stdout/stderr indefinitely. A real
   `bash -c 'sleep 1.5 & exit 0'` run with a 100ms timeout returned success
   after 1511ms because the descendant retained the pipes. **Report-only:**
   enforcing the deadline would change CLI/process results. The existing
   child-kill test covers a running parent, not this case.

7. **Q07 — The enum-to-enum code fix narrows wide values to int.**
   `src/Houtamelo.Spire.CodeFixes/FromEnumCodeFix.cs:61-72` always emits
   `(int)sourceEnum`, although `SpireEnum<T>.From` supports wider input types.
   A long/ulong enum value above the int range loses data. Existing
   `SPIRE016CodeFix/cases/NonFlags_EnumToEnum` covers an int-backed enum only.
   **Report-only:** preserve the existing transformation in this task. Future
   codefix tests should execute wide-backed conversions, not only compile them.

8. **Q08 — Missing-arm fixes assume every SPIRE009 diagnostic is a DU.**
   `AddMissingArmsCodeFix.cs:25-87,260-280` constructs `Subject.Variant`, while
   `SourceGenerators/Analyzers/ExhaustivenessAnalyzer.cs:64-84,106-122` also
   reports missing subtypes of arbitrary exhaustive classes/interfaces and
   missing null. This can emit nonexistent nested subtype names or `.null`.
   **Report-only:** changing action availability or output is forbidden here.
   Add future real-workspace tests for nonnested implementations and nullable
   record DUs, with compile-after-fix verification.

## Maintainability and compatibility gates

9. **Q09 — Structural snapshots do not protect generated text.**
   `GeneratorTestHelper.cs:150-157` normalizes whitespace and checks syntax
   equivalence; the snapshot bases depend on it. Dedicated readability tests
   cover selected conventions but cannot prove all source remains identical.
   **Implement:** capture exact outputs before emitter refactors and compare
   them afterward, including hint names, Unity support templates, JSON/schema,
   ordinary/generic/nested payloads, records, and both performance generators.
   Keep useful structural tests. Golden data must come from the baseline,
   never from a runtime expectation regenerated by the implementation under test.

10. **Q10 — Declaration formatting is copied across emitters.**
    `Emit/{SystemTextJsonEmitter,NewtonsoftJsonEmitter,JsonSchemaEmitter}.cs`
    duplicate escaped generic parameters, constraints, accessibility, and open
    generic formatting; `VariantPayloadEmitter` and `GeneratedAliasEmitter`
    duplicate some of those exact routines. Record/ToString/SourceBuilder have
    a deliberately different raw parameter path. **Implement:** share only
    identical algorithms and preserve raw-versus-escaped behavior explicitly.
    Do not introduce a general converter abstraction or a formatter postpass.

11. **Q11 — Performance parser metadata and diagnostic construction are copied.**
    `Performance/Parsing/{InlinerStructParser,InlinableParser}.cs` duplicate
    containing-type walks, declaration keywords, accessibility, symbol
    locations, and diagnostic coordinates. The two performance generators
    separately duplicate descriptor/location construction. **Implement:**
    factor identical metadata helpers and diagnostic presentation, preserving
    each parser's validation and descriptor selection. Keep the known omitted
    constraints and escaping defects unchanged under this task's boundary.

12. **Q12 — Common analyzer policy setup is repeated across rules.**
    `Rules/SPIRE001`, `SPIRE003`-`SPIRE008`, `SPIRE015` and `SPIRE016` repeat
    attribute/config lookup. `MustBeInitChecks.cs:96-112` and
    `OperationUtilities.cs:18-30` duplicate the same integral-zero classifier.
    **Implement:** small internal shared helpers, retaining each rule's
    early-return behavior, operation registration, symbol lookups, and exact
    attribute-inheritance semantics. No shared mutable compilation state.

13. **Q13 — Codefix arm builders duplicate record-pattern construction.**
    `AddMissingArmsCodeFix.cs:184-218,260-350` builds similar record patterns
    for expressions and statements; throwing expressions and statements must
    remain distinct. **Defer implementation:** existing coverage does not yet
    establish all trivia/capture-name contracts, and a broad common arm model
    would add abstraction for little reduction. Add targeted real-workspace
    characterization for `ExpandWildcardRefactoring` first.

14. **Q14 — Xvfb startup and cleanup are copied.**
    `tools/Spire.Tool.Core/BehavioralWorkflow.cs:182-213` and
    `JetBrainsWorkflow.cs:132-167` independently allocate a private display,
    drain errors, enforce allocation timeout, set display variables, and kill
    the process group. **Implement:** one internal display-session lifecycle;
    preserve each caller's messages, flags, environment overrides, and logs.
    Validate with real Xvfb processes plus a real Unity Editor invocation.

15. **Q15 — SourceBuilder indentation has no invariant check.**
    `Emit/SourceBuilder.cs:11-12` permits negative indentation, silently
    removing expected indentation until balance is restored. No reachable
    malformed generation was found. **Defer:** adding failure behavior is not
    needed for the accepted refactors; exact-output gates offer more value.

16. **Q16 — Several apparent duplicates have different semantics.**
    `GeneratedAliasEmitter.Safe`, `UnitySerializationEmitter.Safe`, drawer
    sanitization, raw/escaped type parameters, and exact/inherited attribute
    checks serve different contracts. `StructPropertyPatternBuilder` mirrors
    camelCase naming in the generator because CodeFixes intentionally has no
    project dependency. **Retain:** do not force these into one helper or add
    inter-project coupling. Test shared contracts through real generated APIs.

17. **Q17 — Reflection and display-string boundaries need focused protection.**
    `Parsing/UnitySerializationValidation.cs:364-365` recognizes List through
    `ToDisplayString()`. `FieldNameResolver.cs:29-48` independently rediscovers
    variant members. Drawer/runtime bridge reflection names span generated
    code and `src/Houtamelo.Spire/UnitySerialization.cs`. **Defer changes:**
    symbol-identity or discovery rewrites may change accepted inputs or output.
    Preserve native alias/cycle/Undo/focus/high-stripping tests; add future
    graph/cycle, aliased-list, and overload-discovery characterization first.

18. **Q18 — Inliner alias rewriting tracks identifier text across nested scopes.**
    `Performance/Emit/InlinableBodyRewriter.cs:67-109` scans all descendant
    locals into one alias dictionary; it does not model scope exit, reassignment,
    or local-function shadowing. This is a verified implementation limitation,
    not an executed failing example in this review. **Report-only:** changing
    rewriting alters generated code. Future cases should distinguish legal
    sibling scopes and captured locals before selecting a semantic rewrite.

19. **Q19 — Numeric partitioning and hierarchy scanning merit separate work.**
    `Algorithm/DecisionTreeBuilder.cs:80-124` appends overlapping constraint
    intersections and does not actually deduplicate despite its comment.
    `ExhaustivenessChecker.cs:17,26` creates fresh hierarchy/domain resolvers
    for every switch. **Defer:** duplicate partitions and repeated hierarchy
    scans are maintenance/performance risks, not proven public failures here.
    A simple subtract-prior-regions loop is not a general partition fix; retain
    all constraint boundaries. Benchmark and differential-test before changing.

## Integration coverage and delivery

20. **Q20 — Global-config end-to-end tests are absent from the solution.**
    `tests/Houtamelo.Spire.GlobalConfig.EndToEnd/Spire.GlobalConfig.EndToEnd.csproj`
    imports the real props file and compiles with real generators, but
    `Spire.Analyzers.slnx:8-15` omits it. Plain solution testing therefore misses
    actual MSBuild-property integration. **Implement:** include this existing
    project and run it in every batch's full gate.

21. **Q21 — Config coverage is concentrated in two rules.**
    `tests/Houtamelo.Spire.Analyzers.Tests/GlobalConfig` explicitly exercises
    SPIRE003/SPIRE015 and suppression; other enum-sensitive rule families read
    the same setting without equivalent focused integration cases.
    **Implement:** true/false/absent/invalid property characterization across
    the distinct allocation/default/unsafe/enum-cast families before common
    policy extraction. Test real Roslyn diagnostics, including nonreports.

22. **Q22 — Public utilities need broader behavioral integration coverage.**
    `SpireEnumTests` spot-checks a subset of overloads. `OfKind` is exercised
    through `ShowcaseAlgorithms.CountMoves`, so it is not entirely untested;
    however empty, deferred, invalid-kind, and null-source cases are absent.
    **Implement:** compile-time generated DU tests for `OfKind` ordering,
    filtering, laziness, and existing error timing; enum overload/boundary tests
    for supported successful and failing conversions. Do not encode proposed
    fixes for Q05 as passing expectations or suppress failures.

23. **Q23 — Preparation tests do not prove native execution.**
    `UnityHarnessRunnerTests`, `UnityBehavioralRunnerTests`, and
    `UnityCustomerWorkflowTests` mostly call `--prepare-only` with controlled
    editor paths. These are useful real filesystem/provenance tests, not
    evidence of executing Unity. Native core/serialization/inspector fixtures
    provide stronger tests but require separate commands. **Implement in this
    task's validation:** run the relevant actual Editor/player journeys and
    report preparation, CoreCLR, Editor Mono, and player results separately.
    Avoid a silent environment-based skip presented as native success.

24. **Q24 — Runtime result validation is inconsistent.**
    `IntegrationWorkflow.cs:20-53` returns the Unity exit code and prints JSON
    counters without asserting report identity/status. The fixture currently
    exits nonzero on failure, so a current false green was not demonstrated.
    `CustomerWorkflow.cs:101-115` checks a minimum test count rather than exact
    names, unlike the compiler harness's identity comparison. **Report-only:**
    stricter report/identity acceptance changes CLI results. Future subprocess
    tests should exercise missing/stale/failing reports with exit zero.

25. **Q25 — Filesystem containment is lexical; process-root discovery is unbounded.**
    `tools/DevTools/FileSystemTools.cs:34-53` allows repository symlinks to
    escape its lexical root. Both this file and `DotnetTools.cs:14-29` drain
    `git` stdout before applying their nominal five-second wait, so a hung git
    can hang discovery. Filesystem tests cover sensitive deletion but not the
    full list/window/regex/copy/move/symlink surface. **Report-only:** resolving
    symlinks, bounding execution, or changing error handling alters tool
    behavior. Add isolated subprocess/path fixtures in a future repair.

26. **Q26 — Portable process-tree termination has race boundaries.**
    `tools/DevTools/ProcessCompatibility.cs:52-78` recursively scans immediate
    `/proc` children; fork/reparent races can escape a snapshot. Current tests
    cover direct descendants and cancellation. **Defer:** do not claim complete
    tree ownership on all hosts; process-group callers are a stronger boundary.
    This review does not change process termination policy.

27. **Q27 — Benchmark distributions are mislabeled.**
    `benchmarks/.../Helpers/ArrayFiller.cs:245-248` selects a random nonfirst
    variant only 20% of the time for `Skewed80`, but otherwise returns
    `index % count`. It does not select the first variant 80% of the time;
    `Uniform` is cycling rather than shuffled. This affects interpretation of
    benchmark results. **Report-only:** changing input distributions changes
    observable benchmark behavior. Existing nullable-accessor experiments use
    independently defined explicit distributions and are a separate artifact.

28. **Q28 — Release CI is narrower than the maintained test surface.**
    `.github/workflows/publish.yml` executes only three test projects, omitting
    compile-time behavioral, real MSBuild-config, tooling, and catalog tests.
    It also packs `Houtamelo.Spire` twice. **Implement:** remove the identical
    redundant pack command. **Defer broader CI:** dedicated Unity/tooling setup
    needs explicit runner provisioning; local full gates cover all maintained
    SDK projects here. No publishing or remote workflow execution is authorized.

29. **Q29 — An accidental space-prefixed tree duplicates one test input.**
    Baseline tracks
    `" tests/Houtamelo.Spire.SourceGenerators.Tests/VariantPayloadConsumers/cases/AddMissingArms_Generic/before.cs"`
    outside the real test project. It is never discovered and can mislead search
    and future editing. **Implement:** remove only this stray copy, retaining
    the actual project fixture and its discovery/compilation tests.

30. **Q30 — Catalog tests are substantial, but reference precedence is implicit.**
    `tools/UnityStructCatalog/Catalog.cs:143-164` keeps the first reference by
    simple assembly name. The existing 20 CLI integration tests already cover
    field classification, generics, partial/conditional declarations, framework
    selection, accessibility, field-count filters, invalid options, and missing
    inputs. The gap is specifically duplicate-name precedence/invalid-reference
    combinations, not a missing catalog test suite. **Defer:** establish an
    assembly-identity contract before changing reference selection.

31. **Q31 — Large test/bridge files contain several distinct responsibilities.**
    The drawer template is 2,062 lines, inspector fixture 2,632 lines,
    VariantPayloadCoreTests 1,613 lines, and PatternMatrix 1,374 lines. Much of
    the drawer complexity protects focus/Undo/graph isolation and is justified
    by real native tests. **Retain generated template text:** splitting or
    rewriting generated bodies would violate the exact-output boundary.
    Future source-file partitioning should follow lifecycle responsibilities,
    not line-count quotas. No generic framework or wholesale rewrite is justified.

32. **Q32 — The optional pre-push guard can lose a failed tag validation.**
    `tools/git-hooks/pre-push:7-14` does not aggregate failure or exit immediately.
    Feeding a mismatched `v0.0.0` tag ref followed by an ordinary branch ref to
    the actual hook printed three version errors but exited zero. No push was
    performed. **Report-only:** correcting exit status changes hook behavior.
    Add future multi-ref success/failure integration tests. Release CI's separate
    version check still rejects a mismatched release tag.

33. **Q33 — Integration-test counts are hard-coded to 73 behavioral facts.**
    `tests/DevTools.Tests/UnityBehavioralRunnerTests.cs:339,344,374,464-465`
    and `tests/UnityBehavioral/Assets/Tests/XunitFactBridgeContractTests.cs:25`
    reject legitimate additions to the original behavioral suite. Adding seven
    real `OfKind` facts reproduced preparation failures (`Expected: 73,
    Actual: 80`). **Implement in batch 2:** derive expectations from an
    independent original-source or reflected-case inventory and compare exact
    names. Preserve provenance, required original cases, ordering, uniqueness,
    and the CLI's existing exact-identity checks; do not replace a magic number
    with a weaker minimum count.

## Additional findings from native and package validation

These existing boundaries became observable after Q01's compile blocker was
removed and the complete harness executed inside Unity.

34. **Q34 — Some dynamically executed tests compile against the wrong runtime.**
    `Behavioral/VariantPayloadCoreTests.cs:857` and
    `ReviewRegressions/ReviewRegressionTests.cs:286` use the .NET 8 reference
    pack even when loading the result in Editor Mono. Five native cases fail
    resolving `DefaultInterpolatedStringHandler`, `ValueTuple`, or
    `NullableContextAttribute` from `System.Runtime, Version=8.0.0.0`.
    **Implement:** use the actual host's references for execution tests while
    retaining the SDK path and all output/behavior assertions. Do not change
    the production generator or globally replace compilation-only references.

35. **Q35 — Independent harness discovery lacks Unity fixture input.**
    `tests/UnityTestHarness/ManifestExporter/Program.cs:59-67` copies portable
    output into its DataRoot. The source-generator project does not include
    `Unity/fixtures` in that output, although `HarnessWorkflow` separately
    supplies it to the Editor. xUnit therefore leaves one MemberData theory
    unexpanded during independent discovery; Unity successfully expands its
    six rows, making exact identities differ despite executing every case.
    **Implement:** make fixture deployment consistent and protect the six
    independently discovered row identities. Retain strict identity matching.

36. **Q36 — Standalone analyzer packaging omits its pattern-analysis dependency.**
    `src/Houtamelo.Spire.Analyzers/Houtamelo.Spire.Analyzers.csproj:30,43-49`
    makes PatternAnalysis a private project reference but packs only the analyzer
    DLL. The freshly built 4.7.6 standalone NuGet confirms the missing assembly;
    the main `Houtamelo.Spire` package includes it correctly. An actual `csc` invocation
    with just the standalone analyzer DLL reports AD0001/FileNotFoundException
    for both ExhaustivenessAnalyzer and CS8509Suppressor. Registering the pattern
    DLL alongside it instead produces the expected SPIRE009 for a missing `Dog`
    arm. **Report-only:** repairing package contents changes delivered analyzer
    behavior. Future packaging tests should compile a consumer using each
    standalone package independently, rather than only packing successfully.
    Reproduction and control: `tmp/full-code-quality-review/package-probe/`.

## Review coverage and evidence

Inventory included 1,654 tracked C# files across first-party implementation,
fixtures, and SDK tests; 1,694 code/build/script files total, approximately
90,069 lines including golden files. This was an architecture-, branch-, and
test-contract review across every first-party project, with direct inspection
of implementation hotspots, existing test families, and supporting scripts;
it is not a claim that every generated golden line or every test case received
an independent line-by-line audit.

Reviewed areas: public attributes/utilities and serialization bridge; all rule
families and flow-state infrastructure; generator pipeline, model, parsing,
ordinary/JSON/schema/record/Unity/performance emitters; standalone code fixes;
pattern domains/matrix/hierarchy; MCP tools and CLI workflows; package/export,
asmdef/compiler/customer/native fixtures; benchmark setup and showcase runtime,
authoring/persistence/scene tests; project/props/version/CI configuration.
Third-party sample source is preserved unchanged. IDE execution, other operating
systems, other Unity versions, and optional Unity package combinations are not
established by this machine's local checks.

Reproduction harness for Q02-Q06: `tmp/full-code-quality-review/probe/` compiles
real Roslyn syntax and invokes the built implementation, rather than a mocked
checker or an algorithm copied into a test. All repro inputs compiled before
generation. This ignored artifact supports the concrete reproductions above;
their essential inputs/results are recorded here so the report stands alone.

## Validation and implementation log

Baseline full command:
`dotnet test Spire.Analyzers.slnx -c Release --logger trx` with writable NuGet
caches and MSBuild node reuse disabled. Results: analyzer 826/826, generator
649/649, pattern analysis 339/339, behavioral 73/73, catalog 20/20, DevTools
87/94. Total 1,994 passed, seven failed (Q01), zero skipped. TRX evidence:
`tmp/full-code-quality-review/baseline/`. Global-config E2E was omitted by the
baseline solution (Q20).

Accepted batch sequence, each followed by the full SDK suite before proceeding:

1. Repair test-host loading, include existing global-config E2E project, remove
   stray fixture copy and redundant release pack. Establish a green baseline.
2. Add exact-output and missing public-utility/config/workspace integration
   characterization. Run new tests against the unchanged production baseline.
3. Consolidate identical emitter formatting, preserving all generated bytes.
4. Consolidate identical performance-parser metadata/diagnostic helpers.
5. Consolidate analyzer policy/integral-zero helpers with unchanged diagnostics.
6. Consolidate Xvfb session ownership, preserving CLI messages and cleanup.

Final gates also include Release package build, exact generated-output checks,
portable Unity harness, and relevant real Unity Editor/player validation.
No production behavior repair from the report-only list is included.

### Batch 1

Repaired Q01 without excluding tests: centralized runtime assembly resolution in
the existing Unity harness and made accessor IL inspection use emitted assembly
reflection, available on both CoreCLR and Mono. The latter also repaired a
second portable-host compilation failure hidden behind the first: its caller
was compiled while its PE inspection helper was conditionally excluded.
Added the existing global-config E2E project to the solution (Q20), removed the
stray space-prefixed fixture (Q29), and removed duplicate NuGet packing (Q28).
Recorded infrastructure history for the baseline commit; no infrastructure
policy/configuration changed in this batch.

Validation: portable host build, zero warnings/errors; focused desktop loading
and accessor tests 16/16; scoped formatting; full Release solution **2,012/2,012
passed, zero skipped** (including all 11 real-MSBuild global-config tests).
Evidence: `tmp/full-code-quality-review/batch1/{summary.json,suite.log,*.trx}`.
Production code and generated output were unchanged.

### Batch 2

Added fixed, reviewable generated-output fixtures and a provenance manifest:
30 scenarios, 77 generated files, all 24 existing performance fixtures, both
Unity compilation modes, generic/private nested Unity payloads, all three
reference modes, records/ToString, JSON/schema, and benchmark generation.
Persistent tests permit platform newline differences only; same-host raw-byte
comparison independently verifies every byte. Expected files are fixed data,
never regenerated by the running tests. Clean Release production rebuild
completed with zero warnings/errors before finalizing these expectations.

Added real generated-DU `OfKind` behavioral tests, enum overload/backing-width
tests, 64 configuration integration cases with compiler-error checks, and ten
real-workspace wildcard tests with regenerate/emit verification. Fixed Q33 by
comparing independent case inventories and exact IDs rather than a fixed count.
The behavioral suite now contains 80 facts. No production files changed.

The first characterization runs caught invalid new fixture input, nullable
xUnit data typing, a nonunique cursor selector, and the old bridge count; each
was corrected without changing production behavior or suppressing assertions.
The apparent record-pattern concern was investigated: `(Result.Text)` is valid
parenthesized type-pattern syntax; no stale-output mismatch was found.

Validation: scoped formatting; clean Release build; portable host build;
full Release solution **2,111/2,111 passed, zero skipped**; **77/77 raw generated
files byte-identical**. Evidence:
`tmp/full-code-quality-review/batch2-final/{summary.json,suite.log,*.trx,actual/}`.
Raw comparison command:
`bash tmp/full-code-quality-review/batch2/compare-fixed-outputs.sh <actual-root>`.

### Batch 3

Consolidated repeated declaration formatting into one internal helper with
explicit raw and escaped type-parameter paths. Updated seven emitters; retained
backend-specific serialization logic and all existing identifier algorithms.
No SourceBuilder/template/test/golden changes. Net production reduction:
57 lines, with one implementation per shared formatting operation.

Validation: scoped formatting and analyzer build (zero warnings/errors), full
Release solution **2,111/2,111 passed, zero skipped**, **77/77 raw generated files
byte-identical**. Evidence: `tmp/full-code-quality-review/batch3/`.

### Batch 4

Extracted shared performance-parser containing-type, keyword, accessibility,
source-location and diagnostic-coordinate logic. Centralized the identical
diagnostic presentation while retaining each generator's descriptor map and
validation flow. Reused raw declaration formatting for the two performance
emitters, preserving their explicit-accessibility and unescaped-name behavior.
No algorithm, supported-input, hint-name, constraint, or golden changes.

Validation: scoped formatting, full Release solution **2,111/2,111 passed, zero
skipped**, **77/77 raw generated files byte-identical**. Existing performance
diagnostic and snapshot tests ran as part of the full gate. Evidence:
`tmp/full-code-quality-review/batch4/`.

### Batch 5

Centralized marker/config policy resolution for nine analyzers, preserving the
separate exhaustiveness marker and required platform-symbol guards. Added a
nullable flow contract to the internal helper; no diagnostic suppressions or
public API changes. Consolidated the identical eight-type integral-zero test
used by operation and enum-member checks. Registrations, diagnostics and rule
algorithms remain unchanged.

Validation: focused analyzer/global-config tests 750/750; independent read-only
code review found no introduced issues; full Release solution **2,111/2,111
passed, zero skipped**, **77/77 raw generated files byte-identical**. Evidence:
`tmp/full-code-quality-review/batch5/`.

### Batch 6

Extracted the identical Xvfb allocation/draining/cleanup lifecycle into one
internal helper. Unity and Rider retain their own environment, messages,
platform handling, command arguments, report handling, and log paths. The
allocation timeout and exit-code semantics are unchanged.

Added four real-Xvfb integration cases: usable display/result, callback exception,
nonzero subprocess result, and cancellation. They verify the actual owned server
exits and its error log is written. This installation's `-displayfd` mode does
not create `.XN-lock` files; ownership is verified through the process's unique
canonical working directory instead. No mocked display server or silent skip.

Validation: scoped formatting; focused lifecycle tests 4/4; full Release
solution **2,115/2,115 passed, zero skipped**; **77/77 raw generated files
byte-identical**. Evidence: `tmp/full-code-quality-review/batch6/`.
Actual Unity execution is the final validation step below.

### Batch 7: native test-host corrections

The first complete Editor harness executed 2,102 cases: 2,093 passed and nine
failed, with no skipped cases. Four newly added display tests assumed .NET's
`ProcessName == "Xvfb"`; the installed Mono returns `"/usr/bin/Xvfb"` and its
`GetProcessesByName("Xvfb")` returns none. The test-only probe now also checks
the basename of actual enumerated processes, preserving canonical working
directory/PID ownership and cleanup assertions. A temporary probe using the
installed Unity Mono reproduced this difference directly.

The other five failures exposed Q34. Shared host-only references now use Unity's
NetStandard 2.1 reference/shim assemblies for dynamically executed non-Unity
sources. The SDK reference path and production generator are unchanged. JSON
execution fixtures explicitly reference both serializer assemblies.

Q35 caused a separate exact-identity mismatch: one expected unexpanded theory
versus six actual concrete rows. Fixture files are now copied into portable
output, and the preparation integration test requires all six distinct argument
rows with their expected source markers. Independent xUnit discovery and strict
Editor identity comparison remain intact.

Focused desktop tests passed 58/58 for host loading/discovery and 4/4 for display
ownership. Portable source-generator and DevTools builds passed with zero
warnings/errors. The first SDK gate caught the new manifest assertion reading
the nullable row index instead of the argument array; correcting that assertion
passed all seven actual harness-preparation tests. Final full Release gate:
**2,115/2,115 passed, zero skipped**; **77/77 raw generated files byte-identical**.
Scoped formatting and whitespace checks passed. SDK evidence:
`tmp/full-code-quality-review/batch7-final/`. First failing native evidence:
`tmp/full-code-quality-review/native/harness-first-failure/`.

### Batch 8: coherent Mono framework identities

The second complete Editor harness passed 2,096/2,102 cases, with all discovery
identities matching and no skips. All original runtime-loading and display
failures were fixed; six strict warning checks exposed CS1701 because Unity's
NetStandard reference directory identifies netstandard as 2.1 while Spire targets
2.0. The host-only reference resolver now uses Unity's `TestHostNetFramework`
facades with the loaded Mono `mscorlib`/`System` assemblies, excluding unrelated
SDK test-platform and serializer binaries. SDK compilation remains unchanged.
No warnings were suppressed and no assertions were relaxed.

A targeted actual Unity Editor Mono run passed all six affected tests without
CS1701/CS1069 diagnostics. Evidence:
`tmp/targeted-mono-project/variant-payload-mono-probe-xvfb3.{xml,log}`.
The temporary bridge was removed afterward. Portable dependency build passed
with zero warnings/errors; focused SDK tests passed 58/58. Final full Release
gate: **2,115/2,115 passed, zero skipped**, **77/77 raw generated files
byte-identical**. Formatting and whitespace checks passed. SDK evidence:
`tmp/full-code-quality-review/batch8-final/`; second failing native run:
`tmp/full-code-quality-review/native/harness-second-failure/`.

### Batch 9: serializer dependency references

The third complete Editor harness passed 2,100/2,102 cases, with exact identities
matching and no skips. Two JSON execution tests needed their serializers'
explicit dependencies: `System.Memory, Version=4.0.1.2` for STJ and Mono's
`System.Core` for Newtonsoft. The host-only resolver now includes Unity's exact
MSBuild System.Memory facade and the loaded Mono System.Core assembly. No
production code, generated output, assertions, or diagnostic policy changed.

A targeted actual Editor Mono run passed all nine affected cases together:
both JSON round trips, the six warning-free core cases, and ToString. Evidence:
`tmp/targeted-mono-project/targeted-json-variant-mono.xml`. Probe-only assets were
removed. The portable build passed with zero warnings/errors. Scoped formatting
and whitespace checks passed. Full Release gate: **2,115/2,115 passed, zero
skipped**, **77/77 raw generated files byte-identical**. SDK evidence:
`tmp/full-code-quality-review/batch9-final/`; third failing native run:
`tmp/full-code-quality-review/native/harness-third-failure/`.

### Final native validation

Actual Unity **6000.5.6f1**, Linux x86-64. All projects are disposable copies
under `tmp/full-code-quality-review/`; tracked fixtures and customer projects
were not changed. SHA-256 checks confirmed all four core fixture DLLs matched
the Release outputs at fixture preparation. Their informational version records
`21c3ac0`; `src/` is identical between that commit and `16412b7`. Later builds
restamp the informational version with HEAD; archived hashes identify the exact
assemblies used, rather than assuming byte equality across revision stamps.

| Execution | Result |
|---|---|
| Editor serialization write | 126 checks passed |
| Fresh-process Editor serialization read | 87 checks passed |
| Invalid-data recovery | 59 checks passed |
| Accessor assertions | 9 checks passed |
| Rendered UI Toolkit/IMGUI Inspector | 16 cases, 147 checks passed, zero unverified |
| Release Mono player, High stripping | 60 checks passed, 64-bit, nondevelopment |
| Release IL2CPP player, High stripping | 60 checks passed, 64-bit, nondevelopment |
| Behavioral suite in Editor Mono | 86 cases passed, including all 80 original fact identities |
| Behavioral suite in IL2CPP player | 88 cases passed, including all 80 original fact identities and IL2CPP runtime marker |
| Editor compiler feasibility probe | 5 cases passed |

Both core player builds reported zero errors and three warnings each; this is
not a warning-free Unity build claim. The fixtures intentionally exercise
unguarded accessor reads. Editor Mono behavioral execution is distinct from
the separate Mono player probe.

Standalone player startup initially failed on the inherited desktop's X11 mode
query; removing the display instead caused a native Unity startup crash. Both
completed successfully on private Xvfb displays with `XDG_SESSION_TYPE=x11`
and no `WAYLAND_DISPLAY`, matching the Editor runner's existing environment.
No production or fixture behavior was changed to address this host condition.

Commands use `tools/Spire.Tool` with `unity-union-core` (the four serialization
methods and graphics-enabled Inspector method listed in
`tests/UnityVariantPayloadCore/README.md`), followed by both built standalone
executables with `-batchmode -nographics -spire-report <path>`.
`unity-behavioral --backend Mono` and `--backend IL2CPP` execute the behavioral
journeys. Reports were checked for success, empty failures, exact behavioral
identities, backend markers, and player configuration; exit zero alone was not
accepted. Logs, reports and package hashes:
`tmp/full-code-quality-review/native/`.

### Release artifacts

Release API/analyzer/codefix builds completed without warnings or errors.
All three NuGet packages were packed successfully at the unchanged synchronized
version **4.7.6**. The main package contains the runtime DLL, all three analyzer
and codefix DLLs, the global-config props file, and README; DLL bytes matched
the Release outputs at package creation. Package SHA-256 and file inventories:
`tmp/full-code-quality-review/packages/summary.json`. The standalone analyzer's
existing missing dependency is Q36, not treated as a successful consumer gate.
An independent executable consumer restored the main package from a local feed
into an isolated package cache, generated a union, and executed its factory,
guarded payload access, and complete switch successfully. Removing one arm
produced the expected SPIRE009 for `Idle`, without AD0001; restoring the complete
input retained the successful runtime result. Evidence:
`tmp/full-code-quality-review/package-consumer/{consumer.log,missing-arm.log,summary.json}`.
Nothing was published or installed into a customer project.
