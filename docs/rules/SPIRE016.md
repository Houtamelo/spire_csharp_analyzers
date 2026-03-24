# SPIRE016: Operation may produce invalid value of [MustBeInit] enum

| Property    | Value           |
|-------------|-----------------|
| **ID**      | SPIRE016        |
| **Category**| Correctness     |
| **Severity**| Error           |
| **Enabled** | Yes             |

## Description

Enums marked with `[MustBeInit]` require all values to correspond to named members. This rule flags operations that may produce unnamed values:

- `default(T)` / `default` literal — flagged only when the enum has no zero-valued named member
- Integer-to-enum casts — flagged when the value is unknown at compile time or doesn't match any named member
- `Unsafe.SkipInit` — always flagged, produces garbage data regardless of zero member
- Zero-initialization operations (`Array.Clear`, `Span<T>.Clear`, `Activator.CreateInstance`, `RuntimeHelpers.GetUninitializedObject`, array allocation) — flagged only when the enum has no zero-valued named member

When an enum has a zero-valued named member, `default` produces that member, which is valid. In that case, default expressions are not flagged.

## Examples

### Violating code

```csharp
[MustBeInit]
enum Status { Active = 1, Inactive = 2, Pending = 3 }

Status s = default;                          // SPIRE016 — no zero-valued member
var s2 = default(Status);                    // SPIRE016
Status s3 = (Status)42;                      // SPIRE016 — 42 is not a named member
int unknown = GetValue();
Status s4 = (Status)unknown;                 // SPIRE016 — value unknown at compile time
```

### Compliant code

```csharp
[MustBeInit]
enum StatusOk { None = 0, Active = 1 }

StatusOk s = default;                        // OK — default = None, a named member
StatusOk s2 = StatusOk.Active;              // OK — named member
StatusOk s3 = (StatusOk)1;                  // OK — matches Active
```

## When to Suppress

Suppress when you intentionally need to create an unnamed enum value, for example as a sentinel or for bit manipulation in `[Flags]` enums where composite values are expected.

```csharp
#pragma warning disable SPIRE016
Status sentinel = default;
#pragma warning restore SPIRE016
```
