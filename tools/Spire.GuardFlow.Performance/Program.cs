using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Houtamelo.Spire;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Spire.GuardFlow.Performance;

internal static class Program
{
    private const int WarmupRuns = 1;
    private const int MeasuredRuns = 3;
    private static readonly TimeSpan AnalysisBudget = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CancellationWaitBudget = TimeSpan.FromSeconds(30);

    public static async Task<int> Main(string[] args)
    {
        var options = Options.Parse(args);
        var workloads = WorkloadCatalog.Create(options.Quick);
        var reports = new List<WorkloadReport>(workloads.Count);

        foreach (var workload in workloads)
        {
            Console.WriteLine($"{workload.Name}: generating and measuring");
            var report = await MeasureWorkloadAsync(workload);
            reports.Add(report);
            Console.WriteLine(
                $"  analyzer median={Median(report.MeasuredRuns.Select(run => run.AnalyzerMilliseconds)):F2} ms " +
                $"diagnostics={report.Diagnostics.Length} hash={report.DiagnosticsHash[..12]}");
        }

        var cancellation = await MeasureCancellationAsync(
            workloads.OrderByDescending(workload => workload.Complexity).First());
        var result = new PerformanceReport(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Commit: Environment.GetEnvironmentVariable("GIT_COMMIT") ?? "unknown",
            Configuration: options.Configuration,
            Quick: options.Quick,
            Host: new HostMetadata(
                Runtime: RuntimeInformation.FrameworkDescription,
                OS: RuntimeInformation.OSDescription,
                Architecture: RuntimeInformation.ProcessArchitecture.ToString(),
                ProcessorCount: Environment.ProcessorCount,
                Roslyn: typeof(CSharpCompilation).Assembly.GetName().Version?.ToString() ?? "unknown",
                SdkVersion: SdkVersion(),
                AnalyzerAssemblyPath: typeof(FieldAccessSafetyAnalyzer).Assembly.Location,
                AnalyzerAssemblySha256: AssemblySha256(typeof(FieldAccessSafetyAnalyzer).Assembly.Location),
                WorktreeState: Environment.GetEnvironmentVariable("GIT_WORKTREE_STATE") ?? "unknown"),
            WarmupRuns,
            MeasuredRuns,
            AnalysisBudgetMilliseconds: AnalysisBudget.TotalMilliseconds,
            Workloads: reports,
            Cancellation: cancellation);

        var output = options.OutputPath ??
            Path.Combine("tmp", "guard-flow-performance.json");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllTextAsync(
            output,
            JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Wrote {output}");
        return 0;
    }

    private static async Task<WorkloadReport> MeasureWorkloadAsync(Workload workload)
    {
        var source = WorkloadSource.Create(workload);
        var baseline = await ExecuteAsync(source, CancellationToken.None);
        if (!baseline.Success)
            throw new InvalidOperationException($"Baseline workload failed: {workload.Name}: {baseline.Error}");
        var expectedDiagnostics = source.ResolveExpectedDiagnostics();
        ValidateDiagnostics(workload.Name, expectedDiagnostics, baseline.Diagnostics);

        for (var index = 0; index < WarmupRuns; index++)
        {
            var warmup = await ExecuteAsync(source, CancellationToken.None);
            if (!warmup.Success)
                throw new InvalidOperationException($"Warmup workload failed: {workload.Name}: {warmup.Error}");
        }

        var runs = new List<Measurement>(MeasuredRuns);
        for (var index = 0; index < MeasuredRuns; index++)
        {
            var run = await ExecuteAsync(source, CancellationToken.None);
            if (!run.Success)
                throw new InvalidOperationException($"Measured workload failed: {workload.Name}: {run.Error}");
            if (run.DiagnosticsHash != baseline.DiagnosticsHash)
            {
                throw new InvalidOperationException(
                    $"Diagnostic set changed between runs for {workload.Name}: " +
                    $"{baseline.DiagnosticsHash} != {run.DiagnosticsHash}");
            }
            runs.Add(run.Measurement!);
        }

        return new WorkloadReport(
            workload.Name,
            workload.Branches,
            workload.Receivers,
            workload.Aliases,
            workload.RecursiveFunctions,
            workload.IncludeAccessor,
            workload.Complexity,
            expectedDiagnostics,
            baseline.Diagnostics,
            baseline.DiagnosticsHash,
            runs);
    }

    private static async Task<CancellationReport> MeasureCancellationAsync(Workload workload)
    {
        var source = WorkloadSource.Create(workload with
        {
            Name = workload.Name + "-cancellation",
            Branches = Math.Max(workload.Branches, 1024),
            Receivers = Math.Max(workload.Receivers, 128),
            Aliases = Math.Max(workload.Aliases, 64),
            RecursiveFunctions = Math.Max(workload.RecursiveFunctions, 64),
            Complexity = workload.Complexity + 1000
        });

        var prepared = Prepare(source, CancellationToken.None);
        if (!prepared.Success)
            throw new InvalidOperationException($"Cancellation preparation failed: {prepared.Error}");

        var completed = await AnalyzeAsync(prepared.Value!, CancellationToken.None);
        if (!completed.Success)
            throw new InvalidOperationException($"Cancellation analyzer baseline failed: {completed.Error}");

        var preCancelledWatch = Stopwatch.StartNew();
        using var preCancelled = new CancellationTokenSource();
        preCancelled.Cancel();
        var preCancelledObserved = false;
        try
        {
            await AnalyzeAsync(prepared.Value!, preCancelled.Token);
        }
        catch (OperationCanceledException)
        {
            preCancelledObserved = true;
        }
        preCancelledWatch.Stop();
        if (!preCancelledObserved)
            throw new InvalidOperationException("The pre-cancelled analysis completed instead of observing cancellation.");

        using var timedCancellation = new CancellationTokenSource();
        var analyzerEntry = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var task = Task.Run(() => AnalyzeAsync(
            prepared.Value!,
            timedCancellation.Token,
            () => analyzerEntry.TrySetResult(true)));
        await analyzerEntry.Task.WaitAsync(CancellationWaitBudget);
        await Task.Delay(TimeSpan.FromMilliseconds(10));
        var requestTimestamp = Stopwatch.GetTimestamp();
        var requestedAtUtc = DateTimeOffset.UtcNow;
        timedCancellation.Cancel();
        var timedObserved = false;
        var returnedTimestamp = requestTimestamp;
        try
        {
            await task.WaitAsync(CancellationWaitBudget);
        }
        catch (OperationCanceledException)
        {
            timedObserved = true;
            returnedTimestamp = Stopwatch.GetTimestamp();
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(
                $"Analyzer did not observe cancellation within {CancellationWaitBudget.TotalSeconds:F0}s.");
        }
        if (!timedObserved)
            returnedTimestamp = Stopwatch.GetTimestamp();
        if (!timedCancellation.IsCancellationRequested || !timedObserved)
        {
            throw new InvalidOperationException(
                "The in-flight analyzer call completed without observing the requested cancellation.");
        }

        var cleanAfterCancellation = await AnalyzeAsync(prepared.Value!, CancellationToken.None);
        if (!cleanAfterCancellation.Success ||
            cleanAfterCancellation.DiagnosticsHash != completed.DiagnosticsHash)
        {
            throw new InvalidOperationException(
                "A cancelled analysis contaminated a later clean analysis.");
        }

        return new CancellationReport(
            source.Workload.Name,
            source.Workload.Complexity,
            BaselineAnalyzerMilliseconds: completed.Measurement!.AnalyzerMilliseconds,
            BaselineAnalyzerAllocatedBytes: completed.Measurement.AnalyzerAllocatedBytes,
            BaselineDiagnosticsHash: completed.DiagnosticsHash,
            PreCancelledObserved: preCancelledObserved,
            PreCancelledMilliseconds: preCancelledWatch.Elapsed.TotalMilliseconds,
            TimedCancellationObserved: timedObserved,
            CancellationRequestedAtUtc: requestedAtUtc,
            TimedCancellationReturnLatencyMilliseconds: Stopwatch.GetElapsedTime(
                requestTimestamp,
                returnedTimestamp).TotalMilliseconds,
            CleanRunAfterCancellation: true,
            CleanRunAnalyzerMilliseconds: cleanAfterCancellation.Measurement!.AnalyzerMilliseconds,
            CleanRunDiagnosticsHash: cleanAfterCancellation.DiagnosticsHash,
            CancellationRequestedAfterAnalyzerEntry: true,
            Note: "Cancellation is requested after the analyzer API entry signal; Roslyn does not identify which internal analyzer phase consumed the token.");
    }

    private static PreparedResult Prepare(
        WorkloadSource source,
        CancellationToken cancellationToken)
    {
        try
        {
            var generationWatch = Stopwatch.StartNew();
            var references = FrameworkReferences();
            var tree = CSharpSyntaxTree.ParseText(
                source.Source,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                path: source.Workload.Name + ".cs");
            var compilation = CSharpCompilation.Create(
                "SpireGuardFlowPerformance_" + source.Workload.Name,
                new[] { tree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(new DiscriminatedUnionGenerator());
            driver = driver.RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out var generatorDiagnostics,
                cancellationToken);
            generationWatch.Stop();
            cancellationToken.ThrowIfCancellationRequested();

            var generationErrors = generatorDiagnostics.Where(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
            var compilationErrors = outputCompilation.GetDiagnostics(cancellationToken)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToArray();
            if (generationErrors.Length > 0 || compilationErrors.Length > 0)
            {
                return PreparedResult.Failed(
                    "Generated compilation failed: " +
                    string.Join(" | ", generationErrors.Concat(compilationErrors).Select(d => d.ToString())));
            }

            var syntaxNodeCount = outputCompilation.SyntaxTrees
                .Sum(syntaxTree => syntaxTree.GetRoot(cancellationToken).DescendantNodesAndSelf().Count());
            var emitWatch = Stopwatch.StartNew();
            using var image = new MemoryStream();
            var emit = outputCompilation.Emit(image, cancellationToken: cancellationToken);
            emitWatch.Stop();
            if (!emit.Success)
                return PreparedResult.Failed(
                    "Emit failed: " + string.Join(" | ", emit.Diagnostics));

            return PreparedResult.Succeeded(new PreparedCompilation(
                source,
                outputCompilation,
                generationWatch.Elapsed.TotalMilliseconds,
                emitWatch.Elapsed.TotalMilliseconds,
                source.Source.Length,
                syntaxNodeCount));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return PreparedResult.Failed(exception.ToString());
        }
    }

    private static async Task<ExecutionResult> ExecuteAsync(
        WorkloadSource source,
        CancellationToken cancellationToken)
    {
        var prepared = Prepare(source, cancellationToken);
        if (!prepared.Success)
            return ExecutionResult.Failed(prepared.Error!);
        return await AnalyzeAsync(prepared.Value!, cancellationToken);
    }

    private static async Task<ExecutionResult> AnalyzeAsync(
        PreparedCompilation prepared,
        CancellationToken cancellationToken,
        Action? analyzerEntry = null)
    {
        try
        {
            using var analysisBudget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            analysisBudget.CancelAfter(AnalysisBudget);
            var analysisToken = analysisBudget.Token;
            var beforeAllocated = GC.GetTotalAllocatedBytes(true);
            var analyzerWatch = Stopwatch.StartNew();
            var withAnalyzers = prepared.Compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new FieldAccessSafetyAnalyzer()),
                new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty));
            analyzerEntry?.Invoke();
            var analyzerDiagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync(analysisToken);
            analysisToken.ThrowIfCancellationRequested();
            analyzerWatch.Stop();
            var afterAllocated = GC.GetTotalAllocatedBytes(true);
            cancellationToken.ThrowIfCancellationRequested();

            var unexpected = analyzerDiagnostics
                .Where(diagnostic => diagnostic.Id is not "SPIRE013" and not "SPIRE014")
                .ToArray();
            if (unexpected.Length > 0)
                return ExecutionResult.Failed(
                    "Unexpected analyzer diagnostics: " + string.Join(" | ", unexpected));

            var diagnostics = analyzerDiagnostics
                .Where(diagnostic => diagnostic.Id is "SPIRE013" or "SPIRE014")
                .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
                .ThenBy(diagnostic => diagnostic.Id, StringComparer.Ordinal)
                .Select(DiagnosticRecord.From)
                .ToArray();
            return ExecutionResult.Succeeded(
                diagnostics,
                new Measurement(
                    prepared.GenerationMilliseconds,
                    prepared.EmitMilliseconds,
                    analyzerWatch.Elapsed.TotalMilliseconds,
                    afterAllocated - beforeAllocated,
                    diagnostics.Length,
                    prepared.SourceCharacters,
                    prepared.SyntaxNodeCount));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Analyzer exceeded the {AnalysisBudget.TotalSeconds:F0}s per-workload budget.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ExecutionResult.Failed(exception.ToString());
        }
    }

