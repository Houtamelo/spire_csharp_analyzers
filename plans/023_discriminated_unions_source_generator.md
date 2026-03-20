# 023 — Discriminated Unions Source Generator: Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement `Spire.SourceGenerators` — a Roslyn IIncrementalGenerator that produces discriminated union types from `[DiscriminatedUnion]` declarations.

**Architecture:** V2 source generator reads partial type declarations, parses `[Variant]` methods, and emits union implementations. Five emission paths: Record (abstract record hierarchy), Class (abstract class hierarchy), Overlap (three-region StructLayout.Explicit struct), BoxedFields (N x object? struct), BoxedTuple (single object? tuple struct). Equatable model records for incremental caching.

**Tech Stack:** Roslyn 5.0.0, IIncrementalGenerator, netstandard2.0, xUnit, .NET 10 tests

**Design spec:** `plans/022_discriminated_unions_design.md`

**Scope:** Source generator ONLY. Analyzers (exhaustiveness, type safety, field access), CS8509 suppressor, and code fixes are separate follow-up plans.

---

## Design Amendments (deviations from 022)

### Variant Declaration Syntax (amended from 022)

**Two different declaration styles** depending on the declaration kind:

#### Struct path: `[Variant]` static partial methods

The design spec (022) shows `static partial Shape Circle(double radius)` — this does NOT compile because non-void partial methods require an explicit access modifier (C# 9+ rules). **Amended to** old-style partial methods (void return, no access modifier):

```csharp
[DiscriminatedUnion]
partial struct Shape {
    [Variant] static partial void Circle(double radius);
    [Variant] static partial void Rectangle(float width, float height);
    [Variant] static partial void Square(int sideLength);
}
```

Old-style partial methods are erased by the compiler if no implementing declaration exists. The generator reads them as metadata only and does NOT implement them. Instead, it generates factory methods (`NewCircle`, `NewRectangle`, etc.), Kind enum, fields, Deconstruct, etc.

#### Record/Class path: nested partial types with explicit inheritance

Users define variant types directly as nested partial types inheriting from the parent:

```csharp
[DiscriminatedUnion]
partial record Option<T> {
    partial record Some(T Value) : Option<T>;
    partial record None() : Option<T>;
}

[DiscriminatedUnion]
partial class Result<T, E> {
    partial class Ok(T Value) : Result<T, E>;
    partial class Err(E Error) : Result<T, E>;
}
```

The generator discovers variants by finding nested partial types inheriting from the parent. It then generates a partial declaration that adds:
- `abstract` on the parent type
- `private` parameterless constructor (prevents external subclassing)
- `sealed` on each variant type

Generated for `Option<T>`:
```csharp
abstract partial record Option<T> {
    private Option() { }
    sealed partial record Some;
    sealed partial record None;
}
```

**No factory methods** for record/class — users construct via `new Option<int>.Some(42)`.

**No `[Variant]` attribute** needed for record/class — the generator discovers variants by finding nested partial types that inherit from the parent type.

### Attribute Injection

Attributes (`DiscriminatedUnionAttribute`, `VariantAttribute`, `Layout` enum) are injected via `RegisterPostInitializationOutput`. Users need only reference the `Spire.SourceGenerators` package.

### Declaration Kind Routing

The parser determines the emission path from the syntax node type:

| User writes | Roslyn syntax | Emission path |
|---|---|---|
| `partial struct Foo` | `StructDeclarationSyntax` | Struct (Overlap/BoxedFields/BoxedTuple) |
| `partial record struct Foo` | `RecordDeclarationSyntax` + StructKeyword | Struct path |
| `partial record Foo` | `RecordDeclarationSyntax` (no StructKeyword) | Record path |
| `partial class Foo` | `ClassDeclarationSyntax` | Class path |

### Generated Field Naming (struct paths)

- Single field: `{variantCamelCase}_{paramName}` — e.g., `circle_radius`
- Multi-field tuple: `{variantCamelCase}` — e.g., `rectangle` for `(float width, float height)`
- Region 2 reference slots (Overlap): `ref_{n}` — e.g., `ref_0`, `ref_1`
- Region 3 boxed slots (Overlap): `obj_{n}` — e.g., `obj_0`, `obj_1`

### Readonly and Accessibility

The generator re-emits the same modifiers as the user's declaration (including `readonly`, `public`/`internal`, etc.) since partial types require modifier agreement across all parts.

### Deconstruct Rules by Path

- **Overlap**: unique arity -> typed (zero boxing), shared arity -> `object?`
- **BoxedFields**: always `object?` (data already boxed)
- **BoxedTuple**: always `(out Kind, out object?)` two-param
- **Record**: built-in from positional record syntax (user-defined, no generation needed)
- **Class**: user-defined (primary constructor gives Deconstruct in C# 12+; otherwise user adds manually)

### Nested Types (rejected for MVP)

Nested union declarations (`class Outer { [DiscriminatedUnion] partial struct Inner { ... } }`) require wrapping the generated partial in the containing type declarations. **For MVP, emit a diagnostic error rejecting nested types.** This simplifies all emitters — they only need to handle top-level namespace wrapping.

### ref struct (rejected)

`ref struct` cannot be boxed, making BoxedFields and BoxedTuple paths impossible. Overlap could theoretically work for all-unmanaged `ref struct`, but the complexity isn't worth it for MVP. **Emit a diagnostic error if `[DiscriminatedUnion]` is applied to a `ref struct`.**

### Fieldless Variant Deconstruct (struct paths)

Fieldless variants (0 fields) are a distinct arity group. In the shared-arity `(out Kind, out object?)` Deconstruct, fieldless variants return `null` for the payload. Pattern: `case (Shape.None, _)` — the discard ignores the null. If a fieldless variant is the ONLY variant with 0 fields, it gets a unique-arity typed Deconstruct with just `(out Kind kind)` — a single out param.

### MustBeInit Integration (deferred)

The generator can optionally emit `[MustBeInit]` on struct unions, conditionally injecting the attribute if `Spire.Analyzers` is not referenced. Deferred to a follow-up task after the core generator works.

---

## File Structure

```
src/Spire.SourceGenerators/
    Spire.SourceGenerators.csproj
    DiscriminatedUnionGenerator.cs      -- IIncrementalGenerator entry point
    Attributes/
        AttributeSource.cs              -- String constants for injected attribute source
    Model/
        UnionDeclaration.cs             -- Parsed union type (equatable record)
        VariantInfo.cs                  -- Single variant (name, fields)
        FieldInfo.cs                    -- Single field (name, type metadata)
        EquatableArray.cs               -- ImmutableArray wrapper with structural equality
    Parsing/
        UnionParser.cs                  -- Syntax + semantic model -> UnionDeclaration
    Emit/
        SourceBuilder.cs                -- StringBuilder wrapper with indentation tracking
        RecordEmitter.cs                -- Abstract record hierarchy
        ClassEmitter.cs                 -- Abstract class hierarchy
        BoxedFieldsEmitter.cs           -- N x object? struct
        BoxedTupleEmitter.cs            -- Single object? tuple struct
        OverlapEmitter.cs               -- Three-region explicit layout struct
        DeconstructEmitter.cs           -- Shared Deconstruct generation logic
        FieldClassifier.cs              -- Classifies fields into Region 1/2/3 (Overlap only)

tests/Spire.SourceGenerators.Tests/
    Spire.SourceGenerators.Tests.csproj
    GeneratorTestHelper.cs              -- Compilation creation + driver execution
    AttributeInjectionTests.cs
    RecordPathTests.cs
    ClassPathTests.cs
    BoxedFieldsPathTests.cs
    BoxedTuplePathTests.cs
    OverlapPathTests.cs
    LayoutDiagnosticTests.cs
```

---

## Task 1: Project scaffold + infrastructure

**Dependency:** None (first task)

**Files:**
- Create: `src/Spire.SourceGenerators/Spire.SourceGenerators.csproj`
- Create: `src/Spire.SourceGenerators/Attributes/AttributeSource.cs`
- Create: `src/Spire.SourceGenerators/Model/UnionDeclaration.cs`
- Create: `src/Spire.SourceGenerators/Model/VariantInfo.cs`
- Create: `src/Spire.SourceGenerators/Model/FieldInfo.cs`
- Create: `src/Spire.SourceGenerators/Model/EquatableArray.cs`
- Create: `src/Spire.SourceGenerators/Parsing/UnionParser.cs`
- Create: `src/Spire.SourceGenerators/Emit/SourceBuilder.cs`
- Create: `src/Spire.SourceGenerators/DiscriminatedUnionGenerator.cs`
- Create: `tests/Spire.SourceGenerators.Tests/Spire.SourceGenerators.Tests.csproj`
- Create: `tests/Spire.SourceGenerators.Tests/GeneratorTestHelper.cs`
- Create: `tests/Spire.SourceGenerators.Tests/AttributeInjectionTests.cs`
- Modify: `Spire.Analyzers.slnx`

- [ ] **Step 1: Create source generator project**

`src/Spire.SourceGenerators/Spire.SourceGenerators.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.0.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.11.0" PrivateAssets="all" />
    <PackageReference Include="PolySharp" Version="1.15.0" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create test project**

`tests/Spire.SourceGenerators.Tests/Spire.SourceGenerators.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14</LangVersion>
    <IsTestProject>true</IsTestProject>
    <NoWarn>$(NoWarn);NU1701;AD0001</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Spire.SourceGenerators\Spire.SourceGenerators.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.2" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Update solution file**

Add to `Spire.Analyzers.slnx`:
```xml
<Folder Name="/src/">
  ...
  <Project Path="src/Spire.SourceGenerators/Spire.SourceGenerators.csproj" />
</Folder>
<Folder Name="/tests/">
  ...
  <Project Path="tests/Spire.SourceGenerators.Tests/Spire.SourceGenerators.Tests.csproj" />
</Folder>
```

- [ ] **Step 4: Create AttributeSource.cs**

String constants for injected source. Namespace: `Spire`. All types `internal sealed` (standard for source-injected attributes).

```csharp
namespace Spire.SourceGenerators.Attributes;

internal static class AttributeSource
{
    public const string Hint_DiscriminatedUnion = "DiscriminatedUnionAttribute.g.cs";
    public const string Hint_Variant = "VariantAttribute.g.cs";
    public const string Hint_Layout = "Layout.g.cs";

    public const string DiscriminatedUnionAttribute = """
        // <auto-generated/>
        #nullable enable

        namespace Spire
        {
            [global::System.AttributeUsage(
                global::System.AttributeTargets.Struct | global::System.AttributeTargets.Class,
                Inherited = false)]
            internal sealed class DiscriminatedUnionAttribute : global::System.Attribute
            {
                public Layout Layout { get; }
                public DiscriminatedUnionAttribute(Layout layout = Layout.Auto)
                    => Layout = layout;
            }
        }
        """;

    public const string VariantAttribute = """
        // <auto-generated/>
        #nullable enable

        namespace Spire
        {
            [global::System.AttributeUsage(
                global::System.AttributeTargets.Method,
                Inherited = false)]
            internal sealed class VariantAttribute : global::System.Attribute { }
        }
        """;

    public const string LayoutEnum = """
        // <auto-generated/>

        namespace Spire
        {
            internal enum Layout
            {
                Auto,
                Overlap,
                BoxedFields,
                BoxedTuple,
            }
        }
        """;
}
```

- [ ] **Step 5: Create model types**

`Model/EquatableArray.cs` — wrapper for `ImmutableArray<T>` with structural equality (required for V2 incremental generator caching):

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Spire.SourceGenerators.Model;

internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly T[]? _array;

    public EquatableArray(ImmutableArray<T> array)
        => _array = array.IsDefault ? null : array.ToArray();

    public int Length => _array?.Length ?? 0;
    public T this[int index] => _array![index];
    public bool IsEmpty => _array is null || _array.Length == 0;

    public bool Equals(EquatableArray<T> other)
    {
        if (_array is null && other._array is null) return true;
        if (_array is null || other._array is null) return false;
        if (_array.Length != other._array.Length) return false;
        for (int i = 0; i < _array.Length; i++)
            if (!_array[i].Equals(other._array[i]))
                return false;
        return true;
    }

    public override bool Equals(object? obj)
        => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (_array is null) return 0;
        int hash = 17;
        foreach (var item in _array)
            hash = hash * 31 + item.GetHashCode();
        return hash;
    }

    public IEnumerator<T> GetEnumerator()
        => ((IEnumerable<T>)(_array ?? Array.Empty<T>())).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
```

`Model/FieldInfo.cs`:
```csharp
using System;

namespace Spire.SourceGenerators.Model;

/// Region classification for Overlap layout field placement.
internal enum FieldRegion { Unmanaged, Reference, Boxed }

internal sealed record FieldInfo(
    string Name,
    string TypeFullName,
    bool IsUnmanaged,
    bool IsReferenceType,
    /// Computed sizeof at parse time (null if not computable).
    /// Populated from ITypeSymbol while the semantic model is available.
    /// Used by FieldClassifier for Overlap region offset computation.
    int? KnownSize
) : IEquatable<FieldInfo>;
```

`Model/VariantInfo.cs`:
```csharp
using System;

namespace Spire.SourceGenerators.Model;

internal sealed record VariantInfo(
    string Name,
    EquatableArray<FieldInfo> Fields
) : IEquatable<VariantInfo>;
```

`Model/UnionDeclaration.cs`:
```csharp
using System;

namespace Spire.SourceGenerators.Model;

internal enum EmitStrategy { Record, Class, Overlap, BoxedFields, BoxedTuple }

/// Carries a diagnostic ID + message for the generator to report.
/// Simpler than passing Roslyn DiagnosticDescriptor through the equatable model.
internal sealed record UnionDiagnostic(
    string Id,
    string Message,
    bool IsError
) : IEquatable<UnionDiagnostic>;

internal sealed record UnionDeclaration(
    string Namespace,
    string TypeName,
    string AccessibilityKeyword,
    string DeclarationKeyword,
    bool IsReadonly,
    EmitStrategy Strategy,
    EquatableArray<string> TypeParameters,
    EquatableArray<VariantInfo> Variants,
    /// Optional diagnostic to report (e.g., Layout on record, Overlap on generic).
    /// If IsError, no source is emitted for this union.
    UnionDiagnostic? Diagnostic
) : IEquatable<UnionDeclaration>;
```

- [ ] **Step 6: Create UnionParser**

`Parsing/UnionParser.cs` — transforms `GeneratorAttributeSyntaxContext` into `UnionDeclaration`:

Key logic:
1. Extract type name, namespace, accessibility, declaration kind from `TargetSymbol` (INamedTypeSymbol)
2. Read `Layout` value from attribute constructor arg (default Auto)
3. Find all methods on the type with `[Variant]` attribute
4. For each variant method: extract name, parameter names and types
5. Resolve EmitStrategy from declaration kind + Layout + generics

```csharp
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spire.SourceGenerators.Model;

namespace Spire.SourceGenerators.Parsing;

internal static class UnionParser
{
    public static UnionDeclaration? Parse(
        GeneratorAttributeSyntaxContext ctx,
        CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        var syntax = (TypeDeclarationSyntax)ctx.TargetNode;
        var declKind = GetDeclarationKind(syntax);
        if (declKind is null) return null;

        var layout = GetLayout(ctx.Attributes);
        var isGeneric = typeSymbol.TypeParameters.Length > 0;
        var strategy = ResolveStrategy(declKind.Value, layout, isGeneric);

        var variants = typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.IsStatic && HasVariantAttribute(m))
            .Select(m => ParseVariant(m))
            .ToImmutableArray();

        if (variants.Length == 0) return null;

        var typeParams = typeSymbol.TypeParameters
            .Select(tp => tp.Name)
            .ToImmutableArray();

        return new UnionDeclaration(
            Namespace: typeSymbol.ContainingNamespace.IsGlobalNamespace
                ? ""
                : typeSymbol.ContainingNamespace.ToDisplayString(),
            TypeName: typeSymbol.Name,
            AccessibilityKeyword: AccessibilityToKeyword(typeSymbol.DeclaredAccessibility),
            DeclarationKeyword: declKind.Value,
            IsReadonly: syntax.Modifiers.Any(m => m.IsKind(
                Microsoft.CodeAnalysis.CSharp.SyntaxKind.ReadOnlyKeyword)),
            Strategy: strategy,
            TypeParameters: new EquatableArray<string>(typeParams),
            Variants: new EquatableArray<VariantInfo>(variants)
        );
    }

    // GetDeclarationKind returns "struct", "record", or "class"
    // GetLayout reads Layout enum from attribute ctor arg (default Auto)
    // ResolveStrategy maps (declKind, layout, isGeneric) -> EmitStrategy
    // ParseVariant reads method name + parameters -> VariantInfo
    // HasVariantAttribute checks for [Variant] on method
    // ComputeKnownSize(ITypeSymbol) -> int? — returns sizeof for primitives,
    //   enums (via underlying type), bool, nint/nuint. null otherwise.
    //   Called at parse time while semantic model is available, stored in FieldInfo.KnownSize.
    //
    // Validation (return null + report diagnostic via model):
    // - typeSymbol.ContainingType != null -> error: nested types not supported
    // - typeSymbol.IsRefLikeType -> error: ref struct not supported
    // - variants.Length == 0 -> warning: no variants found
}
```

The `ResolveStrategy` mapping:
- `struct` + `Auto` + non-generic -> `Overlap`
- `struct` + `Auto` + generic -> `BoxedFields`
- `struct` + `Overlap` -> `Overlap` (error later if generic)
- `struct` + `BoxedFields` -> `BoxedFields`
- `struct` + `BoxedTuple` -> `BoxedTuple`
- `record` -> `Record`
- `class` -> `Class`

- [ ] **Step 7: Create SourceBuilder utility**

`Emit/SourceBuilder.cs` — thin wrapper around StringBuilder with indentation:

```csharp
using System.Text;

