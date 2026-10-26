# Nested property-pattern exhaustiveness research

## Confirmed omission

`PatternMatrix.BuildCore` routes any subject with a property pattern to
`BuildPropertyMatrix` (`src/Houtamelo.Spire.PatternAnalysis/Algorithm/PatternMatrix.cs:380-390`).
That builder collects only each arm's direct `IPropertySubpatternOperation`
member (`:603-617`), creates one `PropertySlot` per direct property (`:635-647`),
and `ConvertPropertyRow` fills those slots from the same direct list
(`:661-697`). A nested pattern is therefore converted by the generic converter
to a wildcard (`:1039-1072`, especially `:1064-1067`). For example,
`{ Inner: { Flag: true } }` constrains `Inner` with a wildcard rather than
constraining `Inner.Flag`; a missing `Flag: false` arm can consequently look
exhaustive.

Two earlier dispatch paths need the same correction: struct unions currently
use a Kind-only matrix, and enforced hierarchies/record unions use a type-only
matrix. Both ignore additional payload/member restrictions. Preserve their root
discriminator columns while adding property paths; repairing only the ordinary
property builder would leave these false exhaustiveness results intact.

The current domain model is already intended for structural cross-products:
`StructuralDomain` stores `(SlotIdentifier, IValueDomain)` slots and splits the
first non-universe slot (`src/Houtamelo.Spire.PatternAnalysis/Domains/StructuralDomain.cs:13-91`).
However, `DomainResolver` returns an empty-slot `PropertyPatternDomain` for
ordinary/unknown types (`src/Houtamelo.Spire.PatternAnalysis/DomainResolver.cs:101-117`),
so an empty structural domain currently reports `IsUniverse` and cannot
distinguish “unsupported/unknown” from “all values covered”.

## Smallest compatible design

1. Extend `SlotIdentifier.PropertySlot` to carry a stable path from the root
   (root property symbols, or an equivalent immutable path object), while
   retaining the existing property symbol for equality/display. The path must
   distinguish `A.B` from `C.B`; a symbol alone cannot do that. The existing
   `RootSlot` and `TupleSlot` conventions are in
   `src/Houtamelo.Spire.PatternAnalysis/SlotIdentifier.cs:5-25`.
2. Replace direct-property collection with a recursive traversal of every
   `IPropertySubpatternOperation`. Emit a column for each path segment that is
   needed as a condition and each finite leaf. For
   `{ Inner: { Flag: true } }`, emit `Inner` and `Inner.Flag` columns. For an
   arm that only says `{ Inner: null }`, constrain `Inner` and leave its child
   columns wildcarded.
3. In each row, recursively convert the pattern tree. A nested property pattern
   must add a non-null constraint to its parent path before filling children.
   A nested type/declaration pattern must additionally constrain the parent path
   with `ConvertTypePattern` (which already handles nullable wrapping and known
   `EnforceExhaustiveDomain` types at `PatternMatrix.cs:1088-1125`). This makes
   `Inner: Child { Flag: true }` correlate the child leaf with `Inner is Child`.
4. Add an explicit root column when the switch subject is nullable/reference
   nullable and property decomposition is used. Every non-null property-pattern
   row gets a root non-null constraint; an explicit `null` arm constrains the
   root null partition. This preserves the nullable behavior already encoded by
   `DomainResolver.Resolve` (`DomainResolver.cs:36-58`) and `NullableDomain` cell
   construction (`PatternMatrix.cs:1138-1150`), instead of treating a property
   pattern as applicable to null.