    private static void ValidateDiagnostics(
        string workloadName,
        IReadOnlyList<ExpectedDiagnostic> expected,
        IReadOnlyList<DiagnosticRecord> actual)
    {
        if (expected.Count != actual.Count)
        {
            throw new InvalidOperationException(
                $"Diagnostic control count mismatch for {workloadName}: " +
                $"expected {expected.Count}, actual {actual.Count}. " +
                string.Join(" | ", actual.Select(diagnostic =>
                    $"{diagnostic.Id}@{diagnostic.Path}:{diagnostic.Start}:{diagnostic.Length}")));
        }

        for (var index = 0; index < expected.Count; index++)
        {
            var expectedDiagnostic = expected[index];
            var actualDiagnostic = actual[index];
            if (!string.Equals(expectedDiagnostic.Id, actualDiagnostic.Id, StringComparison.Ordinal) ||
                !string.Equals(expectedDiagnostic.Severity, actualDiagnostic.Severity, StringComparison.Ordinal) ||
                !string.Equals(expectedDiagnostic.Path, actualDiagnostic.Path, StringComparison.Ordinal) ||
                expectedDiagnostic.Start != actualDiagnostic.Start ||
                expectedDiagnostic.Length != actualDiagnostic.Length ||
                !string.Equals(expectedDiagnostic.Text, actualDiagnostic.Text, StringComparison.Ordinal) ||
                !actualDiagnostic.Message.Contains(expectedDiagnostic.MessageFragment, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Diagnostic control mismatch for {workloadName} at index {index}. " +
                    $"Expected {expectedDiagnostic}; actual {actualDiagnostic}.");
            }
        }
    }