namespace Spire.SourceGenerators.Emit;

internal sealed class SourceBuilder
{
    private readonly StringBuilder _sb = new();
    private int _indent;

    public void Indent() => _indent++;
    public void Dedent() => _indent--;

    public void AppendLine(string line)
    {
        for (int i = 0; i < _indent; i++) _sb.Append("    ");
        _sb.AppendLine(line);
    }

    public void AppendLine() => _sb.AppendLine();

    public void OpenBrace()
    {
        AppendLine("{");
        Indent();
    }

    public void CloseBrace(string suffix = "")
    {
        Dedent();
        AppendLine("}" + suffix);
    }

    public override string ToString() => _sb.ToString();
}
```

- [ ] **Step 8: Create generator entry point**

`DiscriminatedUnionGenerator.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spire.SourceGenerators.Attributes;
using Spire.SourceGenerators.Emit;
using Spire.SourceGenerators.Model;
using Spire.SourceGenerators.Parsing;

namespace Spire.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class DiscriminatedUnionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource(AttributeSource.Hint_DiscriminatedUnion,
                AttributeSource.DiscriminatedUnionAttribute);
            ctx.AddSource(AttributeSource.Hint_Variant,
                AttributeSource.VariantAttribute);
            ctx.AddSource(AttributeSource.Hint_Layout,
                AttributeSource.LayoutEnum);
        });

        var unions = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Spire.DiscriminatedUnionAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, ct) => UnionParser.Parse(ctx, ct)
        ).Where(static u => u is not null);

        context.RegisterSourceOutput(unions, static (ctx, union) =>
        {
            if (union is null) return;
            var source = Emit(union);
            // Include arity to avoid collisions: Option vs Option<T>
            var arity = union.TypeParameters.Length > 0
                ? $"`{union.TypeParameters.Length}"
                : "";
            ctx.AddSource($"{union.TypeName}{arity}.g.cs", source);
        });
    }

    private static string Emit(UnionDeclaration union) => union.Strategy switch
    {
        EmitStrategy.Record => RecordEmitter.Emit(union),
        EmitStrategy.Class => ClassEmitter.Emit(union),
        EmitStrategy.Overlap => OverlapEmitter.Emit(union),
        EmitStrategy.BoxedFields => BoxedFieldsEmitter.Emit(union),
        EmitStrategy.BoxedTuple => BoxedTupleEmitter.Emit(union),
        _ => "// Unknown strategy",
    };
}
```

Create placeholder emitters (empty `Emit` methods returning `// TODO`) for each of the five emitters so the project compiles.

