# Exact-shape code-fix coverage matrix — SPIRE009 and SPIRE015

This matrix defines the generalized missing-arm fix for the two existing Spire
diagnostics. It does not add a compiler-diagnostic fix: CS8509, CS8524, and
CS8655 remain outside the provider’s `FixableDiagnosticIds`.

The current simple DU snapshots in `CodeFix/cases/AddMissingArms_Property_*`
and the variant-payload consumer snapshots remain regression controls. Their
generated captures, accessor naming, and formatting must stay unchanged. The
new cases below exercise missing regions that cannot be reconstructed from a
variant name alone.

## Diagnostic metadata contract

SPIRE009 and SPIRE015 should retain their existing user-facing properties and
add a per-case syntax channel:

```text
MissingPatternCount = N
MissingPattern0 = <complete C# pattern>
MissingPattern1 = <complete C# pattern>
...
UseVariantTemplates = true | false
```

`MissingPattern0..N` are complete patterns, including the root discriminator,
all required property paths, and the remaining bool/enum/numeric/null
constraint. They are independent diagnostic properties rather than a
comma-separated string, because patterns themselves may contain commas.

When a missing region is an entire root DU variant and the existing template
already produces the approved capture form, `UseVariantTemplates=true` keeps
the current `AddMissingArms_Property_*` output byte/structure equivalent. For
restricted payload regions, null, non-DU hierarchies, and any case whose
capture would be unsound, `UseVariantTemplates=false` and the provider inserts
the parsed full pattern. SPIRE015 enum members always use exact patterns so an
enum member named `@null` cannot be confused with nullable `null`.

The exact pattern metadata must preserve the correlation between the root
variant and every nested constraint. Flattening all missing enum/bool values
into a name list is incorrect: `{ IsA: true, a: { flag: false } }`
and `{ IsB: true }` are distinct cases. Newly inserted struct DU patterns use
the approved short predicates, even when existing source uses `kind`.

## Exact-shape snapshot cases

Each row is a `before.cs`/`after.cs` pair in
`CodeFix/cases/{case-name}/`. The added arm or case section throws
`new System.NotImplementedException()` and is appended after existing arms.
The snippets below show the required pattern shape; normal surrounding
namespace/usings and existing simple DU declarations follow the repository’s
current fixtures.