    private static ImmutableArray<MetadataReference> FrameworkReferences()
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
            throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES is unavailable.");

        return trustedPlatformAssemblies
            .Split(Path.PathSeparator)
            .Where(path => path.Length > 0)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(DiscriminatedUnionAttribute).Assembly.Location))
            .Distinct(new MetadataReferenceComparer())
            .ToImmutableArray();
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(value => value).ToArray();
        return sorted[sorted.Length / 2];
    }

    private static string AssemblySha256(string path)
    {
        if (!File.Exists(path))
            return "unavailable";
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    }

    private static string SdkVersion()
    {
        var overrideValue = Environment.GetEnvironmentVariable("DOTNET_SDK_VERSION");
        if (!string.IsNullOrWhiteSpace(overrideValue))
            return overrideValue;
        return typeof(Program).Assembly
                   .GetCustomAttributes<AssemblyMetadataAttribute>()
                   .FirstOrDefault(attribute =>
                       string.Equals(attribute.Key, "SpireGuardFlowSdkVersion", StringComparison.Ordinal))
                   ?.Value ?? "unknown";
    }
}

internal sealed record Options(bool Quick, string Configuration, string? OutputPath)
{
    public static Options Parse(string[] args)
    {
        var quick = args.Any(argument => string.Equals(argument, "--quick", StringComparison.Ordinal));
        var outputIndex = Array.FindIndex(args, argument => argument == "--output");
        var output = outputIndex >= 0 && outputIndex + 1 < args.Length
            ? args[outputIndex + 1]
            : null;
        return new Options(
            quick,
            Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Release",
            output);
    }
}

