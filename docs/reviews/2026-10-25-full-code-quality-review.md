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
