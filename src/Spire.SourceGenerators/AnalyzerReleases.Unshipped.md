### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
SPIRE_DU002 | SourceGeneration | Error | ref struct not supported for [DiscriminatedUnion]
SPIRE_DU003 | SourceGeneration | Warning | No [Variant] methods found
SPIRE_DU004 | SourceGeneration | Warning | Layout parameter is ignored for record/class discriminated unions
SPIRE_DU005 | SourceGeneration | Error | Generic structs cannot use Overlap layout (CLR restriction)
SPIRE009 | Correctness | Error | Switch does not handle all variants of discriminated union
SPIRE010 | Correctness | Warning | Switch uses wildcard instead of exhaustive variant matching
SPIRE011 | Correctness | Error | Discriminated union pattern field type mismatch
SPIRE012 | Correctness | Error | Discriminated union pattern field count mismatch
SPIRE013 | Correctness | Error | Accessing another variant's field
SPIRE014 | Correctness | Warning | Accessing variant field without tag guard
