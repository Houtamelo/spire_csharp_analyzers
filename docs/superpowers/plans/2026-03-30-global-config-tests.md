# Global Config Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Verify that MSBuild `CompilerVisibleProperty` global configuration works for both DU defaults and analyzer enforcement.

**Architecture:** Two test approaches: (1) in-process tests that mock `AnalyzerConfigOptionsProvider` for precise assertions, (2) an end-to-end project with real MSBuild properties set in `.csproj` for pipeline validation. In-process tests go in the existing test projects. End-to-end tests get a new project.

**Tech Stack:** xUnit, Microsoft.CodeAnalysis.CSharp (5.0.0), `AnalyzerConfigOptionsProvider` mock

---

## File Structure

```
tests/Houtamelo.Spire.Analyzers.Tests/
  GlobalConfig/                              # In-process analyzer tests with mocked config
    GlobalConfigAnalyzerTestHelper.cs        # Creates CompilationWithAnalyzers + config
    SPIRE015_GlobalEnforcementTests.cs       # Exhaustive switch on unmarked enums
    SPIRE003_GlobalEnforcementTests.cs       # default(T) on unmarked enums

tests/Houtamelo.Spire.SourceGenerators.Tests/
  GlobalConfig/                              # In-process generator tests with mocked config
    GlobalConfigGeneratorTests.cs            # DU defaults via config provider

tests/Houtamelo.Spire.GlobalConfig.EndToEnd/ # NEW PROJECT — end-to-end MSBuild validation
  Spire.GlobalConfig.EndToEnd.csproj
  EnumExhaustivenessTests.cs                 # All-enum enforcement smoke test
  DUDefaultsTests.cs                         # Generator picks up MSBuild defaults
  Types/
    UnmarkedEnum.cs                          # Enum WITHOUT [EnforceExhaustiveness]
    AdditiveUnion.cs                         # [DiscriminatedUnion] — should use Additive from config
```

---

### Task 1: Create TestAnalyzerConfigOptionsProvider

Shared mock for both analyzer and generator tests.

**Files:**
- Create: `tests/Houtamelo.Spire.Analyzers.Tests/GlobalConfig/TestAnalyzerConfigOptionsProvider.cs`

- [ ] **Step 1: Write the mock**

```csharp
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Houtamelo.Spire.Analyzers.Tests.GlobalConfig;

/// Mock AnalyzerConfigOptionsProvider that returns configured global options.
internal sealed class TestGlobalOptions : AnalyzerConfigOptions
{
    private readonly Dictionary<string, string> _values;

    public TestGlobalOptions(Dictionary<string, string> values)
        => _values = values;

    public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
        => _values.TryGetValue(key, out value);
}

internal sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private static readonly AnalyzerConfigOptions Empty = new TestGlobalOptions(new Dictionary<string, string>());

    private readonly TestGlobalOptions _globalOptions;

    public TestAnalyzerConfigOptionsProvider(Dictionary<string, string> globalOptions)
        => _globalOptions = new TestGlobalOptions(globalOptions);

    public override AnalyzerConfigOptions GlobalOptions => _globalOptions;
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build tests/Houtamelo.Spire.Analyzers.Tests/`
Expected: 0 errors

- [ ] **Step 3: Commit**

---

### Task 2: Create GlobalConfigAnalyzerTestHelper

Runs an analyzer against source code with a custom `AnalyzerConfigOptionsProvider`.

**Files:**
- Create: `tests/Houtamelo.Spire.Analyzers.Tests/GlobalConfig/GlobalConfigAnalyzerTestHelper.cs`

- [ ] **Step 1: Write the helper**

```csharp
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Houtamelo.Spire.Analyzers.Tests.GlobalConfig;

internal static class GlobalConfigAnalyzerTestHelper
{
    private static readonly MetadataReference CoreAssemblyReference =
        MetadataReference.CreateFromFile(typeof(Houtamelo.Spire.EnforceInitializationAttribute).Assembly.Location);

    private static readonly MetadataReference AnalyzerAssemblyReference =
        MetadataReference.CreateFromFile(typeof(Houtamelo.Spire.Analyzers.Rules.SPIRE015ExhaustiveEnumSwitchAnalyzer).Assembly.Location);

    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync<TAnalyzer>(
        string source,
        Dictionary<string, string> globalOptions)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var refs = await ReferenceAssemblies.Net.Net80.ResolveAsync(
            LanguageNames.CSharp, CancellationToken.None);
        refs = refs.Add(CoreAssemblyReference).Add(AnalyzerAssemblyReference);

        var tree = CSharpSyntaxTree.ParseText(source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));

        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { tree }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var configProvider = new TestAnalyzerConfigOptionsProvider(globalOptions);
        var analyzerOptions = new AnalyzerOptions(
            ImmutableArray<AdditionalText>.Empty, configProvider);

        var analyzer = new TAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer), analyzerOptions);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build tests/Houtamelo.Spire.Analyzers.Tests/`
