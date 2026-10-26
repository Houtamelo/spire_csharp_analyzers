# Exact integral ranges: design and coverage research

This is a read-only design report for batch 6 of
`docs/issues/2026-10-26-correctness-fixes.md`. The current analyzer can lose an
integral value when it converts a Roslyn constant to `double`; the reproduced
case is `long x` split by `<= 9007199254740992L` and
`>= 9007199254740994L`, which incorrectly reports no missing value even though
`9007199254740993L` is uncovered. The fix should make the eight supported
fixed-width integral types exact without changing the existing floating-point
path.

## Recommendation

Use a parallel exact integral interval implementation rather than widening the
existing `Interval`/`IntervalSet` double representation:

| Domain | Representation | Behavior |
|---|---|---|
| `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong` | `BigInteger` endpoints in `IntegralInterval`/`IntegralIntervalSet` | Discrete, exact, canonical closed ranges |
| `float`, `double`, `decimal` | Existing `Interval`/`IntervalSet` with `double` endpoints | Preserve current floating-point behavior for this batch |

`NumericDomain` should become the dispatch point. It should carry either the
existing floating set and `double` universe bounds, or the integral set and
`BigInteger` universe bounds. A public/internal `IsIntegral` discriminator and
factory methods such as `CreatePoint`/`CreateRange` should prevent callers from
constructing an integral constraint with the floating constructor. Constraints
should inherit the source domain's exact universe instead of the current
hard-coded `double.MinValue`/`double.MaxValue`.

Keeping the types parallel is the smallest correctness boundary: all existing
floating interval comparison, `NaN`, infinity, and decimal rounding behavior
stays in the current files, while integral operations can implement discrete
adjacency explicitly. A later generic refactor can remove duplication after
the behavior is covered; it is not required for this fix.

The internal NumericDomain test API should move to exact integral factories.
Do not keep a double-to-integer compatibility layer or project exact integer
sets back to double merely to preserve old unit-test construction syntax.
Preserve their range and inclusivity assertions using BigInteger endpoints.

The first implementation passed 34/35 integration cases but still missed the
nullable-long gap: DecisionTreeBuilder accepted the natural null/non-null split
without refining its numeric portion. Integral columns, including nullable
wrappers, must be partitioned into disjoint atoms for every row constraint
(intersection plus remainder). This also preserves numeric/boolean correlations
when ranges overlap. Keep the legacy floating partition policy in this batch.

## Language and Roslyn facts

* `IOperation.ConstantValue` exposes the evaluated constant as an `object` when
  `HasValue` is true. Preserve that typed object and its operation `Type`; do
  not stringify syntax or route every numeric object through `IConvertible.ToDouble`.
  See the [Roslyn `IOperation` API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.ioperation?view=roslyn-dotnet-5.3.0).
