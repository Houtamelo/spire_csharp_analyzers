# Plan 015: Rule Proposals — 30 Analyzer Rules for Spire.Analyzers

## Overview

Research and proposal of 30 new analyzer rules that fit the Spire.Analyzers design: struct correctness and performance pitfalls in C#. Rules are organized by category and numbered SPIRE003–SPIRE032 (continuing from SPIRE001/002).

---

## Category A: `[EnforceInitialization]` Ecosystem Extensions

These extend the `[EnforceInitialization]` attribute system beyond array creation (SPIRE001).

### SPIRE003: `default(T)` where T is `[EnforceInitialization]`

- **Severity:** Error
- **Category:** Correctness
- **Description:** Flags `default(T)` and `default` literals when T is a `[EnforceInitialization]` struct. The whole point of the attribute is to prevent default instances.
- **Flagged:** `Config c = default;`, `return default;` (return type is `[EnforceInitialization]`), `default(Config)`, ternary/null-coalescing producing default
- **Not flagged:** `[EnforceInitialization]` types that have no fields, `default` for non-`[EnforceInitialization]` types, `default` in comparison (`x == default` — detection only, not comparison)

### SPIRE005: `[EnforceInitialization]` field in another struct without initialization guarantee

- **Severity:** Warning
- **Category:** Correctness
- **Description:** A struct that contains a field of `[EnforceInitialization]` type is itself susceptible to producing uninitialized instances. Warns that the containing type should also be marked `[EnforceInitialization]` or must guarantee initialization.
- **Flagged:** A struct (not marked `[EnforceInitialization]`) that has an instance field whose type is `[EnforceInitialization]`
- **Not flagged:** Containing type is already `[EnforceInitialization]`, containing type is a class (classes have constructors that run), field is static

### SPIRE006: `new T()` where T is `[EnforceInitialization]` with `new()` constraint

- **Severity:** Error
- **Category:** Correctness
- **Description:** Generic `new T()` on a struct is equivalent to `default(T)`. When the type argument is `[EnforceInitialization]`, the result is uninitialized.
- **Flagged:** `new T()` in generic context where T is constrained to `struct, new()` and the concrete type argument is `[EnforceInitialization]`
- **Not flagged:** `new T()` where T is a class (constructor runs), `new T(args)` with arguments (not parameterless)

### SPIRE007: `[EnforceInitialization]` struct as `out` parameter not assigned on all paths

- **Severity:** Warning
- **Category:** Correctness
- **Description:** An `out` parameter of `[EnforceInitialization]` type that is assigned `default` or left to implicit default on some code path.
- **Flagged:** `out` parameter of `[EnforceInitialization]` type assigned `= default` explicitly, or method with `out` parameter where some branch doesn't assign a meaningful value
- **Not flagged:** `out` parameter properly assigned a constructed value on all paths

---

## Category B: Defensive Copy Detection

The C# compiler silently inserts struct copies in `readonly` contexts when the struct is not `readonly`. These are invisible performance bugs and are the #1 most-requested struct analyzer feature in the C# ecosystem.

### SPIRE008: Non-readonly struct member access on `in` parameter

- **Severity:** Warning
- **Category:** Performance
- **Description:** Calling a method/property on an `in` parameter of non-readonly struct type triggers a defensive copy. The compiler copies the struct to avoid mutation of the caller's value.
- **Flagged:** `param.Method()`, `param.Property`, `param.ToString()` where `param` is `in T` and T is a non-readonly struct
- **Not flagged:** T is a `readonly struct`, member is marked `readonly`, access to fields (no defensive copy)

### SPIRE009: Non-readonly struct member access on `readonly` field

- **Severity:** Warning
- **Category:** Performance
- **Description:** Same as SPIRE008 but for `readonly` fields. Each member access copies the entire struct. Particularly expensive in tight loops.
- **Flagged:** `this.readonlyField.Method()`, `this.readonlyField.Property` where the field's type is a non-readonly struct
- **Not flagged:** Field type is `readonly struct`, member accessed is `readonly`, direct field access (no copy)

### SPIRE010: Non-readonly struct member access on `ref readonly` local/return

- **Severity:** Warning
- **Category:** Performance
- **Description:** Extends defensive copy detection to `ref readonly` locals and return values.
- **Flagged:** `ref readonly var x = ref GetRef(); x.Method();` where the type is non-readonly struct
- **Not flagged:** `readonly struct` types, `readonly` members

### SPIRE011: Suggest `readonly struct` for struct with no mutable state

