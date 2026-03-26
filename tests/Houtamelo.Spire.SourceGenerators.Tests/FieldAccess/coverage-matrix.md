# SPIRE013 / SPIRE014 Test Coverage Matrix

New cases only — the 13 existing cases are excluded.

---

## Category A: SPIRE013 — Wrong variant field in switch statement / expression

### should_fail

| File Name | Description |
|-----------|-------------|
| `Error_WrongVariantInSwitchStatement` | Switch statement: access Square's field inside Circle case label |
| `Error_WrongVariantInNestedSwitch` | Outer switch guards Circle; inner switch has Square arm — access Circle's field inside Square arm |
| `Error_WrongVariantInElseIf` | if/else-if chain: Circle guard in `if`, Square guard in `else if`, access Circle's field inside the `else if` body |
| `Error_WrongVariantThreeVariants` | Three-variant union: Triangle/Circle/Square; access Triangle's field inside Circle arm |
| `Error_MultipleWrongInSameMethod` | Two separate wrong accesses in the same method body (both lines should emit SPIRE013) |
| `Error_WrongVariantExpressionBodiedMember` | Expression-bodied method with switch expression, wrong field access in one arm |
| `Error_WrongVariantInLocalFunction` | Local function inside a method: switch expression has a wrong field access in one arm |
| `Error_WrongVariantInLambda` | Lambda stored in a `Func<Shape, double>`: switch expression inside, wrong field in one arm |
| `Error_WrongVariantAfterOrPattern` | `(Shape.Kind.Circle or Shape.Kind.Square, _)` arm — accessing `sideLength` (Square-only) inside an or-pattern arm that also covers Circle (?) |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_CorrectSwitchStatement_ThreeVariants` | Switch statement on three-variant union, each arm accesses its own field |
| `Pass_CorrectExpressionBodied` | Expression-bodied method returning from a fully correct switch expression |

---

## Category B: SPIRE013 — Wrong variant field in if-chain / is-pattern / ternary

### should_fail

| File Name | Description |
|-----------|-------------|
| `Error_WrongVariantNestedIsPattern` | `if (s is (Kind.Circle, _))` outer, `if (s is (Kind.Square, _))` inner — access Circle's field inside Square is-pattern |
| `Error_WrongVariantTernary` | Ternary: `s.kind == Kind.Circle ? s.sideLength : 0` — wrong field in the "true" branch |
| `Error_WrongVariantAfterIsPatternChain` | Sequential `if (s is ...) else if (s is ...)` — accesses opposite variant's field in each branch |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_CorrectTernaryBothArms` | `s.kind == Kind.Circle ? s.radius : 0` — correct field in the guarded branch |
| `Pass_CorrectVariantIsPatternChain` | `if (s is (Kind.Circle, _)) { s.radius } else if (s is (Kind.Square, _)) { s.sideLength }` |

---

## Category C: SPIRE014 — Unguarded access in various code locations

### should_fail

| File Name | Description |
|-----------|-------------|
| `Warn_InConstructorBody` | Constructor body accesses variant field with no kind guard |
| `Warn_InPropertyGetter` | Property getter returns `shape.radius` with no guard |
| `Warn_AsMethodArgument` | Passes `s.radius` directly as a method argument (no guard) |
| `Warn_InStringInterpolation` | `$"radius={s.radius}"` inside a method with no guard |
| `Warn_InTernaryNoGuard` | `condition ? s.radius : 0.0` where `condition` is unrelated to kind |
| `Warn_NonKindGuard` | `if (someInt > 5)` guard, then accesses `s.radius` — unrelated guard does not count |
| `Warn_MultipleUnguardedInSameMethod` | Two different variant fields accessed without guard (both lines should warn) |
| `Warn_InLocalFunction` | Local function that takes a union param and accesses its field with no guard |
| `Warn_InLambda` | Lambda `s => s.radius` assigned to `Func<Shape, double>` — no guard |
| `Warn_InAsyncMethod` | `async Task<double>` method returns `s.radius` with no kind guard |
| `Warn_InLinqSelect` | `shapes.Select(s => s.radius)` — field access in LINQ projection without guard |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_GuardedInConstructor` | Constructor checks `if (s.kind == Kind.Circle)` before accessing `s.radius` |
| `Pass_GuardedPropertyGetter` | Property getter checks kind before returning variant field |

---

## Category D: SPIRE014 — Unguarded access edge cases (guard is present but insufficient)

### should_fail

| File Name | Description |
|-----------|-------------|
| `Warn_ElseIfNoGuardForAccess` | `if (kind==Circle) { radius }; else if (someOtherCond) { radius }` — the else-if branch has no kind guard |
| `Warn_GuardOnDifferentVariable` | `if (otherShape.kind == Kind.Circle) { s.radius }` — guard is on a different variable, `s` is unguarded |
| `Warn_SwitchOnDifferentVariable` | Switch expression on `other` (not `s`) has Circle arm; `s.radius` inside that arm — `s` is unguarded |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_EarlyReturnPattern` | `if (s.kind != Kind.Circle) return; s.radius;` — early return establishes kind is Circle for subsequent lines (?) |
| `Pass_KindAccessInConstructor` | Accessing `s.kind` (not a variant field) inside constructor — always safe |
| `Pass_KindAccessInLambda` | `s => s.kind` lambda — accessing kind, not a variant field |
| `Pass_KindAccessInStringInterpolation` | `$"kind={s.kind}"` — kind access, never flagged |

