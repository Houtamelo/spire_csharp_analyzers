# Diagnostic continuity coverage

The generator reports an invalid input while preserving any independent API
that remains constructible. Tests use the real Roslyn generator drivers and,
for Unity serialization, the installed Unity reference assemblies.

| Area | Input | Primary diagnostic | Continuity assertion |
| --- | --- | --- | --- |
| Record DU | Conflicting nested record field names plus a consumer of the root record surface | `SPIRE_DU010` | `IsCount` and `IsText` remain generated; no compiler errors obscure the primary diagnostic |
| Unity alias | `FormerlySerializedAs` collides with a current variant | `SPIRE_DU017` | Canonical factories remain available and invalid alias output does not create duplicate declarations |
| Unsupported DU class | Non-partial class marked as a discriminated union | `SPIRE_DU011` | No fake partial declaration or secondary `CS0260`/partial-method errors |
| Unity capability | Serializable union compiled without Unity references | `SPIRE_DU016` | Core union API remains generated and compiles |
| Inlinable delegate arity | Invalid nine-input delegate beside valid one | `SPIRE024` | Invalid delegate stays ordinary; valid sibling gets an eight-or-fewer-input twin |
| Inlinable ref kind | Invalid `ref` delegate beside valid one | `SPIRE025` | Ref delegate remains by-ref; valid sibling twin remains usable |
| Inlinable containing type | Non-partial containing type | `SPIRE023` | No impossible partial twin is emitted |
| Inlinable body | Delegate returned from body beside valid sibling | `SPIRE021` | Valid sibling twin remains emitted without generated compilation errors |
| Inlinable control | Partial host and eight-input delegate | none | Valid twin uses the supported arity and compiles |
| InlinerStruct containing type | Non-partial host | `SPIRE027` | Invalid shape emits no broken source |
| InlinerStruct arity | Nine-input method | `SPIRE019` | No nonexistent interface reference or generated source |
| InlinerStruct control | Partial host and eight-input method | none | Valid inliner source remains constructible |