- **Severity:** Info
- **Category:** Performance
- **Description:** If a struct has no public mutable fields/properties and no mutating methods, suggest making it `readonly struct` to prevent defensive copies everywhere.
- **Flagged:** Struct declaration where all instance fields are readonly, all auto-properties are get-only or init-only, and the struct is not already `readonly`
- **Not flagged:** Struct already declared `readonly`, struct has mutable instance fields or settable auto-properties

---

## Category C: Boxing Detection

Boxing allocates on the heap and is the most common hidden allocation in C#.

### SPIRE012: Value type cast to `object` or non-generic interface

- **Severity:** Warning
- **Category:** Performance
- **Description:** `object o = myStruct;` or `IFoo f = myStruct;` (where IFoo is not a generic constraint) causes boxing allocation.
- **Flagged:** Implicit or explicit conversion from value type to `object`, `ValueType`, `Enum`, or any interface type
- **Not flagged:** Constrained generic calls (no boxing), struct-to-struct conversions, conversions inside generic methods with constraints

### SPIRE013: `Enum.HasFlag()` causes boxing

- **Severity:** Warning
- **Category:** Performance
- **Description:** `myEnum.HasFlag(otherEnum)` boxes both operands on .NET Framework and older runtimes. Suggest bitwise `(myEnum & flag) != 0` instead.
- **Flagged:** Any call to `Enum.HasFlag(Enum)`
- **Not flagged:** Bitwise flag checks `(x & y) != 0`

### SPIRE014: String interpolation/concatenation boxes value type

- **Severity:** Info
- **Category:** Performance
- **Description:** `$"Value: {myStruct}"` or `"Value: " + myStruct` — value types without a `ToString()` override get boxed. Even with override, older string concatenation paths may box.
- **Flagged:** Interpolation holes or `+` concatenation with value type operand that doesn't override `ToString()`
- **Not flagged:** Value types that override `ToString()`, explicit `.ToString()` call before interpolation

### SPIRE015: Struct used as `IEnumerable`/`IEnumerator` (non-generic)

- **Severity:** Warning
- **Category:** Performance
- **Description:** `foreach` over a collection returning a struct enumerator through `IEnumerator` (non-generic interface) boxes the enumerator on each iteration.
- **Flagged:** Variable of type `IEnumerator` (non-generic) where the runtime type is a struct enumerator
- **Not flagged:** Duck-typed `foreach` (compiler uses struct `GetEnumerator()` directly), generic `IEnumerator<T>` with struct

### SPIRE016: Dictionary/HashSet key is struct without `IEquatable<T>`

- **Severity:** Warning
- **Category:** Performance
- **Description:** `Dictionary<MyStruct, V>` where `MyStruct` doesn't implement `IEquatable<MyStruct>` — every lookup boxes through `EqualityComparer<T>.Default` falling back to `object.Equals`.
- **Flagged:** `Dictionary<TKey, V>`, `HashSet<T>`, `ConcurrentDictionary<TKey, V>` etc. where TKey is a struct not implementing `IEquatable<TKey>`, and no custom comparer is provided
- **Not flagged:** Struct implements `IEquatable<T>`, custom `IEqualityComparer<T>` passed to constructor

---

## Category D: Copy Semantics Bugs

Structs have value semantics. Many bugs come from mutating a copy thinking you're mutating the original.

### SPIRE017: Mutable struct modified through `List<T>` indexer

- **Severity:** Warning
- **Category:** Correctness
- **Description:** `list[i].Field = value;` — the indexer returns a *copy*, so the mutation is silently lost. This is a classic C# gotcha.
- **Flagged:** Member assignment or method call on the result of an indexer that returns a struct by value (List<T>, arrays excluded since they return by ref)
- **Not flagged:** Arrays (indexer returns ref), `Span<T>` (returns ref), custom indexers returning ref

### SPIRE018: Mutable struct modified through property getter

- **Severity:** Warning
- **Category:** Correctness
- **Description:** `obj.StructProp.X = 5;` — the property returns a copy. Mutations are discarded.
- **Flagged:** Member assignment or mutating method call on the result of a property getter that returns a struct by value
- **Not flagged:** `ref` returning properties, direct field access, array indexers

### SPIRE019: Struct implementing `IDisposable` disposed on copy

- **Severity:** Warning
- **Category:** Correctness
- **Description:** When a `using` statement captures a struct by value (copy), `Dispose()` runs on the copy, not the original. Particularly dangerous with `foreach` patterns where the enumerator is a disposable struct.
- **Flagged:** `using var x = structValue;` where the struct is copied (not ref), `using (var x = GetStruct())` where the struct implements `IDisposable`
- **Not flagged:** `using` on class types (reference semantics), `using ref` patterns

### SPIRE020: Mutable struct returned by value from method — mutation discarded

