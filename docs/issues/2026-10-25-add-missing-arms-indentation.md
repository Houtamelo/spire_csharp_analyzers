# Add missing variant arms indentation

User request, recorded before implementation: `Add missing variant arms` inserts
lines with four extra spaces. Newly inserted arms and statements must match the
surrounding source's indentation. Preserve inline property patterns, existing
source, line endings, and pattern/capture semantics.

Confirmed cause: the showcase uses a block namespace whose contents are not
indented. Roslyn cleanup of newly created elastic syntax adds an indentation
level absent from the surrounding source. Existing structural codefix snapshots
normalize whitespace and cannot catch this difference. New expression arms
also lacked leading indentation.

Regression contract: apply the real SPIRE009 code action in an AdhocWorkspace,
compare actual document text with hand-written expected text, regenerate and
emit the fixed source. Do not normalize actual whitespace before comparing it.

| Fixture suffix | Scenario |
|---|---|
| StatementOutdentedNamespace | Existing label and body establish indentation; append fielded and fieldless sections. |
| StatementOutdentedNamespaceTrailingComment | Preserve an existing end-of-line comment while appending sections. |
| ExpressionOutdentedNamespace | Append fielded and fieldless expression arms. |
| ExpressionOutdentedNamespaceNoTrailingComma | Keep separator placement correct when the existing last arm has no trailing comma. |
| EmptyExpressionOutdentedNamespace | Infer indentation from the empty switch's surrounding source. |
| EmptyExpressionOutdentedNamespaceTwoSpaces | Respect a configured two-space indentation unit when no arm exists to copy. |
| StatementOutdentedNamespaceCRLF | Preserve CRLF while aligning inserted sections. |
| StatementOutdentedNamespaceTabs | Preserve tab-based indentation with four-column tabs. |
| StatementIndentedNamespace | Control: normally indented namespace retains its layout. |
| ExpressionFileScopedNamespace | Control: file-scoped namespace adds no extra level. |
| StatementGlobalNamespace | Control: namespace-free consumer remains aligned. |
| RecordStatementOutdentedNamespace | Align record-union sections as well as struct-union sections. |

All fixture names start with `Indentation_AddMissingArms_` under
`tests/Houtamelo.Spire.SourceGenerators.Tests/ReviewRegressions/cases/AddMissingArmsIndentation/`.
Only whitespace in newly added code is in scope; source-generator output is
unchanged. Existing user edits are outside the implementation scope.

The fix uses explicit nonelastic syntax trivia and copies the surrounding case,
arm, and statement indentation. Empty switches use per-document indentation
options. Existing trailing comments remain intact; record pattern construction
retains its previous positional form. The record fixture already handles null
so it isolates indentation from the separate known missing-null-arm defect.

These fixtures belong to the exact-text integration runner because the generic
codefix harness intentionally strips comments before applying fixes. The new
runner compares the actual code action's document text, then regenerates and
emits that source; it does not normalize actual whitespace or skip assertions.

Validation: all 12 exact-text regressions and eight existing inline-pattern
checks passed. Full Release solution: **2,127 passed, zero failed/skipped**.
All 77 fixed generated-output files remain byte-identical. Changed production
and runner C# files were formatted. Refreshed the existing local UnityShowcase
package without changing its version; all four package DLLs match the tested
Release outputs. Reports and package hashes:
`tmp/add-missing-arms-indentation/summary.json`.
