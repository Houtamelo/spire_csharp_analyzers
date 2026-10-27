# AddMissingArms deconstruction coverage and syntax evidence

Implemented and verified: 134 focused tests and all 2,363 solution tests pass,
zero skipped. All 77 generated-source controls remain unchanged. The existing
local UnityShowcase package was refreshed without a version bump. Evidence:
`tmp/pending-requests-20261026/full-suite/` and `verification.json` in its parent.

The approved request from `docs/PENDING.md` changes generated payload arms for
struct discriminated unions from nested property patterns such as
`rectangle: { width: var width, height: var height }` to the shorter
`rectangle: var (width, height)`. The same shared pattern builder is used by
`AddMissingArmsCodeFix` for switch statements and expressions and by
`ExpandWildcardRefactoring`; the implementation should change that shared
output once.

This matrix does not implement the pending discussion about nullable
`effect:null`/`effect:not null` coverage. It also
does not broaden pattern analysis to arbitrary deconstruction. The analyzer
control needed here is limited to proving an all-capture `var (...)` payload
subpattern universal.

## Roslyn 4.10 syntax probe

The scratch probe is in `tmp/pending-requests-20261026/` and references
`Microsoft.CodeAnalysis.CSharp` 4.10.0. It parses each snippet with
`LanguageVersion.Preview`, compiles it with `CSharpCompilation`, and reports
syntax/compilation errors. The relevant results are:

```text
dotnet run --project tmp/pending-requests-20261026/PendingDeconstructionSyntax.csproj --no-restore
dotnet run --project tmp/pending-requests-20261026/GeneratorAnalyzerProbe.csproj --no-restore
```

| Source inside a property subpattern | Parse | Compile | Contract |
|---|---|---|---|
| `{ Payload: var (x, y) }` | no errors | no errors | Valid nested all-capture form for a two-output `Deconstruct`. |
| `{ Payload: var (x) }` | no errors | no errors | Valid nested all-capture form for a one-output `Deconstruct`; use this representation for unary payloads. |
| `{ Payload: var(x) }` | no errors | no errors | Whitespace is optional; generated output should use the readable spaced form. |
| `{ Payload: (var x, var y) }` | no errors | no errors | Valid typed-free positional pattern for two outputs, but not the requested generated form. |
| `{ Payload: (var x) }` | no errors | `CS0029` (`Payload` to `int`) | A unary parenthesized pattern is not a one-output `Deconstruct` call; do not generate it. |
| `{ Payload: Payload(var x) }` | no errors | no errors | Explicit type plus positional pattern is valid, but unnecessarily couples output to a payload type and is not the requested form. |
| `{ Payload: var (@event) }` | no errors | no errors | Escaped capture names remain valid. |
| `{ Payload: var (_1) }` | no errors | no errors | A parameter spelled `_` must be uniquified before emission; `var (_)` would discard the value. |
| `{ Payload: var (value) }` on `Payload<T>` | no errors | no errors | Generic payload deconstruction works without an explicit payload type. |

The syntax-tree probe confirms that `var (x, y)` is a `VarPatternSyntax` with a
`ParenthesizedVariableDesignationSyntax`. In the generator/analyzer probe,
Roslyn binds the nested property subpattern as an
`IRecursivePatternOperation` whose syntax is
`ParenthesizedVariableDesignationSyntax`; its children are plain variable
designation operations. This is why checking only the nested operation's
`Syntax` for `VarPatternSyntax` is insufficient.

The generated-union analyzer control produced these results:

| Input | Compiler/analyzer result |
|---|---|
| Only `Alpha` covered, `alpha: var (value)`, `Beta` missing | Compiler reports `CS8509`; custom `SPIRE009` is not reported because the current analysis deliberately marks the unsupported nested operation indeterminate. |
| `Alpha` and `Beta` both covered with all-capture `var (...)` payloads | No custom diagnostic, but the current suppressor leaves compiler `CS8524` (unnamed enum value) because the nested operation is indeterminate. After the minimal all-capture recognition, the post-fix test must assert that `SPIRE009`, `CS8509`, and `CS8524` are all absent. |

The minimal analysis contract is to recognize a nested parenthesized variable
designation as unrestricted only when every child is an unconstrained capture
(including a generated `_1` capture). Typed/constant/partial positional
patterns remain outside this request and may stay indeterminate.

## Coverage matrix

### Category A: AddMissingArms — payload arity and switch shape

