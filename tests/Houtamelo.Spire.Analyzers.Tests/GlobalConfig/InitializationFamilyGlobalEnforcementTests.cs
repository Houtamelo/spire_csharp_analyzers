using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Houtamelo.Spire.Analyzers.Tests.GlobalConfig;

public class InitializationFamilyGlobalEnforcementTests
{
    private const string GlobalOption =
        "build_property.Spire_EnforceExhaustivenessOnAllEnumTypes";
    private const string NoZeroEnum = "enum Status { Active = 1, Inactive = 2 }";
    private const string ZeroEnum = "enum Status { None = 0, Active = 1 }";
    private const string MarkedNoZeroEnum =
        "[EnforceInitialization]\nenum Status { Active = 1, Inactive = 2 }";
    private const string PlainStruct = "struct PlainStruct { public int Value; }";
    private const string PlainStructWithConstructor =
        "struct PlainStruct { public int Value; public PlainStruct(int value) => Value = value; }";

    private const string ArrayBody = "void M()\n{\n    var values = new Status[4];\n}";
    private const string NewEnumBody = "Status M() => new Status();";
    private const string ActivatorBody =
        "void M()\n{\n    var generic = Activator.CreateInstance<Status>();\n    var byType = Activator.CreateInstance(typeof(Status));\n}";
    private const string ClearBody =
        "void M()\n{\n    var values = new Status[4];\n    Array.Clear(values);\n    Span<Status> span = values;\n    span.Clear();\n}";
    private const string SkipInitBody =
        "void M()\n{\n    Unsafe.SkipInit(out Status value);\n}";
    private const string UninitializedObjectBody =
        "void M()\n{\n    RuntimeHelpers.GetUninitializedObject(typeof(Status));\n}";
    private const string CastBody =
        "Status M(int raw)\n{\n    var invalid = (Status)0;\n    var unknown = (Status)raw;\n    return invalid;\n}";
    private const string ValidNoZeroCastBody = "Status M() => (Status)1;";
    private const string ValidZeroCastBody = "Status M() => (Status)0;";

    private static readonly (string? Value, bool Report)[] Settings =
    {
        ("true", true), ("TRUE", true), ("false", false),
        ("yes", false), ("1", false), (null, false),
    };

    private static readonly Case[] UnmarkedNoZeroCases =
    {
        M("SPIRE001 unmarked enum array", AnalyzerKind.SPIRE001, "SPIRE001",
            Source(NoZeroEnum, ArrayBody), "new Status[4]"),
        M("SPIRE004 unmarked enum creation", AnalyzerKind.SPIRE004, "SPIRE004",
            Source(NoZeroEnum, NewEnumBody), "new Status()"),
        M("SPIRE005 unmarked enum Activator calls", AnalyzerKind.SPIRE005, "SPIRE005",
            Source(NoZeroEnum, ActivatorBody, "using System;"),
            "Activator.CreateInstance<Status>()", "Activator.CreateInstance(typeof(Status))"),
        M("SPIRE006 unmarked enum clear operations", AnalyzerKind.SPIRE006, "SPIRE006",
            Source(NoZeroEnum, ClearBody, "using System;"), "Array.Clear(values)", "span.Clear()"),
        M("SPIRE007 unmarked enum SkipInit", AnalyzerKind.SPIRE007, "SPIRE007",
            Source(NoZeroEnum, SkipInitBody, "using System.Runtime.CompilerServices;"),
            "Unsafe.SkipInit(out Status value)"),
        M("SPIRE008 unmarked enum uninitialized object", AnalyzerKind.SPIRE008, "SPIRE008",
            Source(NoZeroEnum, UninitializedObjectBody,
                "using System;\nusing System.Runtime.CompilerServices;"),
            "RuntimeHelpers.GetUninitializedObject(typeof(Status))"),
        M("SPIRE016 unmarked enum casts", AnalyzerKind.SPIRE016, "SPIRE016",
            Source(NoZeroEnum, CastBody), "(Status)0", "(Status)raw"),
    };