internal sealed record Workload(
    string Name,
    int Branches,
    int Receivers,
    int Aliases,
    int RecursiveFunctions,
    bool IncludeAccessor,
    int Complexity,
    ExpectedDiagnosticSite[] ExpectedSites);

internal sealed record ExpectedDiagnosticSite(
    string Id,
    string Severity,
    string Marker,
    string Text,
    string MessageFragment);

internal sealed record ExpectedDiagnostic(
    string Id,
    string Severity,
    string Path,
    int Start,
    int Length,
    string Text,
    string MessageFragment);

internal static class WorkloadCatalog
{
    public static List<Workload> Create(bool quick)
    {
        var branches = quick ? new[] { 2, 16, 64 } : new[] { 2, 8, 32, 128, 256 };
        var receivers = quick ? new[] { 1, 4, 16 } : new[] { 1, 4, 16, 32, 64 };
        var aliases = quick ? new[] { 0, 4, 16 } : new[] { 0, 4, 16, 32 };
        var recursive = quick ? new[] { 1, 4, 12 } : new[] { 1, 4, 16, 32 };
        var workloads = new List<Workload>();
        workloads.AddRange(branches.Select(value => new Workload(
            "branches-" + value,
            value,
            1,
            0,
            0,
            true,
            value,
            Array.Empty<ExpectedDiagnosticSite>())));
        workloads.AddRange(receivers.Select(value => new Workload(
            "receivers-" + value,
            2,
            value,
            0,
            0,
            true,
            value * 2,
            Array.Empty<ExpectedDiagnosticSite>())));
        workloads.AddRange(aliases.Select(value => new Workload(
            "aliases-" + value,
            2,
            Math.Max(1, value),
            value,
            0,
            true,
            value * 3 + 1,
            Array.Empty<ExpectedDiagnosticSite>())));
        workloads.AddRange(recursive.Select(value => new Workload(
            "recursive-" + value,
            2,
            1,
            0,
            value,
            true,
            value * 4 + 2,
            Array.Empty<ExpectedDiagnosticSite>())));
        workloads.Add(new Workload(
            "irrelevant-128",
            128,
            1,
            0,
            0,
            false,
            1,
            Array.Empty<ExpectedDiagnosticSite>()));
        workloads.Add(new Workload(
            "diagnostic-controls",
            1,
            1,
            0,
            0,
            true,
            10,
            new[]
            {
                new ExpectedDiagnosticSite(
                    "SPIRE014",
                    "Warning",
                    "// guard-control: unknown-access",
                    "command.move",
                    "within this scope, the value of 'kind' is unknown."),
                new ExpectedDiagnosticSite(
                    "SPIRE013",
                    "Error",
                    "// guard-control: wrong-variant",
                    "command.move",
                    "within this scope, it is statically known that 'kind=Stop'.")
            }));
        return workloads;
    }
}

