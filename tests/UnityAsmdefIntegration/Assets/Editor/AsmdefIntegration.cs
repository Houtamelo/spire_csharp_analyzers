using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ReflectionAssembly = System.Reflection.Assembly;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Spire.UnityAsmdefIntegration
{
    [InitializeOnLoad]
    public static class AsmdefIntegration
    {
        private const string StatePath = "Library/Spire.UnityAsmdefIntegration-state.json";
        private const string ReportPath = "asmdef-results.json";
        private const string ErrorSourcePath = "Assets/Fixtures/SPIRE001-error.cs";
        private const string ErrorTemplatePath = "Assets/Fixtures/SPIRE001-error.cs.txt";
        private const string EditorExplicitAsmdefPath =
            "Assets/EditorExplicit/Spire.UnityAsmdefEditorExplicit.asmdef";
        private const int PollTimeoutSeconds = 900;
        private const long PollIntervalTicks = TimeSpan.TicksPerMillisecond * 100;
        private const long StateSaveIntervalTicks = TimeSpan.TicksPerSecond;

        private static readonly string[] BaselineTriggerPaths =
        {
            "Assets/Predefined/__SpireAsmdefBaselineTrigger.cs",
            "Assets/RuntimeAuto/__SpireAsmdefBaselineTrigger.cs",
            "Assets/RuntimeExplicit/__SpireAsmdefBaselineTrigger.cs",
            "Assets/RuntimeBase/__SpireAsmdefBaselineTrigger.cs",
            "Assets/RuntimeChild/__SpireAsmdefBaselineTrigger.cs",
            "Assets/RuntimeTransitive/__SpireAsmdefBaselineTrigger.cs",
            "Assets/EditorRuntime/__SpireAsmdefBaselineTrigger.cs",
            "Assets/EditorExplicit/__SpireAsmdefBaselineTrigger.cs",
        };

        private static readonly ExpectedAssembly[] ExpectedAssemblies =
        {
            new ExpectedAssembly("Assembly-CSharp", "Spire.UnityAsmdefIntegration.Predefined.AsmdefProbe"),
            new ExpectedAssembly("Spire.UnityAsmdefRuntimeAuto", "Spire.UnityAsmdefIntegration.RuntimeAuto.AsmdefProbe"),
            new ExpectedAssembly("Spire.UnityAsmdefRuntimeExplicit", "Spire.UnityAsmdefIntegration.RuntimeExplicit.AsmdefProbe"),
            new ExpectedAssembly("Spire.UnityAsmdefRuntimeBase", "Spire.UnityAsmdefIntegration.RuntimeBase.AsmdefProbe"),
            new ExpectedAssembly("Spire.UnityAsmdefRuntimeChild", "Spire.UnityAsmdefIntegration.RuntimeChild.AsmdefProbe"),
            new ExpectedAssembly("Spire.UnityAsmdefRuntimeTransitive", "Spire.UnityAsmdefIntegration.RuntimeTransitive.AsmdefProbe"),
            new ExpectedAssembly("Spire.UnityAsmdefEditorRuntime", "Spire.UnityAsmdefIntegration.EditorRuntime.AsmdefProbe"),
            new ExpectedAssembly("Spire.UnityAsmdefEditorExplicit", "Spire.UnityAsmdefIntegration.EditorExplicit.AsmdefProbe"),
        };

        private static readonly Regex DiagnosticIdPattern =
            new Regex(@"\b(?:SPIRE\d{3}|CS\d{4})\b", RegexOptions.Compiled);

        private static StateFile _state;
        private static bool _transitionQueued;
        private static long _nextPollUtcTicks;
        private static long _lastStateSaveUtcTicks;

        static AsmdefIntegration()
        {
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            ResumeIfNeeded();
        }

        /// <summary>
        /// Runs the complete asmdef matrix, including the negative compilation phases.
        /// </summary>
        public static void Run()
        {
            Debug.Log("SPIRE_UNITY_ASMDEF_BEGIN");

            _state = new StateFile
            {
                schemaVersion = 1,
                active = true,
                stage = Stage.BaselineCollecting,
                cycle = 1,
                baselineVerificationExpected = ExpectedAssemblies.Length,
                deadlineUtcTicks = DateTime.UtcNow.AddSeconds(PollTimeoutSeconds).Ticks,
                captures = new List<AssemblyCapture>(),
                phases = new List<PhaseResult>(),
                verifications = new List<VerificationResult>(),
            };
            SaveState();
            AttachPoll();
            Queue(BeginBaselineCompilation);
        }

        /// <summary>
        /// Recompiles the fixture after Rider applies the SPIRE016 quick fix and verifies the fixed API.
        /// </summary>
        public static void VerifyRiderFix()
        {
            RiderVerification.Begin();
        }

        /// <summary>Runs the shared compiler and conversion checks after a Visual Studio Quick Action.</summary>
        public static void VerifyVisualStudioFix()
        {
            Environment.SetEnvironmentVariable("SPIRE_IDE_TEST_PROVIDER", "VisualStudio");
            RiderVerification.Begin();
        }

        private static void BeginBaselineCompilation()
        {
            RequestCompilation(forceRefresh: true);
        }

        private static void WriteBaselineTriggers()
        {
            var marker = "// baseline trigger " + DateTime.UtcNow.Ticks;
            for (var index = 0; index < BaselineTriggerPaths.Length; index++)
            {
                var path = ProjectPath(BaselineTriggerPaths[index]);
                File.WriteAllText(
                    path,
                    marker + Environment.NewLine
                    + "namespace Spire.UnityAsmdefIntegration.BaselineTriggers"
                    + Environment.NewLine
                    + "{" + Environment.NewLine
                    + "    internal static class Trigger" + index + " { }"
                    + Environment.NewLine
                    + "}" + Environment.NewLine);
            }
        }

        private static void ResumeIfNeeded()
        {
            _state = LoadState();
            if (_state == null || !_state.active)
                return;

            if (_state.captures == null)
                _state.captures = new List<AssemblyCapture>();
            if (_state.phases == null)
                _state.phases = new List<PhaseResult>();
            if (_state.verifications == null)
                _state.verifications = new List<VerificationResult>();
            if (_state.deadlineUtcTicks == 0)
                _state.deadlineUtcTicks = DateTime.UtcNow.AddSeconds(PollTimeoutSeconds).Ticks;

            AttachPoll();
        }

        private static void AttachPoll()
        {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (_state == null || !_state.active)
            {
                EditorApplication.update -= Poll;
                return;
            }

            var nowUtcTicks = DateTime.UtcNow.Ticks;
            if (nowUtcTicks < _nextPollUtcTicks)
                return;
            _nextPollUtcTicks = nowUtcTicks + PollIntervalTicks;

            _state.polls++;
            if (nowUtcTicks - _lastStateSaveUtcTicks >= StateSaveIntervalTicks)
                SaveState();

            if (nowUtcTicks > _state.deadlineUtcTicks)
            {
                Queue(() => FailAndExit("Timed out waiting for Unity compilation callbacks."));
                return;
            }

            // The request is asynchronous. Waiting a few update ticks prevents the
            // first non-compiling frame from completing a phase before compilation starts.
            if (_state.polls < 4 || !_state.sawCompilationEvent || EditorApplication.isCompiling)
                return;

            Queue(AdvanceStage);
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            if (_state == null || !_state.active)
                return;

            var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
            if (string.IsNullOrEmpty(assemblyName))
                return;

            var capture = new AssemblyCapture
            {
                assembly = assemblyName,
                path = assemblyPath,
                diagnostics = new List<DiagnosticRecord>(),
            };
            if (messages != null)
            {
                foreach (var message in messages)
                {
                    capture.diagnostics.Add(ToDiagnostic(assemblyName, message));
                }
            }

            _state.sawCompilationEvent = true;
            _state.captures.Add(capture);
            SaveState();
        }

        private static DiagnosticRecord ToDiagnostic(string assemblyName, CompilerMessage message)
        {
            var text = message.message ?? string.Empty;
            var match = DiagnosticIdPattern.Match(text);
            return new DiagnosticRecord
            {
                assembly = assemblyName,
                id = match.Success ? match.Value : string.Empty,
                severity = message.type.ToString(),
                message = text,
                file = message.file ?? string.Empty,
                line = message.line,
                column = message.column,
            };
        }

        private static void AdvanceStage()
        {
            _transitionQueued = false;
            if (_state == null || !_state.active)
                return;

            var diagnostics = FlattenDiagnostics(_state.captures);
            switch (_state.stage)
            {
                case Stage.BaselineCollecting:
                    if (!CheckBaseline(diagnostics, out var baselineFailure))
                    {
                        FailAndExit(baselineFailure);
                        return;
                    }

                    _state.baselineVerificationCount = ExpectedAssemblies.Length;
                    AddPhase(
                        "baseline",
                        true,
                        diagnostics,
                        "All eight asmdefs compiled with the expected SPIRE002 warnings.",
                        ExpectedAssemblies.Length);
                    BeginSpire001Phase();
                    return;

                case Stage.Spire001Collecting:
                    if (!CheckSpire001Failure(diagnostics, out var spireFailure))
                    {
                        FailAndExit(spireFailure);
                        return;
                    }

                    AddPhase("spire001", true, diagnostics, "Assembly-CSharp reported the intentional SPIRE001 error.", 0);
                    BeginSpire001Repair();
                    return;

                case Stage.Spire001RepairCollecting:
                    if (!CheckCleanCompilation(diagnostics, out var repairedFailure))
                    {
                        FailAndExit(repairedFailure);
                        return;
                    }

                    AddPhase("spire001-repair", true, diagnostics, "The SPIRE001 source was removed and all assemblies compiled again.", ExpectedAssemblies.Length);
                    BeginMissingCorePhase();
                    return;

                case Stage.MissingCoreCollecting:
                    if (!CheckMissingCoreFailure(diagnostics, out var missingCoreFailure))
                    {
                        FailAndExit(missingCoreFailure);
                        return;
                    }

                    AddPhase("missing-core", true, diagnostics, "The explicit-reference editor asmdef rejected an omitted Houtamelo.Spire.dll reference.", 0);
                    BeginMissingCoreRepair();
                    return;

                case Stage.MissingCoreRepairCollecting:
                    if (!CheckCleanCompilation(diagnostics, out var finalFailure))
                    {
                        FailAndExit(finalFailure);
                        return;
                    }

                    AddPhase("missing-core-repair", true, diagnostics, "The explicit-reference editor asmdef compiled after restoring Houtamelo.Spire.dll.", ExpectedAssemblies.Length);
                    CompleteSuccessfully();
                    return;

                default:
                    FailAndExit("Unknown asmdef integration stage: " + _state.stage);
                    return;
            }
        }

        private static bool CheckBaseline(List<DiagnosticRecord> diagnostics, out string failure)
        {
            var errors = diagnostics.Where(IsError).ToList();
            if (errors.Count != 0)
            {
                failure = "Baseline had unexpected errors: " + FormatDiagnostics(errors);
                return false;
            }

            foreach (var expected in ExpectedAssemblies)
            {
                if (!HasDiagnostic(diagnostics, expected.Name, "SPIRE002", warningOnly: true))
                {
                    failure = "Baseline is missing the SPIRE002 warning for " + expected.Name + ".";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private static bool CheckSpire001Failure(List<DiagnosticRecord> diagnostics, out string failure)
        {
            var errors = diagnostics.Where(IsError).ToList();
            var spireErrors = errors.Where(diagnostic => diagnostic.assembly == "Assembly-CSharp"
                && diagnostic.id == "SPIRE001").ToList();
            var unexpected = errors.Except(spireErrors).ToList();
            if (spireErrors.Count == 0 || unexpected.Count != 0)
            {
                failure = "SPIRE001 phase did not produce only the expected Assembly-CSharp diagnostic. "
                    + FormatDiagnostics(errors);
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool CheckMissingCoreFailure(List<DiagnosticRecord> diagnostics, out string failure)
        {
            var errors = diagnostics.Where(IsError).ToList();
            var missingCore = errors.Where(diagnostic =>
                diagnostic.assembly == "Spire.UnityAsmdefEditorExplicit"
                && (diagnostic.id == "CS0246" || diagnostic.id == "CS0234"
                    || diagnostic.id == "CS8795" || diagnostic.id == "CS0103")).ToList();
            var missingReference = missingCore.Any(diagnostic =>
                diagnostic.id == "CS0246" || diagnostic.id == "CS0234");
            var unexpected = errors.Except(missingCore).ToList();
            if (!missingReference || unexpected.Count != 0)
            {
                failure = "Missing-core phase did not produce only the expected CS0246/CS0234 diagnostics. "
                    + FormatDiagnostics(errors);
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool CheckCleanCompilation(List<DiagnosticRecord> diagnostics, out string failure)
        {
            var errors = diagnostics.Where(IsError).ToList();
            if (errors.Count != 0)
            {
                failure = "Compilation still has errors: " + FormatDiagnostics(errors);
                return false;
            }

            foreach (var expected in ExpectedAssemblies)
            {
                // Unity's Bee pipeline can report analyzer warnings during an earlier
                // pass while delivering an empty diagnostic array for the completion
                // callback used by repair phases. The callback itself is the reliable
                // completion contract here; baseline validation still checks SPIRE002.
                if (!_state.captures.Any(capture => capture.assembly == expected.Name))
                {
                    failure = "Compilation did not report a completion callback for " + expected.Name + ".";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private static bool HasDiagnostic(
            List<DiagnosticRecord> diagnostics,
            string assembly,
            string id,
            bool warningOnly)
        {
            return diagnostics.Any(diagnostic => diagnostic.assembly == assembly
                && diagnostic.id == id
                && (!warningOnly || !IsError(diagnostic)));
        }

        private static bool IsError(DiagnosticRecord diagnostic)
        {
            return string.Equals(diagnostic.severity, "Error", StringComparison.OrdinalIgnoreCase);
        }

        private static void BeginSpire001Phase()
        {
            _state.stage = Stage.Spire001Collecting;
            ResetCycle();
            SaveState();
            Queue(() =>
            {
                var template = ProjectPath(ErrorTemplatePath);
                var target = ProjectPath(ErrorSourcePath);
                if (!File.Exists(template))
                {
                    FailAndExit("SPIRE001 template is missing: " + template);
                    return;
                }

                File.WriteAllText(target, File.ReadAllText(template));
                RequestCompilation(forceRefresh: true);
            });
        }

        private static void BeginSpire001Repair()
        {
            _state.stage = Stage.Spire001RepairCollecting;
            ResetCycle();
            SaveState();
            Queue(() =>
            {
                var target = ProjectPath(ErrorSourcePath);
                if (File.Exists(target))
                    File.Delete(target);
                RequestCompilation(forceRefresh: true);
            });
        }

        private static void BeginMissingCorePhase()
        {
            _state.stage = Stage.MissingCoreCollecting;
            ResetCycle();
            SaveState();
            Queue(() =>
            {
                var path = ProjectPath(EditorExplicitAsmdefPath);
                var content = File.ReadAllText(path);
                const string configured = "\"precompiledReferences\": [\n        \"Houtamelo.Spire.dll\"\n    ]";
                if (content.IndexOf(configured, StringComparison.Ordinal) < 0)
                {
                    FailAndExit("Editor explicit asmdef did not contain the expected Houtamelo.Spire.dll reference.");
                    return;
                }

                File.WriteAllText(path, content.Replace(configured, "\"precompiledReferences\": []"));
                RequestCompilation(forceRefresh: true);
            });
        }

        private static void BeginMissingCoreRepair()
        {
            _state.stage = Stage.MissingCoreRepairCollecting;
            ResetCycle();
            SaveState();
            Queue(() =>
            {
                var path = ProjectPath(EditorExplicitAsmdefPath);
                var content = File.ReadAllText(path);
                const string omitted = "\"precompiledReferences\": []";
                if (content.IndexOf(omitted, StringComparison.Ordinal) < 0)
                {
                    FailAndExit("Editor explicit asmdef was not in the omitted-reference state.");
                    return;
                }

                const string configured = "\"precompiledReferences\": [\n        \"Houtamelo.Spire.dll\"\n    ]";
                File.WriteAllText(path, content.Replace(omitted, configured));
                RequestCompilation(forceRefresh: true);
            });
        }

        private static void RequestCompilation(bool forceRefresh)
        {
            if (forceRefresh)
            {
                WriteBaselineTriggers();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }
            CompilationPipeline.RequestScriptCompilation();
        }

        private static void CompleteSuccessfully()
        {
            _state.verifications = VerifyGeneratedSurfaces();
            var failed = _state.verifications.Where(result => !result.passed).ToList();
            if (failed.Count != 0)
            {
                FailAndExit("Generated API verification failed: " + string.Join("; ", failed.Select(result => result.assembly + ": " + result.message)));
                return;
            }

            try
            {
                GenerateSolution();
            }
            catch (Exception exception)
            {
                FailAndExit("Unity solution generation failed: " + exception.GetBaseException().Message);
                return;
            }

            _state.active = false;
            _state.success = true;
            _state.exitCode = 0;
            SaveState();
            WriteReport();
            Debug.Log("SPIRE_UNITY_ASMDEF_END");
            EditorApplication.Exit(0);
        }

        private static void FailAndExit(string reason)
        {
            if (_state == null)
            {
                _state = new StateFile
                {
                    schemaVersion = 1,
                    phases = new List<PhaseResult>(),
                    captures = new List<AssemblyCapture>(),
                    verifications = new List<VerificationResult>(),
                };
            }

            _state.active = false;
            _state.success = false;
            _state.exitCode = 1;
            _state.failure = reason;
            SaveState();
            WriteReport();
            Debug.LogError("SPIRE_UNITY_ASMDEF_FAILED: " + reason);
            EditorApplication.Exit(1);
        }

        private static void GenerateSolution()
        {
            var visualStudio = Environment.GetEnvironmentVariable("SPIRE_VISUAL_STUDIO");
            if (!string.IsNullOrEmpty(visualStudio))
            {
                Unity.CodeEditor.CodeEditor.SetExternalScriptEditor(visualStudio);
                var current = Unity.CodeEditor.CodeEditor.CurrentEditor;
                if (current.GetType().FullName != "Microsoft.Unity.VisualStudio.Editor.VisualStudioEditor")
                    throw new InvalidOperationException("The Visual Studio Unity project generator was not selected.");
                current.SyncAll();
                RecordGeneratedSolution();
                return;
            }
            var riderType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Packages.Rider.Editor.RiderScriptEditor", throwOnError: false))
                .FirstOrDefault(type => type != null);
            if (riderType == null)
                riderType = Type.GetType("Packages.Rider.Editor.RiderScriptEditor, Unity.Rider.Editor", throwOnError: false);
            if (riderType == null)
                throw new InvalidOperationException("The com.unity.ide.rider editor assembly was not loaded.");

            var syncSolution = riderType.GetMethod("SyncSolution", BindingFlags.Public | BindingFlags.Static);
            if (syncSolution == null)
                throw new InvalidOperationException("RiderScriptEditor.SyncSolution was not found.");

            syncSolution.Invoke(null, null);

            RecordGeneratedSolution();
        }

        private static void RecordGeneratedSolution()
        {
            var projectRoot = ProjectPath(string.Empty);
            var solutions = Directory.GetFiles(projectRoot, "*", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetExtension(path) is ".sln" or ".slnx").ToArray();
            if (solutions.Length == 0)
                throw new InvalidOperationException("CurrentEditor.SyncAll produced no solution file.");

            _state.solutionPath = solutions[0];
            _state.generatedProjects = Directory.GetFiles(projectRoot, "*.csproj", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .ToList();
        }

        private static List<VerificationResult> VerifyGeneratedSurfaces()
        {
            var results = new List<VerificationResult>();
            foreach (var expected in ExpectedAssemblies)
            {
                var result = new VerificationResult { assembly = expected.Name };
                try
                {
                    var assembly = FindAssembly(expected.Name);
                    if (assembly == null)
                        throw new InvalidOperationException("The compiled assembly was not loadable.");

                    var type = assembly.GetType(expected.ProbeType, throwOnError: false);
                    if (type == null)
                        throw new InvalidOperationException("The generated probe type was not found.");

                    var method = type.GetMethod("Verify", BindingFlags.Public | BindingFlags.Static);
                    if (method == null)
                        throw new InvalidOperationException("The probe Verify method was not found.");

                    var value = method.Invoke(null, null);
                    result.passed = value is bool && (bool)value;
                    result.message = result.passed ? "Generated factory, Kind, getter, and initialized control passed." : "Verify returned false.";
                }
                catch (Exception exception)
                {
                    result.passed = false;
                    result.message = exception.GetBaseException().Message;
                }
                results.Add(result);
            }
            return results;
        }

        private static ReflectionAssembly FindAssembly(string name)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(candidate => candidate.GetName().Name == name);
            if (assembly != null)
                return assembly;

            try
            {
                return ReflectionAssembly.Load(name);
            }
            catch
            {
                var path = ProjectPath(Path.Combine("Library", "ScriptAssemblies", name + ".dll"));
                if (!File.Exists(path))
                    return null;
                try
                {
                    return ReflectionAssembly.LoadFrom(path);
                }
                catch
                {
                    return null;
                }
            }
        }

        private static void AddPhase(
            string name,
            bool passed,
            List<DiagnosticRecord> diagnostics,
            string summary,
            int verificationCount)
        {
            _state.phases.Add(new PhaseResult
            {
                name = name,
                passed = passed,
                summary = summary,
                verificationCount = verificationCount,
                diagnostics = diagnostics,
            });
            SaveState();
            WriteReport();
        }

        private static List<DiagnosticRecord> FlattenDiagnostics(List<AssemblyCapture> captures)
        {
            var result = new List<DiagnosticRecord>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var capture in captures)
            {
                if (capture.diagnostics == null)
                    continue;
                foreach (var diagnostic in capture.diagnostics)
                {
                    var key = diagnostic.assembly + "|" + diagnostic.id + "|" + diagnostic.file + "|"
                        + diagnostic.line + "|" + diagnostic.column + "|" + diagnostic.message;
                    if (seen.Add(key))
                        result.Add(diagnostic);
                }
            }
            return result;
        }

        private static string FormatDiagnostics(List<DiagnosticRecord> diagnostics)
        {
            if (diagnostics.Count == 0)
                return "<none>";
            return string.Join(" | ", diagnostics.Select(diagnostic =>
                diagnostic.assembly + " " + diagnostic.id + " " + diagnostic.message));
        }

        private static void ResetCycle()
        {
            _state.cycle++;
            _state.polls = 0;
            _state.sawCompilationEvent = false;
            _state.captures = new List<AssemblyCapture>();
        }

        private static void Queue(Action action)
        {
            if (_transitionQueued)
                return;
            _transitionQueued = true;
            EditorApplication.delayCall += () =>
            {
                _transitionQueued = false;
                action();
            };
        }

        private static StateFile LoadState()
        {
            var path = ProjectPath(StatePath);
            if (!File.Exists(path))
                return null;
            try
            {
                return JsonUtility.FromJson<StateFile>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not read asmdef integration state: " + exception.Message);
                return null;
            }
        }

        private static void SaveState()
        {
            if (_state == null)
                return;
            var path = ProjectPath(StatePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(_state, true));
            _lastStateSaveUtcTicks = DateTime.UtcNow.Ticks;
        }

        private static void WriteReport()
        {
            if (_state == null)
                return;

            var report = new ReportFile
            {
                schemaVersion = 1,
                fixture = "UnityAsmdefIntegration",
                unityVersion = Application.unityVersion,
                success = _state.success,
                exitCode = _state.exitCode,
                status = _state.active ? "running" : (_state.success ? "pass" : "fail"),
                currentStage = _state.stage ?? string.Empty,
                baselineVerificationCount = _state.baselineVerificationCount,
                baselineVerificationExpected = _state.baselineVerificationExpected,
                solutionPath = _state.solutionPath ?? string.Empty,
                generatedProjects = _state.generatedProjects ?? new List<string>(),
                failure = _state.failure ?? string.Empty,
                phases = _state.phases ?? new List<PhaseResult>(),
                verifications = _state.verifications ?? new List<VerificationResult>(),
                expectedAssemblies = ExpectedAssemblies.Select(expected => expected.Name).ToArray(),
                assemblies = BuildAssemblyReports(),
            };
            File.WriteAllText(ProjectPath(ReportPath), JsonUtility.ToJson(report, true));
        }

        private static List<AssemblyReport> BuildAssemblyReports()
        {
            var diagnostics = FlattenDiagnostics(_state.captures ?? new List<AssemblyCapture>());
            var reports = new List<AssemblyReport>();
            foreach (var expected in ExpectedAssemblies)
            {
                var assemblyDiagnostics = diagnostics
                    .Where(diagnostic => diagnostic.assembly == expected.Name)
                    .ToList();
                var verification = (_state.verifications ?? new List<VerificationResult>())
                    .FirstOrDefault(result => result.assembly == expected.Name);
                reports.Add(new AssemblyReport
                {
                    name = expected.Name,
                    compiled = !assemblyDiagnostics.Any(IsError),
                    baselineVerified = _state.baselineVerificationCount == _state.baselineVerificationExpected
                        && _state.baselineVerificationExpected == ExpectedAssemblies.Length,
                    hasSPIRE002 = assemblyDiagnostics.Any(diagnostic => diagnostic.id == "SPIRE002"),
                    diagnostics = assemblyDiagnostics,
                    verificationPassed = verification != null && verification.passed,
                    verificationMessage = verification == null ? string.Empty : verification.message,
                });
            }
            return reports;
        }

        private static string ProjectPath(string relative)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, relative));
        }

        private sealed class ExpectedAssembly
        {
            public ExpectedAssembly(string name, string probeType)
            {
                Name = name;
                ProbeType = probeType;
            }

            public string Name { get; }
            public string ProbeType { get; }
        }

        [Serializable]
        private sealed class StateFile
        {
            public int schemaVersion;
            public bool active;
            public bool success;
            public int exitCode;
            public int cycle;
            public int polls;
            public bool sawCompilationEvent;
            public int baselineVerificationCount;
            public int baselineVerificationExpected;
            public long deadlineUtcTicks;
            public string solutionPath;
            public List<string> generatedProjects;
            public string stage;
            public string failure;
            public List<AssemblyCapture> captures;
            public List<PhaseResult> phases;
            public List<VerificationResult> verifications;
        }

        [Serializable]
        private sealed class ReportFile
        {
            public int schemaVersion;
            public string fixture;
            public string unityVersion;
            public bool success;
            public int exitCode;
            public string status;
            public string currentStage;
            public int baselineVerificationCount;
            public int baselineVerificationExpected;
            public string solutionPath;
            public List<string> generatedProjects;
            public string failure;
            public string[] expectedAssemblies;
            public List<AssemblyReport> assemblies;
            public List<PhaseResult> phases;
            public List<VerificationResult> verifications;
        }

        [Serializable]
        private sealed class AssemblyReport
        {
            public string name;
            public bool compiled;
            public bool baselineVerified;
            public bool hasSPIRE002;
            public bool verificationPassed;
            public string verificationMessage;
            public List<DiagnosticRecord> diagnostics;
        }

        [Serializable]
        private sealed class AssemblyCapture
        {
            public string assembly;
            public string path;
            public List<DiagnosticRecord> diagnostics;
        }

        [Serializable]
        private sealed class DiagnosticRecord
        {
            public string assembly;
            public string id;
            public string severity;
            public string message;
            public string file;
            public int line;
            public int column;
        }

        [Serializable]
        private sealed class PhaseResult
        {
            public string name;
            public bool passed;
            public string summary;
            public int verificationCount;
            public List<DiagnosticRecord> diagnostics;
        }

        [Serializable]
        private sealed class VerificationResult
        {
            public string assembly;
            public bool passed;
            public string message;
        }

        private static class Stage
        {
            public const string BaselineCollecting = "BaselineCollecting";
            public const string Spire001Collecting = "Spire001Collecting";
            public const string Spire001RepairCollecting = "Spire001RepairCollecting";
            public const string MissingCoreCollecting = "MissingCoreCollecting";
            public const string MissingCoreRepairCollecting = "MissingCoreRepairCollecting";
        }
    }
}
