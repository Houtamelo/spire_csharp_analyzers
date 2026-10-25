# Variant-payload final review

This review covers the core generator, parser/configuration model, analyzers,
code fixes, and JSON emitters. Unity drawer/test changes that are still being
validated are outside this review. The focused suites reported by the lead
(core, consumers, diagnostic continuity, and performance generator cases) were
used as the current baseline.

Follow-up: accessor ownership and C# literal escaping are repaired. The four
real-driver review regressions pass, including both JSON libraries, schema
generation, and attribute/global names containing escaped characters. The record
wildcard concern was not reproduced by the new compilation test; that control
passes without changing the provider. Findings below preserve the audit evidence.

## Review: VariantPayload

### Convention Issues

- None found in the reviewed analyzer entry points. The analyzers use
  `CompilationStartAction`, narrow `IOperation` registrations, concurrent
  execution, and generated-code configuration. Descriptor lookup remains
  centralized.

### Correctness Concerns

- **P1 — accessor ownership can be widened by an unrelated parameter name.**
  `VariantFieldMap.ScanProperties` first maps a hidden property by its generated
  accessor name, then also maps that same property when any factory parameter has
  the property name ([VariantFieldMap.cs:67-84](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Analyzers/VariantFieldMap.cs#L67)).
  For `A(int value)` and `B(int a)`, the generated accessor `a` belongs to `A`,
  but `B`'s parameter `a` adds `B` as another owner. The field-access analyzer
  can then accept `a` under a `B` guard and fail to report SPIRE013. Add a
  regression case where a factory parameter equals another variant's accessor;
  map only generated payload accessors, or resolve ownership from the property's
  return payload type/name rather than parameter-name coincidence.

- **P1 — user-controlled JSON names are emitted as unescaped C# string
  literals.** The discriminator, variant names, and field names are interpolated
  directly into generated source in both converters ([SystemTextJsonEmitter.cs:115-144](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Emit/SystemTextJsonEmitter.cs#L115), [SystemTextJsonEmitter.cs:180-194](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Emit/SystemTextJsonEmitter.cs#L180), [NewtonsoftJsonEmitter.cs:140-169](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Emit/NewtonsoftJsonEmitter.cs#L140), [NewtonsoftJsonEmitter.cs:214-227](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Emit/NewtonsoftJsonEmitter.cs#L214)). A `JsonName` or global discriminator containing `\`, `"`, or a newline produces invalid generated C# (and ordinary JSON names are allowed to contain these characters). Escape C# literals at emission and add a compile/round-trip case with a quoted or escaped JSON name.

- **P2 — record wildcard expansion is still malformed.** The record branch of
  `ExpandWildcardRefactoring` puts `Shape.Variant` inside a positional
  `RecursivePattern` as a `ConstantPattern` ([ExpandWildcardCodeFix.cs:221-236](../../src/Houtamelo.Spire.CodeFixes/ExpandWildcardCodeFix.cs#L221)). That is not a record type pattern and should produce invalid code or a pattern that cannot match a record variant. The current code-fix cases cover struct payloads only; add a real record-union wildcard case and emit the same type/property pattern shape used by the existing record-arm code fix.

### Code Quality

- `VariantFieldMap` still carries a legacy `ScanFields` path and comments about
  variant-prefixed flat fields ([VariantFieldMap.cs:88-116](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Analyzers/VariantFieldMap.cs#L88)). The removed storage emitters no longer generate those members. Removing the dead compatibility scan would make ownership analysis smaller and avoid future accidental matches; this is low risk after the dedicated legacy test migration.

- `FieldAccessSafetyAnalyzer` rebuilds `VariantFieldMap` for every hidden field/property operation ([FieldAccessSafetyAnalyzer.cs:60-63](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Analyzers/FieldAccessSafetyAnalyzer.cs#L60), [FieldAccessSafetyAnalyzer.cs:119-121](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Analyzers/FieldAccessSafetyAnalyzer.cs#L119)). This is not a correctness blocker, but a compilation-scoped map cache would avoid repeated symbol scans in large generated unions.

### Test Coverage Gaps

- No focused analyzer test covers the accessor/parameter-name collision described above.
- JSON tests cover ordinary custom names but not quote, backslash, newline, or
  control-character names for either converter or the configured discriminator.
- No code-fix test covers expanding a wildcard on a record union.
- The existing focused tests do cover the approved storage modes, generic
  dependent fields, payload deconstruction, nested property patterns, alias
  validation, and diagnosed generic ancestors; those areas did not reveal an
  additional issue in this pass.

### Summary

The core representation and analyzer structure are in good shape and match the
approved storage/accessor design in the exercised cases. Before shipping, fix the
accessor ownership collision and JSON literal escaping; the record wildcard issue
also needs a code-fix repair or an explicit supported-scope decision. The focused
suite should be extended with those three regression families.