| Case name | Missing shape and required after-pattern |
|-----------|------------------------------------------|
| `SPIRE009_Exact_StructPayloadBoolFalse` | A generated struct DU has `A(bool flag)` and `B()`. Existing arms cover `{ kind: Command.Kind.A, a: { flag: true } }` and `B`. Add exactly `{ IsA: true, a: { flag: false } }` (not a wildcard and not the old whole-variant capture template). |
| `SPIRE009_Exact_StructPayloadEnumMembers` | `A(Color color)` covers only `Color.Red`, with `B` covered. Add two arms, in enum order: `{ IsA: true, a: { color: Color.Green } }` and `{ IsA: true, a: { color: Color.Blue } }`. |
| `SPIRE009_Exact_StructPayloadNumericPointGap` | `A(int value)` is split by `value < 0` and `value > 0`, with another variant covered. Add `{ IsA: true, a: { value: 0 } }`; do not emit a broad `int`/discard pattern. |
| `SPIRE009_Exact_StructPayloadNumericRange` | `A(int value)` covers values below 0 and above 10, with `B` covered. Add `{ IsA: true, a: { value: >= 0 and <= 10 } }`. Render finite closed interval gaps with inclusive endpoints. |
| `SPIRE009_Exact_StructPayloadWideUnsignedPoint` | `A(ulong value)` covers `< 9007199254740993UL` and `> 9007199254740993UL`, with `B` covered. Add the exact `9007199254740993UL` constant, preserving all bits. |
| `SPIRE009_Exact_StructPayloadFloatingRange` | `A(float value)` covers `< 0f` and `> 10f`, with `B` covered. Add the exact `>= 0f and <= 10f` range with source-legal single-precision constants. |
| `SPIRE009_Exact_StructPayloadNullableBool` | `A(bool? flag)` covers `true` and `false`, with `B` covered. Add only `{ IsA: true, a: { flag: null } }`. |
| `SPIRE009_Exact_TwoVariantPayloadCorrelation` | `A(bool flag)` covers true and `B(bool flag)` covers false. Add two correlated patterns: `A` with `a.flag: false`, and `B` with `b.flag: true`. Do not constrain inactive payload accessors or create Cartesian products. |
| `SPIRE009_Exact_NullableParentPath` | `A(Inner? inner)` covers both non-null boolean values through `a.inner.Flag`, with `B` covered. Add `A` with `a.inner: null`, without accessing `Flag` below the null parent. |
| `SPIRE009_Exact_StructNestedPayloadPath` | `A(Inner inner)` has a deterministic nested `Inner.Flag` bool domain and only `{ kind: ..., a: { inner: { flag: true } } }` is present. Add `{ IsA: true, a: { inner: { flag: false } } }`, retaining the root and both property prefixes. |
| `SPIRE009_Exact_KeywordVariantPayload` | A struct variant factory named `@event(bool flag)` collides with generated accessor naming, so the existing arm uses `Command.Kind.@event` and `event_value.flag`. Append `{ Isevent: true, event_value: { flag: false } }`, preserving the generated qualified accessor and valid predicate identifier. |
| `SPIRE009_Exact_InheritedPropertyConstraint` | An `[EnforceExhaustiveness]` abstract `Root` exposes inherited `bool Flag`; `Derived` is covered only with `Flag: true`. Append `Derived { Flag: false }`, preserving the root type constraint and inherited property path. |
| `SPIRE009_Exact_NestedGenericProperty` | An enforced `Root` has sealed `Wrapper` with non-null enforced `Base<int> Value`; `Base<T>` has `LeafA<T>` and `LeafB<T>`. A switch covering `Wrapper { Value: LeafA<int> }` must append `Wrapper { Value: LeafB<int> }` with constructed type arguments. |
| `SPIRE009_Exact_NullableStructDURootNull` | A `Command?` switch covers every non-null root variant but omits null. Add a `null` arm/case only; do not add a synthetic variant or alter the existing payload patterns. |
| `SPIRE009_Exact_NullableStructDUVariantAndNull` | A `Command?` switch covers only variant `A`. Append the missing `B` root arm using `{ IsB: true }`, then append the literal `null` arm; preserve the declaration-first ordering and do not add payload constraints to fieldless variants. |
| `SPIRE009_Exact_RecordPayloadBoolFalse` | A record DU `Result.A(bool Flag)` has `Result.A { Flag: true }` and `Result.B`. Add `Result.A { Flag: false }` as the full type/property pattern. |
| `SPIRE009_Exact_NullableRecordDURootNull` | A nullable `Option<int>?` record DU covers `Some` and `None` but omits null. Add `null` as a separate pattern. This must compile under nullable annotations and remove the SPIRE009 diagnostic. |
| `SPIRE009_Exact_GenericRecordWholeVariant` | A non-null `Option<int>` record DU switch covers `None` only. Add the legacy full variant capture pattern `Option<int>.Some(int Value)` with the placeholder body, preserving the constructed generic type argument. |
| `SPIRE009_Exact_HierarchySiblingSubclass` | An `[EnforceExhaustiveness]` abstract base has unrelated top-level sealed siblings `Dog`, `Cat`, and `Bird`; the switch covers the first two. Add the exact `Bird` type pattern. |
| `SPIRE009_Exact_HierarchySameShortNameNamespaces` | Two namespaces expose distinct sealed `Node` subclasses of the same enforced base; only `global::Alpha.Node` is covered. Add `global::Beta.Node` (or an equivalent symbol-bound fully qualified pattern), never an ambiguous bare `Node`. |
| `SPIRE009_Exact_HierarchyGenericSubtype` | An enforced generic base `Base<int>` has a concrete `Leaf<int>` subtype omitted from the switch. Add the constructed generic type pattern (`global::Shapes.Leaf<int>` or the minimally qualified equivalent that binds to that symbol), preserving type arguments. |
| `SPIRE009_Exact_InterfaceImplementor` | An `[EnforceExhaustiveness]` interface has top-level sealed implementors in separate namespaces; one implementor is missing. Add its symbol-qualified type pattern and leave unrelated same-named types untouched. |

