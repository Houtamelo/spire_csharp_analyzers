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
SPIRE009 | Correctness | Error | Switch does not handle all variants of discriminated union
SPIRE011 | Correctness | Error | Discriminated union pattern field type mismatch
SPIRE012 | Correctness | Error | Discriminated union pattern field count mismatch
SPIRE013 | Correctness | Error | Accessing another variant's field
SPIRE014 | Correctness | Warning | Accessing variant field without kind guard
SPIRE015 | Correctness | Error | Switch does not handle all members of [EnforceExhaustiveness] enum
SPIRE016 | Correctness | Error | Operation may produce invalid value of [EnforceInitialization] enum
SPIRE_DU002 | SourceGeneration | Error | ref struct not supported for [DiscriminatedUnion]
SPIRE_DU003 | SourceGeneration | Warning | No [Variant] methods found
SPIRE_DU004 | SourceGeneration | Warning | Layout parameter is ignored for record discriminated unions
SPIRE_DU005 | SourceGeneration | Error | Generic structs cannot use Overlap layout (CLR restriction)
SPIRE_DU006 | SourceGeneration | Error | System.Text.Json not referenced
SPIRE_DU007 | SourceGeneration | Error | Newtonsoft.Json not referenced
SPIRE_DU008 | SourceGeneration | Error | ref struct cannot use JSON generation
SPIRE_DU009 | SourceGeneration | Error | UnsafeOverlap layout requires AllowUnsafeBlocks
SPIRE_DU010 | SourceGeneration | Error | Field name conflict across variants
