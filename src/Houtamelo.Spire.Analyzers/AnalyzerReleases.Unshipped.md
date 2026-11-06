### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|------
SPIRE021 | Correctness | Warning | Correctness | Error | Downgraded to Warning — the patterns SPIRE021 catches (lambda capture, reassignment, field storage, casts, returns) either silently lose the JIT monomorphization benefit or surface as C# compiler errors in the generated twin; Warning leaves the intentional-override case ergonomic.

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
SPIRE001 | Correctness | Error | Non-empty array of [EnforceInitialization] type produces uninitialized elements
SPIRE002 | Correctness | Warning | [EnforceInitialization] on fieldless type has no effect
SPIRE003 | Correctness | Error | default(T) where T is a [EnforceInitialization] type produces an uninitialized value
SPIRE004 | Correctness | Error | new T() on [EnforceInitialization] struct without parameterless constructor is equivalent to default(T)
SPIRE005 | Correctness | Error | Activator.CreateInstance on [EnforceInitialization] struct produces a default instance
SPIRE006 | Correctness | Error | Clearing array or span of [EnforceInitialization] type produces uninitialized elements
SPIRE007 | Correctness | Error | Unsafe.SkipInit on [EnforceInitialization] struct leaves it uninitialized
SPIRE008 | Correctness | Error | RuntimeHelpers.GetUninitializedObject on [EnforceInitialization] struct bypasses all constructors
SPIRE009 | Correctness | Error | Switch does not handle all possible cases of type marked with `[EnforceExhaustiveness]`
SPIRE011 | Correctness | Error | Discriminated union pattern field type mismatch
SPIRE012 | Correctness | Error | Discriminated union pattern field count mismatch
SPIRE013 | Correctness | Error | Attempting to access mismatched variant data
SPIRE014 | Correctness | Warning | Attempting to access variant data without checking 'kind'
SPIRE015 | Correctness | Error | Switch does not handle all members of [EnforceExhaustiveness] enum
SPIRE016 | Correctness | Error | Operation may produce invalid value of [EnforceInitialization] enum
SPIRE_DU002 | SourceGeneration | Error | ref struct not supported for [DiscriminatedUnion]
SPIRE_DU003 | SourceGeneration | Warning | No [Variant] functions found
SPIRE_DU006 | SourceGeneration | Error | System.Text.Json not referenced
SPIRE_DU007 | SourceGeneration | Error | Newtonsoft.Json not referenced
SPIRE_DU008 | SourceGeneration | Error | ref struct cannot use JSON generation
SPIRE_DU010 | SourceGeneration | Error | Field name conflict across variants
SPIRE_DU011 | SourceGeneration | Error | Class types are not supported for [DiscriminatedUnion]
SPIRE_DU013 | SourceGeneration | Error | Union declaration cannot use Unity serialization
SPIRE_DU015 | SourceGeneration | Error | Member conflicts with generated Unity serialization
SPIRE_DU016 | SourceGeneration | Error | Unity serialization requires UnityEngine references
SPIRE_DU017 | SourceGeneration | Error | Invalid or ambiguous Unity variant serialization alias
SPIRE_DU018 | SourceGeneration | Error | Discriminated union cannot be nested in a generic type; generated API is retained
