using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.FieldAccess;

internal static class GuardGeneratedFlowHarness
{
    private const string Namespace = "Houtamelo.Spire.GeneratedGuardCorpus";

    public static async Task<BatchResult> AnalyzeAndExecuteAsync(
        string batchId,
        IReadOnlyList<GeneratedCase> cases,
        IEnumerable<int>? suppressedDiagnosticSites = null,
        bool reduceOnFailure = true)
    {
        var suppressedSites = (suppressedDiagnosticSites ?? Enumerable.Empty<int>())
            .ToHashSet();
        var sourceWithMarkup = BuildSource(batchId, cases);
        var (source, expected) = StripMarkup(sourceWithMarkup);
        expected = expected
            .Where(item => !suppressedSites.Contains(item.SiteId))
            .ToImmutableArray();

        GeneratorTestHelper.RunGenerator(
            source,
            out var compilation,
            out var generatorDiagnostics,
            path: batchId + ".cs");

        compilation = compilation.WithAssemblyName(
            "SpireGuard_" + new string(batchId.Select(character =>
                char.IsLetterOrDigit(character) ? character : '_').ToArray()));

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        using var image = new MemoryStream();
        var emit = compilation.Emit(image);
        Assert.True(
            emit.Success,
            "Generated guard corpus could not be emitted:\n" +
            string.Join("\n", emit.Diagnostics));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(
                new FieldAccessSafetyAnalyzer()))
            .GetAnalyzerDiagnosticsAsync(timeout.Token)
            .ConfigureAwait(false);