internal sealed record WorkloadSource(Workload Workload, string Source)
{
    public ExpectedDiagnostic[] ResolveExpectedDiagnostics()
    {
        return Workload.ExpectedSites
            .Select(site =>
            {
                var markerStart = Source.IndexOf(site.Marker, StringComparison.Ordinal);
                if (markerStart < 0)
                    throw new InvalidOperationException(
                        $"Expected diagnostic marker was not generated: {site.Marker}");
                var accessStart = Source.IndexOf(
                    site.Text,
                    markerStart + site.Marker.Length,
                    StringComparison.Ordinal);
                if (accessStart < 0)
                {
                    throw new InvalidOperationException(
                        $"Expected diagnostic text was not found after marker: {site.Marker}");
                }
                return new ExpectedDiagnostic(
                    site.Id,
                    site.Severity,
                    Workload.Name + ".cs",
                    accessStart,
                    site.Text.Length,
                    site.Text,
                    site.MessageFragment);
            })
            .ToArray();
    }

    public static WorkloadSource Create(Workload workload)
    {
        var builder = new StringBuilder();
        builder.AppendLine("using Houtamelo.Spire;");
        builder.AppendLine("[DiscriminatedUnion]");
        builder.AppendLine("public partial struct Command");
        builder.AppendLine("{");
        builder.AppendLine("    [Variant] public static partial Command Move(float speed);");
        builder.AppendLine("    [Variant] public static partial Command Stop();");
        builder.AppendLine("    [Variant] public static partial Command Idle();");
        builder.AppendLine("}");
        builder.AppendLine("public static class Workload");
        builder.AppendLine("{");
        builder.AppendLine("    public static float BranchFlow(Command command, bool ready)");
        builder.AppendLine("    {");
        builder.AppendLine("        var isMove = command.kind == Command.Kind.Move;");
        builder.AppendLine("        var result = 0f;");
        for (var index = 0; index < workload.Branches; index++)
        {
            builder.AppendLine("        if (ready)");
            builder.AppendLine($"            result += {index}f;");
            builder.AppendLine("        else");
            builder.AppendLine($"            result -= {index}f;");
        }
        builder.AppendLine("        if (isMove)");
        if (workload.IncludeAccessor)
            builder.AppendLine("            return result + command.move.speed;");
        else
            builder.AppendLine("            return result;");
        builder.AppendLine("        return result;");
        builder.AppendLine("    }");

        builder.AppendLine("    public static float Receivers(Command command)");
        builder.AppendLine("    {");
        builder.AppendLine("        var result = 0f;");
        for (var index = 0; index < Math.Max(1, workload.Receivers); index++)
        {
            builder.AppendLine($"        var receiver{index} = command;");
            builder.AppendLine($"        if (receiver{index}.kind == Command.Kind.Move)");
            if (workload.IncludeAccessor)
                builder.AppendLine($"            result += receiver{index}.move.speed + {index}f;");
            else
                builder.AppendLine("            result += 1f;");
        }
        builder.AppendLine("        return result;");
        builder.AppendLine("    }");

        if (workload.ExpectedSites.Length > 0)
        {
            builder.AppendLine("    public static float UnknownAccess(Command command)");
            builder.AppendLine("    {");
            builder.AppendLine("        // guard-control: unknown-access");
            builder.AppendLine("        return command.move.speed;");
            builder.AppendLine("    }");
            builder.AppendLine("    public static float WrongVariant(Command command)");
            builder.AppendLine("    {");
            builder.AppendLine("        if (command.kind == Command.Kind.Stop)");
            builder.AppendLine("            // guard-control: wrong-variant");
            builder.AppendLine("            return command.move.speed;");
            builder.AppendLine("        return 0f;");
            builder.AppendLine("    }");
        }

        builder.AppendLine("    public static float Aliases(Command command)");
        builder.AppendLine("    {");
        builder.AppendLine("        var result = 0f;");
        for (var index = 0; index < workload.Aliases; index++)
        {
            builder.AppendLine($"        ref Command alias{index} = ref command;");
            builder.AppendLine($"        if (alias{index}.kind == Command.Kind.Move)");
            if (workload.IncludeAccessor)
                builder.AppendLine($"            result += alias{index}.move.speed + {index}f;");
            else
                builder.AppendLine("            result += 1f;");
        }
        builder.AppendLine("        return result;");
        builder.AppendLine("    }");

        if (workload.RecursiveFunctions > 0)
        {
            builder.AppendLine("    public static float Recursive(Command command, int depth)");
            builder.AppendLine("    {");
            for (var index = 0; index < workload.RecursiveFunctions; index++)
            {
                var next = (index + 1) % workload.RecursiveFunctions;
                builder.AppendLine($"        float Function{index}(int current)");
                builder.AppendLine("        {");
                builder.AppendLine("            if (current <= 0)");
                builder.AppendLine("                return command.kind == Command.Kind.Move ? command.move.speed : 0f;");
                builder.AppendLine($"            return Function{next}(current - 1);");
                builder.AppendLine("        }");
            }
            builder.AppendLine("        return Function0(depth);");
            builder.AppendLine("    }");
        }
        builder.AppendLine("}");
        return new WorkloadSource(workload, builder.ToString());
    }
}

