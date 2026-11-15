# SPIRE030 Code Fix Coverage Matrix

The fix is a standalone provider for SPIRE030. Its action title is `Remove
readonly from variant`, with equivalence key
`Spire.RecordVariant.RemoveReadonly`; the provider is fixable for SPIRE030
only and uses the batch fixer. The input must be a public nested `[Variant]`
record struct with positional data, inside a mutable non-ref
`[Serializable, DiscriminatedUnion]` struct union in an active Unity
serialization compilation.

The transformation removes the authored `readonly` modifier from every
declaration of the same record symbol. It preserves attributes, `partial`,
accessibility, parameter lists, comments, directives, line endings, and
unrelated declarations. It does not add serialization attributes, make a
record partial, or edit generated declarations. The resulting solution is
regenerated and compiled; missing serialization metadata may then produce
SPIRE028, which is expected for a non-partial variant.

## Basic source transformations

These are `before.cs`/`after.cs` pairs in the existing CodeFix discovery
format. Each `before.cs` carries `//@ unity` and
`//@ codefix: Spire.RecordVariant.RemoveReadonly`.

| Case name | Before | After |
|---|---|---|
| `SPIRE030_RemoveReadonly_BasicPartial` | `[Variant] public readonly partial record struct Value(int amount);` | `readonly` is removed; `partial record struct Value(int amount)` remains otherwise identical. |
| `SPIRE030_RemoveReadonly_NonPartial` | `[Variant] public readonly record struct Value(int amount);` | The variant becomes mutable and non-partial; no `[Serializable]`, `[SerializeField]`, or `partial` is added. SPIRE028 may remain after regeneration. |
| `SPIRE030_RemoveReadonly_FullyAnnotated` | A readonly partial record with `[Serializable]` and complete positional field annotations. | Only `readonly` is removed; existing type and field attributes remain. SPIRE028 must not be introduced. |
| `SPIRE030_RemoveReadonly_ManagedReference` | A readonly record with a reference positional parameter marked `[field: SerializeReference]` or `[UnitySerializeReference]`. | Only `readonly` is removed; the managed-reference marker and field attribute remain. |
| `SPIRE030_RemoveReadonly_Fieldless_NoAction` | A fieldless readonly record variant (`None` or `Empty()`). | No SPIRE030 diagnostic or action is available; this is a regression guard that the fix does not invent a target. |

The fieldless row is an explicit no-action fixture even though the ordinary
CodeFix discovery runner normally requires an offered action. It belongs in a
direct provider test if the fixture runner cannot express “no action”.

## Declaration shape and trivia

| Case name | Before | After / assertion |
|---|---|---|
| `SPIRE030_RemoveReadonly_ModifierOrder` | `public readonly partial record struct` with additional legal modifiers in an unusual order. | Only the readonly token is removed; all other modifiers retain their relative order. |
| `SPIRE030_RemoveReadonly_LeadingAndTrailingTrivia` | Comments between modifiers, comments adjacent to `readonly`, and trailing comments on the declaration. | The comments remain attached to the same surrounding tokens; only the readonly token and its associated spacing are removed. |
| `SPIRE030_RemoveReadonly_Directives` | `#if`/`#endif` or nullable directives around the declaration and a readonly token on the active branch. | Directives and inactive text remain byte-for-byte unchanged; the active declaration compiles. |
| `SPIRE030_RemoveReadonly_Attributes` | Multiple attributes, including `[Variant]`, `[Serializable]`, and custom attributes before the declaration. | Attribute lists and their order remain unchanged. |
| `SPIRE030_RemoveReadonly_SiblingIsolation` | One readonly target and one mutable or differently named sibling variant in the same union. | Only the targeted record declaration changes; sibling source is unchanged. |

Use a direct workspace assertion for trivia and directives because the generic
fixture runner normalizes syntax before comparison.

## Multiple declarations and documents

These scenarios require a direct Roslyn `Solution` test rather than the
single-source CodeFix fixture runner.