---

## Category E: SPIRE013 / SPIRE014 — Record union field access

Record unions use inheritance-based pattern matching (`is VariantType`). The "field" concept maps to record primary constructor parameters exposed as properties.

Note: Record and class unions use type-pattern matching (`s is Shape.Circle c`), so the guard form differs from struct unions. Whether SPIRE013/SPIRE014 applies to record/class unions at all depends on whether the generator emits flat variant fields on the base type — if it does not (record unions carry data in subtype properties), the analyzer may not apply. Mark with (?) if unclear.

### should_fail (?)

| File Name | Description |
|-----------|-------------|
| `Error_RecordUnionWrongVariantInSwitch` | (?) Record union: in `Some` arm, access `None`'s property — only applicable if generator emits union-level fields |
| `Warn_RecordUnionNoGuard` | (?) Record union: access variant property without type-pattern guard |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_RecordUnionCorrectAccess` | Record union: `is Shape.Circle c` then `c.Radius` — type-pattern binds to subtype, no union-level field access |
| `Pass_ClassUnionCorrectAccess` | Class union: `is Shape.Circle c` then `c.Radius` — same pattern |

---

## Category F: SPIRE013 / SPIRE014 — Readonly struct and layout variants

### should_fail

| File Name | Description |
|-----------|-------------|
| `Error_ReadonlyStructWrongVariant` | `readonly` discriminated union struct: switch expression accesses wrong variant's field in an arm |
| `Warn_ReadonlyStructNoGuard` | `readonly` struct: unguarded field access |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_ReadonlyStructCorrectAccess` | `readonly partial struct` with correct field access inside matching switch arm |
| `Pass_OverlapLayoutCorrectAccess` | `Layout.Overlap` union: correct field access inside matching switch arm (Additive/Overlap emit same member names, guard still works) |
| `Pass_BoxedTupleLayoutCorrectAccess` | `Layout.BoxedTuple` union: correct field access inside switch arm |

---

## Category G: SPIRE013 / SPIRE014 — Complex pattern forms

### should_fail

| File Name | Description |
|-----------|-------------|
| `Error_WrongVariantWithWhenGuard` | `case (Kind.Circle, _) when s.sideLength > 0:` — `when` clause accesses Square's field while arm is Circle |
| `Warn_AfterWhenGuardUnclear` | `case (Kind.Circle, _) when someOtherCond:` body accesses `s.sideLength` — wrong variant field in arm body |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_WhenGuardUsesCorrectField` | `case (Kind.Circle, _) when s.radius > 0:` — when clause uses Circle's own field, correct |
| `Pass_DeconstructPatternBindsField` | `(Kind.Circle, double r) =>` — field bound to pattern variable, no direct union field access in arm |
| `Pass_VarPatternDeconstruct` | `(Kind.Circle, var r) => r` — correct var deconstruct, `r` is pattern-bound not raw union field |

---

## Summary counts (new cases)

| Category | should_fail | should_pass |
|----------|-------------|-------------|
| A — Wrong variant switch stmt/expression | 9 | 2 |
| B — Wrong variant if/ternary | 3 | 2 |
| C — Unguarded various locations | 11 | 2 |
| D — Insufficient guard edge cases | 3 | 4 |
| E — Record/class unions (?) | 2 | 2 |
| F — Readonly struct / layouts | 2 | 3 |
| G — Complex patterns | 2 | 3 |
| **Total** | **32** | **18** |

---

## Open questions for lead (marked `(?)`)

1. **Or-pattern arm field access (`Error_WrongVariantAfterOrPattern`)**: when a switch arm pattern is `(Kind.Circle or Kind.Square, _)`, neither Circle-only nor Square-only fields are safe to access. Should SPIRE013 fire if you access a field that belongs to only one of the or'd variants? Or is this out of scope (SPIRE013 only fires when the guard positively establishes a single variant but the access is for a different one)?

2. **Early-return pattern (`Pass_EarlyReturnPattern`)**: `if (s.kind != Kind.Circle) return; s.radius;` — does the analyzer perform flow-based tracking through early-return guards, or only recognize block-scoped guards (if/switch arms)?

3. **Record/class unions (`Error_RecordUnionWrongVariantInSwitch`, `Warn_RecordUnionNoGuard`)**: do SPIRE013/SPIRE014 apply to record and class unions at all? Record/class variants expose their data as properties on the subtype, not as flat fields on the union base type. If the generator does not emit union-level variant fields for records/classes, these diagnostics would never fire for them and the (?) cases should be dropped or converted to should_pass.

4. **`when` guard establishing kind**: does a `case (Kind.Circle, _) when cond:` arm body count as guarded by Circle for purposes of SPIRE013? Or does the `when` clause weaken the guard? (i.e., body inside a `when`-guarded arm: should wrong-variant access still be SPIRE013?)