* For an integral input, C# constant patterns convert the constant to the
  input's integral type before equality matching, including nullable integral
  inputs. The [C# pattern specification, constant patterns](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/patterns#1123-constant-pattern)
  defines this conversion.
* Relational patterns are compile-time constant comparisons using `<`, `<=`,
  `>`, or `>=`; the specification lists all eight fixed-width integral types
  and the floating types separately. See [relational patterns](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/patterns#1129-relational-pattern).
* The C# conversion reference explicitly warns that `long`/`ulong` to
  `double` can lose precision. See [built-in numeric conversions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/numeric-conversions).
  `BigInteger` is appropriate for exact endpoint arithmetic; it represents
  integral values without narrowing to a floating type. See the [BigInteger
  API](https://learn.microsoft.com/en-us/dotnet/api/system.numerics.biginteger?view=net-10.0).

The helper that turns a Roslyn constant into an integral endpoint should accept
the primitive integral runtime values (`sbyte`, `byte`, `short`, `ushort`,
`int`, `uint`, `long`, `ulong`; optionally `char` for future support) and call
the corresponding `BigInteger` constructor. Do not use `Convert.ToInt64`, which
cannot represent `ulong.MaxValue`, and do not use `ToDouble`. If a value is not
an integral constant for an integral domain, preserve uncertainty rather than
guessing a conversion or treating that pattern as proven wildcard coverage.
The final nested-pattern batch adds the shared unknown-analysis handling.

## Current conversion sites and required changes

| Location | Current behavior | Required routing |
|---|---|---|
| `src/Houtamelo.Spire.PatternAnalysis/Domains/NumericDomain.cs:11-24` | Every set and universe bound is `double`; integral construction calls `NormalizeIntegral` after precision has already been lost. | Add exact integral state and bounds; keep the current fields/path for non-integral domains. |
| `NumericDomain.cs:29-37,102-118` | `GetBounds` casts all eight integer ranges to `double`; `ulong.MaxValue` and `long.MaxValue` are not exact. | Add `GetIntegralBounds` returning `BigInteger`; leave float/double/decimal bounds on the existing path. |
| `NumericDomain.cs:60-99` | Integral `Split` collects `HashSet<double>` boundaries and calls the continuous splitter. | Add a `BigInteger` boundary path. Split with the exact universe, not ±infinity sentinels. |
| `NumericDomain.cs:135-189` | `Math.Floor`/`Math.Ceiling` normalize a value that may already be rounded. | Normalize an open integral endpoint with `+ BigInteger.One` or `- BigInteger.One`; normalize to closed ranges. |
| `Domains/Numeric/Interval.cs:6-109` | Endpoint storage/comparison is inherently `double`. | Leave it as the floating implementation; add `IntegralInterval` with `BigInteger` endpoints and equivalent inclusivity APIs. |
| `Domains/Numeric/IntervalSet.cs:8-165,169-401` | All set operations and split masks use `double`, including ±infinity. | Add `IntegralIntervalSet` operations with `BigInteger` comparisons. Its split masks must use `[universeMin,b]` and `(b,universeMax]` (or `b + 1` after normalization), not BigInteger infinity values. |
| `Algorithm/PatternMatrix.cs:319-334` | Traditional `case value:` numeric constants call `ToDouble`. | Call one shared typed-constant helper; preserve the original `NumericDomain` universe and integral/floating kind. |
| `PatternMatrix.cs:1183-1198` | Constant patterns duplicate the same `ToDouble` conversion. | Route through the same helper as single-value cases. |
| `PatternMatrix.cs:1226-1276` | Relational bounds call `ToDouble`, then use `double.MinValue`/`double.MaxValue` for every domain. | Branch on `NumericDomain.IsIntegral`; create exact BigInteger ranges for integral inputs and retain the current double path otherwise. |
| `DomainResolver.cs:61-74,120-139` | Resolves the eight fixed-width integral types plus float/double/decimal. | No resolver behavior change is needed for this batch; `nint`/`nuint` are currently outside the resolver's numeric set even though the C# spec supports them. |
| `Algorithm/DecisionTreeBuilder.cs:55-105,127-169` | Uses only `IValueDomain` operations (`Split`, `Intersect`, `Subtract`). | No algorithm rewrite is required, but exact discrete `Split`/`CoversAll` semantics must be supplied by `NumericDomain`. |

The `ConvertSingleValueClause` and `ConvertConstantPattern` duplication should
be removed or made to call the same numeric helper. Otherwise one of the three
entry points can regress to `double` independently.

## Integral interval invariants

The integral set should canonicalize every non-empty interval to an inclusive
closed range. Given an exact BigInteger endpoint `b`:

```text
<  b  -> [universeMin, b - 1]
<= b  -> [universeMin, b]
>  b  -> [b + 1, universeMax]
>= b  -> [b, universeMax]
```

The open/closed flags should still be represented at the construction boundary
so that relational operators are not accidentally treated as inclusive. The
canonicalization step, not `double.Epsilon`, `Floor`, or `Ceiling`, performs the
`BigInteger.One` adjustment. BigInteger arithmetic is safe at `long.MinValue`
and `ulong.MaxValue`; intersecting with the exact universe should remove any
out-of-range result.

Because integral values are discrete, `[1, 3]` and `[4, 5]` have no missing
integer. `IntegralIntervalSet` must either merge these during normalization or
make `CoversAll`/gap checks treat `current.Hi + 1 == next.Lo` as adjacent. The
existing floating `IntervalSet` must retain its continuous semantics.

`SplitByBoundaries` must also preserve this invariant. For an integral boundary
`b`, its right mask represents values `>= b + 1`, not values `> b` encoded with
floating infinity. This matters at both universe extrema and when a singleton is
subtracted from a range.

## Coverage matrix

All rows below are intended as real Roslyn compilation/integration cases using
the existing `ExhaustivenessTestBase` `//@ exhaustive` and `//@ not_exhaustive`
format. For `not_exhaustive` cases, a `//~` marker should name the exact missing
pattern and become exhaustive only after that marker is inserted.

| ID | Area | Concrete case | Expected result / assertion |
|---|---|---|---|
| I01 | All eight widths | Run the same endpoint/open-bound matrix for `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, and `ulong`. Use `> 0` + `<= 0`, `< max` + `>= max`, and a singleton at each type's min/max. | Every valid full partition is exhaustive; every omitted endpoint remains missing. Do not cast expected endpoints to `double` in unit assertions. |
| I02 | Typed constants | For each of `long` and `ulong`, use both a suffixed literal and a `const` identifier: `const long N = 9007199254740993L;` and `const ulong U = 9007199254740993UL;`. | `ConstantValue.Value` remains an exact `long`/`ulong`; constant-pattern and relational-pattern paths agree. |
| I03 | >2^53 gap (primary regression) | `long x` with `<= 9007199254740992L` and `>= 9007199254740994L`; marker `9007199254740993L`. Repeat with `ulong` and `UL` suffixes. | Initially not exhaustive; exactly the odd middle value closes the gap. This reproduces the quality-review defect. |
| I04 | Exact adjacent singleton/complement | `long x` with `9007199254740992L` and `not 9007199254740993L`; marker `9007199254740993L`. | Initially missing exactly `9007199254740993L`; after the marker, exhaustive. This catches a rounded singleton inside `not`. |
| I05 | Signed extrema | `long x` cases around `long.MinValue` and `long.MaxValue`: `< long.MinValue`, `> long.MinValue`, `< long.MaxValue`, `>= long.MaxValue`, plus singleton extrema. | Empty out-of-range open intervals remain empty; `long.MinValue`/`long.MaxValue` are each matchable and not rounded. |
| I06 | Unsigned extrema | `ulong x` with `< ulong.MaxValue` and marker `ulong.MaxValue`; add a separate empty-bound check for `> ulong.MaxValue`. | Max is preserved as `18446744073709551615`; no signed conversion or overflow. |
| I07 | Open endpoint combinations | For each signed/unsigned representative, test all four operators around an interior value and around an extreme. Include `< N` + `>= N`, and `<= N` + `> N`. | No endpoint is dropped or duplicated; open bounds become exactly adjacent closed integer ranges. |
| I08 | `and`/`or` | Use `> N and < N+2`, `<= N or >= N+2`, and complementary arms around the >2^53 values. | The one-value intersection/gap at `N+1` is retained through `ConvertBinaryPattern`, union, and intersection. |
| I09 | Nullable root | `long? x` with `null`, `<= N`, and `>= N+2`; marker `N+1`. Repeat a full `null` + numeric partition. | Null remains a separate `NullableDomain` partition; numeric missing value is exact and only the marker closes it. |
| I10 | Direct struct property | A non-nullable struct with `long Value`, switched using `{ Value: <= N }` and `{ Value: >= N+2 }`; marker `{ Value: N+1 }`. | `BuildPropertyMatrix` and `ConvertPropertyRow` preserve the exact property-domain endpoint and report the missing property value. Nested property paths are intentionally not covered by this batch. |
| I11 | Tuple column | `(long Value, byte Tag)` switched using `(<= N, _)` and `(>= N+2, _)`; marker `(N+1, _)`. | `FlattenTupleColumns`/`FlattenTupleRow` retain an exact first-column interval while the wildcard second column remains exhaustive. |
| I12 | Traditional switch statement | `case <= N:`, `case >= N+2:`, then marker `case N+1:`. | `ConvertSingleValueClause` and pattern clauses do not use different numeric conversion behavior. |
| F01 | Floating regression | Keep `DoubleRelational` and add baseline `float` and `decimal` relational partitions using existing range values. | Existing floating results stay unchanged; no integral helper is used for `float`, `double`, or `decimal`. |
| F02 | Floating constants | Exercise a `double` singleton, a `float` singleton, and a decimal constant/relational bound. | Existing `ToDouble(CultureInfo.InvariantCulture)` behavior and current universe handling remain unchanged for this batch. |

Recommended unit coverage for `IntegralInterval`/`IntegralIntervalSet` mirrors
the existing `IntervalTests`/`IntervalSetTests`: empty/equal bounds, all four
inclusivity combinations, overlap/intersection/subtraction, complement, union
of discrete-adjacent ranges, exact `CoversAll`, and split at min/max and at an
odd value above 2^53. The integration rows are still required because they
exercise Roslyn's typed `ConstantValue` and the PatternMatrix routing.

## Deliberate scope boundaries

* Do not broaden this batch to `nint`/`nuint`; they are not currently resolved
  by `DomainResolver`. Add a separate design/test decision if native-sized
  integers are required.
* Do not repair decimal precision, NaN/infinity universe modeling, or nested
  property-path analysis here. Floating-point behavior must remain stable while
  this exact integral representation lands.
* Do not parse literal text to infer a value. Roslyn has already performed the
  C# constant-expression conversion; the analyzer must preserve the typed
  semantic value exposed by the operation.