- **Severity:** Info
- **Category:** Correctness
- **Description:** `GetConfig().Name = "x";` — mutations on a method's return value are discarded.
- **Flagged:** Field assignment or mutating method call on the return value of a method/property that returns a non-readonly struct by value
- **Not flagged:** Return value assigned to a local first, `ref` returns, readonly structs (no mutation possible)

---

## Category E: Large Struct Performance

### SPIRE021: Large struct passed by value to method

- **Severity:** Info
- **Category:** Performance
- **Description:** Struct exceeding a configurable threshold (default: 16 bytes) passed by value instead of `in` or `ref`. Each call copies the entire struct.
- **Flagged:** Method call where an argument of struct type > threshold bytes is passed by value
- **Not flagged:** Struct ≤ threshold, parameter is already `in`/`ref`/`ref readonly`, the struct is `readonly` and small enough for the JIT to optimize

### SPIRE022: Large struct captured by lambda/async

- **Severity:** Warning
- **Category:** Performance
- **Description:** A large struct captured in a closure or async state machine is copied into the heap-allocated capture object. Surprising allocation + copy cost.
- **Flagged:** Lambda or async method that captures a local/parameter of struct type > threshold bytes
- **Not flagged:** Small structs, structs passed by ref to the lambda, static lambdas

### SPIRE023: Large struct copied in `foreach` loop variable