Expected: 0 errors

- [ ] **Step 3: Commit**

---

### Task 3: SPIRE015 global enforcement tests

Verify that `Spire_EnforceExhaustivenessOnAllEnumTypes=true` makes SPIRE015 fire on all enum switches.

**Files:**
- Create: `tests/Houtamelo.Spire.Analyzers.Tests/GlobalConfig/SPIRE015_GlobalEnforcementTests.cs`

- [ ] **Step 1: Write the tests**

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.Rules;
using Xunit;

namespace Houtamelo.Spire.Analyzers.Tests.GlobalConfig;

public class SPIRE015_GlobalEnforcementTests
{
    private const string UnmarkedEnumSource = @"
public enum Color { Red, Green, Blue }
public class C
{
    public int M(Color c) => c switch
    {
        Color.Red => 1,
        Color.Green => 2,
        _ => 0,
    };
}
";

    [Fact]
    public async Task GlobalEnabled_UnmarkedEnum_MissingMember_Reports()
    {
        var options = new Dictionary<string, string>
        {
            ["build_property.Spire_EnforceExhaustivenessOnAllEnumTypes"] = "true"
        };

        var diagnostics = await GlobalConfigAnalyzerTestHelper
            .GetDiagnosticsAsync<SPIRE015ExhaustiveEnumSwitchAnalyzer>(UnmarkedEnumSource, options);

        Assert.Contains(diagnostics, d => d.Id == "SPIRE015");
    }

    [Fact]
    public async Task GlobalDisabled_UnmarkedEnum_MissingMember_NoReport()
    {
        var options = new Dictionary<string, string>();

        var diagnostics = await GlobalConfigAnalyzerTestHelper
            .GetDiagnosticsAsync<SPIRE015ExhaustiveEnumSwitchAnalyzer>(UnmarkedEnumSource, options);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SPIRE015");
    }

    [Fact]
    public async Task GlobalEnabled_UnmarkedEnum_AllMembersCovered_NoReport()
    {
        var source = @"
public enum Color { Red, Green, Blue }
public class C
{
    public int M(Color c) => c switch
    {
        Color.Red => 1,
        Color.Green => 2,
        Color.Blue => 3,
    };
}
";
        var options = new Dictionary<string, string>
        {
            ["build_property.Spire_EnforceExhaustivenessOnAllEnumTypes"] = "true"
        };

        var diagnostics = await GlobalConfigAnalyzerTestHelper
            .GetDiagnosticsAsync<SPIRE015ExhaustiveEnumSwitchAnalyzer>(source, options);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SPIRE015");
    }

