# Plan 011 -- Research: .NET Methods That Create Arrays With Default-Initialized Elements

**Status**: Complete
**Date**: 2026-03-10

## Purpose

Comprehensive inventory of all .NET APIs that create or expand arrays where elements are initialized to `default(T)`. This is needed to define the detection surface for SPIRE001 (and future rules) -- any API that can produce an array of `[EnforceInitializationialized]` structs with uninitialized (default) elements is a potential diagnostic target.

---

## Category 1: C# Language Constructs (Syntax-Level)

These are not method calls but language-level array creation expressions. They produce arrays with `default(T)` elements.

### 1a. `new T[length]` -- Array creation expression (no initializer)

- **Creates default elements**: YES -- all elements are `default(T)`
- **Element type**: The array element type in the expression
- **Size parameter**: The length expression(s) between brackets
- **Roslyn node**: `ArrayCreationExpressionSyntax` / `IArrayCreationOperation`
- **Notes**: `new T[5]` creates 5 default-initialized elements. `new T[] { a, b }` does NOT (all elements provided). Multi-dimensional: `new T[2,3]` also default-initializes.

### 1b. `stackalloc T[length]` -- Stack-allocated array (no initializer)

- **Creates default elements**: YES -- zero-initialized by the CLR
- **Element type**: The type in the expression
- **Size parameter**: The length expression between brackets
- **Roslyn node**: `StackAllocArrayCreationExpressionSyntax` (no `IOperation` equivalent)
- **Notes**: `stackalloc T[5]` is zero-initialized. `stackalloc T[] { a, b }` provides all values. Only works with unmanaged types. Results in `Span<T>`, not `T[]`.

### 1c. Collection expressions targeting arrays (C# 12+)

- **Creates default elements**: NO -- elements are always provided in the expression `[a, b, c]`
- **Out of scope**: No way to produce default elements via collection expressions alone.

---

## Category 2: `System.Array` Static Methods