- [ ] **Step 9: Create test helper**

`tests/Spire.SourceGenerators.Tests/GeneratorTestHelper.cs`:

```csharp
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Spire.SourceGenerators.Tests;

internal static class GeneratorTestHelper
{
    private static readonly MetadataReference[] BaseReferences =
        GetBaseReferences();

    private static MetadataReference[] GetBaseReferences()
    {
        // Collect core BCL references from the running test process.
        // Includes System.Runtime, System.Collections, etc.
        var trustedAssemblies = ((string)System.AppContext.GetData(
            "TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(System.IO.Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToArray();
        return trustedAssemblies;
    }

    public static GeneratorDriverRunResult RunGenerator(
        string source,
        out Compilation outputCompilation,
        out ImmutableArray<Diagnostic> diagnostics)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            BaseReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DiscriminatedUnionGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out outputCompilation,
                out diagnostics);

        return driver.GetRunResult();
    }

    public static void AssertNoCompilationErrors(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (errors.Count > 0)
        {
            var messages = string.Join("\n",
                errors.Select(e => $"  {e.Location}: {e.Id}: {e.GetMessage()}"));
            Assert.Fail($"Compilation has {errors.Count} error(s):\n{messages}");
        }
    }

    public static void AssertNoGeneratorDiagnostics(
        ImmutableArray<Diagnostic> diagnostics)
    {
        var errors = diagnostics
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .ToList();

        if (errors.Count > 0)
        {
            var messages = string.Join("\n",
                errors.Select(e => $"  {e.Id}: {e.GetMessage()}"));
            Assert.Fail($"Generator reported {errors.Count} diagnostic(s):\n{messages}");
        }
    }
}
```

