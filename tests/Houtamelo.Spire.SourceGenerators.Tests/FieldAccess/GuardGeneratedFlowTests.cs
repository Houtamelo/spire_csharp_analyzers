using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.FieldAccess;

public sealed class GuardGeneratedFlowTests
{
    [Fact]
    public async Task GeneratedBooleanCorpus_ExercisesAndOrNegationAndSavedSnapshots()
    {
        var cases = new[]
        {
            new GuardGeneratedFlowHarness.GeneratedCase(
                "safe-and",
                "SafeAnd",
                """
                var command = Create(input);
                var ready = (input / 3) == 0;
                if (command.IsMove && ready)
                {
                    GeneratedTrace.ReachedSites[1] = 1;
                    GeneratedTrace.ActualKinds[1] = (int)command.kind;
                    if (GeneratedTrace.ActualKinds[1] != (int)Command.Kind.Move)
                        GeneratedTrace.MismatchSites[1] = 1;
                    /* SITE:1 */ return command.move.speed;
                }
                return 0;
                """,
                1),
            new GuardGeneratedFlowHarness.GeneratedCase(
                "unsafe-or",
                "UnsafeOr",
                """
                var command = Create(input);
                if (command.IsMove || input == 1)
                {
                    GeneratedTrace.ReachedSites[2] = 1;
                    GeneratedTrace.ActualKinds[2] = (int)command.kind;
                    if (GeneratedTrace.ActualKinds[2] != (int)Command.Kind.Move)
                        GeneratedTrace.MismatchSites[2] = 1;
                    /* SITE:2 */ return [|SPIRE014:command.move|].speed;
                }
                return 0;
                """,
                2).WithUnsafeSites(2),
            new GuardGeneratedFlowHarness.GeneratedCase(
                "unsafe-saved-snapshot",
                "UnsafeSavedSnapshot",
                """
                var command = Create(input);
                var move = command.IsMove;
                command = Command.Stop();
                if (move)
                {
                    GeneratedTrace.ReachedSites[3] = 1;
                    GeneratedTrace.ActualKinds[3] = (int)command.kind;
                    if (GeneratedTrace.ActualKinds[3] != (int)Command.Kind.Move)
                        GeneratedTrace.MismatchSites[3] = 1;
                    /* SITE:3 */ return [|SPIRE014:command.move|].speed;
                }
                return 0;
                """,
                3).WithUnsafeSites(3),
            new GuardGeneratedFlowHarness.GeneratedCase(
                "safe-demorgan",
                "SafeDeMorgan",
                """
                var command = Create(input);
                var ready = (input / 3) == 0;
                if (!(!command.IsMove || !ready))
                {
                    GeneratedTrace.ReachedSites[4] = 1;
                    GeneratedTrace.ActualKinds[4] = (int)command.kind;
                    if (GeneratedTrace.ActualKinds[4] != (int)Command.Kind.Move)
                        GeneratedTrace.MismatchSites[4] = 1;
                    /* SITE:4 */ return command.move.speed;
                }
                return 0;
                """,
                4),
        };

        var result = await GuardGeneratedFlowHarness.AnalyzeAndExecuteAsync("boolean-corpus", cases);
        Assert.Equal(cases.Length, result.CaseCount);
        Assert.Equal(2, result.DiagnosticCount);
        Assert.Equal(48, result.Observations.Count);
    }

