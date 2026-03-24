# SPIRE008: RuntimeHelpers.GetUninitializedObject on [MustBeInit] struct

| Property    | Value        |
|-------------|--------------|
| **ID**      | SPIRE008     |
| **Category**| Correctness  |
| **Severity**| Error        |
| **Enabled** | Yes          |

## Description

`RuntimeHelpers.GetUninitializedObject(Type)` bypasses all constructors and field initializers,
producing a zero-initialized instance. When the type is marked with `[MustBeInit]`, this
defeats the purpose of the attribute.

For enums marked with `[MustBeInit]`, the rule only flags when the enum has no zero-valued named member.

Only flags calls where the argument is a direct `typeof(T)` expression resolving to a concrete
`[MustBeInit]` struct with fields. Indirect type references (variables, method returns, generic
type parameters) are not tracked.

### Flagged patterns

- `RuntimeHelpers.GetUninitializedObject(typeof(MustInitStruct))` where `MustInitStruct` is a `[MustBeInit]` struct with fields

### Not flagged

- Types not marked `[MustBeInit]`
- Fieldless `[MustBeInit]` structs (SPIRE002 handles this)
- Indirect type arguments: `Type t = typeof(T); GetUninitializedObject(t);`
- Generic type parameters: `GetUninitializedObject(typeof(T))` in generic method

## Examples

### Violating code

```csharp
[MustBeInit]
struct Config { public string Name; public Config(string name) => Name = name; }

var obj = RuntimeHelpers.GetUninitializedObject(typeof(Config)); // SPIRE008
```

### Compliant code

```csharp
var c = new Config("default");  // proper initialization
```

## When to suppress

Suppress only in serialization or framework code where you guarantee the struct will be fully
initialized before any read.

```csharp
#pragma warning disable SPIRE008
var obj = RuntimeHelpers.GetUninitializedObject(typeof(Config));
#pragma warning restore SPIRE008
// ... manually initialize all fields via reflection
```

## See also

- [SPIRE005](SPIRE005.md) — similar detection for `Activator.CreateInstance`
