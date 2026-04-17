## Release 4.6.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
SPIRE017 | SourceGeneration | Error | [InlinerStruct] method has unsupported parameter modifier
SPIRE018 | SourceGeneration | Error | [InlinerStruct] declared on a ref struct
SPIRE019 | SourceGeneration | Error | [InlinerStruct] arity exceeds 8
SPIRE020 | SourceGeneration | Error | Generated inliner struct name collides
SPIRE021 | Correctness | Error | [Inlinable] parameter used in unsupported form
SPIRE022 | Correctness | Error | [Inlinable] on non-delegate parameter
SPIRE023 | SourceGeneration | Error | Containing type of [Inlinable] method is not partial
SPIRE024 | SourceGeneration | Error | [Inlinable] delegate arity exceeds 8
SPIRE025 | Correctness | Error | [Inlinable] parameter has unsupported ref-kind
SPIRE026 | Correctness | Error | [Inlinable] on property/indexer parameter
SPIRE027 | SourceGeneration | Error | Declaring/enclosing type is not partial