    [Fact]
    public async Task GeneratedControlFlowCorpus_ExercisesJoinsLoopsAndAliases()
    {
        var cases = new[]
        {
            new GuardGeneratedFlowHarness.GeneratedCase(
                "unsafe-join",
                "UnsafeJoin",
                """
                var command = Create(input);
                if (command.IsMove)
                    command = Command.Stop();
                else if (!command.IsStop)
                    command = Command.Move(23);
                if (command.IsStop)
                {
                    GeneratedTrace.ReachedSites[5] = 1;
                    GeneratedTrace.ActualKinds[5] = (int)command.kind;
                    if (GeneratedTrace.ActualKinds[5] != (int)Command.Kind.Move)
                        GeneratedTrace.MismatchSites[5] = 1;
                    /* SITE:5 */ return [|SPIRE013:command.move|].speed;
                }
                return 0;
                """,
                5).WithUnsafeSites(5),
            new GuardGeneratedFlowHarness.GeneratedCase(
                "safe-loop-recheck",
                "SafeLoopRecheck",
                """
                var command = Create(input);
                var total = 0;
                for (var index = 0; index < 3; index++)
                {
                    command = Create(index % 3);
                    if (!command.IsMove)
                        continue;
                    GeneratedTrace.ReachedSites[6] = 1;
                    GeneratedTrace.ActualKinds[6] = (int)command.kind;
                    if (GeneratedTrace.ActualKinds[6] != (int)Command.Kind.Move)
                        GeneratedTrace.MismatchSites[6] = 1;
                    /* SITE:6 */ total += command.move.speed;
                }
                return total;
                """,
                6),
            new GuardGeneratedFlowHarness.GeneratedCase(
                "safe-direct-alias",
                "SafeDirectAlias",
                """
                var command = Create(input);
                ref var alias = ref command;
                if (alias.IsMove)
                {
                    GeneratedTrace.ReachedSites[7] = 1;
                    GeneratedTrace.ActualKinds[7] = (int)alias.kind;
                    if (GeneratedTrace.ActualKinds[7] != (int)Command.Kind.Move)
                        GeneratedTrace.MismatchSites[7] = 1;
                    /* SITE:7 */ return alias.move.speed;
                }
                return 0;
                """,
                7),
            new GuardGeneratedFlowHarness.GeneratedCase(
                "unsafe-alias-write",
                "UnsafeAliasWrite",
                """
                var command = Create(input);
                ref var alias = ref command;
                if (command.IsMove)
                {
                    alias = Command.Stop();
                    GeneratedTrace.ReachedSites[8] = 1;
                    GeneratedTrace.ActualKinds[8] = (int)command.kind;
                    if (GeneratedTrace.ActualKinds[8] != (int)Command.Kind.Move)
                        GeneratedTrace.MismatchSites[8] = 1;
                    /* SITE:8 */ return [|SPIRE014:command.move|].speed;
                }
                return 0;
                """,
                8).WithUnsafeSites(8),
        };

        var result = await GuardGeneratedFlowHarness.AnalyzeAndExecuteAsync("control-flow-corpus", cases);
        Assert.Equal(cases.Length, result.CaseCount);
        Assert.Equal(2, result.DiagnosticCount);
        Assert.Equal(48, result.Observations.Count);
    }

    [Fact]
    public async Task GeneratedEffectCorpus_ExercisesCallbackAndStableByValueEffects()
    {
        var cases = new[]
        {
            new GuardGeneratedFlowHarness.GeneratedCase(
                "unsafe-callback",
                "UnsafeCallback",
                """
                var command = Create(input);
                Action mutate = () => command = Command.Stop();
                if (command.IsMove)
                {
                    mutate();
                    GeneratedTrace.ReachedSites[9] = 1;
                    GeneratedTrace.ActualKinds[9] = (int)command.kind;
                    if (GeneratedTrace.ActualKinds[9] != (int)Command.Kind.Move)
                        GeneratedTrace.MismatchSites[9] = 1;
                    /* SITE:9 */ return [|SPIRE014:command.move|].speed;
                }
                return 0;
                """,
                9).WithUnsafeSites(9),
            new GuardGeneratedFlowHarness.GeneratedCase(
                "safe-by-value-call",
                "SafeByValueCall",
                """
                var command = Create(input);
                if (!command.IsMove)
                    return 0;
                Observe(command);
                GeneratedTrace.ReachedSites[10] = 1;
                GeneratedTrace.ActualKinds[10] = (int)command.kind;
                if (GeneratedTrace.ActualKinds[10] != (int)Command.Kind.Move)
                    GeneratedTrace.MismatchSites[10] = 1;
                /* SITE:10 */ return command.move.speed;
                """,
                10),
            new GuardGeneratedFlowHarness.GeneratedCase(
                "safe-multiple-receivers",
                "SafeMultipleReceivers",
                """
                var first = Create(input);
                var second = Create(0);
                if (first.IsMove && second.IsMove)
                {
                    GeneratedTrace.ReachedSites[11] = 1;
                    GeneratedTrace.ActualKinds[11] = (int)first.kind;
                    if (GeneratedTrace.ActualKinds[11] != (int)Command.Kind.Move)
                        GeneratedTrace.MismatchSites[11] = 1;
                    GeneratedTrace.ReachedSites[12] = 1;
                    GeneratedTrace.ActualKinds[12] = (int)second.kind;
                    if (GeneratedTrace.ActualKinds[12] != (int)Command.Kind.Move)
                        GeneratedTrace.MismatchSites[12] = 1;
                    var total = /* SITE:11 */ first.move.speed;
                    /* SITE:12 */ total += second.move.speed;
                    return total;
                }
                return 0;
                """,
                11,
                12),
        };

        var result = await GuardGeneratedFlowHarness.AnalyzeAndExecuteAsync("effects-corpus", cases);
        Assert.Equal(cases.Length, result.CaseCount);
        Assert.Equal(1, result.DiagnosticCount);
        Assert.Equal(36, result.Observations.Count);
    }