| Case Name | Description |
|-----------|-------------|
| `AddMissingArms_Deconstruct_ZeroPayload` | before: a struct union with only fieldless variants and one missing arm. after: the generated arm stays `{ kind: Union.Kind.Variant }`; no empty deconstruction is emitted. Update `AddMissingArms_Property_AllFieldless`, `Property_TwoFieldless`, and `Property_FieldlessVariant` expected output as applicable. |
| `AddMissingArms_Deconstruct_UnaryExpression` | before: expression switch missing a one-field variant. after: `payload: var (field)` in declaration order; expected source compiles through the generator and analyzer. Update the unary arms in `FieldedExpression`, `Generic`, and `KeywordAndNullable`. |
| `AddMissingArms_Deconstruct_UnaryStatement` | before: statement switch missing a one-field variant. after: `case { kind: ..., payload: var (field) }:` with the existing body/indentation preserved. Keep `FieldlessStatement` as the zero-payload statement control and add/adjust a fielded statement fixture. |
| `AddMissingArms_Deconstruct_MultipleExpression` | before: expression switch with a present arm and missing two- and three-field variants. after: `var (x, y)` and `var (x, y, z)` captures, in factory/`Deconstruct` order. Update `MultiField`, `MultipleVariants`, `Struct`, and `ExistingNestedPattern`. |
| `AddMissingArms_Deconstruct_MultipleStatement` | before: statement switch with multiple missing fielded variants. after: each generated case uses all-capture `var (...)`; no generated property clause is split across lines. Include the statement indentation fixture. |
| `AddMissingArms_Deconstruct_AllMissing` | before: empty switch expression over a union with unary and multi-field variants. after: every fielded variant is added in deconstruction form and the fieldless variant is kind-only. This is the zero-existing-arm style fallback. |
| `AddMissingArms_Deconstruct_AsyncAndExpressionBodied` | before: missing arm inside an async method and an expression-bodied property. after: only the targeted switch changes; `await`/expression-bodied syntax and inferred captures remain valid. Update `Property_AsyncMethod` and `Property_ExpressionBodiedProperty`. |
| `AddMissingArms_Deconstruct_MultipleDiagnostics` | before: two incomplete switches in one file. after: applying the first action changes only the diagnostic's switch; the second switch and its user arms remain byte-for-byte/structurally unchanged. |

### Category B: AddMissingArms — payload names, types, and collisions

| Case Name | Description |
|-----------|-------------|
| `AddMissingArms_Deconstruct_GenericUnion` | before: closed `Result<int>`/open `Option<T>` union with a missing fielded variant. after: `var (value)` captures the generic `Deconstruct` output; the `Union<T>.Kind.Variant` qualification remains correct. |
| `AddMissingArms_Deconstruct_NestedUnion` | before: union nested in a nongeneric outer type. after: nested type qualification is unchanged and the payload arm uses `var (...)`. Nesting inside a generic outer type remains unsupported. |
| `AddMissingArms_Deconstruct_EscapedVariantAndField` | before: variants or parameters named `@event`, `@class`, or another C# keyword. after: kind/accessor names and captures retain required `@` escaping and compile; no bare keyword is emitted. |
| `AddMissingArms_Deconstruct_UnderscoreParameter` | before: variant parameter named `_` whose missing arm body consumes the value. after: generated capture is `_1` (or the established unique equivalent), not `_`, so it is a value and not a discard. |
| `AddMissingArms_Deconstruct_FieldNameCollision` | before: incompatible same-name fields such as `Count(int value)` and `Text(string value)`. after: each variant retains its own accessor while deconstruction captures follow factory order and avoid enclosing names. |
| `AddMissingArms_Deconstruct_AcronymAndFactoryConflict` | before: `run` and `HTTPRequest` factory names plus colliding fields. after: acronym-normalized/qualified accessor names are unchanged and each generated `var (...)` capture binds the intended payload. Update `Property_AcronymAndFactoryConflict`. |
| `AddMissingArms_Deconstruct_CaptureCollision` | before: enclosing locals/patterns already declare `value`, `value1`, etc. after: generated designation names are uniquified (`value2`, ...), preserve declaration order, and compile. Include both leading and trailing local collisions. |
| `AddMissingArms_Deconstruct_SpecialFieldTypes` | before: nullable reference/value, array, enum, and user struct fields. after: all-capture `var (...)` preserves inferred types and null matching; no explicit nullable declaration pattern is emitted. Update ArrayField, EnumField, StructField, and nullable regression cases. |
| `AddMissingArms_Deconstruct_TupleElementNameCollision` | before: fields whose underlying tuple types match but element names differ. after: shared/qualified generated property lookup is unchanged and captures use the `Deconstruct` parameter order, not tuple display names. |

### Category C: formatting and source preservation

