# SPIRE_DU020: Reject record-struct union declarations

Status: Implemented and validated.

Struct-based unions must use `struct`, optionally `readonly`, rather than
`record struct`. Report an enabled-by-default SourceGeneration error on the
authored `record` keyword of a `[DiscriminatedUnion]` record struct. The message
is `Struct-based discriminated unions cannot be records. Remove the 'record' keyword.`

Reject the declaration before variant discovery and Unity/JSON validation.
Emit no core union source, JSON converter/schema, drawer, or Unity support
solely for a rejected declaration. Other valid unions in the compilation still
generate normally. Existing source-level errors in user code remain visible;
avoid the generated partial-type/operator/interface compiler cascade.

Flag readonly, generic, nested, fieldless, positional, and serializable outer
record structs in both portable and Unity compilations. Do not flag ordinary
struct unions, record-class unions, unannotated record structs, nested
`[Variant] record struct` payloads, or a record-struct containing type whose
annotated union is itself an ordinary struct. Attribute matching remains the
generator's existing semantic `ForAttributeWithMetadataName` contract.

Extend the existing parser and source-output rejection guard. Register
SPIRE_DU020 in the generator diagnostic registry and unshipped release table.
Do not introduce a duplicate coupled analyzer or an unrelated numeric rule.

Provide a dedicated code fix titled `Remove record from union`, with key
`Spire.Union.RemoveRecord`. Convert every authored partial declaration of the
same union symbol to an ordinary struct, preserving modifiers, attributes,
generic constraints, base lists, bodies, nested variants, comments and line
endings. Handle bodyless declarations and empty positional lists by producing
an empty struct body where needed. Never discard nonempty positional
parameters: offer no fix if any authored part has them. The error still directs
the user to refactor that declaration manually. No action for unrelated,
stale, indeterminate, generated, lookalike, or non-union targets.

Use actual generator diagnostics in code-fix tests. The existing fixture base
currently collects analyzer diagnostics only; extend it narrowly to include
generator diagnostics remapped to the workspace source trees and validate the
new fix by regeneration and compilation. Retain existing analyzer-codefix
behavior.

Tests precede implementation. Cover exact error severity/location/message,
generation suppression, Unity priority, supported declaration exemptions,
multi-union and multi-document cases, code-fix trivia and partial parts,
nonempty positional refusal, regeneration/compilation and representative
runtime conversion/equality after the fix. Update the SPIRE030 regression to
expect SPIRE_DU020 with no CS0261 cascade. Run focused red/green checks and the
full relevant generator/code-fix and runtime test projects.
