# SPIRE004: new T() on [EnforceInitialization] struct without parameterless constructor

| Property    | Value        |
|-------------|--------------|
| Rule ID     | SPIRE004     |
| Category    | Correctness  |
| Severity    | Error        |
| Enabled     | Yes          |

## Description

For structs without a user-defined parameterless constructor, `new T()` produces the exact same zeroed-out instance as `default(T)`. When the struct is marked with `[EnforceInitialization]`, this bypasses the required initialization and should be flagged.

For enums marked with `[EnforceInitialization]`, `new T()` always produces `default(T) = 0`. When the enum has no zero-valued named member, this is flagged. Constructor and field-initializer checks do not apply to enums.

This rule does **not** flag `new T()` when:
- The struct has a user-defined parameterless constructor (the constructor performs meaningful initialization)
- All instance fields and auto-properties have field/property initializers (the compiler generates a parameterless constructor that runs the initializers)

### Flagged patterns

- `new T()` where T is a `[EnforceInitialization]` struct without a user-defined parameterless constructor
- `new T()` where T is a `[EnforceInitialization]` struct with some but not all fields/auto-properties initialized

### Not flagged

- `new T()` where T has a user-defined parameterless constructor
- `new T()` where all fields and auto-properties have initializers
- `new T()` where T is not marked with `[EnforceInitialization]`
- `new T()` where T has no instance fields (fieldless struct)
- `new T(args)` with arguments (calls a parameterized constructor)

## Examples

### Violating code

```csharp
[EnforceInitialization]
struct Config
{
    public string Name;
    public Config(string name) => Name = name;
}

var c = new Config();           // SPIRE004 — equivalent to default(Config)
Config GetEmpty() => new();     // SPIRE004
```

### Compliant code

```csharp
[EnforceInitialization]
struct Config
{
    public string Name;

    public Config()              // user-defined parameterless ctor
    {
        Name = "unnamed";
    }

    public Config(string name) => Name = name;
}

var c = new Config();           // OK — calls the parameterless constructor
var c2 = new Config("hello");   // OK — parameterized constructor

[EnforceInitialization]
struct Defaults
{
    public string Name = "unnamed";  // all fields initialized
}

var d = new Defaults();         // OK — field initializer runs
```

## When to suppress

Suppress when you intentionally need a zeroed instance and understand the struct will be in an uninitialized state:

```csharp
#pragma warning disable SPIRE004
var sentinel = new Config();
#pragma warning restore SPIRE004
```
