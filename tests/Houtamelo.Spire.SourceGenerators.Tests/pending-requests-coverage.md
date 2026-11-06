# Pending request coverage matrix

This matrix covers the two requests from `docs/PENDING.md`:
the SPIRE009 wording for `[EnforceExhaustiveness]` hierarchies and the extra
blank line before the closing brace of a statement fixed by `AddMissingArms`.
The tests should use the existing real generator/analyzer/workspace pipeline;
they must compile and re-run the analyzer after applying the fix.

Existing helpers to reuse:

- `GeneratorTestHelper.RunGenerator` and `RunGeneratedCompilation` for
  discriminated-union inputs.
- `CreateDocument`, `CreateDocumentFromCompilation`, `GetSingleDiagnosticAsync`,
  and `ApplyFirstFixAsync` in `ExactMissingShapeCodeFixTests`.
- `ConfigureFormatting` for LF/CRLF and spaces/tabs.
- `ExhaustivenessAnalyzer` and `CS8509Suppressor` through the existing
  `GeneratorAnalyzerTestBase`/`ExhaustivenessTests` setup.

## Category A: SPIRE009 diagnostic wording

The descriptor contract has a fallback title and an interface-specific title:

- Fallback `Title`: `Switch does not handle all possible cases of type marked with \`[EnforceExhaustiveness]\``.
- Interface `Title`: `Switch does not handle all possible cases of interface marked with \`[EnforceExhaustiveness]\``.
- `GetMessage()`: `Switch on '{0}' does not handle case(s): {1}`.
- `Description`: must describe exhaustive switches for both discriminated unions
  and types marked with `[EnforceExhaustiveness]`, using “all possible cases”.

### should_fail

| File Name | Description |
|-----------|-------------|
| `DiagnosticMessage_EnforceRecord_Statement` | Use the pending `BaseState` abstract-record hierarchy (`IdleState`, `MovingState`, `SpeakingState`) and an empty statement switch. Assert one `SPIRE009`, the fallback title, the generalized description, and `GetMessage() == "Switch on 'BaseState' does not handle case(s): 'IdleState', 'MovingState', 'SpeakingState'"`. |
| `DiagnosticMessage_EnforceClass_Expression` | Use an `[EnforceExhaustiveness]` abstract class with sealed derived classes in a switch expression. Assert the fallback title/description and the new `case(s)` wording. |
| `DiagnosticMessage_EnforceInterface_Statement` | Use an `[EnforceExhaustiveness]` interface with known implementations in a statement switch. Assert the interface-specific title (`...cases of interface marked with...`) and the same `case(s)` full message. This guards selection of the interface descriptor rather than the fallback descriptor. |
| `DiagnosticMessage_EnforceGenericRecord` | Use a generic `[EnforceExhaustiveness]` record hierarchy and omit a derived case. Assert the fallback “type marked with” title, not the interface title, and verify the constructed subject name/message. |
| `DiagnosticMessage_GeneratedStructUnion` | Generate a two- or three-variant struct `[DiscriminatedUnion]` and omit one root variant. Assert the fallback title and `GetMessage()` use `case(s)` and the missing variant name; the old “variant(s)” wording must be absent. |
| `DiagnosticMessage_GeneratedRecordUnion` | Generate a record-shaped union, omit one subtype, and assert the fallback title and new full-message wording. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `DiagnosticMessage_EnforceHierarchy_CompleteStatement` | Cover every sealed derived record in a statement switch. Assert no `SPIRE009`; this is a control proving only wording changed. |
| `DiagnosticMessage_EnforceInterface_Complete` | Cover every known interface implementation. Assert no `SPIRE009` and no descriptor is produced. |
| `DiagnosticMessage_GeneratedUnion_Complete` | Cover every generated union variant with `Is{Variant}: true` or `kind` patterns. Assert no `SPIRE009` and no compiler exhaustiveness diagnostic after the normal suppressor pipeline. |

## Category B: Statement code-fix closing-brace whitespace

Every case must apply `AddMissingArmsCodeFix`, compare the resulting source, emit
the fixed compilation, and re-run `ExhaustivenessAnalyzer`. The final generated
statement must have exactly one line break between the final inserted statement
and the switch closing brace: no whitespace-only blank line is allowed.

### should_fail

| File Name | Description |
|-----------|-------------|
| `CodeFix_Statement_GeneratedUnion_NoBlankLine` | Generated struct union with an empty or partial `switch` statement and missing root variants. Assert the inserted `case` sections end directly before `}` and the fixed generated source compiles with no remaining `SPIRE009`. This exercises the existing `UseVariantTemplates` path. |
| `CodeFix_Statement_EnforceHierarchy_NoBlankLine` | The pending `BaseState` hierarchy with an empty statement switch. Assert the derived-type sections end directly before `}` and the fixed source compiles with no remaining `SPIRE009`. This exercises exact-pattern metadata rather than DU templates. |
| `CodeFix_Statement_ExistingCase_NoBlankLine` | Start with one user-authored case and a missing sibling, leaving the source close brace immediately after the existing section. Assert the new section is appended without introducing a whitespace-only line before `}`. |
| `CodeFix_Statement_CommentBeforeClose_NoBlankLine` | Keep a comment attached to the existing final section or close brace. Assert the comment remains in the fixed source while the generated arm still meets the no-blank-line rule. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `CodeFix_Statement_LineEndingsAndIndentation` | Parameterize LF/CRLF and spaces/tabs using `ConfigureFormatting`. For both generated-union and hierarchy statements, assert the expected indentation and that CRLF input has no lone LF (and LF input has no CR). |
| `CodeFix_Expression_Unchanged` | Apply the fix to a switch expression with missing cases and assert its existing arm formatting remains unchanged; the statement-only closing-brace cleanup must not alter expression output. |

## Implementation/test notes

- Prefer one new diagnostic integration class (for example,
  `Exhaustiveness/DiagnosticMessageTests.cs`) and one code-fix integration class
  (for example, `CodeFix/PendingSwitchFormattingTests.cs`) over broad snapshot
  duplication.
- Assert `Diagnostic.Descriptor.Title`, `Diagnostic.Descriptor.Description`,
  and `Diagnostic.GetMessage()` from the actual analyzer result; do not test a
  descriptor by constructing a separate `DiagnosticDescriptor` in the test.
- For hierarchy messages, assert separately quoted case names in ordinal lexical
  order, using declarations in a different order. DU messages retain variant
  declaration order.
- The no-blank-line assertion should inspect the switch body, not the entire
  source file, so a final newline at end-of-file remains valid. A normalized
  switch body should not contain `lineEnding + indentation + lineEnding` between
  the last inserted statement and the close brace.