- [ ] **Step 10: Write attribute injection test**

`tests/Spire.SourceGenerators.Tests/AttributeInjectionTests.cs`:

```csharp
using Xunit;

namespace Spire.SourceGenerators.Tests;

public class AttributeInjectionTests
{
    [Fact]
    public void Attributes_AreInjected_AndCompile()
    {
        var source = """
            using Spire;

            [DiscriminatedUnion]
            partial struct Dummy
            {
                [Variant] static partial void A(int x);
            }
            """;

        GeneratorTestHelper.RunGenerator(source,
            out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);

        // Verify the attribute types exist in compilation
        var duAttr = compilation.GetTypeByMetadataName(
            "Spire.DiscriminatedUnionAttribute");
        Assert.NotNull(duAttr);

        var variantAttr = compilation.GetTypeByMetadataName(
            "Spire.VariantAttribute");
        Assert.NotNull(variantAttr);

        var layoutEnum = compilation.GetTypeByMetadataName("Spire.Layout");
        Assert.NotNull(layoutEnum);
    }
}
```

- [ ] **Step 11: Build and run**

```bash
dotnet build
dotnet test --filter "FullyQualifiedName~AttributeInjection"
```

Attributes test should pass (types are injected). Compilation may have errors from the TODO emitters — that's expected; the attribute test only checks that attribute types exist, not that the union compiles fully.

- [ ] **Step 12: Commit**

```
feat: scaffold Spire.SourceGenerators project with attributes, model, parser, and test infrastructure
```

---

## Task 2: Record emitter + tests

**Dependency:** Task 1