        var actual = diagnostics
            .Where(diagnostic => diagnostic.Id is "SPIRE013" or "SPIRE014")
            .Where(diagnostic => !suppressedSites.Contains(GetSiteId(source, diagnostic)))
            .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .ThenBy(diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ToArray();

        var unexpected = diagnostics
            .Where(diagnostic => diagnostic.Id is not "SPIRE013" and not "SPIRE014")
            .ToArray();
        Assert.True(
            unexpected.Length == 0,
            "Unexpected diagnostics:\n" +
            string.Join("\n", unexpected.Select(diagnostic => diagnostic.ToString())));

        AssertDiagnostics(expected, actual, source, cases);
        Assert.Equal(
            cases.Sum(testCase => testCase.ExpectedDiagnosticSiteIds
                .Count(siteId => !suppressedSites.Contains(siteId))),
            expected.Length);

        var diagnosticSites = actual
            .Select(diagnostic => GetSiteId(source, diagnostic))
            .ToHashSet();
        var expectedDiagnosticSites = cases
            .SelectMany(testCase => testCase.ExpectedDiagnosticSiteIds)
            .Where(siteId => !suppressedSites.Contains(siteId))
            .ToHashSet();
        Assert.True(
            expectedDiagnosticSites.All(diagnosticSites.Contains),
            "At least one expected unsafe diagnostic site was not reported. Expected: " +
            string.Join(",", expectedDiagnosticSites) + "; actual: " +
            string.Join(",", diagnosticSites));

        var assembly = TestHarnessEnvironment.LoadAssembly(image.ToArray());
        var probeType = assembly.GetType(Namespace + ".GeneratedProbe", throwOnError: true)!;
        var traceType = assembly.GetType(Namespace + ".GeneratedTrace", throwOnError: true)!;
        var reset = traceType.GetMethod("Reset", BindingFlags.Public | BindingFlags.Static)!;
        var reached = traceType.GetField("ReachedSites", BindingFlags.Public | BindingFlags.Static)!;
        var mismatches = traceType.GetField("MismatchSites", BindingFlags.Public | BindingFlags.Static)!;
        var actualKinds = traceType.GetField("ActualKinds", BindingFlags.Public | BindingFlags.Static)!;
        var observations = new List<RuntimeObservation>();

        foreach (var testCase in cases)
        {
            var method = probeType.GetMethod(
                testCase.MethodName,
                BindingFlags.Public | BindingFlags.Static)!;
            Assert.NotNull(method);

            var caseReached = new HashSet<int>();
            var caseMismatches = new HashSet<int>();
            foreach (var input in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 })
            {
                reset.Invoke(null, null);
                try
                {
                    _ = method.Invoke(null, new object[] { input });
                }
                catch (TargetInvocationException exception)
                {
                    throw new Xunit.Sdk.XunitException(
                        $"Generated case '{testCase.Name}' threw for input {input}: " +
                        exception.InnerException,
                        exception.InnerException);
                }

                var reachedValue = (int[])reached.GetValue(null)!;
                var mismatchValue = (int[])mismatches.GetValue(null)!;
                var actualKindValue = (int[])actualKinds.GetValue(null)!;
                var mismatchSites = testCase.SiteIds
                    .Where(siteId => mismatchValue[siteId] != 0)
                    .ToImmutableArray();
                foreach (var siteId in testCase.SiteIds)
                {
                    if (reachedValue[siteId] != 0)
                        caseReached.Add(siteId);
                    if (mismatchValue[siteId] != 0)
                        caseMismatches.Add(siteId);
                }
                observations.Add(new RuntimeObservation(
                    testCase.Name,
                    input,
                    reachedValue.Count(value => value != 0),
                    mismatchValue.Count(value => value != 0),
                    mismatchSites,
                    string.Join(
                        ";",
                        testCase.SiteIds.Select(siteId =>
                            reachedValue[siteId] + ":" +
                            mismatchValue[siteId] + ":" +
                            actualKindValue[siteId]))));
            }

            foreach (var siteId in testCase.SiteIds)
            {
                Assert.True(
                    caseReached.Contains(siteId),
                    $"Generated case '{testCase.Name}' did not reach site {siteId}.");
            }
            foreach (var unsafeSite in testCase.ExpectedUnsafeSiteIds)
            {
                if (caseMismatches.Contains(unsafeSite))
                    continue;
                ThrowOracleFailure(
                    batchId,
                    source,
                    testCase,
                    new ObservedFailure(
                        "ExpectedUnsafeSiteNotObserved",
                        unsafeSite,
                        -1),
                    suppressedSites,
                    reduceOnFailure);
            }

            foreach (var mismatchSite in caseMismatches)
            {
                if (diagnosticSites.Contains(mismatchSite))
                    continue;
                var mismatchObservation = observations
                    .FirstOrDefault(observation =>
                        observation.CaseName == testCase.Name &&
                        observation.MismatchSiteIds.Contains(mismatchSite));
                Assert.NotNull(mismatchObservation);
                var input = mismatchObservation!.Input;
                ThrowOracleFailure(
                    batchId,
                    source,
                    testCase,
                    new ObservedFailure("AcceptedUnsafe", mismatchSite, input),
                    suppressedSites,
                    reduceOnFailure);
            }
        }