5. Introduce an `Unknown` cell/status rather than mapping unsupported recursive
   patterns to `Cell.Wildcard`. A wildcard means proven coverage in
   `PatternMatrix.Specialize` (`PatternMatrix.cs:66-113`); an unsupported
   operation must instead mark the analysis as not-proven. The least invasive
   result API is an `ExhaustivenessResult` uncertainty flag alongside
   `MissingCases` (`ExhaustivenessResult.cs:6-20`), propagated by
   `DecisionTreeBuilder`, with `CS8509Suppressor` requiring both no missing
   cases and no uncertainty (`CS8509Suppressor.cs:50-59`). Unknown rows should
   never be sufficient to prove exhaustiveness. Keep existing wildcard behavior
   for actual discard/`var` patterns only.

   Unsupported rows contribute no proven coverage. If the supported rows alone
   cover the universe, the result is still determinately exhaustive. Otherwise
   mark the result indeterminate: consumers must not report speculative missing
   arms as definite omissions or suppress compiler warnings. `MissingCases` may
   be empty for an indeterminate result; emptiness alone is no longer proof.
   Keep existing guard handling and avoid introducing a new diagnostic.

The parent-column approach preserves correlations without redesigning the
Maranget matrix: specialization on `Inner` removes the null/type-incompatible
partitions before `Inner.Flag` is considered. Independent leaf columns alone
would incorrectly admit impossible child values under null or the wrong
runtime subtype.

## Relevant current code excerpts

```text
PatternMatrix.cs:388-390
if (HasPropertyPatterns(patterns))
    return BuildPropertyMatrix(subjectType, patterns, resolver);

PatternMatrix.cs:603-617
foreach (var propSub in recursive.PropertySubpatterns)
{
    var prop = GetPropertyFromSubpattern(propSub);
    if (prop != null && !propertySet.ContainsKey(prop))
        propertySet[prop] = propertySet.Count;
}

PatternMatrix.cs:683-691
foreach (var propSub in recursive.PropertySubpatterns)
{
    var prop = GetPropertyFromSubpattern(propSub);
    if (prop != null && propertySet.TryGetValue(prop, out int idx))
        cellBuilder[idx] = ConvertPattern(propSub.Pattern, columns[idx].Domain);
}

PatternMatrix.cs:1064-1067
case IRecursivePatternOperation:
    // Structural decomposition — conservative wildcard for now.
    return Cell.Wildcard.Instance;
```

## Coverage matrix for implementation

- Struct field/property leaf: finite `bool` and finite `enum` leaves, all
  combinations covered; omit one leaf value and require a diagnostic.
- Nested struct/object path: `{ Child: { Flag: true } }` plus false; verify
  sibling paths (`A.Flag` versus `B.Flag`) do not alias.
- Nullable parent: `Child?` with `{ Child: { Flag: ... } }` and `Child: null`;
  verify null is neither accidentally covered by child rows nor lost.
- Nullable/root reference: nullable root with property arms plus explicit
  `null`; non-null root with property arms must not require a null case.
- Type test parent: base-typed `Child` with derived finite types and nested
  leaves; verify `Child is Derived { Flag: ... }` correlates the type and leaf.
- Correlated bool/enum leaves: rows for `(Kind=A, Flag=true)` and
  `(Kind=B, Flag=false)` must not be treated as independent universal leaf
  coverage; omit one valid correlated row and require a diagnostic.
- Unsupported pattern uncertainty: recursive/deconstruction/operation forms
  not implemented by the flattener must not yield an empty `MissingCases`
  result, and `CS8509Suppressor` must leave compiler CS8509/CS8524 active.
- Preserve actual wildcards: discard, `var`, and explicit default patterns
  remain proven coverage; guards remain excluded by `PatternMatrix.Build`
  (`PatternMatrix.cs:156-173`).

## Confidence and follow-up

Confidence: high. The direct-property-only path and recursive wildcard are
verified in the current source. The parent-column design is the smallest
compatible extension because `PatternMatrix.Specialize` already preserves
all rows compatible with each partition. Implementation should inspect actual
Roslyn `IPropertySubpatternOperation` nesting to build path segments, then add
focused PatternAnalysis and source-generator suppressor cases before changing
the public verdict behavior.
