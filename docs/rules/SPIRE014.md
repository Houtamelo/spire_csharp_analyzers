# SPIRE014: Attempting to access variant data without checking 'kind'

| Property    | Value        |
|-------------|--------------|
| Rule ID     | SPIRE014     |
| Category    | Correctness  |
| Severity    | Warning      |
| Enabled     | Yes          |

## Description

Code reads a variant accessor (for example, `shape.circle`) without checking
the active variant through a switch, if-pattern, equality check on `kind`, or
generated `IsVariant` predicate.
An inactive accessor can read unrelated unmanaged storage or fail a shared-slot
cast. A captured payload's fields are ordinary fields and need no further guard.

Accessing `kind` or a generated `IsVariant` predicate is always safe and does not
trigger this diagnostic. Checks follow the true and false paths of `&&`, `||`,
`!`, `==`, `!=`, nested conditions, switches, and early exits. Boolean `&` and
`|` evaluate both operands, so their left operand cannot guard a right-hand read.
Saving a predicate in a Boolean local preserves its relationship to the receiver
while that receiver remains unchanged. Compound conditions can establish checks
for multiple receivers.

Checks must refer to the same unchanged union value. Assignment, writes through
aliases, and ref/out calls can invalidate a proof. Mutation-capable struct
methods, getters, setters, and event accessors can change their receiver.
Other user code, including constructors, operators, and callbacks, can also
invalidate proofs about shared fields or writable captures. Suspension has the
same effect on shared storage. An ordinary unescaped value local can retain its
proof across unrelated calls, `await`, and `yield`. An `in` parameter remains a
view of caller-owned storage, so its readonly modifier does not guarantee that
the caller's value stays unchanged.
Indirect writes through ref fields, ref-return indexers, or escaped pointers
also invalidate related checks. A user getter inside a property pattern can
change the discriminator before a later payload subpattern is evaluated.

Repeated property getters, indexers, calls, and volatile receiver paths do not
identify one stable value. Copy their result to a local and guard that snapshot.
Lambdas and local functions establish their own guards.

The diagnostic names the required kind check and shows how to suppress SPIRE014
when that check is guaranteed by external logic.

## Examples

### Violating code

```csharp
[DiscriminatedUnion]
partial struct Shape
{
    [Variant] public static partial Shape Circle(double radius);
    [Variant] public static partial Shape Square(int sideLength);
}

void Bad(Shape s)
{
    var r = s.circle.radius;  // SPIRE014: no kind guard
}
```

### Compliant code

```csharp
void Ok(Shape s)
{
    if (s.IsCircle)
    {
        var r = s.circle.radius;  // equivalent to checking s.kind == Shape.Kind.Circle
    }
}

bool PositiveCircle(Shape s)
    => s.IsCircle && s.circle.radius > 0;

bool NotAPositiveCircle(Shape s)
    => !s.IsCircle || s.circle.radius <= 0;

// Also fine: switch expression/statement arms
double Area(Shape s) => s switch
{
    { IsCircle: true, circle: var (r) } => Math.PI * r * r,
    { IsSquare: true, square: var (x) } => x * x,
};
```

## When to suppress

Suppress when you've verified the variant through external logic not visible to the analyzer.

```csharp
#pragma warning disable SPIRE014
var r = shapes[knownCircleIndex].circle.radius;
#pragma warning restore SPIRE014
```
