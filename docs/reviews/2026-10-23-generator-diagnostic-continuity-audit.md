# Generator diagnostic continuity audit

Audit date: 2026-10-23. The findings below describe the pre-repair source under
`src/Houtamelo.Spire.Analyzers/SourceGenerators`. The bounded record, alias,
unsupported-class, and Inlinable repairs are now implemented and pass 12
real-driver regression cases in `DiagnosticContinuity`. Performance-generator
coverage passes 43 cases. Obsolete benchmark layout generation has been replaced.
Drawer alias metadata and full legacy-fixture validation are tracked separately.
This does not claim that the full repository suite passes.
The approved policy is that a Spire diagnostic reports the invalid input, but
diagnostic severity is not an emission gate when the requested generated API is
still constructible. The relevant design text is
[`Diagnostic reporting and generation continuity`](../designs/2026-10-20-union-storage-redesign.md#diagnostic-reporting-and-generation-continuity).

Roslyn treats `SourceProductionContext.ReportDiagnostic` as adding a diagnostic
to the user's compilation; it does not prescribe that the generator stop
producing source ([API documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.sourceproductioncontext.reportdiagnostic?view=roslyn-dotnet-5.0.0)).
The generator therefore has to make the emission decision from input
capabilities and model completeness, not from `DiagnosticSeverity.Error`.

## Result summary

The current implementation has one confirmed avoidable suppression in the DU
path, two performance-generator model/gating gaps, and one optional Unity
alias-output hazard:

1. `DiscriminatedUnionGenerator` reports record field conflicts and then returns
   before emitting the otherwise independent record surface. This is the
   highest-priority continuity repair.
2. `InlinableParser` accepts delegate parameters that the authoritative
   `InlinableUsageAnalyzer` has already rejected for ref-kind or arity. The
   twin emitter then drops ref-kind or names a nonexistent `IActionInliner` /
   `IFuncInliner` arity. This produces compiler errors alongside the primary
   Spire diagnostic and must be aligned without discarding valid sibling
   parameters.
3. `InlinableParser` does not validate non-partial containing types, so the
   twin generator emits `partial` declarations that cannot join the user's
   type. The existing `decl.Diagnostic` gate is latent because this parser
   always sets `Diagnostic: null`.
4. Invalid `FormerlySerializedAs` aliases are retained (correctly) but all
   alias payload declarations and restore branches are still emitted. Alias
   collisions can produce duplicate generated payload types. Preserve the
   canonical union API and suppress only invalid alias-specific output.

`InlinerStructGenerator` also has a report-and-return path, but its diagnostics
describe shapes for which the requested inliner struct cannot be constructed
under the existing v1 API. That suppression is intentional, not a continuity
bug. `BenchmarkUnionGenerator` has no diagnostic model; it has an independent
unsafe-layout declaration-order bug documented below.

## Generator trace inventory

| Generator | Input/model | Diagnostic path | Output decision | Audit result |
| --- | --- | --- | --- | --- |
| `DiscriminatedUnionGenerator` | `UnionParser.Parse` -> `UnionDeclaration` | Separate `RegisterSourceOutput` reports `union.Diagnostic` | Core output is normally independent of diagnostic; record conflict is an early return | One confirmed repair; Unity capability gates are otherwise severity-independent |
| `InlinerStructGenerator` | `InlinerStructParser.Parse` -> complete `InlinerStructDecl` | `decl.Diagnostic` | Reports and returns | Intentional for all current SPIRE017-020/027 shapes |
| `InlinableTwinGenerator` | `InlinableParser.Parse` -> `InlinableHostDecl` | `decl.Diagnostic`, currently never populated | Reports and returns if a future/model validation sets it | Latent gate; parser must first align per-parameter validation |
| `BenchmarkUnionGenerator` | `ParseBenchmarkUnion` -> local `BenchmarkDef` | No diagnostics | Emits benchmark-only types; skips unsafe layout when unavailable | No diagnostic-continuity path; unsafe declaration ordering needs a bounded fix |

Roslyn's incremental-generator contract is initialization plus registered
output steps ([`IIncrementalGenerator`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.iincrementalgenerator)); the separate diagnostic and source-output registrations in the DU generator are therefore a sound structure. The defects below are the predicates inside those output steps and the metadata retained by their parsers.

## 1. Discriminated-union generator

### 1.1 Confirmed avoidable suppression: record field conflict

The source-output callback first resolves configuration and capabilities, then
does this:

```csharp
// DiscriminatedUnionGenerator.cs:73-80
if (union.DeclarationKeyword == "record" && HasFieldNameConflicts(ctx, union))
    return;

var source = Emit(union);
ctx.AddSource($"{hintPrefix}.g.cs", source);
```

See [`DiscriminatedUnionGenerator.cs:73-80`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/DiscriminatedUnionGenerator.cs#L73-L80).
`HasFieldNameConflicts` reports `SPIRE_DU010` at
[`DiscriminatedUnionGenerator.cs:220-263`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/DiscriminatedUnionGenerator.cs#L220-L263), then returns `true`.

The parser has already retained the nested variants and their fields. The
record emitter only adds the root partial-record constructor, variant partial
declarations, and `Is<Variant>` properties; it does not emit the conflicting
field properties itself ([`RecordEmitter.cs:10-45`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Emit/RecordEmitter.cs#L10-L45)). Therefore the conflict is not a reason to suppress that source.

Example:

```csharp
[DiscriminatedUnion]
public partial record Result
{
    public partial record Count(int value) : Result;
    public partial record Text(string value) : Result;
}
```

Current behavior reports `SPIRE_DU010`, but omits `Result.g.cs`. The missing
surface is the generated root partial declaration and `IsCount`/`IsText` (and
any other record-only generated members), so a consumer that uses those
members can receive secondary missing-member errors that obscure the intended
field-conflict diagnostic.

Model data retained/lost:

- Retained: `UnionDeclaration`, nested variants, field names/types, namespace,
  type parameters, and containing types.
- Lost at the output gate: all generated record source; no parser metadata is
  lost.

Bounded repair: retain the diagnostic, remove only this source-output `return`,
and let `RecordEmitter` plus the existing optional outputs run. Do not qualify
record fields or introduce a new record collision policy; that is outside this
audit. The existing focused test is
[`FieldCollisionsTests.RecordFieldTypeConflicts_RetainTheExistingDiagnostic`](../../tests/Houtamelo.Spire.SourceGenerators.Tests/FieldCollisions/FieldCollisionsTests.cs#L420-L432).
Extend it with a real generator-driver consumer that references `Result.IsCount`
and `Result.IsText`, asserts exactly `SPIRE_DU010` among generator diagnostics,
and rejects `CS0117`/`CS0246`/`CS1061`-style missing generated API errors.

### 1.2 Existing continuity fixes verified in the current source

The generic-ancestor diagnostic no longer discards the model. `UnionParser`
records `SPIRE_DU018` before discovering variants
([`UnionParser.cs:61-90`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Parsing/UnionParser.cs#L61-L90)); the normal return retains variants and containing types
([`UnionParser.cs:146-184`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Parsing/UnionParser.cs#L146-L184)). `ResolveGlobalConfig` also resolves global values regardless of a diagnostic ([`UnionParser.cs:187-211`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Parsing/UnionParser.cs#L187-L211)). The model explicitly documents the contract: `Diagnostic` does not suppress generated API ([`UnionDeclaration.cs:52-88`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Model/UnionDeclaration.cs#L52-L88)).

The current tests exercise this contract for direct, indirect, and multiple
generic ancestors:
[`VariantPayloadCoreTests.cs:791-826`](../../tests/Houtamelo.Spire.SourceGenerators.Tests/Behavioral/VariantPayloadCoreTests.cs#L791-L826).

### 1.3 Unity capability gates are not severity gates

The output callback applies only compilation capabilities and declaration shape:

```csharp
EmitUnitySerialization = union.IsUnitySerializable && info.HasUnitySerialization &&
    union.DeclarationKeyword == "struct" && !union.IsReadonly && !union.IsRefStruct &&
    union.Variants.Length > 0;
```

See [`DiscriminatedUnionGenerator.cs:168-178`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/DiscriminatedUnionGenerator.cs#L168-L178). The drawer requires the same runtime capability plus an editor reference ([`DiscriminatedUnionGenerator.cs:168-170`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/DiscriminatedUnionGenerator.cs#L168-L170)). Shared Unity support is emitted from those booleans, not from `Diagnostic.IsError` ([`DiscriminatedUnionGenerator.cs:138-165`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/DiscriminatedUnionGenerator.cs#L138-L165)).

This gives the intended results:

- `SPIRE_DU013` for a readonly/ref/record Unity declaration disables Unity
  serialization because the declaration shape cannot implement the requested
  callback surface, while the core union remains constructible.
- `SPIRE_DU016` for missing Unity references disables Unity-specific output but
  retains factories, payload types, and core members. Existing coverage is
  [`UnityVariantPayloadGeneratorTests.cs:303-322`](../../tests/Houtamelo.Spire.SourceGenerators.Tests/Unity/UnityVariantPayloadGeneratorTests.cs#L303-L322).
- `SPIRE_DU018` keeps the complete core model and does not become a missing-
  member cascade.
- Missing JSON references (`SPIRE_DU006`/`SPIRE_DU007`) omit only the converter
  file; the core file and compile-time schema still emit
  ([`DiscriminatedUnionGenerator.cs:94-132`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/DiscriminatedUnionGenerator.cs#L94-L132), [`JsonSchemaEmitter.cs:13-59`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Emit/JsonSchemaEmitter.cs#L13-L59)).

Do not reintroduce a blanket `Diagnostic.IsError` gate into these paths.

### 1.4 Unity diagnostics that require selective output review

`UnitySerializationValidation` returns one diagnostic while retaining all
variants ([`UnitySerializationValidation.cs:18-52`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Parsing/UnitySerializationValidation.cs#L18-L52)). For reserved generated members (`SPIRE_DU015`), changing the generated names would be a policy change; the requested API is genuinely unavailable because the user-owned member already occupies the generated name. Keep this as a primary diagnostic and do not invent a renamed fallback in Phase 4.

Invalid or ambiguous aliases (`SPIRE_DU017`) are different. The canonical
variant API remains constructible, but `VariantPayloadEmitter` emits both the
canonical and alias payload for every non-empty alias
([`VariantPayloadEmitter.cs:43-63`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Emit/VariantPayloadEmitter.cs#L43-L63)). For example, an alias equal to another current variant causes the same `UnionSecond` payload name to be emitted twice. The validator correctly diagnoses this at [`UnitySerializationValidation.cs:202-280`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Parsing/UnitySerializationValidation.cs#L202-L280), but the model does not carry which alias was invalid, so restore branches and drawer metadata also continue to use it.

This is a secondary continuity/diagnostic-quality repair, not a reason to drop
the union. Preserve `FormerlySerializedAs` in the model for diagnostics and
source provenance, but add a bounded per-alias validity result (or equivalent
emitter predicate) so invalid alias payload declarations, alias restore
branches, and alias drawer metadata are omitted. Continue emitting canonical
payloads, factories, accessors, and the primary `SPIRE_DU017`. Do not silently
reinterpret an invalid alias as a valid historical name.

Adapt the existing alias tests at
[`UnitySerializationDiagnosticTests.cs:385-430`](../../tests/Houtamelo.Spire.SourceGenerators.Tests/Unity/UnitySerializationDiagnosticTests.cs#L385-L430)
and the reserved-surface cases at
[`UnitySerializationDiagnosticTests.cs:620-710`](../../tests/Houtamelo.Spire.SourceGenerators.Tests/Unity/UnitySerializationDiagnosticTests.cs#L620-L710)
to assert real output-compilation diagnostics. Add a valid-alias control from
the lifecycle tests to ensure canonical and former payloads still emit when no
`SPIRE_DU017` is present.

### 1.5 Parser null/metadata cases

The following parser returns are not diagnostic-continuity bugs:

- `ctx.TargetSymbol` not being an `INamedTypeSymbol` is an impossible input for
  the typed attribute provider and has no diagnostic
  ([`UnionParser.cs:17-24`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Parsing/UnionParser.cs#L17-L24)).
- A union with zero variants returns a model with its namespace, type
  parameters, containing types, and diagnostic; there are no factories or
  variant members to preserve ([`UnionParser.cs:92-130`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Parsing/UnionParser.cs#L92-L130)).
- A class declaration enters the explicit unsupported-class branch and returns
  an intentionally empty model with `SPIRE_DU011`
  ([`UnionParser.cs:33-58`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Parsing/UnionParser.cs#L33-L58)). Classes are not a supported DU language feature, so do not recover their nested class variants or invent a class strategy. The current branch does, however, emit an empty additive partial class for a partial input. A non-partial unsupported class can receive an avoidable `CS0260` from that fake output. A low-risk cleanup is to mark this model as diagnostic-only and emit no class source; add a partial and non-partial class control asserting the primary `SPIRE_DU011` without generated-class compiler errors. This is not a missing intended DU API.

The model stores only one `UnionDiagnostic` and validation returns on the first
Unity issue. That can hide a second independent diagnostic, but it does not
discard factories or type/member metadata. Treat multi-diagnostic aggregation
as separate observability work, not as a reason to gate source emission here.

## 2. `InlinerStructGenerator`

### Trace and current gate

`InlinerStructParser` collects namespace, containing types, declaring type
parameters, method parameters, and return type before calling `ValidateMethod`
([`InlinerStructParser.cs:29-76`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Parsing/InlinerStructParser.cs#L29-L76)). It stores the complete model plus one `InlinerDiagnostic`; it does not erase variants/parameters when validation fails. The generator reports and returns at [`InlinerStructGenerator.cs:24-35`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/InlinerStructGenerator.cs#L24-L35).

The return is intentional for every current parser diagnostic:

| ID | Parser condition | Why the requested generated API is not constructible | Model/output evidence |
| --- | --- | --- | --- |
| `SPIRE018` | Declaring type is `ref struct` | The v1 attribute explicitly does not support ref-struct declaring types; the emitter's type keyword path emits an ordinary `partial struct`, not a supported ref-struct implementation | [`InlinerStructParser.cs:79-85`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Parsing/InlinerStructParser.cs#L79-L85), [`InlinerStructEmitter.cs:64-78`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Emit/InlinerStructEmitter.cs#L64-L78) |
| `SPIRE027` | Declaring or enclosing type is not partial | Generated members must join a partial type; adding a generated partial declaration would create a compiler error instead of an inliner API | [`InlinerStructParser.cs:87-92`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Parsing/InlinerStructParser.cs#L87-L92) |
| `SPIRE017` | `ref`, `in`, `out`, `ref readonly`, or `params` parameter | `InlinerParamInfo` retains only name/type and `BuildInvoke` emits by-value parameters/calls; the public inliner interfaces are by-value. Preserving a by-ref signature would require a different API contract. `params` is also explicitly unsupported by the existing v1 contract | [`InlinerStructParser.cs:94-102`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Parsing/InlinerStructParser.cs#L94-L102), [`InlinerParamInfo.cs:6-9`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Model/InlinerParamInfo.cs#L6-L9), [`InlinerStructEmitter.cs:115-148`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Emit/InlinerStructEmitter.cs#L115-L148) |
| `SPIRE019` | Total method arity exceeds eight | `BuildInterface` would name an `IActionInliner`/`IFuncInliner` arity that does not exist; the public interfaces stop at eight inputs | [`InlinerStructParser.cs:104-109`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Parsing/InlinerStructParser.cs#L104-L109), [`InlinerStructEmitter.cs:86-113`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Emit/InlinerStructEmitter.cs#L86-L113), [`IActionInliner.cs:8-64`](../../src/Houtamelo.Spire/IActionInliner.cs#L8-L64) |
| `SPIRE020` | `{MethodName}Inliner` already exists | Emitting the requested fixed name would create a duplicate member; renaming it changes the public API | [`InlinerStructParser.cs:111-117`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Parsing/InlinerStructParser.cs#L111-L117) |

The user-facing attribute documents these unsupported cases, including the
partial-type, ref-struct, modifier, and arity requirements
([`InlinerStructAttribute.cs:7-17`](../../src/Houtamelo.Spire/InlinerStructAttribute.cs#L7-L17)). The source generator should keep the report-and-return behavior. The missing `{MethodName}Inliner` is deliberate for invalid input, not a missing API caused by diagnostic severity.

The null returns at [`InlinerStructParser.cs:18-27`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Parsing/InlinerStructParser.cs#L18-L27) are discovery failures with no diagnostic and are not actionable continuity paths.

### Tests to strengthen the intentional gate

The existing real-driver case families are under
[`Performance/InlinerStructDiagnostics/cases`](../../tests/Houtamelo.Spire.SourceGenerators.Tests/Performance/InlinerStructDiagnostics/cases)
and already cover SPIRE017-020/027 controls. Add assertions in the diagnostic
runner that each invalid case has no generated `.InlinerStruct.g.cs` and no
secondary missing-type/member diagnostic, while the existing plain-parameter,
arity-eight, and all-partial cases still contain generated source and compile.
Do not turn these invalid cases into generated fake structs.

## 3. `InlinableTwinGenerator` and parser/model

### Trace

The syntax provider only selects methods with parameter attributes, verifies the
`InlinableAttribute`, and calls `InlinableParser.Parse`
([`InlinableTwinGenerator.cs:17-36`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/InlinableTwinGenerator.cs#L17-L36)). The parser returns null when there is no parser-recognized inlinable delegate parameter and otherwise builds all positional parameters, containing types, body text, and using directives. Non-delegate attributed parameters are retained as `OtherParams`, so a valid sibling parameter is not lost ([`InlinableParser.cs:40-66`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Parsing/InlinableParser.cs#L40-L66), [`InlinableHostDecl.cs:14-32`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Model/InlinableHostDecl.cs#L14-L32)).

`InlinableTwinGenerator` reports a model diagnostic and returns at
[`InlinableTwinGenerator.cs:38-48`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/InlinableTwinGenerator.cs#L38-L48), but the parser currently constructs every successful model with `Diagnostic: null` ([`InlinableParser.cs:117-135`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Parsing/InlinableParser.cs#L117-L135)). This is a latent gate, not a current suppression. Once parser validation is added, only method-level conditions that make a twin impossible should use that gate; per-parameter failures must remain `OtherParams` when valid inlinable siblings remain.

### Confirmed alignment gaps

The separate `InlinableUsageAnalyzer` is authoritative for SPIRE021-026. It
rejects ref-kinds and delegate arity above eight
([`InlinableUsageAnalyzer.cs:97-139`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Analyzers/InlinableUsageAnalyzer.cs#L97-L139)), and it rejects non-partial containing types at
[`InlinableUsageAnalyzer.cs:83-95`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Analyzers/InlinableUsageAnalyzer.cs#L83-L95).
The generator parser does not mirror those gates:

1. **SPIRE024, delegate arity above eight (P1).** `TryBuildInlinable` accepts
   every `Action`/`Func` arity ([`InlinableParser.cs:137-195`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Parsing/InlinableParser.cs#L137-L195)), while the emitter always constructs an inliner interface from all delegate type arguments ([`InlinableTwinEmitter.cs:163-175`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Emit/InlinableTwinEmitter.cs#L163-L175)). A nine-input delegate therefore gets an `IActionInliner<T1,...,T9>` or `IFuncInliner<...>` reference that the public API does not define. The analyzer's `SPIRE024` is accompanied by generated C# errors. Enforce the same maximum in `TryBuildInlinable`; put the invalid attributed parameter into `OtherParams` with its original delegate type. If all attributed parameters are invalid, the existing null result is correct; if valid siblings remain, emit the sibling twin.
2. **SPIRE025, ref-kind (P1).** `TryBuildInlinable` tests only the parameter type, while `InlinableParam` has no ref-kind field ([`InlinableParam.cs:6-14`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Model/InlinableParam.cs#L6-L14)). The emitter consequently changes `[Inlinable] ref Action action` into a by-value generic parameter and rewrites its invocation. The analyzer reports `SPIRE025`, but the generated twin is a different API rather than the original ref shape. Reject ref/in/out/ref-readonly before `TryBuildInlinable`, retain the original parameter in `OtherParams`, and preserve valid siblings. This does not add support for ref-kind inliners.
3. **SPIRE023, non-partial containing type (P1).** `InlinableParser` builds `partial` wrapper declarations but never calls an equivalent of `FindNonPartialEnclosingType`; the emitter always writes `partial` for the declaring and containing types ([`InlinableTwinEmitter.cs:30-45`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Emit/InlinableTwinEmitter.cs#L30-L45), [`InlinableTwinEmitter.cs:178-190`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Emit/InlinableTwinEmitter.cs#L178-L190)). The analyzer reports `SPIRE023`, while the generated twin can add `CS0260`/related partial-declaration errors. Add parser validation that produces the existing `SPIRE023` diagnostic and let the model-level report-and-return gate suppress this impossible twin. Do not suppress a valid twin merely because a sibling parameter has SPIRE022/024/025.

`SPIRE026` is not a missing-output path: property/indexer accessors are not
`MethodDeclarationSyntax`, so the twin syntax provider does not select them;
the analyzer reports the diagnostic and no twin is expected. `SPIRE022` is
already handled safely: invalid non-delegate attributed parameters remain
ordinary host parameters, and only an all-invalid method returns null.

### SPIRE021 body diagnostics

`InlinableUsageAnalyzer` reports SPIRE021 for captures, casts, stores, returns,
and other body uses ([`InlinableUsageAnalyzer.cs:280-301`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Analyzers/InlinableUsageAnalyzer.cs#L280-L301)). `InlinableTwinGenerator` still emits the twin, and `InlinableBodyRewriter` deliberately leaves unsupported shapes unchanged ([`InlinableBodyRewriter.cs:14-65`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Emit/InlinableBodyRewriter.cs#L14-L65)). For cases such as returning the delegate, this can produce a generated twin compile error. This is not a report-and-return suppression and SPIRE021 is a warning, but it is a related diagnostic/output mismatch. If Phase 4 covers it, the bounded behavior is to gate only the invalid method twin after the same body validation, preserving the original user method; do not broaden the rewriter to support new delegate shapes without a separate policy decision.

### Tests to add

Use the real `CSharpGeneratorDriver` helpers in
[`GeneratorTestHelper.cs:89-121`](../../tests/Houtamelo.Spire.SourceGenerators.Tests/GeneratorTestHelper.cs#L89-L121), and run the analyzer against the same output compilation where needed:

- `[Inlinable] Func<...9 inputs...>`: assert `SPIRE024`, no nonexistent
  interface reference, and no generated C# arity error; add a valid eight-input
  control.
- `[Inlinable] ref Action` and a method with one valid plus one ref-invalid
  parameter: assert `SPIRE025`, unchanged ref parameter in the twin, and a
  valid generic parameter/twin still emitted for the sibling.
- Non-partial declaring and nested types: assert `SPIRE023`, no generated twin,
  and no `CS0260`; retain the all-partial snapshot controls.
- Non-delegate-only and mixed valid/non-delegate parameters: assert the current
  `OtherParams` behavior and no missing generated API.
- One SPIRE021 body violation plus a direct-invocation valid control; record
  whether the chosen warning policy is diagnostics-only or twin suppression.

## 4. `BenchmarkUnionGenerator`

This generator has no `Diagnostic` field, no `ReportDiagnostic` call, and no
Spire error path. `ParseBenchmarkUnion` returns null for a declaration with no
`[Variant]` methods ([`BenchmarkUnionGenerator.cs:129-183`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/BenchmarkUnionGenerator.cs#L129-L183)); that is an intentionally non-actionable benchmark declaration, not a user-facing DU API. Field conflicts suppress only optional property/JSON benchmark code while retaining benchmark union types ([`BenchmarkUnionGenerator.cs:340-359`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/BenchmarkUnionGenerator.cs#L340-L359)).

There is one independent output-order defect worth recording. The loop adds
`{typeName}.BenchDecl.g.cs` before checking `AllowUnsafeBlocks`, then skips the
implementation for `UnsafeOverlap`:

```csharp
ctx.AddSource($"{typeName}.BenchDecl.g.cs", EmitStructDeclaration(b, typeName));
if (strategy == EmitStrategy.UnsafeOverlap)
{
    if (!compInfo.AllowsUnsafe) continue;
    source = UnsafeOverlapEmitter.Emit(union, compInfo.HasInlineArray);
}
```

See [`BenchmarkUnionGenerator.cs:66-85`](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/BenchmarkUnionGenerator.cs#L66-L85). The declaration contains accessible partial variant methods, so omitting the implementation can produce partial-method compiler errors even though the benchmark code later filters that type. This is P2 generator hygiene, not diagnostic continuity. Move the capability check before `BenchDecl.g.cs`, or emit both declaration and implementation only when unsafe is available. Add real-driver unsafe-enabled and unsafe-disabled controls if the benchmark generator remains active; do not add a fake UnsafeOverlap implementation.

## Prioritized repair list

| Priority | Repair | Why bounded |
| --- | --- | --- |
| P0 | Remove the record field-conflict source-output `return`; retain `SPIRE_DU010` and emit the record root surface | The record emitter does not use the conflicting fields, and all model metadata is already present |
| P1 | Align `InlinableParser` with SPIRE023/024/025: validate partial containment, reject ref-kind and >8 delegate arity into `OtherParams`, preserve valid siblings | Reuses existing diagnostics and interface limits; does not support new language/API shapes |
| P1 | Carry invalid Unity alias validity separately from `FormerlySerializedAs`; omit only invalid alias payload/restore/drawer branches | Canonical factories/payloads remain; invalid historical names are not silently accepted |
| P2 | Make unsupported DU class output diagnostic-only, or otherwise prevent fake non-partial class source | No supported class API is being removed; avoids secondary partial errors on an explicitly unsupported feature |
| P2 | Move Benchmark unsafe capability check before declaration emission | Prevents orphan partial declarations; benchmark-only and no diagnostic-policy change |
| P2 (optional) | Decide whether SPIRE021 warning cases should skip only the invalid twin | Existing docs acknowledge generated-twin failure for unsupported body shapes; requires a shared body-validation decision |
| No repair | Keep `InlinerStructGenerator` report-and-return for SPIRE017-020/027 | Each requested inliner shape is genuinely unavailable under the documented v1 contract |

## Real generator-driver coverage plan

Every invalid case should assert both the intended Spire diagnostic and the
output compilation/source surface. A diagnostic-only assertion is insufficient
because it cannot detect missing generated members or secondary compiler
errors. Valid controls must assert generated source and successful compilation.

| Area | Invalid control | Valid control and assertions |
| --- | --- | --- |
| DU record conflict | Existing `RecordFieldConflictSource` with a consumer calling `IsCount`/`IsText`; exactly `SPIRE_DU010`, no missing-member diagnostics | Existing record snapshot/control; generated root, variant markers, and compilation succeed |
| DU generic ancestor | Direct, indirect, multiple generic ancestors, and generic union itself from `VariantPayloadCoreTests` | Nongeneric containing type and top-level generic union; retain `CommandRun`, factory, and payload symbols |
| Unity references | Serializable mutable union without Unity references (`DU016`) | Same source with Unity references; assert core API in both and Unity interface/support only in capable compilation |
| Unity declaration shape | readonly/ref/record Unity unions (`DU013`) | Mutable struct with one and multiple variants; assert no Unity interface for rejected shape and core API retained |
| Unity alias | Current-name, duplicate, and sanitized-collision aliases (`DU017`) | One valid former alias; assert canonical output, no invalid alias duplicate type, and valid alias restoration/output |
| Unity reserved surface | `_kind`, callback, payload, and `__Spire` collisions (`DU015`) | No reserved member; keep the primary diagnostic and separately document that renaming is a policy change |
| Unsupported class | Partial and non-partial `[DiscriminatedUnion] class` (`DU011`) | Existing partial record control; assert no fake class source for the chosen policy and no secondary partial errors |
| InlinerStruct | Existing SPIRE017/018/019/020/027 cases | Existing snapshot and arity-eight/all-partial controls; assert invalid cases have no generated inliner and no missing-type cascade |
| Inlinable arity/ref-kind | SPIRE024 nine-input delegate; SPIRE025 ref/in/out/ref-readonly delegate | Eight-input/direct-value controls; mixed valid and invalid parameters retain valid twin API and original invalid parameter |
| Inlinable containment | SPIRE023 non-partial declaring and enclosing types | All-partial nested control; assert generated twin and no `CS0260` |
| Inlinable body | SPIRE021 capture/store/cast/return warning cases | Direct invocation and single-assignment `var` alias cases; record the selected warning/twin policy |
| Benchmark capability | `AllowUnsafeBlocks=false` with a non-generic benchmark union | `AllowUnsafeBlocks=true`; assert no orphan `UnsafeOverlap` partial declaration and expected benchmark type set |

Do not use the old full layout fixture suite as the gate for this audit. The
focused generator-driver suites and the already-recorded native Unity phase
boundaries remain separate validation inputs.

## Sources

- Project policy and current execution status: [union storage redesign, diagnostic continuity](../designs/2026-10-20-union-storage-redesign.md#diagnostic-reporting-and-generation-continuity).
- Roslyn diagnostic output: [`SourceProductionContext.ReportDiagnostic`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.sourceproductioncontext.reportdiagnostic?view=roslyn-dotnet-5.0.0).
- Roslyn incremental generators: [`IIncrementalGenerator`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.iincrementalgenerator).
- C# partial declarations: [Partial type reference](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/partial-type).
- C# ref-struct restrictions: [`ref struct` types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/ref-struct).
- Nested types in generic classes: [C# language specification, nested types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/classes#15397-nested-types-in-generic-classes).