| Case Name | Description |
|-----------|-------------|
| `AddMissingArms_Deconstruct_InlinePropertyPattern` | before: existing user arms contain inline property patterns and unusual spacing. after: newly generated `var (...)` remains inline; existing property arms are not rewritten. Keep the structural inline assertions in `VariantPayloadCodeFixTests`. |
| `AddMissingArms_Deconstruct_IndentationVariants` | before: outdented/indented block namespaces, file-scoped/global namespaces, two-space indentation, tabs, and CRLF. after: only inserted arms change, with the established indentation and line endings. Update all 12 `ReviewRegressions/cases/AddMissingArmsIndentation` pairs. |
| `AddMissingArms_Deconstruct_TrailingComment` | before: the last existing arm has an end-of-line comment. after: the comment stays attached to the original arm and the new deconstruction arm is inserted at the correct indentation. |
| `AddMissingArms_Deconstruct_ExistingPropertyArmPreserved` | before: a user-written fielded property arm such as `circle: { radius: var radius }`. after: that arm is unchanged; only missing generated arms use `circle: var (radius)`. |
| `AddMissingArms_Deconstruct_ExistingPositionalArmPreserved` | before: a user-written positional/typed payload pattern. after: the code fix does not normalize or rewrite it; generated missing arms use all-capture deconstruction independently. |
| `AddMissingArms_Deconstruct_ExistingBodiesPreserved` | before: non-throwing bodies, comments, and expression arms. after: generated bodies retain the current code-fix body contract and user bodies are untouched. |
| `AddMissingArms_Deconstruct_OneActionScope` | before: multiple fixable diagnostics or switches. after: applying one action changes one switch/diagnostic only, with no global replacement of property patterns. |

### Category D: ExpandWildcard and non-target controls

| Case Name | Description |
|-----------|-------------|
| `ExpandWildcard_Deconstruct_StructFielded` | before: struct-union switch expression with an explicit arm and `_`. after: each generated fielded arm uses the same `payload: var (...)` representation as AddMissingArms; wildcard body is copied to every missing variant. Update `StructUnion_ExplicitArmAndWildcard_ExpandsAndEmits`. |
| `ExpandWildcard_Deconstruct_StructZeroPayload` | before: wildcard over remaining fieldless variants. after: generated arms remain kind-only; no `var ()` or empty property clause. Update `WildcardBody_IsPreservedForEveryMissingStructVariant`. |
| `ExpandWildcard_Deconstruct_GenericAndNested` | before: generic and nested struct union wildcard switches. after: generic Kind qualification and nested type qualification remain correct while payloads use `var (...)`. |
| `ExpandWildcard_Deconstruct_CaptureCollision` | before: wildcard body has enclosing `value`/`value1` locals. after: generated captures are uniquely named and the copied body still binds the intended value. Update `StructUnion_CaptureCollision_UsesUniqueCaptureName`. |
| `ExpandWildcard_RecordControl` | before: record union wildcard expansion with positional record arms. after: record type/property/positional output is unchanged; the shared struct payload style must not rewrite record arms. Keep `RecordUnion_ExplicitArmAndWildcard_ExpandsAndEmits`. |
| `FixFieldType_PositionalPayload_Control` | before/after: existing `FixFieldType_PositionalPayload` continues to correct only the requested typed positional field. The AddMissing/ExpandWildcard change must not alter this other fix. |
| `AddMissingArms_RecordStatementIndentation_Control` | before: record union statement with a missing record variant. after: record arm shape stays `JobStatus.Pending(string jobName)`; only indentation behavior remains under test. |
| `CodeFix_NonUnionAndNoAction_Controls` | before: non-DU wildcard, explicit arm cursor, fully covered wildcard, and switch-statement wildcard refactoring. after: no action, as currently specified; do not broaden the ready request. |

## Runner assertions

`tests/Houtamelo.Spire.SourceGenerators.Tests/CodeFix/CodeFixTests.cs` and
`VariantPayloadConsumers/VariantPayloadCodeFixTests.cs` already register the
real generator, `ExhaustivenessAnalyzer`, `CS8509Suppressor`, `TypeSafetyAnalyzer`,
`AddMissingArmsCodeFix`, and `FixFieldTypeCodeFix`. For the deconstruction cases,
the expected-after pipeline must additionally:

1. Apply the real code action and compare the changed user document with the
   expected source (structural comparison for ordinary cases, exact text for
   indentation cases).
2. Run the generator on the changed source and assert no generator or compiler
   errors.
3. Rerun the analyzer on the changed source and assert no `SPIRE009` at the
   switch. For switch expressions, also assert that compiler `CS8509` and
   `CS8524` are absent (or are suppressed only after the checker proves the
   all-capture arms).
4. Emit the changed compilation. Where a capture is intended to be used,
   replace the throw body with a typed probe or execute a generated helper so
   that a wrong deconstruction arity/order/type fails at compile/runtime rather
   than only as text.
5. Assert that unchanged user arms, record arms, and unrelated `FixFieldType`
   cases remain unchanged.

The shared builder should therefore have one output contract: zero payload
fields produce no nested payload pattern; one or more fields produce
`accessor: var (capture1, capture2, ...)` with collision-safe escaped
designations. `AddMissingArms` statement/expression paths and `ExpandWildcard`
must consume this contract; no mode-specific duplicate builder is needed.