**Files:**
- Modify: `src/Spire.SourceGenerators/Parsing/UnionParser.cs` (add record/class variant discovery)
- Modify: `src/Spire.SourceGenerators/Emit/RecordEmitter.cs`
- Create: `tests/Spire.SourceGenerators.Tests/RecordPathTests.cs`

### Declaration style (record path)

User writes nested partial records with explicit inheritance:
```csharp
[DiscriminatedUnion]
partial record Option<T> {
    partial record Some(T Value) : Option<T>;
    partial record None() : Option<T>;
}
```

### Generated output

```csharp
// <auto-generated/>
#nullable enable

namespace MyLib
{
    public abstract partial record Option<T>
    {
        private Option() { }

        public sealed partial record Some;
        public sealed partial record None;
    }
}
```

Key points:
- Generator adds `abstract` to parent, `sealed` to each variant, private parameterless ctor
- Variants are discovered by finding nested partial types inheriting from the parent (NOT `[Variant]` methods)
- No factory methods — users construct via `new Option<int>.Some(42)`
- No Deconstruct generation — records get it from positional syntax

### Parser changes

`UnionParser` needs a second discovery path for record/class:
- For struct: find `[Variant]` static partial void methods (existing logic)
- For record/class: find nested `INamedTypeSymbol` members where `BaseType` equals the containing type
- Both paths produce `EquatableArray<VariantInfo>` — same model, different discovery

The variant name comes from the nested type's name. Fields come from the primary constructor parameters (for records) or from public properties (for classes).

- [ ] **Step 1: Write failing tests**

`tests/Spire.SourceGenerators.Tests/RecordPathTests.cs` — test cases:

1. `BasicGenericRecord` — `Option<T>` with Some(T) and None(), verify: parent is abstract, variants are sealed, compiles
2. `NonGenericRecord` — `Shape` with Circle(double) and Square(int)
3. `FieldlessVariantOnly` — record with only fieldless variants
4. `MultiFieldVariant` — `Rectangle(float width, float height)`, verify positional params
5. `NamespacedRecord` — verify namespace wrapping
6. `InternalRecord` — verify `internal` accessibility

Each test:
```csharp
var source = @"
using Spire;
namespace TestNs {
    [DiscriminatedUnion]
    partial record Option<T> {
        partial record Some(T Value) : Option<T>;
        partial record None() : Option<T>;
    }
}";
GeneratorTestHelper.RunGenerator(source, out var compilation, out var diagnostics);
GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
GeneratorTestHelper.AssertNoCompilationErrors(compilation);
// Verify type symbols
```

- [ ] **Step 2: Run tests — verify they fail**

- [ ] **Step 3: Update UnionParser for record/class variant discovery**

Add logic: when `declKind` is "record" or "class", find variants by scanning nested types instead of `[Variant]` methods.

- [ ] **Step 4: Implement RecordEmitter**

Generate:
1. `// <auto-generated/>` header + `#nullable enable`
2. Namespace block (if non-empty)
3. `{accessibility} abstract partial record {Name}{TypeParams}`
4. Private constructor `private {Name}() { }`
5. For each variant: `public sealed partial record {Name};`

- [ ] **Step 4: Run tests — verify they pass**

```bash
dotnet test --filter "FullyQualifiedName~RecordPath"
```

- [ ] **Step 5: Commit**

```
feat: implement Record emitter for discriminated unions
```

---

## Task 3: Class emitter + tests

**Dependency:** Task 1 (Task 2 recommended first since it adds the record/class parser discovery logic)

**Files:**
- Modify: `src/Spire.SourceGenerators/Emit/ClassEmitter.cs`
- Create: `tests/Spire.SourceGenerators.Tests/ClassPathTests.cs`

### Declaration style (class path)

User writes nested partial classes with explicit inheritance:
```csharp
[DiscriminatedUnion]
partial class Result<T, E> {
    partial class Ok(T Value) : Result<T, E>;
    partial class Err(E Error) : Result<T, E>;
}
```

### Generated output

```csharp
// <auto-generated/>
#nullable enable

namespace UserNamespace
{
    public abstract partial class Result<T, E>
    {
        private Result() { }

        public sealed partial class Ok;
        public sealed partial class Err;
    }
}
```

Key points:
- Same pattern as Record emitter: `abstract` parent, `sealed` variants, private ctor
- User defines the classes with properties, constructors, Deconstruct (via primary ctor or manually)
- Generator only adds scaffolding — no properties, constructors, or Deconstruct generated
- Variant discovery uses same nested-type-inheriting-parent logic as record path

- [ ] **Step 1: Write failing tests**

`ClassPathTests.cs`:
1. `BasicGenericClass` — `Result<T, E>` with Ok(T) and Err(E). Verify: abstract parent, sealed variants, compiles
2. `FieldlessVariant` — class variant with no properties
3. `MultiFieldVariant` — variant with multiple fields
4. `NonGenericClass` — non-generic class union
5. `NamespacedClass` — verify namespace wrapping
6. `InternalClass` — verify internal accessibility

- [ ] **Step 2: Run tests — verify they fail**
- [ ] **Step 3: Implement ClassEmitter**

Generate: `abstract partial class`, private ctor, `sealed partial class` for each variant. Identical structure to RecordEmitter but with `class` keyword.

- [ ] **Step 4: Run tests — verify they pass**
- [ ] **Step 5: Commit**

```
feat: implement Class emitter for discriminated unions
```

---

## Task 4: BoxedFields struct emitter + tests

**Dependency:** Task 1

**Files:**
- Create: `src/Spire.SourceGenerators/Emit/BoxedFieldsEmitter.cs`
- Create: `src/Spire.SourceGenerators/Emit/DeconstructEmitter.cs`
- Create: `tests/Spire.SourceGenerators.Tests/BoxedFieldsPathTests.cs`

**Generated output structure** for `[DiscriminatedUnion(Layout.BoxedFields)] partial struct Shape`:

```csharp
// <auto-generated/>
#nullable enable

partial struct Shape
{
    public enum Kind
    {
        Circle,
        Rectangle,
        Square,
    }

    public const Kind Circle = Kind.Circle;
    public const Kind Rectangle = Kind.Rectangle;
    public const Kind Square = Kind.Square;

    public readonly Kind tag;
    readonly object? _f0;
    readonly object? _f1;

    Shape(Kind tag, object? f0, object? f1)
    {
        this.tag = tag;
        this._f0 = f0;
        this._f1 = f1;
    }

    public static Shape NewCircle(double radius)
        => new Shape(Kind.Circle, radius, null);
    public static Shape NewRectangle(float width, float height)
        => new Shape(Kind.Rectangle, width, height);
    public static Shape NewSquare(int sideLength)
        => new Shape(Kind.Square, sideLength, null);

    // Deconstruct: shared arity (Circle, Square both 1-field) -> object?
    public void Deconstruct(out Kind kind, out object? f0)
    {
        kind = this.tag;
        f0 = this._f0;
    }

    // Deconstruct: unique arity (Rectangle has 2 fields) -> typed
    public void Deconstruct(out Kind kind, out float width, out float height)
    {
        kind = this.tag;
        width = (float)this._f0!;
        height = (float)this._f1!;
    }
}
```

Key points:
- N `object?` fields where N = max field count across variants
- Constructor takes `(Kind, object?, object?, ...)` — boxing happens at construction
- Deconstruct rules: shared arity -> `object?`, unique arity -> typed (cast from object)
- Constants for pattern matching
- No StructLayout, no FieldOffset

### DeconstructEmitter (shared logic)

`Emit/DeconstructEmitter.cs` — used by BoxedFields, BoxedTuple, and Overlap:

1. Group variants by field count
2. For each group:
   - If only one variant has this count -> typed Deconstruct
   - If multiple variants share this count -> `object?` Deconstruct
3. Emit the Deconstruct method signatures and bodies

The body differs by layout:
- BoxedFields: cast from `_f{n}` fields
- BoxedTuple: cast/destructure from `_payload`
- Overlap: read from typed fields directly

- [ ] **Step 1: Write failing tests**

`BoxedFieldsPathTests.cs`:
1. `BasicBoxedFields_Compiles` — 3 variants, verify no errors
2. `KindEnum_Generated` — verify Kind enum has correct members
3. `Factories_HaveCorrectSignatures` — verify NewX methods exist with right params
4. `Constants_Generated` — verify Kind constants on the struct
5. `Deconstruct_SharedArity` — verify 2-param Deconstruct exists
6. `Deconstruct_UniqueArity` — verify typed Deconstruct for unique field count
7. `GenericStruct_BoxedFields` — generic struct forced to BoxedFields via Auto

- [ ] **Step 2: Run tests — verify they fail**
- [ ] **Step 3: Implement DeconstructEmitter**

Shared logic for grouping variants by field count and determining shared vs unique arity.

- [ ] **Step 4: Implement BoxedFieldsEmitter**

Generate: Kind enum, constants, tag field, object? fields, constructor, factory methods, Deconstruct overloads.

- [ ] **Step 5: Run tests — verify they pass**
- [ ] **Step 6: Commit**

```
feat: implement BoxedFields struct emitter with Deconstruct
```

---

## Task 5: BoxedTuple struct emitter + tests

**Dependency:** Task 1 (Task 4 recommended first for shared DeconstructEmitter)

**Files:**
- Create: `src/Spire.SourceGenerators/Emit/BoxedTupleEmitter.cs`
- Create: `tests/Spire.SourceGenerators.Tests/BoxedTuplePathTests.cs`

**Generated output structure:**

```csharp
partial struct Shape
{
    public enum Kind { Circle, Rectangle, Square, }

    public const Kind Circle = Kind.Circle;
    public const Kind Rectangle = Kind.Rectangle;
    public const Kind Square = Kind.Square;

    public readonly Kind tag;
    readonly object? _payload;

    Shape(Kind tag, object? payload)
    {
        this.tag = tag;
        this._payload = payload;
    }

    public static Shape NewCircle(double radius)
        => new Shape(Kind.Circle, radius);
    public static Shape NewRectangle(float width, float height)
        => new Shape(Kind.Rectangle, (width, height));
    public static Shape NewSquare(int sideLength)
        => new Shape(Kind.Square, sideLength);

    // Single Deconstruct — always (Kind, object?)
    public void Deconstruct(out Kind kind, out object? payload)
    {
        kind = this.tag;
        payload = this._payload;
    }
}
```

Key points:
- Single `object?` payload field — smallest struct
- Multi-field variants box a ValueTuple: `(width, height)`
- Single-field variants box the raw value
- Fieldless variants: payload is null
- Only one Deconstruct overload: `(Kind, object?)`

- [ ] **Step 1: Write failing tests**

`BoxedTuplePathTests.cs`:
1. `BasicBoxedTuple_Compiles` — 3 variants, no errors
2. `SinglePayload_Field` — verify struct has exactly one object? field (plus tag)
3. `Factories_BoxCorrectly` — verify factories exist
4. `Deconstruct_SingleOverload` — verify only (Kind, object?) Deconstruct

- [ ] **Step 2: Run tests — verify they fail**
- [ ] **Step 3: Implement BoxedTupleEmitter**
- [ ] **Step 4: Run tests — verify they pass**
- [ ] **Step 5: Commit**

```
feat: implement BoxedTuple struct emitter
```

---

## Task 6: FieldClassifier + Overlap struct emitter + tests

**Dependency:** Task 1 (largest task, depends on DeconstructEmitter from Task 4)

**Files:**
- Create: `src/Spire.SourceGenerators/Emit/FieldClassifier.cs`
- Create: `src/Spire.SourceGenerators/Emit/OverlapEmitter.cs`
- Create: `tests/Spire.SourceGenerators.Tests/OverlapPathTests.cs`

### FieldClassifier

Classifies each field of each variant into one of three regions:

1. **Region 1 (Unmanaged)** — `IsUnmanagedType == true` AND sizeof computable. Primitives, enums, unmanaged structs. Fields overlap across variants at `FieldOffset(sizeof(Kind))`.

2. **Region 2 (Reference)** — `IsReferenceType == true`. All pointer-sized (8 bytes on 64-bit). Overlap across variants at shared ref slots.

3. **Region 3 (Boxed)** — Everything else: managed value types, generic type params, unknown-size types. Stored as `object?`.

**Sizeof computation:** The generator must compute sizes at generation time for offset calculation. Only types with known compile-time sizeof are Region 1:
- `byte/sbyte`: 1, `short/ushort/char`: 2, `int/uint/float`: 4, `long/ulong/double`: 8
- `bool`: 1, `nint/nuint`: pointer size (8)
- Enums: sizeof(underlying type)
- Structs composed entirely of the above: sum with alignment padding
- `ValueTuple<...>` of the above: computable
- Everything else: NOT sizeof-computable -> Region 3

For the MVP, classify as Region 1 only primitive types, enums, and `ValueTuple` of primitives/enums. User-defined unmanaged structs go to Region 3 (conservative). This avoids needing to recursively compute struct sizes.

**Offset computation:**

```
R1_size    = max across variants of (size of Region 1 data for that variant)
R2_slots   = max across variants of (count of Region 2 fields)
R3_slots   = max across variants of (count of Region 3 fields)

Kind offset    = 0
R1 offset      = sizeof(Kind)  [always 4 for int-backed enum]
R2 offset      = R1_offset + R1_size, aligned to 8
R3 offset      = R2_offset + R2_slots * 8
Total          = R3_offset + R3_slots * 8
```

### Overlap emitter output structure

For `readonly partial struct Shape` in namespace `MyGame` (all Region 1, no Region 2/3):

```csharp
// <auto-generated/>
#nullable enable
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.ComponentModel;

namespace MyGame
{
    [StructLayout(LayoutKind.Explicit)]
    readonly partial struct Shape
    {
        public enum Kind
        {
            Circle,
            Rectangle,
            Square,
        }

        public const Kind Circle = Kind.Circle;
        public const Kind Rectangle = Kind.Rectangle;
        public const Kind Square = Kind.Square;

        [FieldOffset(0)]
        public readonly Kind tag;

        [FieldOffset(4)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public readonly double circle_radius;

        [FieldOffset(4)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public readonly int square_sideLength;

        [FieldOffset(4)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public readonly (float width, float height) rectangle;

        Shape(Kind tag) : this() { this.tag = tag; }

        public static Shape NewCircle(double radius)
        {
            var s = new Shape(Kind.Circle);
            Unsafe.AsRef(in s.circle_radius) = radius;
            return s;
        }

        public static Shape NewRectangle(float width, float height)
        {
            var s = new Shape(Kind.Rectangle);
            Unsafe.AsRef(in s.rectangle) = (width, height);
            return s;
        }

        public static Shape NewSquare(int sideLength)
        {
            var s = new Shape(Kind.Square);
            Unsafe.AsRef(in s.square_sideLength) = sideLength;
            return s;
        }

        // Deconstruct: shared arity (1-field: Circle, Square — NOT Rectangle)
        // Only variants with exactly 1 field are included here.
        public void Deconstruct(out Kind kind, out object? f0)
        {
            kind = this.tag;
            f0 = kind switch
            {
                Kind.Circle => circle_radius,
                Kind.Square => square_sideLength,
                _ => null,
            };
        }

        // Deconstruct: unique arity (2-field: Rectangle only) -> typed, no boxing
        public void Deconstruct(out Kind kind, out float width, out float height)
        {
            kind = this.tag;
            width = rectangle.width;
            height = rectangle.height;
        }
    }
}
```

**Namespace wrapping:** Every emitter must wrap the generated partial in the same namespace as the user's declaration. If `Namespace` is empty (global namespace), skip the namespace block. Always use block-scoped namespace (not file-scoped) for compatibility.

**Readonly modifier:** The generated partial must re-emit `readonly` if `IsReadonly` is true (partial types require modifier agreement). The `Unsafe.AsRef(in field) = value` pattern in factory methods is safe because `s` is a stack local — the mutation completes before the value is returned.

Key points:
- `[StructLayout(LayoutKind.Explicit)]` on the struct
- `[FieldOffset(n)]` on every field
- Fields marked `[EditorBrowsable(Never)]` (hidden from IntelliSense)
- Private constructor with `: this()` (zero-initializes before setting tag)
- Factory methods use `Unsafe.AsRef(in field) = value`
- Region 1 fields overlap at the same offset
- Single-field variants: raw typed field `{variant}_{param}`
- Multi-field variants: ValueTuple `{variant}`

For mixed unions with Region 2 (references):
```csharp
// Region 2 fields — reference slots overlapping across variants
[FieldOffset(R2_OFFSET)]
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly string? label_text;       // slot 0

[FieldOffset(R2_OFFSET)]
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly string? error_message;    // slot 0 (overlaps with label_text)

[FieldOffset(R2_OFFSET + 8)]
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly string? richtext_font;    // slot 1
```

For Region 3 (boxed):
```csharp
[FieldOffset(R3_OFFSET)]
readonly object? _obj_0;

// Typed accessor property
public PlayerInfo RichText_Player => (PlayerInfo)_obj_0!;
```

- [ ] **Step 1: Write failing tests**

`OverlapPathTests.cs`:
1. `AllUnmanaged_Compiles` — Shape with Circle(double), Rectangle(float,float), Square(int)
2. `KindEnum_HasVariants` — verify Kind enum
3. `Fields_HaveFieldOffset` — verify FieldOffset attributes present
4. `Factories_UseUnsafeAsRef` — verify factory methods exist (compilation is the test)
5. `MixedManagedUnmanaged_Compiles` — variant with string field goes to Region 2
6. `RegionTwoRefOverlap` — two variants with different ref types at same slot
7. `Deconstruct_SharedAndUnique` — verify both overloads generated
8. `FieldlessVariant_Handled` — variant with no fields (e.g., Point())