        return new BatchResult(
            batchId,
            source,
            cases.Count,
            observations,
            actual.Length);
    }

    public static string BuildSource(
        string batchId,
        IReadOnlyList<GeneratedCase> cases)
    {
        _ = batchId;
        var methods = string.Join(
            Environment.NewLine + Environment.NewLine,
            cases.Select(testCase =>
                "        public static int " + testCase.MethodName + "(int input)\n" +
                "        {\n" +
                Indent(testCase.Body, 12) +
                "        }"));

        return """
            using System;
            using Houtamelo.Spire;

            namespace Houtamelo.Spire.GeneratedGuardCorpus;

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(int speed);
                [Variant] public static partial Command Stop();
                [Variant] public static partial Command Idle();
            }

            public static class GeneratedTrace
            {
                public static readonly int[] ReachedSites = new int[1024];
                public static readonly int[] MismatchSites = new int[1024];
                public static readonly int[] ActualKinds = new int[1024];

                public static void Reset()
                {
                    Array.Clear(ReachedSites, 0, ReachedSites.Length);
                    Array.Clear(MismatchSites, 0, MismatchSites.Length);
                    Array.Clear(ActualKinds, 0, ActualKinds.Length);
                }

            }

            public static class GeneratedProbe
            {
                private static Command Create(int input)
                    => (input % 3) switch
                    {
                        0 => Command.Move(17),
                        1 => Command.Stop(),
                        _ => Command.Idle(),
                    };

                private static void Observe(Command value)
                    => _ = value.kind;

            """ + methods + """
            }
            """;
    }

    public static string Reduce(string source, Func<string, bool> stillFails)
    {
        var lines = source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .ToList();
        var changed = true;
        while (changed)
        {
            changed = false;
            for (var index = 0; index < lines.Count; index++)
            {
                var candidate = lines.ToArray();
                var reduced = candidate.Take(index).Concat(candidate.Skip(index + 1));
                var candidateSource = string.Join("\n", reduced);
                if (!stillFails(candidateSource))
                    continue;
                lines.RemoveAt(index);
                changed = true;
                break;
            }
        }

        return string.Join("\n", lines);
    }

    private static void ThrowOracleFailure(
        string batchId,
        string fullSource,
        GeneratedCase testCase,
        ObservedFailure failure,
        IEnumerable<int> suppressedSites,
        bool reduceOnFailure)
    {
        ReductionReport? reduction = null;
        if (reduceOnFailure)
        {
            reduction = ReduceFailure(
                batchId,
                testCase.Name,
                fullSource,
                testCase.Body,
                new[] { failure.Input },
                failure.Category,
                failure.SiteId,
                failure.Input,
                candidate =>
                {
                    var replayCase = testCase.WithBody(candidate);
                    try
                    {
                        AnalyzeAndExecuteAsync(
                                batchId + "-replay",
                                new[] { replayCase },
                                suppressedSites,
                                reduceOnFailure: false)
                            .GetAwaiter()
                            .GetResult();
                        return null;
                    }
                    catch (GuardFlowOracleException exception)
                    {
                        return exception.Failure;
                    }
                    catch
                    {
                        return null;
                    }
                });
        }

        throw new GuardFlowOracleException(failure, reduction);
    }

    public static ReductionReport ReduceFailure(
        string batchId,
        string caseId,
        string originalSource,
        string reductionSource,
        IReadOnlyList<int> inputs,
        string failureCategory,
        int failureSiteId,
        int failureInput,
        Func<string, ObservedFailure?> stillFails)
    {
        var minimized = Reduce(
            reductionSource,
            candidate => MatchesFailure(
                stillFails(candidate),
                failureCategory,
                failureSiteId,
                failureInput));
        var replayed = stillFails(minimized);
        Assert.True(
            MatchesFailure(replayed, failureCategory, failureSiteId, failureInput),
            "The minimized generated failure did not replay the same category and site. Observed: " +
            (replayed is null
                ? "<none>"
                : replayed.Category + ":" + replayed.SiteId + ":" + replayed.Input) +
            ". Minimized source:\n" + minimized);
        return new ReductionReport(
            batchId,
            caseId,
            inputs.ToImmutableArray(),
            failureCategory,
            failureSiteId,
            originalSource,
            minimized,
            batchId + ":" + caseId + ":" + failureCategory + ":" + failureSiteId + ":" +
            string.Join(",", inputs));
    }

    private static bool MatchesFailure(
        ObservedFailure? failure,
        string category,
        int siteId,
        int input)
        => failure is not null &&
           failure.Category == category &&
           failure.SiteId == siteId &&
           failure.Input == input;

    private static string Indent(string source, int spaces)
    {
        var prefix = new string(' ', spaces);
        return string.Join(
            Environment.NewLine,
            source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(line => prefix + line)) + Environment.NewLine;
    }

    private static (string Source, ImmutableArray<ExpectedDiagnostic> Expected) StripMarkup(
        string sourceWithMarkup)
    {
        var clean = new System.Text.StringBuilder(sourceWithMarkup.Length);
        var expected = ImmutableArray.CreateBuilder<ExpectedDiagnostic>();
        var cursor = 0;
        while (cursor < sourceWithMarkup.Length)
        {
            var open = sourceWithMarkup.IndexOf("[|", cursor, StringComparison.Ordinal);
            if (open < 0)
            {
                clean.Append(sourceWithMarkup, cursor, sourceWithMarkup.Length - cursor);
                break;
            }

            clean.Append(sourceWithMarkup, cursor, open - cursor);
            var colon = sourceWithMarkup.IndexOf(':', open + 2);
            var close = colon < 0
                ? -1
                : sourceWithMarkup.IndexOf("|]", colon + 1, StringComparison.Ordinal);
            if (colon < 0 || close < 0)
                throw new InvalidOperationException("Malformed generated guard diagnostic markup.");

            var id = sourceWithMarkup.Substring(open + 2, colon - open - 2);
            var text = sourceWithMarkup.Substring(colon + 1, close - colon - 1);
            var markerStart = sourceWithMarkup.LastIndexOf("/* SITE:", open, StringComparison.Ordinal);
            var markerEnd = markerStart < 0
                ? -1
                : sourceWithMarkup.IndexOf(" */", markerStart, StringComparison.Ordinal);
            if (markerStart < 0 || markerEnd < 0)
                throw new InvalidOperationException(
                    "Generated guard diagnostic markup must have a preceding site marker.");
            var siteText = sourceWithMarkup.Substring(
                markerStart + "/* SITE:".Length,
                markerEnd - markerStart - "/* SITE:".Length);
            if (!int.TryParse(siteText, out var siteId))
                throw new InvalidOperationException("Generated guard site marker was not an integer.");
            var start = clean.Length;
            clean.Append(text);
            expected.Add(new ExpectedDiagnostic(
                id,
                id == "SPIRE013" ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
                start,
                text.Length,
                text,
                siteId));
            cursor = close + 2;
        }

        return (clean.ToString(), expected.ToImmutable());
    }

    private static void AssertDiagnostics(
        ImmutableArray<ExpectedDiagnostic> expected,
        IReadOnlyList<Microsoft.CodeAnalysis.Diagnostic> actual,
        string source,
        IReadOnlyList<GeneratedCase> cases)
    {
        var allSites = cases.SelectMany(testCase => testCase.SiteIds).ToHashSet();
        var actualBySite = actual.ToLookup(diagnostic => GetSiteId(source, diagnostic));
        Assert.True(
            actual.All(diagnostic => allSites.Contains(GetSiteId(source, diagnostic))),
            "A diagnostic was reported for an uninstrumented generated site. Actual: " +
            string.Join(",", actual.Select(diagnostic => GetSiteId(source, diagnostic))));
        foreach (var expectedDiagnostic in expected)
        {
            var matching = actualBySite[expectedDiagnostic.SiteId]
                .Where(diagnostic =>
                    diagnostic.Id == expectedDiagnostic.Id &&
                    diagnostic.Location.SourceSpan.Start == expectedDiagnostic.Start &&
                    diagnostic.Location.SourceSpan.Length == expectedDiagnostic.Length)
                .ToArray();
            Assert.Single(matching);
        }
        foreach (var group in actual.GroupBy(diagnostic =>
                     (SiteId: GetSiteId(source, diagnostic), diagnostic.Id)))
        {
            Assert.True(
                group.Count() == 1,
                "Duplicate generated diagnostic at site " + group.Key.SiteId + ":" + group.Key.Id +
                "\n" + string.Join("\n", group.Select(diagnostic =>
                    diagnostic.Location.SourceSpan.Start + ":" + diagnostic.Location.SourceSpan.Length)));
        }
    }

    private static int GetSiteId(
        string source,
        Microsoft.CodeAnalysis.Diagnostic diagnostic)
    {
        if (!diagnostic.Location.IsInSource)
            return -1;
        var start = diagnostic.Location.SourceSpan.Start;
        var markerStart = source.LastIndexOf("/* SITE:", start, StringComparison.Ordinal);
        var markerEnd = markerStart < 0
            ? -1
            : source.IndexOf(" */", markerStart, StringComparison.Ordinal);
        if (markerStart < 0 || markerEnd < 0 || markerEnd > start)
            return -1;
        var siteText = source.Substring(
            markerStart + "/* SITE:".Length,
            markerEnd - markerStart - "/* SITE:".Length);
        return int.TryParse(siteText, out var siteId) ? siteId : -1;
    }

    private static string SourceContext(string source, int start)
    {
        var contextStart = Math.Max(0, start - 80);
        var contextLength = Math.Min(source.Length - contextStart, 160);
        return source.Substring(contextStart, contextLength).Replace('\n', ' ');
    }

    public sealed class GeneratedCase
    {
        public string Name { get; }
        public string MethodName { get; }
        public string Body { get; }
        public ImmutableArray<int> SiteIds { get; }
        public ImmutableArray<int> ExpectedUnsafeSiteIds { get; }
        public ImmutableArray<int> ExpectedDiagnosticSiteIds { get; }

        public GeneratedCase(
            string name,
            string methodName,
            string body,
            params int[] siteIds)
        {
            Name = name;
            MethodName = methodName;
            Body = body;
            SiteIds = siteIds.ToImmutableArray();
            ExpectedUnsafeSiteIds = ImmutableArray<int>.Empty;
            ExpectedDiagnosticSiteIds = ImmutableArray<int>.Empty;
        }

        private GeneratedCase(
            string name,
            string methodName,
            string body,
            ImmutableArray<int> siteIds,
            ImmutableArray<int> expectedUnsafeSiteIds,
            ImmutableArray<int> expectedDiagnosticSiteIds)
        {
            Name = name;
            MethodName = methodName;
            Body = body;
            SiteIds = siteIds;
            ExpectedUnsafeSiteIds = expectedUnsafeSiteIds;
            ExpectedDiagnosticSiteIds = expectedDiagnosticSiteIds;
        }

        public GeneratedCase WithBody(string body)
            => new(
                Name,
                MethodName,
                body,
                SiteIds,
                ExpectedUnsafeSiteIds,
                ExpectedDiagnosticSiteIds);

        public GeneratedCase WithUnsafeSites(params int[] siteIds)
            => new(
                Name,
                MethodName,
                Body,
                SiteIds,
                siteIds.ToImmutableArray(),
                ExpectedDiagnosticSiteIds.IsDefaultOrEmpty
                    ? siteIds.ToImmutableArray()
                    : ExpectedDiagnosticSiteIds);

        public GeneratedCase WithDiagnosticSites(params int[] siteIds)
            => new(
                Name,
                MethodName,
                Body,
                SiteIds,
                ExpectedUnsafeSiteIds,
                siteIds.ToImmutableArray());
    }

    public sealed record RuntimeObservation(
        string CaseName,
        int Input,
        int Reached,
        int Mismatches,
        ImmutableArray<int> MismatchSiteIds,
        string Trace);

    public sealed record BatchResult(
        string BatchId,
        string Source,
        int CaseCount,
        IReadOnlyList<RuntimeObservation> Observations,
        int DiagnosticCount);

    public sealed record ReductionReport(
        string BatchId,
        string CaseId,
        ImmutableArray<int> Inputs,
        string FailureCategory,
        int FailureSiteId,
        string OriginalSource,
        string MinimizedSource,
        string ReplayKey);

    public sealed record ObservedFailure(
        string Category,
        int SiteId,
        int Input);

    public sealed class GuardFlowOracleException : Exception
    {
        public ObservedFailure Failure { get; }
        public ReductionReport? Reduction { get; }

        public GuardFlowOracleException(
            ObservedFailure failure,
            ReductionReport? reduction)
            : base(BuildMessage(failure, reduction))
        {
            Failure = failure;
            Reduction = reduction;
        }

        private static string BuildMessage(
            ObservedFailure failure,
            ReductionReport? reduction)
        {
            if (reduction is null)
            {
                return "Generated guard oracle failure: " +
                    failure.Category + " at site " + failure.SiteId +
                    " for input " + failure.Input;
            }

            return "Generated guard oracle failure: " + failure.Category +
                " at site " + failure.SiteId + " for input " + failure.Input +
                ". Batch=" + reduction.BatchId +
                ", case=" + reduction.CaseId +
                ", replay=" + reduction.ReplayKey +
                ". Original source:\n" + reduction.OriginalSource +
                "\nMinimized source:\n" + reduction.MinimizedSource;
        }
    }

    private readonly record struct ExpectedDiagnostic(
        string Id,
        DiagnosticSeverity Severity,
        int Start,
        int Length,
        string Text,
        int SiteId);
}