The existing `AddMissingArms_Property_*` cases must continue to use their
legacy `Is{Variant}: true`/capture templates when a whole root variant is the
missing region. The exact-shape rows above must use `MissingPatternN` because
substituting a full variant template would erase the payload restriction.

## Integration assertions

These checks use the real generator output, analyzer, `AdhocWorkspace`, and
code-fix provider. They are required in addition to normalized before/after
snapshots.

| Check | Required assertion |
|-------|--------------------|
| `SPIRE009_ExactPatterns_CompileEmitAndRerun` | Apply every exact-shape action, rerun the generator and SPIRE analyzers, and assert no user-source SPIRE009/SPIRE015 remains. The output compilation emits successfully; where a fixture has a runtime entry point, invoke it to prove the inserted pattern binds the intended symbol. |
| `SPIRE009_ExactPatterns_PreserveCorrelation` | Inspect diagnostic properties before applying the fix: `MissingPatternCount` matches the number of uncovered regions and each `MissingPatternN` contains the complete root-plus-path pattern. The provider must not derive a Cartesian product from independent name lists. |
| `SPIRE009_NoAction_Indeterminate` | For a switch for which `PatternAnalysis` returns `IsDeterminate=false` (unsupported/ambiguous nested pattern), no SPIRE009 code action is offered. The provider must not guess with a wildcard or a root-only arm. |
| `SPIRE009_NoAction_UnenforcedPlainSubjects` | Plain tuple/bool subjects, unmarked hierarchies, and sealed `[EnforceExhaustiveness]` types with no resolved root variants produce no SPIRE009 and no action. Do not manufacture a Spire diagnostic for these cases. |
| `SPIRE009_FixAll_MixedDiagnostics` | A batch fix-all updates multiple SPIRE009 switches and SPIRE015 enum switches in one or more documents, applying each diagnostic’s own exact patterns without cross-contamination. |
| `SPIRE009_TriviaAndStatementShape` | Existing comments, CRLF/tabs, and mixed inline/block statement sections remain attached; inserted sections are siblings at the switch level. Existing statement and expression templates remain structurally unchanged. |
| `SPIRE009_CodeActionTitleAndEquivalence` | SPIRE009 keeps the existing **Add missing variant arms** action contract; SPIRE015 keeps its enum-member action contract. Equivalence keys are stable for fix-all and no provider claims compiler diagnostic IDs. |
| `SPIRE009_ExactPatterns_NoRerunDiagnostic` | After applying a fix, the analyzer is rerun against the changed document rather than only comparing syntax. A structurally plausible but semantically misqualified pattern (same short name, wrong generic construction, or wrong `@` escaping) fails this check. |

## Known limits and deliberately excluded cases

- `ExtractMissingVariantNames` previously retained only root names. The new
  metadata must be produced from determinate `MissingCases` while preserving
  root/property/leaf relationships; do not reimplement coverage in the
  provider.
- Plain bools, tuples, and ordinary numeric subjects are not SPIRE009 inputs
  unless they are inside an enforced DU/hierarchy domain. Their PatternAnalysis
  tests remain unit/integration coverage, not code-fix cases.
- A sealed `[EnforceExhaustiveness]` type with no discovered concrete root
  types resolves to a structural domain and currently emits no SPIRE009. It
  must not acquire a speculative wildcard or an invented missing type.
- Unsupported nested deconstruction, unknown property paths, and other
  indeterminate regions must remain no-action cases. A fix that inserts a
  root-only arm would claim exhaustiveness it cannot prove.
- Nullable roots require a literal `null` pattern in addition to non-null DU
  or hierarchy patterns. The null case must be represented separately from a
  member/type whose source spelling happens to be `null`.
- Compiler exhaustiveness diagnostics are controls only. The provider fixes
  SPIRE009/SPIRE015 diagnostics and must not register for CS8509, CS8524, or
  CS8655.