- [ ] **Step 2: Run tests — verify they fail**
- [ ] **Step 3: Implement FieldClassifier**

`Emit/FieldClassifier.cs`:
- `Classify(FieldInfo field) -> FieldRegion`:
  - `IsReferenceType` -> Region2
  - `IsUnmanaged && KnownSize != null` -> Region1
  - everything else -> Region3
- No sizeof computation needed here — `KnownSize` was computed at parse time by `UnionParser.ComputeKnownSize` while the semantic model was available

- [ ] **Step 4: Implement OverlapEmitter**

Build the output in phases:
1. Compute region sizes and offsets
2. Emit Kind enum + constants
3. Emit tag field at offset 0
4. Emit Region 1 fields (unmanaged overlap)
5. Emit Region 2 fields (reference overlap)
6. Emit Region 3 fields (boxed slots + typed accessors)
7. Emit private constructor
8. Emit factory methods with Unsafe.AsRef
9. Emit Deconstruct overloads

- [ ] **Step 5: Run tests — verify they pass**
- [ ] **Step 6: Commit**

```
feat: implement Overlap struct emitter with three-region explicit layout
```

---

## Task 7: Layout diagnostics + Auto resolution + tests

**Dependency:** Tasks 2-6 (all emitters must exist)

**Files:**
- Modify: `src/Spire.SourceGenerators/Parsing/UnionParser.cs`
- Modify: `src/Spire.SourceGenerators/DiscriminatedUnionGenerator.cs`
- Create: `tests/Spire.SourceGenerators.Tests/LayoutDiagnosticTests.cs`

**Diagnostics to implement:**

| Scenario | Severity | Message |
|---|---|---|
| `Layout` on `partial record` or `partial class` | Warning | "Layout only applies to structs; ignored for record/class" |
| `Layout.Overlap` on generic struct | Error | "Generic structs cannot use Overlap layout (CLR restriction). Use BoxedFields or BoxedTuple." |
| `Layout.Auto` on generic struct | No diagnostic (silently picks BoxedFields) |
| `[DiscriminatedUnion]` on a nested type | Error | "Discriminated unions cannot be nested types. Declare at namespace scope." |
| `[DiscriminatedUnion]` on a `ref struct` | Error | "Discriminated unions cannot be ref structs." |
| No `[Variant]` methods found | Warning | "No [Variant] methods found on discriminated union type." |

**Implementation:**

1. Create a `Diagnostics.cs` file with `DiagnosticDescriptor` constants
2. In `UnionParser.Parse`, after resolving strategy, check for diagnostic conditions
3. Return diagnostics alongside the `UnionDeclaration` (or report them in the SourceOutput)

Since `ForAttributeWithMetadataName` doesn't directly support emitting diagnostics, the pattern is:
- The model includes an optional diagnostic
- `RegisterSourceOutput` checks for diagnostics and reports them via `SourceProductionContext.ReportDiagnostic`

OR: use a combined provider that collects both the declaration and any diagnostics.

- [ ] **Step 1: Write failing tests**

`LayoutDiagnosticTests.cs`:
1. `LayoutOnRecord_EmitsWarning` — `[DiscriminatedUnion(Layout.Overlap)] partial record Foo`
2. `LayoutOnClass_EmitsWarning` — `[DiscriminatedUnion(Layout.BoxedFields)] partial class Foo`
3. `OverlapOnGenericStruct_EmitsError` — `[DiscriminatedUnion(Layout.Overlap)] partial struct Foo<T>`
4. `AutoOnGenericStruct_UsesBoxedFields` — no diagnostic, verify BoxedFields emitter used
5. `AutoOnNonGenericStruct_UsesOverlap` — no diagnostic, verify Overlap emitter used
6. `NestedType_EmitsError` — `class Outer { [DiscriminatedUnion] partial struct Inner { ... } }`
7. `RefStruct_EmitsError` — `[DiscriminatedUnion] partial ref struct Foo { ... }`
8. `NoVariants_EmitsWarning` — `[DiscriminatedUnion] partial struct Foo { }` (no [Variant] methods)
9. `VariantNamedKind_NoCollision` — variant named `Kind` does not collide with the Kind enum
10. `FileScopedNamespace_Compiles` — union in a file-scoped namespace works

- [ ] **Step 2: Run tests — verify they fail**
- [ ] **Step 3: Implement diagnostics**

Create `Diagnostics.cs` with descriptor constants. Modify `UnionParser` to return diagnostics. Modify generator to report diagnostics.

- [ ] **Step 4: Run tests — verify they pass**
- [ ] **Step 5: Run full test suite**

```bash
dotnet test --project tests/Spire.SourceGenerators.Tests
```

All tests from Tasks 1-7 should pass.

- [ ] **Step 6: Commit**

```
feat: implement layout diagnostics and Auto resolution
```

---

## Parallelization Guide

For subagent-driven execution:

- **Sequential (must go first):** Task 1
- **Parallel batch 1:** Tasks 2, 3 (Record + Class — independent emitters, no shared deps)
- **Sequential:** Task 4 (BoxedFields — creates shared DeconstructEmitter)
- **Parallel batch 2:** Tasks 5, 6 (BoxedTuple + Overlap — both use DeconstructEmitter)
- **Sequential (last):** Task 7 (diagnostics — needs all emitters)

Alternative if more aggressive parallelism desired: Tasks 2-6 can all run in parallel if each emitter inlines its own Deconstruct logic initially, with a refactoring pass at the end to extract DeconstructEmitter.

---

## Follow-Up Plans (not in scope)

1. **Analyzers plan** — Exhaustiveness checking, type safety, variant field access safety
2. **Code fixes plan** — Add missing arms, fix wrong types, expand wildcards
3. **CS8509 suppressor** — Suppress "switch expression does not handle all possible values"
4. **MustBeInit integration** — Emit `[MustBeInit]` on struct unions
5. **ToString / Equality** (struct path) — Generate readable ToString and IEquatable