| Case name | Setup | Expected result |
|---|---|---|
| `SPIRE030_RemoveReadonly_AllAuthoredPartialParts` | The same `Value` record symbol has two authored partial declarations; both carry `readonly`, and only one contains the positional parameter list. | One action removes `readonly` from both authored declarations. The record compiles and generates once. |
| `SPIRE030_RemoveReadonly_MultiDocument` | The same record symbol is split across two documents, with the diagnostic located in one document. | The changed solution updates every authored declaration across documents, preserving document identities and unrelated text. |
| `SPIRE030_RemoveReadonly_OneReadonlyPart` | A partial record has one readonly declaration and one already mutable declaration. | The action removes the modifier only from the readonly declaration; the mutable part is unchanged. |
| `SPIRE030_RemoveReadonly_MultipleDiagnostics` | One union contains two readonly fielded variants, each producing SPIRE030. | Applying the action to one diagnostic changes only that variant; applying Fix All changes both. No duplicate modifier removal occurs. |
| `SPIRE030_RemoveReadonly_GeneratedPartIsolation` | Generated output includes a partial declaration with readonly while the authored declaration is the diagnostic target. | The provider changes authored syntax only; generated trees are not edited directly. |

The implementation should identify declarations by `INamedTypeSymbol` using
semantic equality, not by record name or text matching. This avoids modifying a
same-named record in another union or namespace.

## Post-fix validation

| Case name | Validation |
|---|---|
| `SPIRE030_RemoveReadonly_RegenCompiles` | Apply the action, rerun the generator and analyzer, and assert no C# or generator errors. Runtime conversion and payload reconstruction remain usable. |
| `SPIRE030_RemoveReadonly_NonPartialLeavesSPIRE028` | Apply the action to a non-partial record missing serialization metadata. SPIRE030 disappears; SPIRE028 can remain and the fix does not silently add metadata. |
| `SPIRE030_RemoveReadonly_PreservesSPIRE029` | A readonly record also has an authored positional property/field that triggers SPIRE029. After the fix, SPIRE029 remains while SPIRE030 disappears. |
| `SPIRE030_RemoveReadonly_EqualityAndHashing` | After regeneration, equal payload values still compare equal and have matching hashes. This confirms the fix changes declaration mutability only. |

## No-action and negative cases

These should be direct `RegisterCodeFixesAsync` tests that assert zero actions.

| Case name | Input / invocation | Expected result |
|---|---|---|
| `SPIRE030_RemoveReadonly_OtherDiagnostic` | Invoke the provider with SPIRE028, SPIRE029, SPIRE_DU013, or an unrelated diagnostic. | No action is registered. `FixableDiagnosticIds` contains SPIRE030 only. |
| `SPIRE030_RemoveReadonly_StaleDiagnostic` | Diagnostic span no longer resolves to the authored readonly record declaration. | No action is registered and the solution is unchanged. |
| `SPIRE030_RemoveReadonly_AlreadyMutable` | Diagnostic properties/span point to a record whose readonly modifier was already removed. | No action is registered; the provider does not rewrite the declaration. |
| `SPIRE030_RemoveReadonly_NonVariant` | A readonly nested record struct without `[Variant]`, or a same-named record in another union. | No action is registered. Semantic ownership prevents text-based edits. |
| `SPIRE030_RemoveReadonly_LookalikeAttributes` | Source-local attributes with names resembling `Variant`, `DiscriminatedUnion`, or `Serializable`. | No action is registered unless the diagnostic is genuine SPIRE030 from the real attributes. |
| `SPIRE030_RemoveReadonly_OuterUnsupportedUnion` | A readonly/ref/record outer union that already produces an unsupported Unity serialization diagnostic. | No SPIRE030 action is registered as a cascade. |

## Acceptance checks

- At least four fixture pairs cover basic, non-partial, fully annotated, and
  managed-reference variants.
- At least one direct workspace test covers multi-document, multi-part edits;
  one covers trivia/directives; and one covers Fix All.
- Every changed solution is rerun through the actual generator and compiler;
  no mock syntax-only success is sufficient.
- Unity-dependent tests carry `Trait("Environment", "Unity")` and use the
  installed Unity references. Portable tests must not claim SPIRE030 behavior.
- The provider never edits `src` generated trees, unrelated sibling variants,
  or serialization attributes.