    [Fact]
    public async Task GeneratedMetamorphicCorpus_IsDeterministicAndPreservesRenaming()
    {
        var first = BuildMetamorphicCases("command", "ready");
        var second = BuildMetamorphicCases("value", "enabled");
        var firstSource = GuardGeneratedFlowHarness.BuildSource("metamorphic", first);
        var secondSource = GuardGeneratedFlowHarness.BuildSource("metamorphic", second);
        Assert.NotEqual(firstSource, secondSource);

        var firstResult = await GuardGeneratedFlowHarness.AnalyzeAndExecuteAsync("metamorphic-a", first);
        var secondResult = await GuardGeneratedFlowHarness.AnalyzeAndExecuteAsync("metamorphic-b", second);
        Assert.Equal(firstResult.DiagnosticCount, secondResult.DiagnosticCount);
        Assert.Equal(
            firstResult.Observations.Select(observation => observation.Trace),
            secondResult.Observations.Select(observation => observation.Trace));
        Assert.Equal(24, firstResult.Observations.Count);
    }

    [Fact]
    public async Task GeneratedRecipeCorpus_CrossesGuardReceiverAndMutationDimensions()
    {
        var cases = BuildRecipeCases();
        Assert.Equal(512, cases.Length);
        var diagnosticCount = 0;
        var observationCount = 0;
        for (var offset = 0; offset < cases.Length; offset += 64)
        {
            var batch = cases.Skip(offset).Take(64).ToArray();
            var result = await GuardGeneratedFlowHarness.AnalyzeAndExecuteAsync(
                "recipe-corpus-" + offset,
                batch);
            Assert.Equal(64, result.CaseCount);
            diagnosticCount += result.DiagnosticCount;
            observationCount += result.Observations.Count;
        }

        // Unsafe sites require diagnostics; conservative warnings on bounded-safe
        // loop/control recipes remain valid and are counted in the result.
        Assert.True(diagnosticCount >= 256, "Every generated unsafe site must be diagnosed.");
        Assert.Equal(6144, observationCount);
    }