- **Severity:** Info
- **Category:** Performance
- **Description:** `foreach (var item in largeStructList)` — each iteration copies the struct. Suggest `ref` foreach (C# 13+), `Span<T>`, or manual indexing.
- **Flagged:** `foreach` where the iteration variable is a struct > threshold bytes and the collection doesn't provide ref enumeration
- **Not flagged:** `foreach ref var item`, `Span<T>` enumeration, small structs

---

## Category F: Equality & Hashing Pitfalls

### SPIRE024: Struct without `Equals`/`GetHashCode` override

- **Severity:** Info
- **Category:** Performance
- **Description:** The default `ValueType.Equals()` uses reflection, is slow, and can produce incorrect results for structs containing reference-type or floating-point fields.
- **Flagged:** Struct declaration that does not override `Equals(object)` and `GetHashCode()`, AND has reference-type fields or floating-point fields
- **Not flagged:** Struct overrides both, struct is a `record struct` (auto-generates), struct has only blittable value-type fields (runtime can use fast-path memcmp)

### SPIRE025: `GetHashCode` depends on mutable field

- **Severity:** Warning
- **Category:** Correctness
- **Description:** If `GetHashCode()` reads a mutable field, the hash changes after mutation. This breaks `Dictionary`, `HashSet`, etc. when the struct is used as a key after modification.
- **Flagged:** `GetHashCode()` override in a struct that references a non-readonly field
- **Not flagged:** `readonly struct` (all fields immutable), only readonly fields referenced, `record struct` (generated code handles this)

### SPIRE026: Struct overrides `Equals` but not `GetHashCode` (or vice versa)

- **Severity:** Error
- **Category:** Correctness
- **Description:** Overriding one without the other violates the contract that equal objects must have equal hash codes. This is already CS0661/CS0660 in the compiler, but only as warnings. Elevating to error for structs is valuable.
- **Flagged:** Struct that overrides `Equals(object)` but not `GetHashCode()`, or vice versa
- **Not flagged:** Both overridden, neither overridden, `record struct` (auto-generated)

---

## Category G: Struct Design & Initialization

### SPIRE027: Non-readonly struct with only readonly members

- **Severity:** Info
- **Category:** Performance
- **Description:** All instance members are readonly but the struct isn't declared `readonly`. Adding `readonly` prevents defensive copies and communicates intent.
- **Flagged:** Struct where all instance fields are readonly, all auto-properties are get-only or init-only, and the struct is not declared `readonly`
- **Not flagged:** Already `readonly struct`, has mutable fields/properties
- **Note:** This may overlap with SPIRE011. Consider merging into one rule.

### SPIRE028: Struct parameterless constructor doesn't initialize all fields

- **Severity:** Warning
- **Category:** Correctness
- **Description:** C# 10+ allows struct parameterless constructors. If one exists but doesn't initialize all fields, `new T()` and `default(T)` produce different results, which is confusing and error-prone.
- **Flagged:** Struct with explicit parameterless constructor that does not assign all instance fields
- **Not flagged:** All fields assigned in constructor, struct has no parameterless constructor, fields have initializers

### SPIRE029: Struct over 64 bytes — consider using a class

- **Severity:** Info
- **Category:** Performance
- **Description:** Very large structs hurt cache performance and are extremely expensive to copy. Suggest converting to a class or using `ref` patterns everywhere.
- **Flagged:** Struct declaration where total instance field size > 64 bytes (configurable)
- **Not flagged:** Struct ≤ threshold, struct is only used by ref

### SPIRE030: `record struct` with mutable positional parameters

- **Severity:** Info
- **Category:** Design
- **Description:** `record struct Point(int X, int Y)` generates mutable `{ get; set; }` properties, unlike `record class` which generates `{ get; init; }`. Users often don't realize the struct is mutable.
- **Flagged:** `record struct` with positional parameters that are not `readonly record struct`
- **Not flagged:** `readonly record struct`, non-positional record structs, record classes

---

## Category H: Async / Iterator Struct Pitfalls

### SPIRE031: Struct local used after `await` — may be stale copy

- **Severity:** Warning
- **Category:** Correctness
- **Description:** A local struct value copied before an `await` may hold stale data after resumption if the source was mutated concurrently. The async state machine preserves the copy, not a reference.
- **Flagged:** Use of a struct local variable after an `await` point, where the variable was assigned from a field or ref-returning method before the await
- **Not flagged:** Immutable/readonly structs (stale data isn't a concern), locals assigned from pure expressions, locals not used after await

### SPIRE032: Struct implementing `IDisposable` used in `async` method without `using`

- **Severity:** Warning
- **Category:** Correctness
- **Description:** In async methods, a disposable struct may be copied into the state machine. If not wrapped in `using`, `Dispose()` may never run or may run on the wrong copy.
- **Flagged:** Local variable of disposable struct type in an async method, not wrapped in `using` statement/declaration
- **Not flagged:** Wrapped in `using`, class types, non-async methods

---

## Priority Matrix

### Tier 1: High Impact — Implement First
| Rule | Rationale |
|------|-----------|
| SPIRE003 | Natural `[EnforceInitialization]` extension, straightforward IOperation detection |
| SPIRE008 | #1 most-requested struct analyzer rule, defensive copy on `in` param |
| SPIRE009 | #2 most-requested, defensive copy on `readonly` field |
| SPIRE012 | Boxing detection is high value, high visibility |
| SPIRE017 | Classic C# gotcha, catches real bugs in List<T> indexer mutation |

### Tier 2: Medium Impact — Good ROI
| Rule | Rationale |
|------|-----------|
| SPIRE004 | Simple `[EnforceInitialization]` extension |
| SPIRE005 | Transitive `[EnforceInitialization]` propagation |
| SPIRE011 | Suggests `readonly struct`, prevents defensive copies proactively |
| SPIRE013 | Enum.HasFlag boxing, simple to detect |
| SPIRE016 | Dictionary key boxing, common perf issue |
| SPIRE024 | Missing Equals/GetHashCode, common correctness issue |
| SPIRE025 | Mutable GetHashCode, catches real bugs |

### Tier 3: Lower Priority — Unique Value
| Rule | Rationale |
|------|-----------|
| SPIRE006 | Generic `new T()` edge case |
| SPIRE010 | ref readonly defensive copy (less common than in/readonly field) |
| SPIRE014 | String interpolation boxing (info-level, noisy) |
| SPIRE015 | Non-generic IEnumerator boxing (rare in modern code) |
| SPIRE018 | Property getter mutation (compiler catches some) |
| SPIRE019 | IDisposable struct copy (complex, rare) |
| SPIRE020 | Method return mutation (compiler catches some) |
| SPIRE021 | Large struct by value (configurable threshold, needs .editorconfig support) |
| SPIRE022 | Large struct in async capture (complex analysis) |
| SPIRE023 | Large struct foreach copy (needs ref foreach detection) |
| SPIRE026 | Equals/GetHashCode mismatch (compiler already warns) |
| SPIRE027 | Overlaps with SPIRE011 — may merge |
| SPIRE028 | Parameterless constructor partial init (niche) |
| SPIRE029 | Very large struct suggestion (design guidance, not bug detection) |
| SPIRE030 | Record struct mutability (info-level, design) |
| SPIRE031 | Struct after await (complex flow analysis) |
| SPIRE032 | Disposable struct in async (complex, rare) |
| SPIRE007 | Out parameter default assignment (complex flow analysis) |

---

## Notes

- Rules SPIRE011 and SPIRE027 overlap significantly (both suggest `readonly struct`). Consider merging into one rule.
- Large struct rules (SPIRE021/022/023/029) all need a size computation utility — implement once in `Spire.Analyzers.Utils`.
- Defensive copy rules (SPIRE008/009/010) share detection logic — implement a shared helper for "is this a readonly context accessing a non-readonly struct member".
- Boxing rules (SPIRE012/013/014/015/016) could share a "is this a boxing conversion" utility.
- The `[EnforceInitialization]` rules (SPIRE003–007) naturally build on the existing SPIRE001/002 infrastructure.