    private static readonly Case[] ControlCases =
    {
        F("SPIRE001 unmarked enum with zero", AnalyzerKind.SPIRE001, "SPIRE001",
            Source(ZeroEnum, ArrayBody), "true", false),
        F("SPIRE004 unmarked enum with zero", AnalyzerKind.SPIRE004, "SPIRE004",
            Source(ZeroEnum, NewEnumBody), "true", false),
        F("SPIRE005 unmarked enum with zero", AnalyzerKind.SPIRE005, "SPIRE005",
            Source(ZeroEnum, ActivatorBody, "using System;"), "true", false),
        F("SPIRE006 unmarked enum with zero", AnalyzerKind.SPIRE006, "SPIRE006",
            Source(ZeroEnum, ClearBody, "using System;"), "true", false),
        F("SPIRE007 unmarked enum with zero still reports", AnalyzerKind.SPIRE007, "SPIRE007",
            Source(ZeroEnum, SkipInitBody, "using System.Runtime.CompilerServices;"), "true", true,
            "Unsafe.SkipInit(out Status value)"),
        F("SPIRE008 unmarked enum with zero", AnalyzerKind.SPIRE008, "SPIRE008",
            Source(ZeroEnum, UninitializedObjectBody,
                "using System;\nusing System.Runtime.CompilerServices;"), "true", false),
        F("SPIRE016 unmarked enum valid constant", AnalyzerKind.SPIRE016, "SPIRE016",
            Source(NoZeroEnum, ValidNoZeroCastBody), "true", false),
        F("SPIRE016 unmarked enum zero-valued constant", AnalyzerKind.SPIRE016, "SPIRE016",
            Source(ZeroEnum, ValidZeroCastBody), "true", false),

        F("SPIRE001 marked enum with global disabled", AnalyzerKind.SPIRE001, "SPIRE001",
            Source(MarkedNoZeroEnum, ArrayBody, "using Houtamelo.Spire;"), "false", true,
            "new Status[4]"),
        F("SPIRE004 marked enum with global disabled", AnalyzerKind.SPIRE004, "SPIRE004",
            Source(MarkedNoZeroEnum, NewEnumBody, "using Houtamelo.Spire;"), "false", true,
            "new Status()"),
        F("SPIRE005 marked enum with global disabled", AnalyzerKind.SPIRE005, "SPIRE005",
            Source(MarkedNoZeroEnum, ActivatorBody, "using System;\nusing Houtamelo.Spire;"),
            "false", true, "Activator.CreateInstance<Status>()",
            "Activator.CreateInstance(typeof(Status))"),
        F("SPIRE006 marked enum with global disabled", AnalyzerKind.SPIRE006, "SPIRE006",
            Source(MarkedNoZeroEnum, ClearBody, "using System;\nusing Houtamelo.Spire;"),
            "false", true, "Array.Clear(values)", "span.Clear()"),
        F("SPIRE007 marked enum with global disabled", AnalyzerKind.SPIRE007, "SPIRE007",
            Source(MarkedNoZeroEnum, SkipInitBody,
                "using System.Runtime.CompilerServices;\nusing Houtamelo.Spire;"),
            "false", true, "Unsafe.SkipInit(out Status value)"),
        F("SPIRE008 marked enum with global disabled", AnalyzerKind.SPIRE008, "SPIRE008",
            Source(MarkedNoZeroEnum, UninitializedObjectBody,
                "using System;\nusing System.Runtime.CompilerServices;\nusing Houtamelo.Spire;"),
            "false", true, "RuntimeHelpers.GetUninitializedObject(typeof(Status))"),
        F("SPIRE016 marked enum with global disabled", AnalyzerKind.SPIRE016, "SPIRE016",
            Source(MarkedNoZeroEnum, CastBody, "using Houtamelo.Spire;"), "false", true,
            "(Status)0", "(Status)raw"),

        F("SPIRE001 unmarked struct attribute boundary", AnalyzerKind.SPIRE001, "SPIRE001",
            Source(PlainStruct, ArrayBody.Replace("Status", "PlainStruct")), "true", false),
        F("SPIRE004 unmarked struct attribute boundary", AnalyzerKind.SPIRE004, "SPIRE004",
            Source(PlainStructWithConstructor, NewEnumBody.Replace("Status", "PlainStruct")),
            "true", false),
        F("SPIRE005 unmarked struct attribute boundary", AnalyzerKind.SPIRE005, "SPIRE005",
            Source(PlainStruct, ActivatorBody.Replace("Status", "PlainStruct"), "using System;"),
            "true", false),
        F("SPIRE006 unmarked struct attribute boundary", AnalyzerKind.SPIRE006, "SPIRE006",
            Source(PlainStruct, ClearBody.Replace("Status", "PlainStruct"), "using System;"),
            "true", false),
        F("SPIRE007 unmarked struct attribute boundary", AnalyzerKind.SPIRE007, "SPIRE007",
            Source(PlainStruct, SkipInitBody.Replace("Status", "PlainStruct"),
                "using System.Runtime.CompilerServices;"), "true", false),
        F("SPIRE008 unmarked struct attribute boundary", AnalyzerKind.SPIRE008, "SPIRE008",
            Source(PlainStruct, UninitializedObjectBody.Replace("Status", "PlainStruct"),
                "using System;\nusing System.Runtime.CompilerServices;"), "true", false),
    };