    [Fact]
    public async Task HarnessSelfCheck_RejectsAnInjectedMissingDiagnosticAtOneSite()
    {
        var testCase = CreateMixedSiteCase();

        var exception = await Assert.ThrowsAsync<GuardGeneratedFlowHarness.GuardFlowOracleException>(() =>
            GuardGeneratedFlowHarness.AnalyzeAndExecuteAsync(
                "mixed-site-injected-fault",
                new[] { testCase },
                suppressedDiagnosticSites: new[] { 29 }));
        Assert.Contains("site 29", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(exception.Reduction);
        Assert.InRange(exception.Failure.Input, 0, 11);
        Assert.Contains(
            ":" + exception.Failure.Input,
            exception.Reduction!.ReplayKey,
            StringComparison.Ordinal);
        Assert.Contains("mixed-site-injected-fault", exception.Message, StringComparison.Ordinal);
        Assert.Contains("MixedSite", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Original source", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Minimized source", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerPipeline_DoesNotCaptureNonPumpingSynchronizationContext()
    {
        var context = new NonPumpingSynchronizationContext();
        var completed = new ManualResetEventSlim(false);
        Exception? failure = null;
        var worker = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                GuardGeneratedFlowHarness.AnalyzeAndExecuteAsync(
                        "sync-context-regression",
                        new[] { CreateMixedSiteCase() },
                        suppressedDiagnosticSites: new[] { 29 })
                    .GetAwaiter()
                    .GetResult();
                failure = new InvalidOperationException(
                    "The injected accepted-unsafe failure unexpectedly completed without an oracle exception.");
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
                completed.Set();
            }
        })
        {
            IsBackground = true,
            Name = "Spire guard-flow synchronization-context regression",
        };
        worker.Start();

        var completedBeforeCleanup = completed.Wait(TimeSpan.FromSeconds(10));
        if (!completedBeforeCleanup)
        {
            // A pre-fix implementation queues the await continuation here.
            // Cleanup must not run on the Unity/test thread: reducer replay can
            // synchronously wait for further continuations.
            var cleanupCompleted = new ManualResetEventSlim(false);
            var cleanup = new Thread(() =>
            {
                SynchronizationContext.SetSynchronizationContext(null);
                try
                {
                    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
                    while (!completed.IsSet && DateTime.UtcNow < deadline)
                    {
                        context.PumpAll();
                        Thread.Sleep(10);
                    }
                    context.PumpAll();
                }
                finally
                {
                    cleanupCompleted.Set();
                }
            })
            {
                IsBackground = true,
                Name = "Spire guard-flow synchronization-context cleanup",
            };
            cleanup.Start();
            Assert.True(
                cleanupCompleted.Wait(TimeSpan.FromSeconds(12)),
                "Synchronization-context cleanup exceeded its watchdog.");
            Assert.True(
                completed.Wait(TimeSpan.FromSeconds(2)),
                "Reducer worker did not exit after bounded synchronization-context cleanup.");
            Assert.True(cleanup.Join(TimeSpan.FromSeconds(2)), "Synchronization-context cleanup did not join.");
            cleanupCompleted.Dispose();
        }

        Assert.True(worker.Join(TimeSpan.FromSeconds(2)), "Reducer worker did not join after completion.");
        Assert.True(
            completedBeforeCleanup,
            "Reducer pipeline captured a non-pumping synchronization context before cleanup.");
        completed.Dispose();

        var oracleFailure = Assert.IsType<GuardGeneratedFlowHarness.GuardFlowOracleException>(failure);
        Assert.Equal("AcceptedUnsafe", oracleFailure.Failure.Category);
        Assert.Equal(29, oracleFailure.Failure.SiteId);
        Assert.InRange(oracleFailure.Failure.Input, 0, 11);
        Assert.NotNull(oracleFailure.Reduction);
        Assert.Contains(
            ":" + oracleFailure.Failure.Input,
            oracleFailure.Reduction!.ReplayKey,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Reducer_MinimizesAnInjectedOracleFailure()
    {
        const string source = "keep\nnoise-a\nnoise-b\nbad\nnoise-c";
        var reduced = GuardGeneratedFlowHarness.Reduce(
            source,
            candidate => candidate.Contains("bad", StringComparison.Ordinal));
        Assert.Equal("bad", reduced);
    }

    [Fact]
    public async Task Reducer_ReplaysCompilerAnalyzerAndRuntimeFailure()
    {
        const string recipe = "var noiseA = 1;\ncommand = Command.Stop();\nvar noiseB = 2;";
        var report = GuardGeneratedFlowHarness.ReduceFailure(
            "reducer-replay",
            "ReducerReplay",
            recipe,
            recipe,
            new[] { 0 },
            "AcceptedUnsafe",
            27,
            0,
            candidate =>
            {
                var body = $$"""
                    var command = Create(input);
                    var move = command.IsMove;
                    {{candidate}}
                    if (move)
                    {
                        GeneratedTrace.ReachedSites[27] = 1;
                        GeneratedTrace.ActualKinds[27] = (int)command.kind;
                        if (GeneratedTrace.ActualKinds[27] != (int)Command.Kind.Move)
                            GeneratedTrace.MismatchSites[27] = 1;
                        /* SITE:27 */ return [|SPIRE014:command.move|].speed;
                    }
                    return 0;
                    """;
                try
                {
                    _ = GuardGeneratedFlowHarness.AnalyzeAndExecuteAsync(
                            "reducer-replay",
                            new[]
                            {
                                new GuardGeneratedFlowHarness.GeneratedCase(
                                    "reducer",
                                    "ReducerReplay",
                                    body,
                                    27)
                                    .WithUnsafeSites(27),
                            },
                            suppressedDiagnosticSites: new[] { 27 },
                            reduceOnFailure: false)
                        .GetAwaiter()
                        .GetResult();
                    return null;
                }
                catch (GuardGeneratedFlowHarness.GuardFlowOracleException exception)
                {
                    return exception.Failure;
                }
                catch
                {
                    return null;
                }
            });

        Assert.Equal("command = Command.Stop();", report.MinimizedSource);
        Assert.Equal(recipe, report.OriginalSource);
        Assert.Equal(new[] { 0 }, report.Inputs);
        Assert.Equal("AcceptedUnsafe", report.FailureCategory);
        Assert.Equal(27, report.FailureSiteId);
        Assert.Equal(
            "reducer-replay:ReducerReplay:AcceptedUnsafe:27:0",
            report.ReplayKey);
    }

    private static GuardGeneratedFlowHarness.GeneratedCase[] BuildMetamorphicCases(
        string receiver,
        string readiness)
        => new[]
        {
            new GuardGeneratedFlowHarness.GeneratedCase(
                "direct",
                "MetamorphicDirect",
                $$"""
                var {{receiver}} = Create(input);
                var {{readiness}} = (((input / 3) & 1) == 0);
                if ({{receiver}}.IsMove && {{readiness}})
                {
                    {{TraceStatements(13, receiver)}}
                    /* SITE:13 */ return {{receiver}}.move.speed;
                }
                return 0;
                """,
                13),
            new GuardGeneratedFlowHarness.GeneratedCase(
                "rewritten",
                "MetamorphicRewritten",
                $$"""
                var {{receiver}} = Create(input);
                var {{readiness}} = (((input / 3) & 1) == 0);
                if (!(!{{receiver}}.IsMove || !{{readiness}}))
                {
                    {{TraceStatements(14, receiver)}}
                    /* SITE:14 */ return {{receiver}}.move.speed;
                }
                return 0;
                """,
                14),
        };

    private static GuardGeneratedFlowHarness.GeneratedCase CreateMixedSiteCase()
        => new GuardGeneratedFlowHarness.GeneratedCase(
                "mixed-site",
                "MixedSite",
                """
                var first = Create(input);
                var second = Create(input);
                var result = 0;
                if (first.IsMove)
                {
                    first = Command.Stop();
                    GeneratedTrace.ReachedSites[28] = 1;
                    GeneratedTrace.ActualKinds[28] = (int)first.kind;
                    if (GeneratedTrace.ActualKinds[28] != (int)Command.Kind.Move)
                        GeneratedTrace.MismatchSites[28] = 1;
                    /* SITE:28 */ result += [|SPIRE014:first.move|].speed;
                }
                if (second.IsMove)
                {
                    second = Command.Stop();
                    GeneratedTrace.ReachedSites[29] = 1;
                    GeneratedTrace.ActualKinds[29] = (int)second.kind;
                    if (GeneratedTrace.ActualKinds[29] != (int)Command.Kind.Move)
                        GeneratedTrace.MismatchSites[29] = 1;
                    /* SITE:29 */ result += second.move.speed;
                }
                return result;
                """,
                28,
                29)
            .WithUnsafeSites(28, 29)
            .WithDiagnosticSites(28);


    private static GuardGeneratedFlowHarness.GeneratedCase[] BuildRecipeCases()
    {
        var guardForms = new (string Guard, string Base)[]
        {
            ("BASE && ready", "RECEIVER.IsMove"),
            ("(BASE == true) && ready", "RECEIVER.IsMove"),
            ("BASE && ready", "RECEIVER.kind == Command.Kind.Move"),
            ("BASE && ready", "RECEIVER.kind != Command.Kind.Stop && RECEIVER.kind != Command.Kind.Idle"),
        };
        var readyForms = new[]
        {
            "(((input / 3) & 1) == 0)",
            "((((input / 3) & 1) == 0) != false)",
        };
        var controls = new[] { "direct", "nested", "loop", "early" };
        var effects = new[] { "none", "direct", "callback", "fresh" };
        var cases = new GuardGeneratedFlowHarness.GeneratedCase[512];
        var index = 0;
        foreach (var readyForm in readyForms)
        {
            foreach (var useAlias in new[] { false, true })
            {
                foreach (var saved in new[] { false, true })
                {
                    foreach (var guard in guardForms)
                    {
                        foreach (var control in controls)
                        {
                            foreach (var effect in effects)
                            {
                                var receiver = useAlias ? "alias" : "command";
                                var setup = useAlias
                                    ? "ref var alias = ref command;\n"
                                    : string.Empty;
                                var basePredicate = guard.Base.Replace(
                                    "RECEIVER",
                                    receiver,
                                    StringComparison.Ordinal);
                                var condition = guard.Guard.Replace(
                                    "BASE",
                                    saved ? "saved" : basePredicate,
                                    StringComparison.Ordinal);

                                var preamble = saved
                                    ? "var saved = " + basePredicate + ";\n"
                                    : string.Empty;
                                var effectPrefix = effect == "fresh"
                                    ? "command = Command.Stop();\ncommand = Command.Move(31);\n" +
                                      (saved ? "saved = " + basePredicate + ";\n" : string.Empty)
                                    : string.Empty;
                                var effectBody = effect switch
                                {
                                    "none" => "",
                                    "direct" => receiver + " = Command.Stop();\n",
                                    "callback" => "Action mutate = () => command = Command.Stop();\nmutate();\n",
                                    "fresh" => "",
                                    _ => throw new InvalidOperationException(effect),
                                };
                                var unsafeEffect = effect is "direct" or "callback";
                                var site = 30 + index;
                                var payload = unsafeEffect
                                    ? $"/* SITE:{site} */ return [|SPIRE014:{receiver}.move|].speed;"
                                    : $"/* SITE:{site} */ return {receiver}.move.speed;";
                                var read = TraceStatements(site, receiver) +
                                    Environment.NewLine + payload;
                                var loopRead = TraceStatements(site, receiver) +
                                    Environment.NewLine +
                                    (unsafeEffect
                                        ? $"/* SITE:{site} */ total += [|SPIRE014:{receiver}.move|].speed;"
                                        : $"/* SITE:{site} */ total += {receiver}.move.speed;");
                                var loopBody = effectBody + loopRead +
                                    Environment.NewLine +
                                    (saved ? "saved = " + basePredicate + ";\n" : string.Empty) +
                                    "command = iteration == 0 ? Command.Stop() : Command.Move(31);";
                                var guardedBody = Indent(effectBody + read, 4);
                                var controlBody = control switch
                                {
                                    "direct" => $$"""
                                        if ({{condition}})
                                        {
                                        {{guardedBody}}
                                        }
                                        """,
                                    "nested" => $$"""
                                        if ({{condition}})
                                        {
                                            if (alternate)
                                            {
                                        {{Indent(effectBody + read, 8)}}
                                            }
                                        }
                                        """,
                                    "loop" => $$"""
                                        var total = 0;
                                        for (var iteration = 0; iteration < 2; iteration++)
                                        {
                                            if ({{condition}})
                                            {
                                        {{Indent(loopBody, 8)}}
                                            }
                                        }
                                        return total;
                                        """,
                                    "early" => $$"""
                                        if (!({{condition}}))
                                            return 0;
                                        {{effectBody}}{{read}}
                                        """,
                                    _ => throw new InvalidOperationException(control),
                                };
                                cases[index] = new GuardGeneratedFlowHarness.GeneratedCase(
                                    "recipe-" + index,
                                    "Recipe" + index,
                                    $$"""
                                    var command = Create(input);
                                    var ready = {{readyForm}};
                                    var alternate = (((input / 3) & 2) == 0);
                                    {{setup}}{{preamble}}{{effectPrefix}}{{controlBody}}
                                    return 0;
                                    """,
                                    site)
                                    .WithUnsafeSites(unsafeEffect ? new[] { site } : Array.Empty<int>());
                                index++;
                            }
                        }
                    }
                }
            }
        }

        return cases;
    }

    private static string Indent(string source, int spaces)
    {
        var prefix = new string(' ', spaces);
        return string.Join(
            Environment.NewLine,
            source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(line => prefix + line));
    }

    private static string TraceStatements(int siteId, string receiver)
        => $"GeneratedTrace.ReachedSites[{siteId}] = 1;\n" +
           $"GeneratedTrace.ActualKinds[{siteId}] = (int){receiver}.kind;\n" +
           $"if (GeneratedTrace.ActualKinds[{siteId}] != (int)Command.Kind.Move)\n" +
           $"    GeneratedTrace.MismatchSites[{siteId}] = 1;";

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queued = new();

        public override void Post(SendOrPostCallback callback, object? state)
            => _queued.Enqueue((callback, state));

        public void PumpAll()
        {
            while (_queued.TryDequeue(out var work))
                work.Callback(work.State);
        }
    }
}
