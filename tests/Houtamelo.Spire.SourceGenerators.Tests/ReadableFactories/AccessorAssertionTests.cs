using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.ReadableFactories;

public sealed class AccessorAssertionTests
{
    [Fact]
    [Trait("Environment", "Unity")]
    public void UnityFieldsKeepShortAttributesOnTheFieldLine()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnitySource(),
            includeEditorReferences: false);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        foreach (var tree in result.RunResult.GeneratedTrees)
        {
            var sourceRoot = tree.GetRoot();
            foreach (var field in sourceRoot.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                var declarationLine = field.Declaration.GetLocation().GetLineSpan().StartLinePosition.Line;
                foreach (var attributes in field.AttributeLists.Where(IsShortAttributeList))
                {
                    Assert.Equal(
                        declarationLine,
                        attributes.GetLocation().GetLineSpan().StartLinePosition.Line);
                }
            }
        }

        var root = result.RunResult.GeneratedTrees
            .Select(tree => tree.GetRoot())
            .Single(tree => tree.DescendantNodes()
                .OfType<StructDeclarationSyntax>()
                .Any(structure => structure.Identifier.ValueText == "Command"));
        var command = root.DescendantNodes()
            .OfType<StructDeclarationSyntax>()
            .Single(structure => structure.Identifier.ValueText == "Command");

        foreach (var field in command.Members.OfType<FieldDeclarationSyntax>())
        {
            if (field.AttributeLists.Count == 0)
                continue;

            var fieldLine = field.GetLocation().GetLineSpan().StartLinePosition.Line;
            Assert.All(
                field.AttributeLists,
                attributes => Assert.Equal(
                    fieldLine,
                    attributes.GetLocation().GetLineSpan().StartLinePosition.Line));
        }

        var payload = root.DescendantNodes()
            .OfType<StructDeclarationSyntax>()
            .Single(structure => structure.Identifier.ValueText == "Move" &&
                structure.Members.OfType<FieldDeclarationSyntax>().Any(field =>
                    field.Declaration.Variables.Any(variable => variable.Identifier.ValueText == "_node")));
        var node = payload.Members
            .OfType<FieldDeclarationSyntax>()
            .Single(field => field.Declaration.Variables.Single().Identifier.ValueText == "_node");
        Assert.All(
            node.AttributeLists,
            attributes => Assert.Equal(
                node.GetLocation().GetLineSpan().StartLinePosition.Line,
                attributes.GetLocation().GetLineSpan().StartLinePosition.Line));
        var nodeAttributeNames = node.AttributeLists
            .SelectMany(attributes => attributes.Attributes)
            .Select(attribute => attribute.Name.ToString())
            .ToArray();
        Assert.Contains(nodeAttributeNames, name => name.EndsWith("SerializeReference", StringComparison.Ordinal));
        Assert.DoesNotContain(nodeAttributeNames, name => name.EndsWith("SerializeField", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void UnitySerializationPayloadBaseUsesAnAliasWhenItIsSafe()
    {
        var result = UnityGeneratorTestHarness.Run(UnitySource());
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var source = result.GeneratedSource;
        Assert.Contains(
            "using UnitySerializationPayloadBase = global::Houtamelo.Spire.UnitySerializationPayloadBase;",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "[@SerializeReference] UnitySerializationPayloadBase? _serializedPayload;",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "global::Houtamelo.Spire.UnitySerializationPayloadBase? _serializedPayload;",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void UnitySerializationPayloadBaseAliasIsSkippedWhenTheUnionOwnsThatName()
    {
        var result = UnityGeneratorTestHarness.Run(
            """
            using System;
            using Houtamelo.Spire;

            [Serializable, DiscriminatedUnion]
            public partial struct UnitySerializationPayloadBase
            {
                [Variant]
                public static partial UnitySerializationPayloadBase Idle();
            }
            """);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var source = result.GeneratedSource;
        Assert.DoesNotContain(
            "using UnitySerializationPayloadBase = global::Houtamelo.Spire.UnitySerializationPayloadBase;",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "[@SerializeReference] global::Houtamelo.Spire.UnitySerializationPayloadBase? _serializedPayload;",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StandaloneVariantAccessorsAreReadonlyGettersWithKindAssertions()
    {
        var generated = GenerateStandalone(StandaloneSource());
        AssertNoErrors(generated);

        var root = CSharpSyntaxTree.ParseText(generated.Source).GetRoot();
        var union = root.DescendantNodes()
            .OfType<StructDeclarationSyntax>()
            .Single(structure => structure.Identifier.ValueText == "Command");

        AssertVariantAccessor(union, "idle", "Idle");
        AssertVariantAccessor(union, "move", "Move");
        AssertVariantAccessor(union, "speak", "Speak");
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void UnityVariantAccessorsUseUnityAssertions()
    {
        var result = UnityGeneratorTestHarness.Run(UnitySource());
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var root = result.RunResult.GeneratedTrees
            .Select(tree => tree.GetRoot())
            .Single(tree => tree.DescendantNodes()
                .OfType<StructDeclarationSyntax>()
                .Any(structure => structure.Identifier.ValueText == "Command"));
        var union = root.DescendantNodes()
            .OfType<StructDeclarationSyntax>()
            .Single(structure => structure.Identifier.ValueText == "Command");

        AssertVariantAccessor(union, "idle", "Idle", "global::UnityEngine.Debug.Assert");
        AssertVariantAccessor(union, "move", "Move", "global::UnityEngine.Debug.Assert");
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void UnityAssertionsFollowTheUnityAssertionsSymbolInCSharp9Output()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnitySource(),
            languageVersion: LanguageVersion.CSharp9);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var disabled = ReparseUnityCompilation(result, includeAssertions: false);
        var enabled = ReparseUnityCompilation(result, includeAssertions: true);
        AssertNoCompilationErrors(disabled);
        AssertNoCompilationErrors(enabled);

        var disabledIl = ReadAccessorIl(disabled);
        var enabledIl = ReadAccessorIl(enabled);
        Assert.False(disabledIl.CallsAssert);
        Assert.False(disabledIl.ContainsAssertionMessage);
        Assert.True(enabledIl.CallsAssert);
        Assert.True(enabledIl.ContainsAssertionMessage);
    }

    [Fact]
    public void GenericNestedUnionWithKeywordFieldCompilesWithAccessorAssertions()
    {
        var generated = GenerateStandalone(
            """
            #nullable enable
            using Houtamelo.Spire;

            public static partial class Outer
            {
                [DiscriminatedUnion]
                public partial struct Command<U> where U : unmanaged
                {
                    [Variant]
                    public static partial Command<U> Run(U @event);

                    [Variant]
                    public static partial Command<U> Move(U target);
                }
            }
            """);
        AssertNoErrors(generated);

        var root = CSharpSyntaxTree.ParseText(generated.Source).GetRoot();
        var union = root.DescendantNodes()
            .OfType<StructDeclarationSyntax>()
            .Single(structure => structure.Identifier.ValueText == "Command");
        AssertVariantAccessor(union, "run", "Run");
        AssertVariantAccessor(union, "move", "Move");
        Assert.Contains("where U : unmanaged", generated.Source, StringComparison.Ordinal);
    }

#if !SPIRE_UNITY_TEST_HOST
    [Fact]
    public void StandaloneDebugBuildReportsInactiveVariantAccess()
    {
        var generated = GenerateStandalone(StandaloneSource(), "DEBUG");
        AssertNoErrors(generated);
        var il = ReadAccessorIl(generated.Compilation);
        Assert.True(il.CallsAssert);
        Assert.True(il.ContainsAssertionMessage);

        var child = RunAccessorChild(generated, includeInactiveAccess: true);
        var output = child.StandardOutput + child.StandardError;
        Assert.Contains(
            "Accessing the `Move` variant's fields is only allowed when `kind == Kind.Move`. Actual kind: Idle",
            output,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StandaloneDebugBuildAllowsActiveVariantAccess()
    {
        var generated = GenerateStandalone(StandaloneSource(), "DEBUG");
        AssertNoErrors(generated);
        var child = RunAccessorChild(generated, includeInactiveAccess: false);
        var output = child.StandardOutput + child.StandardError;
        Assert.True(child.ExitCode == 0, $"Child exited {child.ExitCode}: {output}");
        Assert.False(output.Contains("Actual kind: Move", StringComparison.Ordinal), output);
    }

    [Fact]
    public void StandaloneReleaseBuildOmitsInactiveVariantAssertion()
    {
        var generated = GenerateStandalone(StandaloneSource());
        AssertNoErrors(generated);
        var il = ReadAccessorIl(generated.Compilation);
        Assert.False(il.CallsAssert);
        Assert.False(il.ContainsAssertionMessage);

        var child = RunAccessorChild(generated, includeInactiveAccess: true);
        var output = child.StandardOutput + child.StandardError;
        Assert.True(child.ExitCode == 0, $"Child exited {child.ExitCode}: {output}");
        Assert.False(output.Contains("Accessing the `Move` variant's fields", StringComparison.Ordinal), output);
    }
#endif

    private static AccessorIl ReadAccessorIl(Compilation compilation)
    {
        var assembly = UnityGeneratorTestHarness.EmitAndLoad(
            compilation,
            "Accessor assertion IL inspection");
        var unionType = assembly.GetType("Command", throwOnError: true)!;
        var property = unionType.GetProperty(
            "move",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(property);
        var getter = property!.GetGetMethod(nonPublic: true);
        Assert.NotNull(getter);
        var body = getter!.GetMethodBody();
        Assert.NotNull(body);
        var il = body!.GetILAsByteArray();
        Assert.NotNull(il);
        var callsAssert = false;
        var containsMessage = false;

        foreach (var instruction in ReadIlInstructions(il!))
        {
            if (instruction.OperandType == OperandType.InlineMethod)
            {
                var method = getter.Module.ResolveMethod(instruction.Operand);
                Assert.NotNull(method);
                callsAssert |= method!.Name == "Assert";
            }
            else if (instruction.OperandType == OperandType.InlineString)
            {
                var text = getter.Module.ResolveString(instruction.Operand);
                containsMessage |= text.Contains(
                    "Accessing the `Move` variant's fields",
                    StringComparison.Ordinal);
            }
        }

        return new AccessorIl(callsAssert, containsMessage);
    }

    private static IEnumerable<IlInstruction> ReadIlInstructions(byte[] il)
    {
        var opcodes = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)!)
            .ToDictionary(opcode => unchecked((ushort)opcode.Value));
        var offset = 0;
        while (offset < il.Length)
        {
            var opcodeValue = (ushort)il[offset++];
            if (opcodeValue == 0xFE)
                opcodeValue = (ushort)(0xFE00 | il[offset++]);
            var opcode = opcodes[opcodeValue];
            var operand = 0;
            switch (opcode.OperandType)
            {
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                    operand = BitConverter.ToInt32(il.AsSpan(offset, 4));
                    offset += 4;
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    offset += 8;
                    break;
                case OperandType.InlineSwitch:
                    {
                        var count = BitConverter.ToInt32(il.AsSpan(offset, 4));
                        offset += 4 + (count * 4);
                        break;
                    }
                case OperandType.InlineVar:
                    operand = BitConverter.ToUInt16(il.AsSpan(offset, 2));
                    offset += 2;
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    operand = il[offset++];
                    break;
                case OperandType.ShortInlineR:
                    offset += 4;
                    break;
            }

            yield return new IlInstruction(opcode.OperandType, operand);
        }
    }

#if !SPIRE_UNITY_TEST_HOST
    private static ChildResult RunAccessorChild(
        StandaloneGenerated generated,
        bool includeInactiveAccess)
    {
        var programSource = includeInactiveAccess
            ? """
                public static class Program
                {
                    public static int Main()
                    {
                        var idle = Command.Idle();
                        _ = idle.move;
                        return 0;
                    }
                }
                """
            : """
                public static class Program
                {
                    public static int Main()
                    {
                        var move = Command.Move(1);
                        _ = move.move;
                        return 0;
                    }
                }
                """;
        var programTree = CSharpSyntaxTree.ParseText(
            programSource,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest)
                .WithPreprocessorSymbols(generated.PreprocessorSymbols));
        var compilation = (CSharpCompilation)generated.Compilation
            .AddSyntaxTrees(programTree)
            .WithOptions(((CSharpCompilationOptions)generated.Compilation.Options)
                .WithOutputKind(OutputKind.ConsoleApplication));
        var image = EmitImage(compilation);
        var directory = Path.Combine(
            Directory.GetCurrentDirectory(),
            "tmp",
            "accessor-assertions-child-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var assemblyPath = Path.Combine(directory, "AccessorAssertions.dll");
            var runtimeConfigPath = Path.Combine(directory, "AccessorAssertions.runtimeconfig.json");
            File.WriteAllBytes(assemblyPath, image);
            File.Copy(
                typeof(Houtamelo.Spire.DiscriminatedUnionAttribute).Assembly.Location,
                Path.Combine(directory, "Houtamelo.Spire.dll"));
            File.WriteAllText(
                runtimeConfigPath,
                $"{{\"runtimeOptions\":{{\"tfm\":\"net{Environment.Version.Major}.{Environment.Version.Minor}\",\"framework\":{{\"name\":\"Microsoft.NETCore.App\",\"version\":\"{Environment.Version.Major}.{Environment.Version.Minor}.{Environment.Version.Build}\"}}}}}}");

            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            if (OperatingSystem.IsLinux())
            {
                startInfo.FileName = "prlimit";
                startInfo.ArgumentList.Add("--core=0");
                startInfo.ArgumentList.Add("--");
                startInfo.ArgumentList.Add("dotnet");
            }
            else
            {
                startInfo.FileName = "dotnet";
            }

            startInfo.ArgumentList.Add("exec");
            startInfo.ArgumentList.Add("--runtimeconfig");
            startInfo.ArgumentList.Add(runtimeConfigPath);
            startInfo.ArgumentList.Add(assemblyPath);
            startInfo.Environment["DOTNET_DbgEnableMiniDump"] = "0";
            startInfo.Environment["COMPlus_DbgEnableMiniDump"] = "0";
            startInfo.Environment["COMPlus_DbgEnableMiniDumpType"] = "0";
            startInfo.Environment["DOTNET_EnableDiagnostics"] = "0";

            using var process = Process.Start(startInfo)!;
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Accessor assertion child process did not exit within ten seconds.");
            }

            return new ChildResult(
                process.ExitCode,
                standardOutput.GetAwaiter().GetResult(),
                standardError.GetAwaiter().GetResult());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
#endif

    private static byte[] EmitImage(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        return stream.ToArray();
    }

    private readonly record struct IlInstruction(OperandType OperandType, int Operand);

    private readonly record struct AccessorIl(bool CallsAssert, bool ContainsAssertionMessage);

    private readonly record struct ChildResult(int ExitCode, string StandardOutput, string StandardError);

    private static void AssertNoErrors(StandaloneGenerated generated)
    {
        var errors = generated.Diagnostics
            .Concat(generated.Compilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
    }

    private static Assembly EmitAndLoad(Compilation compilation)
    {
        var image = EmitImage(compilation);
        return TestHarnessEnvironment.LoadAssembly(image);
    }

    private static void AssertVariantAccessor(
        StructDeclarationSyntax union,
        string propertyName,
        string variantName,
        string debugAssert = "global::System.Diagnostics.Debug.Assert")
    {
        var property = union.Members
            .OfType<PropertyDeclarationSyntax>()
            .Single(candidate => candidate.Identifier.ValueText == propertyName);
        Assert.Contains(
            property.Modifiers,
            modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword));

        var getter = Assert.Single(property.AccessorList!.Accessors);
        Assert.True(getter.IsKind(SyntaxKind.GetAccessorDeclaration));
        Assert.NotNull(getter.Body);
        Assert.Null(property.ExpressionBody);
        Assert.True(getter.Body!.Statements.Count >= 2);

        var assertStatement = Assert.IsType<ExpressionStatementSyntax>(getter.Body.Statements[0]);
        var invocation = Assert.IsType<InvocationExpressionSyntax>(assertStatement.Expression);
        Assert.Equal(debugAssert, invocation.Expression.ToString());
        Assert.Equal(2, invocation.ArgumentList.Arguments.Count);
        Assert.Equal(
            $"this._kind == Kind.{variantName}",
            invocation.ArgumentList.Arguments[0].Expression.ToString());
        Assert.Equal(
            $"$\"Accessing the `{variantName}` variant's fields is only allowed when `kind == Kind.{variantName}`. Actual kind: {{this._kind}}\"",
            invocation.ArgumentList.Arguments[1].Expression.ToString());
        Assert.Contains(
            getter.Body.Statements,
            statement => statement is ReturnStatementSyntax);
    }

    private static bool IsShortAttributeList(AttributeListSyntax attributes)
        => attributes.Attributes.Any(attribute =>
        {
            var name = attribute.Name.ToString().TrimStart('@');
            return name is "SerializeField" or "SerializeReference" or "NonSerialized" or
                "Preserve" or "Serializable" or "EditorBrowsable" or "EditorBrowsableState";
        });

    private static Compilation ReparseUnityCompilation(
        UnityGeneratorRun result,
        bool includeAssertions)
    {
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.CSharp9)
            .WithPreprocessorSymbols(includeAssertions ? new[] { "UNITY_ASSERTIONS" } : Array.Empty<string>());
        var trees = result.OutputCompilation.SyntaxTrees
            .Select(tree => CSharpSyntaxTree.ParseText(tree.GetText(), parseOptions, tree.FilePath));
        return CSharpCompilation.Create(
            "UnityAccessorAssertions",
            trees,
            UnityGeneratorTestHarness.GetReferences(
                includeUnityReferences: true,
                includeEditorReferences: false),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    private static void AssertNoCompilationErrors(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
    }

    private static StandaloneGenerated GenerateStandalone(
        string source,
        params string[] preprocessorSymbols)
    {
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Latest)
            .WithPreprocessorSymbols(preprocessorSymbols);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "accessor-assertions.cs");
        var references = TestHarnessEnvironment.FrameworkReferences
            .Append(MetadataReference.CreateFromFile(
                typeof(Houtamelo.Spire.DiscriminatedUnionAttribute).Assembly.Location));
        var compilation = CSharpCompilation.Create(
            "AccessorAssertions",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var generator = new DiscriminatedUnionGenerator();
        var driver = CSharpGeneratorDriver.Create(
                generators: new[] { generator.AsSourceGenerator() },
                parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out var diagnostics);
        var sourceText = GeneratorTestHelper.GetUnionGeneratedSource(driver.GetRunResult());
        Assert.NotNull(sourceText);
        return new StandaloneGenerated(outputCompilation, diagnostics, sourceText!, preprocessorSymbols);
    }

    private static string StandaloneSource() => """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct Command
        {
            [Variant]
            public static partial Command Idle();

            [Variant]
            public static partial Command Move(int distance);

            [Variant]
            public static partial Command Speak(string text);
        }
        """;

    private static string UnitySource() => """
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct Command
        {
            [Variant]
            public static partial Command Idle();

            [Variant]
            public static partial Command Move(
                int distance,
                [UnitySerializeReference] Effect? node);
        }

        [Serializable]
        public sealed class Effect { }
        """;

    private sealed record StandaloneGenerated(
        Compilation Compilation,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Diagnostics,
        string Source,
        string[] PreprocessorSymbols);

}