Source: [System.Array Class](https://learn.microsoft.com/en-us/dotnet/api/system.array?view=net-10.0)

### 2a. `Array.CreateInstance` (6 overloads)

All overloads create arrays with elements initialized to `default(T)`.

| # | Signature | Element type param | Size param(s) |
|---|-----------|-------------------|---------------|
| 1 | `Array.CreateInstance(Type elementType, int length)` | `elementType` (1st) | `length` (2nd) |
| 2 | `Array.CreateInstance(Type elementType, int[] lengths)` | `elementType` (1st) | `lengths` array |
| 3 | `Array.CreateInstance(Type elementType, long[] lengths)` | `elementType` (1st) | `lengths` array |
| 4 | `Array.CreateInstance(Type elementType, int length1, int length2)` | `elementType` (1st) | `length1`, `length2` |
| 5 | `Array.CreateInstance(Type elementType, int[] lengths, int[] lowerBounds)` | `elementType` (1st) | `lengths` array |
| 6 | `Array.CreateInstance(Type elementType, int length1, int length2, int length3)` | `elementType` (1st) | `length1`, `length2`, `length3` |

- **Creates default elements**: YES (all overloads)
- **How to determine element type**: First parameter is `System.Type` -- resolved at runtime, not generic. An analyzer can try to track it but it requires data-flow analysis of `typeof(T)` expressions.
- **Availability**: All .NET versions

Source: [Array.CreateInstance](https://learn.microsoft.com/en-us/dotnet/api/system.array.createinstance?view=net-10.0)

### 2b. `Array.CreateInstanceFromArrayType` (3 overloads, .NET 9+)

| # | Signature | Type param | Size param(s) |
|---|-----------|-----------|---------------|
| 1 | `Array.CreateInstanceFromArrayType(Type arrayType, int length)` | `arrayType` (array type, not element type) | `length` |
| 2 | `Array.CreateInstanceFromArrayType(Type arrayType, int[] lengths)` | `arrayType` | `lengths` array |
| 3 | `Array.CreateInstanceFromArrayType(Type arrayType, int[] lengths, int[] lowerBounds)` | `arrayType` | `lengths` array |

- **Creates default elements**: YES (all overloads)
- **Key difference**: First parameter is the **array type** (e.g. `typeof(int[])`) not the element type
- **Availability**: .NET 9+
- **Notes**: Preferred over `CreateInstance` for better performance and AOT compatibility

Source: [Array.CreateInstanceFromArrayType](https://learn.microsoft.com/en-us/dotnet/api/system.array.createinstancefromarraytype?view=net-10.0)

### 2c. `Array.Resize<T>(ref T[]? array, int newSize)`

- **Creates default elements**: YES -- when `newSize > array.Length`, the new slots are `default(T)`. Also when `array` is `null`, creates an entirely new array of `default(T)`.
- **Generic type parameter**: `T` (element type)
- **Size parameter**: `newSize` (2nd parameter)
- **Mechanics**: Allocates a new array, copies elements from old, extra slots are `default(T)`
- **Availability**: All .NET versions

Source: [Array.Resize](https://learn.microsoft.com/en-us/dotnet/api/system.array.resize?view=net-10.0)

### 2d. `Array.Empty<T>()`

- **Creates default elements**: NO -- returns a cached zero-length array. Zero elements means no default-initialized elements exist.
- **Out of scope**: No elements to be concerned about.

Source: [Array.Empty](https://learn.microsoft.com/en-us/dotnet/api/system.array.empty?view=net-10.0)

### 2e. `Array.Clone()`

- **Creates default elements**: NO -- shallow-copies all existing elements. No default-initialized slots.
- **Out of scope**: Elements are copies of the source, not `default(T)`.

### 2f. `Array.ConvertAll<TInput, TOutput>(TInput[], Converter<TInput, TOutput>)`

- **Creates default elements**: NO -- every element is produced by the converter delegate.
- **Out of scope**: Elements are provided, not defaulted.

### 2g. `Array.FindAll<T>(T[], Predicate<T>)`

- **Creates default elements**: NO -- returns a new array containing matching elements from the source.
- **Out of scope**: Elements are filtered copies.

---

## Category 3: `System.GC` Allocation Methods

Source: [GC Class](https://learn.microsoft.com/en-us/dotnet/api/system.gc?view=net-10.0)

### 3a. `GC.AllocateArray<T>(int length, bool pinned = false)`

- **Creates default elements**: YES -- zero-initialized (standard .NET array semantics)
- **Generic type parameter**: `T` (element type)
- **Size parameter**: `length` (1st parameter)
- **Availability**: .NET 5+
- **Notes**: Optional `pinned` parameter allocates on the Pinned Object Heap

Source: [GC.AllocateArray](https://learn.microsoft.com/en-us/dotnet/api/system.gc.allocatearray?view=net-9.0)

### 3b. `GC.AllocateUninitializedArray<T>(int length, bool pinned = false)`

- **Creates default elements**: NO (intentionally) -- skips zero-initialization for performance
- **Generic type parameter**: `T` (element type)
- **Size parameter**: `length` (1st parameter)
- **Availability**: .NET 5+
- **Notes**: Elements contain **garbage data**, not `default(T)`. This is actually **worse** than default-initialization from a correctness perspective -- elements are not even zeroed. However, for the analyzer's purpose, flagging this is arguably even MORE important than flagging default-initialized arrays, since the struct is not just default but contains arbitrary memory.

Source: [GC.AllocateUninitializedArray](https://learn.microsoft.com/en-us/dotnet/api/system.gc.allocateuninitializedarray?view=net-9.0)

### 3c. No other array allocation methods exist on `System.GC`

---

## Category 4: `System.Runtime.CompilerServices`

### 4a. `RuntimeHelpers.InitializeArray(Array, RuntimeFieldHandle)`

- **Creates default elements**: NO -- this *populates* an existing array from a field's raw data (used by the compiler for array initializers like `new int[] { 1, 2, 3 }`).
- **Out of scope**: Does not create arrays; it fills them.

### 4b. `RuntimeHelpers.GetSubArray<T>(T[] array, Range range)`

- **Creates default elements**: NO -- copies a slice of the source array into a new array. All elements are copied, none are default.
- **Out of scope**: Elements come from the source.

### 4c. No other array-creating methods in `System.Runtime.CompilerServices`

`Unsafe.SkipInit<T>(out T)` bypasses initialization of a single variable but does not create arrays.

---

## Category 5: `System.Buffers.ArrayPool<T>`

### 5a. `ArrayPool<T>.Shared.Rent(int minimumLength)` / `ArrayPool<T>.Rent(int minimumLength)`

- **Creates default elements**: MAYBE/NO -- returned arrays may contain **stale data** from previous uses. The pool does not guarantee zero-initialization. The returned array may also be **larger** than requested.
- **Generic type parameter**: `T` on the `ArrayPool<T>` class
- **Size parameter**: `minimumLength` (1st parameter)
- **Assessment**: Similar concern to `GC.AllocateUninitializedArray` -- elements are not guaranteed to be `default(T)`. However, `Rent` is a pool operation, not an allocation. The array was originally created elsewhere.
- **Recommendation**: OUT OF SCOPE for SPIRE001. The primary concern of SPIRE001 is *creation* of arrays, not pool rental. `Rent` could be a separate rule (SPIRE-future) about "renting arrays of `[EnforceInitializationialized]` structs from pool without clearing".

Source: [ArrayPool\<T\>.Rent](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1.rent?view=net-8.0)

---

## Category 6: `System.Runtime.InteropServices.Marshal`

### 6a. No array creation methods

`Marshal` deals with unmanaged memory (`AllocHGlobal`, `AllocCoTaskMem`) and marshaling between managed/unmanaged boundaries. Its `Copy` methods copy data between arrays and `IntPtr`, but they do not create new managed arrays.

- **Out of scope**: No managed array creation.

### 6b. `CollectionsMarshal.SetCount<T>(List<T>, int)`

- **Not an array**: Operates on `List<T>`, not arrays.
- **Creates default elements in list**: YES -- when increasing count, new elements are `default(T)`.
- **Assessment**: Out of scope for array rules, but could be relevant for a future `List<T>` rule.

---

## Category 7: LINQ Methods

### 7a. `Enumerable.ToArray<TSource>(IEnumerable<TSource>)`

- **Creates default elements**: NO -- materializes the enumerable into an array. All elements come from the source sequence.
- **Out of scope**: Elements are provided by the source.

### 7b. `Enumerable.Repeat<TResult>(TResult element, int count).ToArray()`

- **Creates default elements**: DEPENDS -- if called as `Enumerable.Repeat(default(MyStruct), 10).ToArray()`, it creates an array of default structs. But the intent is explicit, and the `default` keyword is visible.
- **Assessment**: Out of scope. The user explicitly provides the element value.

### 7c. `Enumerable.Range`, `Enumerable.Select`, etc.

- **Creates default elements**: NO -- elements are computed/transformed.
- **Out of scope**.

---

## Category 8: `System.Collections.Immutable.ImmutableArray`

### 8a. `ImmutableArray.Create<T>(...)` overloads

- **Creates default elements**: NO -- all overloads accept explicit element values (individual items, spans, or arrays).
- **Out of scope**: Elements are always provided.

### 8b. `ImmutableArray.CreateBuilder<T>(int initialCapacity)`

- **Creates default elements**: NOT DIRECTLY -- creates a builder with `Count = 0` and `Capacity = initialCapacity`. No default-initialized elements are accessible until `Count` is increased.

### 8c. `ImmutableArray<T>.Builder.Count` setter

- **Creates default elements**: YES -- when `Count` is increased, new elements are `default(T)`.
- **Assessment**: This is a property setter on a builder, not an array creation method. Edge case. Could be relevant for a future rule but is atypical usage.
- **Out of scope for SPIRE001**: Builder is not an array.

Source: [ImmutableArray\<T\>.Builder.Count](https://learn.microsoft.com/en-us/dotnet/api/system.collections.immutable.immutablearray-1.builder.count?view=net-10.0)

### 8d. `ImmutableArray<T>.Builder.MoveToImmutable()` / `DrainToImmutable()`

- **Creates default elements**: NO (indirectly) -- materializes the builder's contents into an `ImmutableArray<T>`. Elements are whatever was added to the builder. However, if `Count` was increased via the setter (8c), those default elements carry through.
- **Out of scope for SPIRE001**: `ImmutableArray<T>` is a struct wrapping an array, not an array itself.

---

## Summary: Methods In Scope for Analyzer Detection

### Tier 1 -- Primary Targets (language constructs)

| Construct | Roslyn Detection | Element Type Source | Size Source |
|-----------|-----------------|-------------------|------------|
| `new T[length]` (no initializer) | `IArrayCreationOperation` with empty/no initializer | Operation's element type | Dimension sizes in operation |
| `stackalloc T[length]` (no initializer) | `StackAllocArrayCreationExpressionSyntax` (syntax only) | Type in syntax | Size expression in syntax |

### Tier 2 -- API Method Calls (should detect)

| Method | Namespace | Element Type Source | Size Source | Since |
|--------|-----------|-------------------|------------|-------|
| `Array.CreateInstance(Type, ...)` (6 overloads) | `System` | 1st param (`Type`) | Remaining params | .NET 1.0 |
| `Array.CreateInstanceFromArrayType(Type, ...)` (3 overloads) | `System` | 1st param (array `Type`) | Remaining params | .NET 9 |
| `Array.Resize<T>(ref T[], int)` | `System` | Generic type param `T` | 2nd param `newSize` | .NET 2.0 |
| `GC.AllocateArray<T>(int, bool)` | `System` | Generic type param `T` | 1st param `length` | .NET 5 |
| `GC.AllocateUninitializedArray<T>(int, bool)` | `System` | Generic type param `T` | 1st param `length` | .NET 5 |

### Tier 3 -- Out of Scope (elements are provided, not defaulted)

- `Array.Empty<T>()` -- zero-length, no elements
- `Array.Clone()` -- copies existing elements
- `Array.ConvertAll<TInput, TOutput>()` -- converter provides elements
- `Array.FindAll<T>()` -- filter of existing elements
- `RuntimeHelpers.GetSubArray<T>()` -- slice copy
- `RuntimeHelpers.InitializeArray()` -- fills existing array from metadata
- `Enumerable.ToArray()` -- materializes source elements
- `ImmutableArray.Create<T>()` -- elements provided
- `ArrayPool<T>.Rent()` -- pool reuse, not creation (separate concern)
- `Marshal.*` -- no managed array creation
- `CollectionsMarshal.SetCount<T>()` -- operates on `List<T>`, not arrays

---

## Detection Strategy Notes

### For `Array.CreateInstance` / `Array.CreateInstanceFromArrayType`

These use `System.Type` parameters rather than generics. To determine the element type:
1. Check if the argument is a `typeof(T)` expression -- if so, extract `T` and check for `[EnforceInitializationialized]`
2. If the argument is a variable or complex expression, the analyzer cannot statically determine the type -- skip (no false positives)

### For `Array.Resize<T>`

The element type comes from the generic type argument. Detection should flag the call if `T` has `[EnforceInitializationialized]`. Note that `Resize` only creates default elements when *growing* the array, but statically determining growth vs shrink requires data-flow analysis of the size argument. Recommend: flag all `Array.Resize<T>` calls where `T` is `[EnforceInitializationialized]`, since the intent is ambiguous.

### For `GC.AllocateArray<T>` and `GC.AllocateUninitializedArray<T>`

Element type comes from the generic type argument. Straightforward to detect via `IInvocationOperation` with method symbol matching.

---

## Sources

- [System.Array Class -- .NET 10 API Reference](https://learn.microsoft.com/en-us/dotnet/api/system.array?view=net-10.0)
- [Array.CreateInstance Method](https://learn.microsoft.com/en-us/dotnet/api/system.array.createinstance?view=net-10.0)
- [Array.CreateInstanceFromArrayType Method](https://learn.microsoft.com/en-us/dotnet/api/system.array.createinstancefromarraytype?view=net-10.0)
- [Array.Resize Method](https://learn.microsoft.com/en-us/dotnet/api/system.array.resize?view=net-10.0)
- [Array.Empty Method](https://learn.microsoft.com/en-us/dotnet/api/system.array.empty?view=net-10.0)
- [GC.AllocateArray Method](https://learn.microsoft.com/en-us/dotnet/api/system.gc.allocatearray?view=net-9.0)
- [GC.AllocateUninitializedArray Method](https://learn.microsoft.com/en-us/dotnet/api/system.gc.allocateuninitializedarray?view=net-9.0)
- [RuntimeHelpers Class](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.runtimehelpers?view=net-7.0)
- [RuntimeHelpers.GetSubArray Method](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.runtimehelpers.getsubarray?view=net-9.0)
- [ArrayPool\<T\>.Rent Method](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1.rent?view=net-8.0)
- [CollectionsMarshal.SetCount Method](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.collectionsmarshal.setcount?view=net-9.0)
- [ImmutableArray.CreateBuilder Method](https://learn.microsoft.com/en-us/dotnet/api/system.collections.immutable.immutablearray.createbuilder?view=net-8.0)
- [ImmutableArray\<T\>.Builder.Count Property](https://learn.microsoft.com/en-us/dotnet/api/system.collections.immutable.immutablearray-1.builder.count?view=net-10.0)
- [Marshal Class](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.marshal?view=net-10.0)
- [Enumerable.ToArray Method](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.toarray?view=net-9.0)
- [stackalloc expression](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/stackalloc)
