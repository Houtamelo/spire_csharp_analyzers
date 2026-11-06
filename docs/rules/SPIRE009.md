# SPIRE009: Switch does not handle all possible cases of type marked with `[EnforceExhaustiveness]`

| Property    | Value        |
|-------------|--------------|
| Rule ID     | SPIRE009     |
| Category    | Correctness  |
| Severity    | Error        |
| Enabled     | Yes          |

## Description

Switch expressions and statements on discriminated unions and types marked
`[EnforceExhaustiveness]` must handle all possible cases explicitly. Missing cases
can make a switch silently fall through or throw at runtime.

For interface subjects, the diagnostic title says "interface marked with
`[EnforceExhaustiveness]`". The message lists the missing cases:
`Switch on 'BaseState' does not handle case(s): 'IdleState', 'MovingState', 'SpeakingState'`.
Hierarchy case names are listed in ordinal lexical order; union variants retain
their declaration order.

Applies to struct unions with variant payload patterns, record unions with
type patterns, and class/interface hierarchies marked `[EnforceExhaustiveness]`.
Positional deconstruction belongs to the payload accessor.
Generated `IsVariant: true` property patterns and the corresponding `kind`
patterns are equivalent. `IsVariant: false` excludes that variant and covers the
remaining variants; payload restrictions still limit the arm's coverage.

Nullable reference payloads participate in coverage even when their type is
an ordinary class without `[EnforceExhaustiveness]`. For `Effect? effect`,
an arm restricted to `effect: null` leaves non-null values uncovered; an arm
restricted to `effect: not null` leaves null uncovered. Cover both partitions
or capture the payload without a restriction. Under enabled nullable
annotations, `Effect effect` does not require a null arm. This null-state
check does not enumerate the ordinary class's subtypes.

Generated payload positional patterns use the same value domains as named
property patterns. For example, `moving: (Vector2 destination, _)` covers its
entire payload when the first output is `Vector2`, while `speaking: (string text, _)`
leaves null uncovered when that output is `string?`. Boolean constants preserve
their `true`/`false` partitions. Constraints remain correlated across outputs
and combine with named property arms. Arbitrary user-defined deconstructors
are not assumed to return values from same-named properties.

### Code fix

"Add missing variant arms" — generates arms for each uncovered variant with `throw new NotImplementedException()` as the body.

New struct arms use the shorter predicate and deconstruct the variant payload:
`{ IsRectangle: true, rectangle: var (width, height) }`. Fieldless arms use
`{ IsPoint: true }`. Captures preserve factory parameter order and accept null
field values. Local names are uniquified against the enclosing scope. Existing
arms remain in place. Added statement cases end immediately before the closing
brace, without a trailing blank line; comments and directives are preserved.

For restricted payloads and class/interface hierarchies, the fix inserts the
complete missing patterns computed by the checker, including property paths,
type names, value constraints, and `null`. It preserves correlations between
the variant and its payload instead of replacing a partial gap with a whole
variant arm. A fix requires a determinate, representable missing pattern.
Decimal payload gaps currently lack an exact fix because the checker stores
their bounds as `double`, which can lose decimal precision before a missing
pattern is generated.

## Examples

### Violating code

```csharp
[DiscriminatedUnion]
partial struct Shape
{
    [Variant] public static partial Shape Circle(double radius);
    [Variant] public static partial Shape Square(int sideLength);
    [Variant] public static partial Shape Rectangle(float width, float height);
}

int Area(Shape s) => s switch
{
    { IsCircle: true, circle: var (r) } => (int)(Math.PI * r * r),
    { IsSquare: true, square: var (x) } => x * x,
    // SPIRE009: missing 'Rectangle'
};
```

### Compliant code

```csharp
int Area(Shape s) => s switch
{
    { IsCircle: true, circle: var (r) } => (int)(Math.PI * r * r),
    { IsSquare: true, square: var (x) } => x * x,
    { IsRectangle: true, rectangle: var (width, height) } => (int)(width * height),
};
```

## When to suppress

Rarely appropriate. If suppressing, ensure the missing variants are logically unreachable in context.

```csharp
#pragma warning disable SPIRE009
var result = s switch { ... };
#pragma warning restore SPIRE009
```
