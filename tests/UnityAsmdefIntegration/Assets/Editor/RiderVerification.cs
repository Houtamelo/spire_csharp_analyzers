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
    internal static class RiderVerification
    {
        private const string StatePath = "Library/Spire.UnityAsmdefIntegration-rider-state.json";
        private static string IdeName => Environment.GetEnvironmentVariable("SPIRE_IDE_TEST_PROVIDER") == "VisualStudio"
            ? "Visual Studio" : "Rider";
        private static string ReportPath => Environment.GetEnvironmentVariable("SPIRE_IDE_TEST_PROVIDER") == "VisualStudio"
            ? "visual-studio-recompile-results.json" : "rider-recompile-results.json";
        private const string RiderSourcePath = "Assets/RuntimeAuto/_rider.cs";
        private const int TimeoutSeconds = 900;
        private const long PollIntervalTicks = TimeSpan.TicksPerMillisecond * 100;
        private const long StateSaveIntervalTicks = TimeSpan.TicksPerSecond;

        private static readonly string[] TriggerPaths =
        {
            "Assets/Predefined/__SpireRiderVerificationTrigger.cs",
            "Assets/RuntimeAuto/__SpireRiderVerificationTrigger.cs",
            "Assets/RuntimeExplicit/__SpireRiderVerificationTrigger.cs",
            "Assets/RuntimeBase/__SpireRiderVerificationTrigger.cs",
            "Assets/RuntimeChild/__SpireRiderVerificationTrigger.cs",
            "Assets/RuntimeTransitive/__SpireRiderVerificationTrigger.cs",
            "Assets/EditorRuntime/__SpireRiderVerificationTrigger.cs",
            "Assets/EditorExplicit/__SpireRiderVerificationTrigger.cs",
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

        private static RiderState _state;
        private static bool _transitionQueued;
        private static long _nextPollUtcTicks;
        private static long _lastStateSaveUtcTicks;

        static RiderVerification()
        {
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            ResumeIfNeeded();
        }

        public static void Begin()
        {
            Debug.Log("SPIRE_UNITY_RIDER_BEGIN");
            _state = new RiderState
            {
                schemaVersion = 1,
                active = true,
                stage = RiderStage.Collecting,
                cycle = 1,
                deadlineUtcTicks = DateTime.UtcNow.AddSeconds(TimeoutSeconds).Ticks,
                captures = new List<AssemblyCapture>(),
                phases = new List<PhaseResult>(),
                verifications = new List<VerificationResult>(),
            };
            SaveState();
            AttachPoll();
            Queue(BeginCompilation);
        }

        private static void ResumeIfNeeded()
        {
            _state = LoadState();
            if (_state == null || !_state.active)
                return;

            _state.captures = _state.captures ?? new List<AssemblyCapture>();
            _state.phases = _state.phases ?? new List<PhaseResult>();
            _state.verifications = _state.verifications ?? new List<VerificationResult>();
            if (_state.deadlineUtcTicks == 0)
                _state.deadlineUtcTicks = DateTime.UtcNow.AddSeconds(TimeoutSeconds).Ticks;
            AttachPoll();
        }

        private static void BeginCompilation()
        {
            var marker = "// Rider verification trigger " + DateTime.UtcNow.Ticks;
            for (var index = 0; index < TriggerPaths.Length; index++)
            {
                File.WriteAllText(
                    ProjectPath(TriggerPaths[index]),
                    marker + Environment.NewLine
                    + "namespace Spire.UnityAsmdefIntegration.RiderTriggers"
                    + Environment.NewLine
                    + "{" + Environment.NewLine
                    + "    internal static class Trigger" + index + " { }"
                    + Environment.NewLine
                    + "}" + Environment.NewLine);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            CompilationPipeline.RequestScriptCompilation();
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

            var now = DateTime.UtcNow.Ticks;
            if (now < _nextPollUtcTicks)
                return;
            _nextPollUtcTicks = now + PollIntervalTicks;
            _state.polls++;
            if (now - _lastStateSaveUtcTicks >= StateSaveIntervalTicks)
                SaveState();

            if (now > _state.deadlineUtcTicks)
            {
                Queue(() => FailAndExit("Timed out waiting for Rider verification compilation callbacks."));
                return;
            }

            if (_state.polls < 4 || !_state.sawCompilationEvent || EditorApplication.isCompiling)
                return;
            Queue(Complete);
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

        private static DiagnosticRecord ToDiagnostic(string assembly, CompilerMessage message)
        {
            var text = message.message ?? string.Empty;
            var match = DiagnosticIdPattern.Match(text);
            return new DiagnosticRecord
            {
                assembly = assembly,
                id = match.Success ? match.Value : string.Empty,
                severity = message.type.ToString(),
                message = text,
                file = message.file ?? string.Empty,
                line = message.line,
                column = message.column,
            };
        }

        private static void Complete()
        {
            _transitionQueued = false;
            if (_state == null || !_state.active)
                return;

            var diagnostics = FlattenDiagnostics(_state.captures);
            var failure = ValidateCompilation(diagnostics);
            if (failure.Length != 0)
            {
                FailAndExit(failure);
                return;
            }

            _state.verifications = VerifyGeneratedSurfaces();
            var failedVerifications = _state.verifications.Where(result => !result.passed).ToList();
            if (failedVerifications.Count != 0)
            {
                FailAndExit("Generated API verification failed: "
                    + string.Join("; ", failedVerifications.Select(result => result.assembly + ": " + result.message)));
                return;
            }

            _state.runtimeProbe = VerifyRiderRuntime();
            if (!_state.runtimeProbe.passed)
            {
                FailAndExit("Rider runtime probe failed: " + _state.runtimeProbe.message);
                return;
            }

            _state.phases.Add(new PhaseResult
            {
                name = IdeName == "Rider" ? "rider-recompile" : "visual-studio-recompile",
                passed = true,
                summary = "All fixture assemblies recompiled after " + IdeName + "'s SPIRE016 fix.",
                diagnostics = diagnostics,
            });
            _state.active = false;
            _state.success = true;
            _state.exitCode = 0;
            SaveState();
            WriteReport();
            Debug.Log("SPIRE_UNITY_RIDER_END");
            EditorApplication.Exit(0);
        }

        private static string ValidateCompilation(List<DiagnosticRecord> diagnostics)
        {
            var errors = diagnostics.Where(IsError).ToList();
            if (errors.Count != 0)
                return "Rider recompilation produced compiler errors: " + FormatDiagnostics(errors);

            var missingAssemblies = ExpectedAssemblies
                .Where(expected => !_state.captures.Any(capture => capture.assembly == expected.Name))
                .Select(expected => expected.Name)
                .ToList();
            if (missingAssemblies.Count != 0)
                return "Rider recompilation did not report all fixture assemblies: "
                    + string.Join(", ", missingAssemblies);

            foreach (var expected in ExpectedAssemblies)
            {
                if (!HasDiagnostic(diagnostics, expected.Name, "SPIRE002"))
                    return "Rider recompilation is missing SPIRE002 for " + expected.Name + ".";
            }

            var riderSPIRE016 = diagnostics.Where(diagnostic => diagnostic.id == "SPIRE016"
                && IsRiderSource(diagnostic.file)).ToList();
            if (riderSPIRE016.Count != 0)
                return "The Rider source still reports SPIRE016: " + FormatDiagnostics(riderSPIRE016);

            return string.Empty;
        }

        private static bool HasDiagnostic(List<DiagnosticRecord> diagnostics, string assembly, string id)
        {
            return diagnostics.Any(diagnostic => diagnostic.assembly == assembly && diagnostic.id == id);
        }

        private static bool IsRiderSource(string path)
        {
            var normalized = (path ?? string.Empty).Replace('\\', '/');
            return normalized.EndsWith("/RuntimeAuto/_rider.cs", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Assets/RuntimeAuto/_rider.cs", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsError(DiagnosticRecord diagnostic)
        {
            return string.Equals(diagnostic.severity, "Error", StringComparison.OrdinalIgnoreCase);
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
                    var type = assembly.GetType(expected.ProbeType, false);
                    if (type == null)
                        throw new InvalidOperationException("The generated probe type was not found.");
                    var method = type.GetMethod("Verify", BindingFlags.Public | BindingFlags.Static);
                    if (method == null)
                        throw new InvalidOperationException("The generated probe Verify method was not found.");
                    result.passed = method.Invoke(null, null) is bool value && value;
                    result.message = result.passed ? "Generated API probe passed." : "Verify returned false.";
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

        private static RuntimeProbeResult VerifyRiderRuntime()
        {
            var result = new RuntimeProbeResult();
            try
            {
                var assembly = FindAssembly("Spire.UnityAsmdefRuntimeAuto");
                if (assembly == null)
                    throw new InvalidOperationException("RuntimeAuto assembly was not loadable.");
                var probeType = assembly.GetType(
                    "Spire.UnityAsmdefIntegration.RuntimeAuto.RiderCodeFixProbe", false);
                var enumType = assembly.GetType(
                    "Spire.UnityAsmdefIntegration.RuntimeAuto.ProbeKind", false);
                if (probeType == null || enumType == null)
                    throw new InvalidOperationException("The Rider probe types were not found.");
                var convert = probeType.GetMethod("Convert", BindingFlags.Public | BindingFlags.Static);
                if (convert == null)
                    throw new InvalidOperationException("The Rider Convert method was not found.");

                var empty = Enum.Parse(enumType, "Empty");
                var one = convert.Invoke(null, new object[] { 1 });
                if (!Equals(one, empty))
                    throw new InvalidOperationException("Convert(1) did not return ProbeKind.Empty.");
                result.convertedOneToEmpty = true;

                try
                {
                    convert.Invoke(null, new object[] { 0 });
                    throw new InvalidOperationException("Convert(0) returned instead of throwing.");
                }
                catch (TargetInvocationException exception)
                {
                    var inner = exception.InnerException;
                    if (!(inner is ArgumentException))
                        throw new InvalidOperationException(
                            "Convert(0) threw " + (inner == null ? "no inner exception" : inner.GetType().FullName) + ".");
                    result.exceptionType = inner.GetType().FullName;
                    result.zeroThrewExpected = true;
                }

                result.passed = true;
                result.message = "Convert(1) returned Empty and Convert(0) threw an ArgumentException.";
            }
            catch (Exception exception)
            {
                result.passed = false;
                result.message = exception.GetBaseException().Message;
            }
            return result;
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

        private static string FormatDiagnostics(List<DiagnosticRecord> diagnostics)
        {
            if (diagnostics.Count == 0)
                return "<none>";
            return string.Join(" | ", diagnostics.Select(diagnostic =>
                diagnostic.assembly + " " + diagnostic.id + " " + diagnostic.message));
        }

        private static void FailAndExit(string reason)
        {
            _state.active = false;
            _state.success = false;
            _state.exitCode = 1;
            _state.failure = reason;
            SaveState();
            WriteReport();
            Debug.LogError("SPIRE_UNITY_RIDER_FAILED: " + reason);
            EditorApplication.Exit(1);
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

        private static RiderState LoadState()
        {
            var path = ProjectPath(StatePath);
            if (!File.Exists(path))
                return null;
            try
            {
                return JsonUtility.FromJson<RiderState>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not read Rider verification state: " + exception.Message);
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
            var diagnostics = FlattenDiagnostics(_state.captures ?? new List<AssemblyCapture>());
            var riderSPIRE016Count = diagnostics.Count(diagnostic => diagnostic.id == "SPIRE016"
                && IsRiderSource(diagnostic.file));
            var report = new RiderReport
            {
                schemaVersion = 1,
                fixture = "UnityAsmdefIntegration",
                ide = IdeName,
                unityVersion = Application.unityVersion,
                status = _state.active ? "running" : (_state.success ? "pass" : "fail"),
                success = _state.success,
                exitCode = _state.exitCode,
                source = RiderSourcePath,
                expectedAssemblies = ExpectedAssemblies.Select(expected => expected.Name).ToArray(),
                riderSourceSPIRE016Count = riderSPIRE016Count,
                stage = _state.stage ?? string.Empty,
                failure = _state.failure ?? string.Empty,
                phases = _state.phases ?? new List<PhaseResult>(),
                diagnostics = diagnostics,
                assemblies = BuildAssemblyReports(diagnostics),
                verifications = _state.verifications ?? new List<VerificationResult>(),
                runtimeProbe = _state.runtimeProbe ?? new RuntimeProbeResult(),
            };
            File.WriteAllText(ProjectPath(ReportPath), JsonUtility.ToJson(report, true));
        }

        private static List<AssemblyReport> BuildAssemblyReports(List<DiagnosticRecord> diagnostics)
        {
            var reports = new List<AssemblyReport>();
            foreach (var expected in ExpectedAssemblies)
            {
                var assemblyDiagnostics = diagnostics
                    .Where(diagnostic => diagnostic.assembly == expected.Name)
                    .ToList();
                reports.Add(new AssemblyReport
                {
                    name = expected.Name,
                    compiled = !assemblyDiagnostics.Any(IsError),
                    hasSPIRE002 = assemblyDiagnostics.Any(diagnostic => diagnostic.id == "SPIRE002"),
                    diagnostics = assemblyDiagnostics,
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

        private static class RiderStage
        {
            public const string Collecting = "Collecting";
        }

        [Serializable]
        private sealed class RiderState
        {
            public int schemaVersion;
            public bool active;
            public bool success;
            public int exitCode;
            public int cycle;
            public int polls;
            public bool sawCompilationEvent;
            public long deadlineUtcTicks;
            public string stage;
            public string failure;
            public List<AssemblyCapture> captures;
            public List<PhaseResult> phases;
            public List<VerificationResult> verifications;
            public RuntimeProbeResult runtimeProbe;
        }

        [Serializable]
        private sealed class RiderReport
        {
            public int schemaVersion;
            public string fixture;
            public string ide;
            public string unityVersion;
            public string status;
            public bool success;
            public int exitCode;
            public string source;
            public string[] expectedAssemblies;
            public int riderSourceSPIRE016Count;
            public string stage;
            public string failure;
            public List<PhaseResult> phases;
            public List<DiagnosticRecord> diagnostics;
            public List<AssemblyReport> assemblies;
            public List<VerificationResult> verifications;
            public RuntimeProbeResult runtimeProbe;
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
            public List<DiagnosticRecord> diagnostics;
        }

        [Serializable]
        private sealed class VerificationResult
        {
            public string assembly;
            public bool passed;
            public string message;
        }

        [Serializable]
        private sealed class RuntimeProbeResult
        {
            public bool passed;
            public bool convertedOneToEmpty;
            public bool zeroThrewExpected;
            public string exceptionType;
            public string message;
        }

        [Serializable]
        private sealed class AssemblyReport
        {
            public string name;
            public bool compiled;
            public bool hasSPIRE002;
            public List<DiagnosticRecord> diagnostics;
        }
    }
}