internal sealed record ExecutionResult(
    bool Success,
    string? Error,
    DiagnosticRecord[] Diagnostics,
    string DiagnosticsHash,
    Measurement? Measurement)
{
    public static ExecutionResult Failed(string error)
        => new(false, error, Array.Empty<DiagnosticRecord>(), string.Empty, null);

    public static ExecutionResult Succeeded(
        DiagnosticRecord[] diagnostics,
        Measurement measurement)
        => new(true, null, diagnostics, DiagnosticRecord.Hash(diagnostics), measurement);
}

internal sealed record PreparedCompilation(
    WorkloadSource Source,
    Compilation Compilation,
    double GenerationMilliseconds,
    double EmitMilliseconds,
    int SourceCharacters,
    int SyntaxNodeCount);

internal sealed record PreparedResult(
    bool Success,
    string? Error,
    PreparedCompilation? Value)
{
    public static PreparedResult Failed(string error)
        => new(false, error, null);

    public static PreparedResult Succeeded(PreparedCompilation value)
        => new(true, null, value);
}

internal sealed record Measurement(
    double GenerationMilliseconds,
    double EmitMilliseconds,
    double AnalyzerMilliseconds,
    long AnalyzerAllocatedBytes,
    int DiagnosticCount,
    int SourceCharacters,
    int SyntaxNodeCount);

