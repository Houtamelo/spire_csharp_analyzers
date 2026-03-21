### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
SPIRE_DU002 | SourceGeneration | Error | ref struct not supported for [DiscriminatedUnion]
SPIRE_DU003 | SourceGeneration | Warning | No [Variant] methods found
SPIRE_DU004 | SourceGeneration | Warning | Layout parameter is ignored for record/class discriminated unions
SPIRE_DU005 | SourceGeneration | Error | Generic structs cannot use Overlap layout (CLR restriction)
SPIRE_DU006 | SourceGeneration | Error | System.Text.Json not referenced
SPIRE_DU007 | SourceGeneration | Error | Newtonsoft.Json not referenced
SPIRE_DU008 | SourceGeneration | Error | ref struct cannot use JSON generation
SPIRE_DU009 | SourceGeneration | Error | UnsafeOverlap layout requires AllowUnsafeBlocks
SPIRE_DU010 | SourceGeneration | Error | Field name conflict across variants
SPIRE009 | Correctness | Error | Switch does not handle all variants of discriminated union
SPIRE011 | Correctness | Error | Discriminated union pattern field type mismatch
SPIRE012 | Correctness | Error | Discriminated union pattern field count mismatch
SPIRE013 | Correctness | Error | Accessing another variant's field
SPIRE014 | Correctness | Warning | Accessing variant field without tag guard