    [Fact]
    public async Task GlobalEnabled_ExternalEnum_MissingMember_Reports()
    {
        // System.DayOfWeek — an external enum not in the test assembly
        var source = @"
public class C
{
    public int M(System.DayOfWeek d) => d switch
    {
        System.DayOfWeek.Monday => 1,
        _ => 0,
    };
}
";
        var options = new Dictionary<string, string>
        {
            ["build_property.Spire_EnforceExhaustivenessOnAllEnumTypes"] = "true"
        };

        var diagnostics = await GlobalConfigAnalyzerTestHelper
            .GetDiagnosticsAsync<SPIRE015ExhaustiveEnumSwitchAnalyzer>(source, options);

        Assert.Contains(diagnostics, d => d.Id == "SPIRE015");
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/Houtamelo.Spire.Analyzers.Tests/ --filter "FullyQualifiedName~SPIRE015_GlobalEnforcementTests"`
Expected: 4 passed, 0 failed

- [ ] **Step 3: Commit**

---

### Task 4: SPIRE003 global enforcement tests

Verify that `Spire_EnforceExhaustivenessOnAllEnumTypes=true` makes SPIRE003 fire on `default(UnmarkedEnum)`.

**Files:**
- Create: `tests/Houtamelo.Spire.Analyzers.Tests/GlobalConfig/SPIRE003_GlobalEnforcementTests.cs`

- [ ] **Step 1: Write the tests**

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.Rules;
using Xunit;

namespace Houtamelo.Spire.Analyzers.Tests.GlobalConfig;

public class SPIRE003_GlobalEnforcementTests
{
    // Enum with no zero-valued member — default(Status) produces unnamed value
    private const string EnumNoZeroMember = @"
public enum Status { Active = 1, Inactive = 2 }
public class C
{
    public Status M() => default(Status);
}
";

    // Enum WITH a zero-valued member — default(Color) = Red, which is valid
    private const string EnumWithZeroMember = @"
public enum Color { Red = 0, Green = 1, Blue = 2 }
public class C
{
    public Color M() => default(Color);
}
";

    [Fact]
    public async Task GlobalEnabled_NoZeroMember_DefaultReports()
    {
        var options = new Dictionary<string, string>
        {
            ["build_property.Spire_EnforceExhaustivenessOnAllEnumTypes"] = "true"
        };

        var diagnostics = await GlobalConfigAnalyzerTestHelper
            .GetDiagnosticsAsync<SPIRE003DefaultOfEnforceInitializationStructAnalyzer>(
                EnumNoZeroMember, options);

        Assert.Contains(diagnostics, d => d.Id == "SPIRE003");
    }

    [Fact]
    public async Task GlobalDisabled_NoZeroMember_DefaultNoReport()
    {
        var options = new Dictionary<string, string>();

        var diagnostics = await GlobalConfigAnalyzerTestHelper
            .GetDiagnosticsAsync<SPIRE003DefaultOfEnforceInitializationStructAnalyzer>(
                EnumNoZeroMember, options);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SPIRE003");
    }

    [Fact]
    public async Task GlobalEnabled_WithZeroMember_DefaultNoReport()
    {
        // Even with global enforcement, default(Color) = Red which is a valid named member
        var options = new Dictionary<string, string>
        {
            ["build_property.Spire_EnforceExhaustivenessOnAllEnumTypes"] = "true"
        };

        var diagnostics = await GlobalConfigAnalyzerTestHelper
            .GetDiagnosticsAsync<SPIRE003DefaultOfEnforceInitializationStructAnalyzer>(
                EnumWithZeroMember, options);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SPIRE003");
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/Houtamelo.Spire.Analyzers.Tests/ --filter "FullyQualifiedName~SPIRE003_GlobalEnforcementTests"`
Expected: 3 passed, 0 failed

- [ ] **Step 3: Commit**

---

### Task 5: Generator global config tests

Verify that DU MSBuild defaults are picked up by the source generator.

**Files:**
- Create: `tests/Houtamelo.Spire.SourceGenerators.Tests/GlobalConfig/GlobalConfigGeneratorTests.cs`

- [ ] **Step 1: Copy `TestAnalyzerConfigOptionsProvider` to generator tests project**

Create the same mock in the generator test project (or use `[InternalsVisibleTo]` — but a copy is simpler since the mock is small).

Create: `tests/Houtamelo.Spire.SourceGenerators.Tests/GlobalConfig/TestAnalyzerConfigOptionsProvider.cs`

Same content as Task 1, but namespace `Houtamelo.Spire.SourceGenerators.Tests.GlobalConfig`.

- [ ] **Step 2: Write the tests**

```csharp
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.GlobalConfig;

public class GlobalConfigGeneratorTests
{
    private static readonly MetadataReference CoreRef =
        MetadataReference.CreateFromFile(typeof(Houtamelo.Spire.DiscriminatedUnionAttribute).Assembly.Location);

    private static readonly MetadataReference[] BaseRefs = GetBaseRefs();

    private static MetadataReference[] GetBaseRefs()
    {
        return ((string)System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(System.IO.Path.PathSeparator)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(CoreRef)
            .ToArray();
    }

    private static GeneratorDriverRunResult RunWithConfig(
        string source, Dictionary<string, string> globalOptions)
    {
        var tree = CSharpSyntaxTree.ParseText(source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { tree }, BaseRefs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        var configProvider = new TestAnalyzerConfigOptionsProvider(globalOptions);
        var generator = new DiscriminatedUnionGenerator();
        var driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator.AsSourceGenerator() },
            optionsProvider: configProvider);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        // No error diagnostics expected
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        return driver.GetRunResult();
    }

    private const string BasicUnionSource = @"
using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct Shape
{
    [Variant] public static partial Shape Circle(float radius);
    [Variant] public static partial Shape Rect(float w, float h);
}
";

    [Fact]
    public void DefaultLayout_Auto_NonGeneric_EmitsOverlap()
    {
        // No global config → ReadGlobalCfg → defaults to Auto → Overlap for non-generic
        var result = RunWithConfig(BasicUnionSource, new Dictionary<string, string>());
        var source = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .FirstOrDefault(s => s.Contains("partial struct Shape"));

        Assert.NotNull(source);
        // Overlap uses [StructLayout(LayoutKind.Explicit)] and [FieldOffset]
        Assert.Contains("FieldOffset", source);
    }

    [Fact]
    public void GlobalLayout_Additive_EmitsAdditive()
    {
        var options = new Dictionary<string, string>
        {
            ["build_property.Spire_DU_DefaultLayout"] = "Additive"
        };

        var result = RunWithConfig(BasicUnionSource, options);
        var source = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .FirstOrDefault(s => s.Contains("partial struct Shape"));

        Assert.NotNull(source);
        // Additive does NOT use FieldOffset — fields are side-by-side
        Assert.DoesNotContain("FieldOffset", source);
    }

    [Fact]
    public void ExplicitLayout_Overrides_GlobalDefault()
    {
        // Attribute says Overlap, global says Additive → Overlap wins
        var source = @"
using Houtamelo.Spire;

[DiscriminatedUnion(layout: Layout.Overlap)]
public partial struct Shape
{
    [Variant] public static partial Shape Circle(float radius);
    [Variant] public static partial Shape Rect(float w, float h);
}
";
        var options = new Dictionary<string, string>
        {
            ["build_property.Spire_DU_DefaultLayout"] = "Additive"
        };

        var result = RunWithConfig(source, options);
        var generated = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .FirstOrDefault(s => s.Contains("partial struct Shape"));

        Assert.NotNull(generated);
        Assert.Contains("FieldOffset", generated);
    }

    [Fact]
    public void GlobalJson_SystemTextJson_EmitsConverter()
    {
        var options = new Dictionary<string, string>
        {
            ["build_property.Spire_DU_DefaultJson"] = "SystemTextJson"
        };

        var result = RunWithConfig(BasicUnionSource, options);
        var stjSource = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .FirstOrDefault(s => s.Contains("JsonConverter"));

        Assert.NotNull(stjSource);
    }

    [Fact]
    public void NoGlobalJson_NoConverter()
    {
        var result = RunWithConfig(BasicUnionSource, new Dictionary<string, string>());
        var stjSource = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .FirstOrDefault(s => s.Contains("JsonConverter"));

        Assert.Null(stjSource);
    }

    [Fact]
    public void GlobalGenerateDeconstruct_No_NoDeconstructMethods()
    {
        var options = new Dictionary<string, string>
        {
            ["build_property.Spire_DU_DefaultGenerateDeconstruct"] = "false"
        };

        var result = RunWithConfig(BasicUnionSource, options);
        var source = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .FirstOrDefault(s => s.Contains("partial struct Shape"));

        Assert.NotNull(source);
        Assert.DoesNotContain("Deconstruct", source);
    }

    [Fact]
    public void GlobalJsonDiscriminator_Custom_UsedInConverter()
    {
        var options = new Dictionary<string, string>
        {
            ["build_property.Spire_DU_DefaultJson"] = "SystemTextJson",
            ["build_property.Spire_DU_DefaultJsonDiscriminator"] = "type"
        };

        var result = RunWithConfig(BasicUnionSource, options);
        var stjSource = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .FirstOrDefault(s => s.Contains("JsonConverter"));

        Assert.NotNull(stjSource);
        // The discriminator property name appears in the converter
        Assert.Contains("\"type\"", stjSource);
    }
}
```

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/Houtamelo.Spire.SourceGenerators.Tests/ --filter "FullyQualifiedName~GlobalConfigGeneratorTests"`
Expected: 7 passed, 0 failed

- [ ] **Step 4: Commit**

---

### Task 6: End-to-end MSBuild project

A standalone test project that sets MSBuild properties and validates behavior at build time.

**Files:**
- Create: `tests/Houtamelo.Spire.GlobalConfig.EndToEnd/Spire.GlobalConfig.EndToEnd.csproj`
- Create: `tests/Houtamelo.Spire.GlobalConfig.EndToEnd/Types/UnmarkedEnum.cs`
- Create: `tests/Houtamelo.Spire.GlobalConfig.EndToEnd/Types/ConfiguredUnion.cs`
- Create: `tests/Houtamelo.Spire.GlobalConfig.EndToEnd/EnumExhaustivenessTests.cs`
- Create: `tests/Houtamelo.Spire.GlobalConfig.EndToEnd/DUDefaultsTests.cs`

- [ ] **Step 1: Create the project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>preview</LangVersion>
    <IsTestProject>true</IsTestProject>
    <Nullable>enable</Nullable>

    <!-- Global config under test -->
    <Spire_EnforceExhaustivenessOnAllEnumTypes>true</Spire_EnforceExhaustivenessOnAllEnumTypes>
    <Spire_DU_DefaultLayout>Additive</Spire_DU_DefaultLayout>
    <Spire_DU_DefaultGenerateDeconstruct>true</Spire_DU_DefaultGenerateDeconstruct>
    <Spire_DU_DefaultJson>None</Spire_DU_DefaultJson>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Houtamelo.Spire.Analyzers\Houtamelo.Spire.Analyzers.csproj"
                      OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
    <ProjectReference Include="..\..\src\Houtamelo.Spire\Houtamelo.Spire.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.2" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the enum type**

`Types/UnmarkedEnum.cs`:
```csharp
namespace Houtamelo.Spire.GlobalConfig.EndToEnd.Types;

// No [EnforceExhaustiveness] attribute — global config should make SPIRE015 fire
public enum Direction { Up, Down, Left, Right }
```

- [ ] **Step 3: Create the union type**

`Types/ConfiguredUnion.cs`:
```csharp
using Houtamelo.Spire;

namespace Houtamelo.Spire.GlobalConfig.EndToEnd.Types;

// No explicit Layout — should use Additive from global config
[DiscriminatedUnion]
public partial struct Shape
{
    [Variant] public static partial Shape Circle(float radius);
    [Variant] public static partial Shape Rect(float w, float h);
}
```

- [ ] **Step 4: Write enum exhaustiveness test**

`EnumExhaustivenessTests.cs`:
```csharp
using Houtamelo.Spire.GlobalConfig.EndToEnd.Types;
using Xunit;

namespace Houtamelo.Spire.GlobalConfig.EndToEnd;

public class EnumExhaustivenessTests
{
    [Fact]
    public void ExhaustiveSwitch_AllMembersCovered_Compiles()
    {
        // This compiles because all members are covered.
        // If any member were missing, SPIRE015 would fire (TreatWarningsAsErrors).
        var result = Direction.Up switch
        {
            Direction.Up => "up",
            Direction.Down => "down",
            Direction.Left => "left",
            Direction.Right => "right",
        };

        Assert.Equal("up", result);
    }
}
```

- [ ] **Step 5: Write DU defaults test**

`DUDefaultsTests.cs`:
```csharp
using System.Runtime.InteropServices;
using Houtamelo.Spire.GlobalConfig.EndToEnd.Types;
using Xunit;

namespace Houtamelo.Spire.GlobalConfig.EndToEnd;

public class DUDefaultsTests
{
    [Fact]
    public void Union_UsesAdditiveLayout()
    {
        // Additive layout does NOT use [StructLayout(LayoutKind.Explicit)].
        // If the global config is working, Shape should be Additive.
        var attr = typeof(Shape).GetCustomAttributes(typeof(StructLayoutAttribute), false);

        // Additive layout uses Sequential (default), not Explicit
        // LayoutKind.Explicit = 2, Sequential = 0, Auto = 3
        var layout = typeof(Shape).StructLayoutAttribute;
        Assert.NotNull(layout);
        Assert.NotEqual(LayoutKind.Explicit, layout!.Value);
    }

    [Fact]
    public void Union_HasDeconstructMethod()
    {
        // GenerateDeconstruct defaults to true — should have Deconstruct methods
        var shape = Shape.Circle(5f);
        // If Deconstruct exists, we can deconstruct
        if (shape.IsCircle)
        {
            shape.Deconstruct(out float radius);
            Assert.Equal(5f, radius);
        }
    }
}
```

- [ ] **Step 6: Restore, build, and run tests**

Run: `dotnet restore tests/Houtamelo.Spire.GlobalConfig.EndToEnd/`
Run: `dotnet test tests/Houtamelo.Spire.GlobalConfig.EndToEnd/`
Expected: 3 passed, 0 failed

- [ ] **Step 7: Commit**

---

### Task 7: Final verification

- [ ] **Step 1: Run all tests across all projects**

Run: `dotnet test` (entire solution)
Expected: All existing tests + new tests pass, 0 failures

- [ ] **Step 2: Commit all changes**

Single commit with all global config test infrastructure and test cases.