    public static IEnumerable<object[]> Cases()
    {
        foreach (var definition in UnmarkedNoZeroCases)
            foreach (var (setting, report) in Settings)
                yield return Row(definition with { Setting = setting, ShouldReport = report });

        foreach (var definition in ControlCases)
            yield return Row(definition);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task GlobalSettingBehavior(
        string caseName, AnalyzerKind analyzer, string ruleId, string source,
        string? setting, bool shouldReport, string[] expectedExpressions)
    {
        var diagnostics = await GetDiagnosticsAsync(analyzer, source, setting, caseName);
        if (shouldReport)
            AssertExactDiagnostics(diagnostics, ruleId, source, expectedExpressions);
        else
            Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task MissingSpireReference_DoesNotTurnPlainStructIntoEnforcedType()
    {
        const string source = "struct PlainStruct { public int Value; }\n"
            + "class C { void M() { var values = new PlainStruct[4]; } }";
        var diagnostics = await GetDiagnosticsWithoutSpireReferenceAsync(source, "true");
        Assert.Empty(diagnostics);
    }

    private static string Source(string declaration, string body, string usings = "")
        => (usings.Length == 0 ? "" : usings + "\n") + declaration
            + "\nclass C\n{\n" + body + "\n}\n";

    private static Case M(string name, AnalyzerKind analyzer, string ruleId,
        string source, params string[] expressions)
        => new(name, analyzer, ruleId, source, null, false, expressions);

    private static Case F(string name, AnalyzerKind analyzer, string ruleId,
        string source, string? setting, bool report, params string[] expressions)
        => new(name, analyzer, ruleId, source, setting, report, expressions);

    private static object[] Row(Case definition)
        => new object[]
        {
            definition.Name, definition.Analyzer, definition.RuleId, definition.Source,
            definition.Setting!, definition.ShouldReport, definition.ExpectedExpressions,
        };

    private static Dictionary<string, string> Options(string? setting)
    {
        var options = new Dictionary<string, string>();
        if (setting is not null)
            options[GlobalOption] = setting;
        return options;
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        AnalyzerKind analyzer, string source, string? setting, string caseName)
    {
        var options = Options(setting);
        var compilerDiagnostics = await GlobalConfigAnalyzerTestHelper
            .GetCompilerDiagnosticsAsync(source, options);
        AssertNoCompilerErrors(caseName, compilerDiagnostics);
        return await RunAnalyzerAsync(analyzer, source, options);
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        AnalyzerKind analyzer, string source, Dictionary<string, string> options)
        => analyzer switch
        {
            AnalyzerKind.SPIRE001 => await GlobalConfigAnalyzerTestHelper
                .GetDiagnosticsAsync<SPIRE001ArrayOfEnforceInitializationStructAnalyzer>(source, options),
            AnalyzerKind.SPIRE004 => await GlobalConfigAnalyzerTestHelper
                .GetDiagnosticsAsync<SPIRE004NewOfEnforceInitializationStructWithoutCtorAnalyzer>(source, options),
            AnalyzerKind.SPIRE005 => await GlobalConfigAnalyzerTestHelper
                .GetDiagnosticsAsync<SPIRE005ActivatorCreateInstanceOfEnforceInitializationStructAnalyzer>(source, options),
            AnalyzerKind.SPIRE006 => await GlobalConfigAnalyzerTestHelper
                .GetDiagnosticsAsync<SPIRE006ClearOfEnforceInitializationElementsAnalyzer>(source, options),
            AnalyzerKind.SPIRE007 => await GlobalConfigAnalyzerTestHelper
                .GetDiagnosticsAsync<SPIRE007UnsafeSkipInitOfEnforceInitializationStructAnalyzer>(source, options),
            AnalyzerKind.SPIRE008 => await GlobalConfigAnalyzerTestHelper
                .GetDiagnosticsAsync<SPIRE008GetUninitializedObjectOfEnforceInitializationStructAnalyzer>(source, options),
            AnalyzerKind.SPIRE016 => await GlobalConfigAnalyzerTestHelper
                .GetDiagnosticsAsync<SPIRE016InvalidEnforceInitializationEnumValueAnalyzer>(source, options),
            _ => throw new ArgumentOutOfRangeException(nameof(analyzer), analyzer, null),
        };

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsWithoutSpireReferenceAsync(
        string source, string setting)
    {
        var references = await ReferenceAssemblies.Net.Net80.ResolveAsync(
            LanguageNames.CSharp, CancellationToken.None);
        references = references.Add(MetadataReference.CreateFromFile(
            typeof(SPIRE001ArrayOfEnforceInitializationStructAnalyzer).Assembly.Location));
        var tree = CSharpSyntaxTree.ParseText(
            source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        AssertNoCompilerErrors(nameof(GetDiagnosticsWithoutSpireReferenceAsync), compilation.GetDiagnostics());

        var options = Options(setting);
        var analyzerOptions = new AnalyzerOptions(
            ImmutableArray<AdditionalText>.Empty,
            new TestAnalyzerConfigOptionsProvider(options));
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(
                new SPIRE001ArrayOfEnforceInitializationStructAnalyzer()),
            analyzerOptions);
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static void AssertNoCompilerErrors(
        string caseName, ImmutableArray<Diagnostic> diagnostics)
    {
        var errors = diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.True(
            errors.Length == 0,
            caseName + " produced compiler errors: " + string.Join(
                Environment.NewLine,
                errors.Select(diagnostic => diagnostic.Id + ": " + diagnostic.GetMessage())));
    }

    private static void AssertExactDiagnostics(
        ImmutableArray<Diagnostic> diagnostics, string ruleId, string source,
        params string[] expressions)
    {
        Assert.Equal(expressions.Length, diagnostics.Length);
        Assert.All(diagnostics, diagnostic => Assert.Equal(ruleId, diagnostic.Id));
        foreach (var expression in expressions)
        {
            var start = source.IndexOf(expression, StringComparison.Ordinal);
            Assert.True(start >= 0, "Expected expression was not found: " + expression);
            var expectedSpan = new TextSpan(start, expression.Length);
            var diagnostic = Assert.Single(
                diagnostics.Where(diagnostic => diagnostic.Location.SourceSpan.Equals(expectedSpan)));
            Assert.Equal(ruleId, diagnostic.Id);
            Assert.Equal(expectedSpan, diagnostic.Location.SourceSpan);
        }
    }

    public enum AnalyzerKind { SPIRE001, SPIRE004, SPIRE005, SPIRE006, SPIRE007, SPIRE008, SPIRE016 }

    private sealed record Case(
        string Name, AnalyzerKind Analyzer, string RuleId, string Source,
        string? Setting, bool ShouldReport, string[] ExpectedExpressions);
}
