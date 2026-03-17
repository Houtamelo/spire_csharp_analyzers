# Spire.Analyzers

Roslyn analyzers for C# struct correctness.

## Installation

```shell
dotnet add package Spire.Analyzers
```

## Rules

| Rule                               | Description                                                          |
|------------------------------------|----------------------------------------------------------------------|
| [SPIRE001](docs/rules/SPIRE001.md) | Non-empty array of `[MustBeInit]` struct produces default instances  |
| [SPIRE002](docs/rules/SPIRE002.md) | `[MustBeInit]` on fieldless type has no effect                       |
| [SPIRE003](docs/rules/SPIRE003.md) | `default(T)` where T is a `[MustBeInit]` struct                      |
| [SPIRE004](docs/rules/SPIRE004.md) | `new T()` on `[MustBeInit]` struct without parameterless constructor |
| [SPIRE005](docs/rules/SPIRE005.md) | `Activator.CreateInstance` on `[MustBeInit]` struct                  |
| [SPIRE006](docs/rules/SPIRE006.md) | Clearing array or span of `[MustBeInit]` struct                      |
| [SPIRE007](docs/rules/SPIRE007.md) | `Unsafe.SkipInit` on `[MustBeInit]` struct                           |
| [SPIRE008](docs/rules/SPIRE008.md) | `RuntimeHelpers.GetUninitializedObject` on `[MustBeInit]` struct     |

## License

[MIT](LICENSE)
