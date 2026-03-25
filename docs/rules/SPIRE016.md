# SPIRE016: Cast to [MustBeInit] enum may produce invalid variant

| Property    | Value           |
|-------------|-----------------|
| **ID**      | SPIRE016        |
| **Category**| Correctness     |
| **Severity**| Error           |
| **Enabled** | Yes             |

## Description

Flags casts to `[MustBeInit]` enums when the resulting value may not correspond to a valid variant. Covers both integer-to-enum and enum-to-enum casts.

- **Non-constant casts** (`(MarkedEnum)variable`) — always flagged, value cannot be verified at compile time
- **Constant casts** (`(MarkedEnum)42`) — flagged when the value doesn't match any named member
- **Enum-to-enum casts** (`(MarkedEnum)(OtherEnum.Value)`) — same rules as integer casts, based on the source's underlying value
- **`[Flags]` enums** — constant composite values are valid if all bits are covered by named members (e.g., `(Flags)3` where `Read=1, Write=2` is valid because `3 = Read|Write`)

Other `[MustBeInit]` enum checks (default expressions, array allocation, Clear, SkipInit, etc.) are handled by SPIRE001–008.

## Examples

### Violating code

```csharp
[MustBeInit]
enum Status { Active = 1, Inactive = 2, Pending = 3 }

Status s = (Status)42;                       // SPIRE016 — 42 is not a named member
int unknown = GetValue();
Status s2 = (Status)unknown;                 // SPIRE016 — value unknown at compile time

[MustBeInit, Flags]
enum Perms { Read = 1, Write = 2, Execute = 4 }

Perms p = (Perms)8;                          // SPIRE016 — bit 3 not covered by any member
```

### Compliant code

```csharp
Status s = (Status)1;                        // OK — matches Active
Status s2 = Status.Active;                   // OK — named member

Perms p = (Perms)3;                          // OK — 3 = Read|Write, valid composite
Perms p2 = (Perms)7;                         // OK — 7 = Read|Write|Execute
```

## When to Suppress

Suppress when you intentionally create an enum value from a trusted integer source that the analyzer cannot verify at compile time.

```csharp
#pragma warning disable SPIRE016
Status s = (Status)trustedValue;
#pragma warning restore SPIRE016
```