internal sealed record WorkloadReport(
    string Name,
    int Branches,
    int Receivers,
    int Aliases,
    int RecursiveFunctions,
    bool IncludeAccessor,
    int Complexity,
    ExpectedDiagnostic[] ExpectedDiagnostics,
    DiagnosticRecord[] Diagnostics,
    string DiagnosticsHash,
    List<Measurement> MeasuredRuns);

internal sealed record CancellationReport(
    string Workload,
    int Complexity,
    double BaselineAnalyzerMilliseconds,
    long BaselineAnalyzerAllocatedBytes,
    string BaselineDiagnosticsHash,
    bool PreCancelledObserved,
    double PreCancelledMilliseconds,
    bool TimedCancellationObserved,
    DateTimeOffset CancellationRequestedAtUtc,
    double TimedCancellationReturnLatencyMilliseconds,
    bool CleanRunAfterCancellation,
    double CleanRunAnalyzerMilliseconds,
    string CleanRunDiagnosticsHash,
    bool CancellationRequestedAfterAnalyzerEntry,
    string Note);

internal sealed record PerformanceReport(
    int SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string Commit,
    string Configuration,
    bool Quick,
    HostMetadata Host,
    int WarmupRuns,
    int MeasuredRuns,
    double AnalysisBudgetMilliseconds,
    List<WorkloadReport> Workloads,
    CancellationReport Cancellation);

internal sealed record HostMetadata(
    string Runtime,
    string OS,
    string Architecture,
    int ProcessorCount,
    string Roslyn,
    string SdkVersion,
    string AnalyzerAssemblyPath,
    string AnalyzerAssemblySha256,
    string WorktreeState);

internal sealed record DiagnosticRecord(
    string Id,
    string Severity,
    string Path,
    int Start,
    int Length,
    string Text,
    string Message)
{
    public static DiagnosticRecord From(Diagnostic diagnostic)
    {
        var span = diagnostic.Location.SourceSpan;
        return new DiagnosticRecord(
            diagnostic.Id,
            diagnostic.Severity.ToString(),
            diagnostic.Location.SourceTree?.FilePath ?? string.Empty,
            span.Start,
            span.Length,
            diagnostic.Location.SourceTree?.GetText().ToString(span) ?? string.Empty,
            diagnostic.GetMessage());
    }

    public static string Hash(IEnumerable<DiagnosticRecord> diagnostics)
    {
        var canonical = string.Join(
            "\n",
            diagnostics.Select(diagnostic => string.Join(
                "|",
                diagnostic.Id,
                diagnostic.Severity,
                diagnostic.Path,
                diagnostic.Start,
                diagnostic.Length,
                diagnostic.Text,
                diagnostic.Message)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

internal sealed class MetadataReferenceComparer : IEqualityComparer<MetadataReference>
{
    public bool Equals(MetadataReference? left, MetadataReference? right)
        => string.Equals(left?.Display, right?.Display, StringComparison.OrdinalIgnoreCase);

    public int GetHashCode(MetadataReference reference)
        => StringComparer.OrdinalIgnoreCase.GetHashCode(reference.Display ?? string.Empty);
}
