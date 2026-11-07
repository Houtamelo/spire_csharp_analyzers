#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spire.UnityCustomerAcceptance
{
    [InitializeOnLoad]
    public static class CustomerAcceptance
    {
        private const string PackageName = "individual.antonio-pedro-goncalves-ferreira.spire";
        private const string PackagePathEnvironmentVariable = "SPIRE_CUSTOMER_PACKAGE";
        private const string PhaseEnvironmentVariable = "SPIRE_CUSTOMER_PHASE";
        private const string RuntimeAssemblyGuid = "de1fa9429c4a323daa60cd365f1c7de3";
        private const string ReportName = "customer-acceptance.json";
        private const string BaselineSnapshotName = "customer-baseline.json";
        private const string StageName = "Library/customer-acceptance-stage.json";
        private const int MaximumOperationSeconds = 120;
        private const int PackageReplacementGraceSeconds = 2;

        private static AcceptanceReport? _report;
        private static AcceptanceStage? _stage;
        private static AddRequest? _addRequest;
        private static bool _exitRequested;

        static CustomerAcceptance()
        {
            ResumePendingStage();
        }

        public static void Install()
        {
            StartOperation("Install");

            if (!TryReadInputs(out var packagePath, out var phase))
                return;

            if (!File.Exists(packagePath) && !Directory.Exists(packagePath))
            {
                Fail("Package path does not exist: " + packagePath);
                return;
            }

            _report!.requestedPackage = packagePath;
            _report.phase = phase;
            _stage = NewStage("install", "install-requested", packagePath);
            SaveStageAndReport();

            try
            {
                _addRequest = Client.Add("file:" + packagePath);
                _report.checks.Add("PASS Client.Add: requested " + packagePath);
                SaveStageAndReport();
                RegisterPoll();
            }
            catch (Exception exception)
            {
                Fail("Client.Add threw: " + exception);
            }
        }

        public static void CaptureBaseline()
        {
            StartOperation("CaptureBaseline");

            if (!TryReadInputs(out _, out var phase))
                return;

            if (phase != "baseline")
            {
                Fail("CaptureBaseline requires SPIRE_CUSTOMER_PHASE=baseline.");
                return;
            }

            if (!RequireRegisteredPackage())
                return;

            if (TryResolveSampleRoot() is null)
            {
                Fail("Baseline consumer assets are missing; the runtime assembly GUID did not resolve.");
                return;
            }

            BeginCapture();
        }

        public static void VerifyPollingTimeout()
        {
            StartOperation("VerifyPollingTimeout");
            Require(TryResolveSampleRoot() is null, "Polling regression requires a clean project without the sample.");
            _stage = NewStage("sample-import", "sample-import-pending", "");
            for (var index = 0; index < 6002 && !_exitRequested; index++)
                Poll();
            if (_exitRequested)
                return;
            _report!.checks.Add("PASS More than 6000 Editor updates do not exhaust the time budget.");
            _stage!.startedUtcTicks = DateTime.UtcNow.AddSeconds(-MaximumOperationSeconds - 1).Ticks;
            Poll();
            if (!_exitRequested)
                Fail("An expired polling deadline did not fail.");
        }

        public static void ImportSample()
        {
            StartOperation("ImportSample");

            if (!TryReadInputs(out _, out var phase))
                return;

            if (phase != "candidate")
            {
                Fail("ImportSample requires SPIRE_CUSTOMER_PHASE=candidate.");
                return;
            }

            if (!RequireRegisteredPackage())
                return;

            if (TryResolveSampleRoot() is not null)
            {
                Fail("ImportSample requires a clean candidate project; the showcase runtime assembly GUID already resolves.");
                return;
            }

            BeginSampleImport();
        }

        public static void VerifyImportedSample()
        {
            StartOperation("VerifyImportedSample");

            if (!TryReadInputs(out _, out var phase))
                return;

            if (phase != "candidate")
            {
                Fail("VerifyImportedSample requires SPIRE_CUSTOMER_PHASE=candidate.");
                return;
            }

            if (!RequireRegisteredPackage())
                return;

            _stage = NewStage("sample-verify", "sample-verify-pending", _report!.requestedPackage);
            SaveStageAndReport();
            RegisterPoll();
        }

        public static void ConfigureSample()
        {
            StartOperation("ConfigureSample");

            if (!TryReadInputs(out _, out var phase))
                return;

            if (phase != "candidate")
            {
                Fail("ConfigureSample requires SPIRE_CUSTOMER_PHASE=candidate.");
                return;
            }

            if (!RequireRegisteredPackage())
                return;

            _stage = NewStage("configure-sample", "configure-sample-pending", _report!.requestedPackage);
            SaveStageAndReport();
            RegisterPoll();
        }

        public static void VerifyUpgrade()
        {
            StartOperation("VerifyUpgrade");

            if (!TryReadInputs(out _, out var phase))
                return;

            if (phase != "candidate")
            {
                Fail("VerifyUpgrade requires SPIRE_CUSTOMER_PHASE=candidate.");
                return;
            }

            if (!RequireRegisteredPackage())
                return;

            _stage = NewStage("upgrade-verify", "upgrade-verify-pending", _report!.requestedPackage);
            SaveStageAndReport();
            RegisterPoll();
        }

        private static void ResumePendingStage()
        {
            var stage = ReadStage();
            if (stage is null)
                return;

            _stage = stage;
            _report = ReadReport() ?? NewReport(stage.operation, stage.phase);
            _report.status = "running";
            _report.stage = stage.stage;
            RegisterPoll();
            Debug.Log("SPIRE_CUSTOMER_ACCEPTANCE_RESUME " + stage.operation + "/" + stage.stage);
        }

        private static void RegisterPoll()
        {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        private static void UnregisterPoll()
        {
            EditorApplication.update -= Poll;
        }

        private static void Poll()
        {
            if (_exitRequested || _stage is null || _report is null)
                return;

            if (DateTime.UtcNow.Ticks - _stage.startedUtcTicks > TimeSpan.FromSeconds(MaximumOperationSeconds).Ticks)
            {
                Fail("Timed out while waiting for " + _stage.operation + ". " + _stage.lastError);
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            try
            {
                switch (_stage.stage)
                {
                    case "install-requested":
                        PollInstall();
                        break;
                    case "baseline-capture-pending":
                        PollCapture();
                        break;
                    case "sample-import-pending":
                        PollSampleImport();
                        break;
                    case "sample-verify-pending":
                    case "upgrade-verify-pending":
                        PollVerification();
                        break;
                    case "configure-sample-pending":
                        PollConfigureSample();
                        break;
                    default:
                        Fail("Unknown persisted stage: " + _stage.stage);
                        break;
                }
            }
            catch (Exception exception)
            {
                Fail(exception.ToString());
            }
        }

        private static void PollInstall()
        {
            if (_addRequest is not null && _addRequest.IsCompleted)
            {
                if (_addRequest.Status == StatusCode.Failure)
                {
                    Fail("Client.Add failed: " + (_addRequest.Error?.message ?? "unknown Package Manager error"));
                    return;
                }

                _report!.checks.Add("PASS Client.Add: request completed successfully.");
            }

            var package = TryFindRegisteredPackage();
            if (package is null)
            {
                _stage!.lastError = "Package Manager has not registered " + PackageName + " yet.";
                SaveStageAndReport();
                return;
            }

            _report!.packageVersion = package.version;
            _report.resolvedPackage = NormalizePath(package.resolvedPath);
            _report.packageSource = package.source.ToString();

            if (!PathsEqual(_report.requestedPackage, _report.resolvedPackage))
            {
                // After a domain reload the AddRequest object is lost. Give Unity a short
                // grace window to finish replacing an older local package before reporting
                // a definitive path mismatch.
                if (DateTime.UtcNow.Ticks - _stage!.startedUtcTicks < TimeSpan.FromSeconds(PackageReplacementGraceSeconds).Ticks)
                {
                    _stage.lastError = "Registered package still points to " + _report.resolvedPackage;
                    SaveStageAndReport();
                    return;
                }

                Fail(
                    "Package Manager resolved " + _report.resolvedPackage +
                    ", but the requested package was " + _report.requestedPackage + ".");
                return;
            }

            _report.checks.Add("PASS PackageInfo.resolvedPath: " + _report.resolvedPackage);
            Complete("Package installed and resolved to the requested external package.");
        }

        private static void PollCapture()
        {
            var snapshot = CaptureSnapshot();
            ValidateRuntimeSnapshot(snapshot.runtime, "captured showcase preset");
            _report!.observed = snapshot;
            _report.baseline = snapshot;
            WriteBaselineSnapshot(snapshot);
            Complete("Baseline scene, preset, GUIDs, hashes, and reflected values captured.");
        }

        private static void PollSampleImport()
        {
            var root = TryResolveSampleRoot();
            if (root is null)
            {
                _stage!.lastError = "The imported sample runtime assembly GUID has not resolved yet.";
                SaveStageAndReport();
                return;
            }

            _report!.sampleRoot = root;
            _stage!.stage = "sample-verify-pending";
            _report!.stage = _stage.stage;
            SaveStageAndReport();
        }

        private static void PollVerification()
        {
            if (_stage!.operation == "upgrade-verify")
            {
                VerifyUpgradeNow();
                return;
            }

            VerifyImportedSampleNow();
        }

        private static void PollConfigureSample()
        {
            var authoringType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Houtamelo.Spire.Showcase.Editor.ShowcaseAuthoring"))
                .FirstOrDefault(type => type is not null);
            Require(authoringType is not null, "Houtamelo.Spire.Showcase.Editor is not loaded yet.");

            var configure = authoringType!.GetMethod(
                "ConfigureUrp",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Require(configure is not null, "ShowcaseAuthoring.ConfigureUrp is missing.");
            configure!.Invoke(null, null);
            _report!.checks.Add("PASS ShowcaseAuthoring.ConfigureUrp: imported sample URP configured.");
            Complete("Imported showcase URP configuration completed.");
        }

        private static void BeginCapture()
        {
            _stage = NewStage("baseline", "baseline-capture-pending", _report!.requestedPackage);
            SaveStageAndReport();
            RegisterPoll();
        }

        private static void BeginSampleImport()
        {
            var package = TryFindRegisteredPackage();
            if (package is null)
            {
                Fail("The package is not registered; run Install before importing the sample.");
                return;
            }

            var samples = Sample.FindByPackage(PackageName, package.version)
                .Where(item => item.displayName == "Unity Showcase (URP)").ToArray();
            if (samples.Length != 1)
            {
                Fail(
                    "Expected exactly one Showcase sample for " + PackageName + " " + package.version +
                    ", found " + samples.Length + ".");
                return;
            }

            var sample = samples.Single();
            _report!.sampleDisplayName = sample.displayName;
            _report.sampleImportPath = sample.importPath;
            _report.sampleResolvedPath = sample.resolvedPath;
            _stage = NewStage("sample-import", "sample-import-pending", _report.requestedPackage);
            SaveStageAndReport();

            try
            {
                if (!sample.Import(Sample.ImportOptions.OverridePreviousImports))
                {
                    Fail("Sample.Import returned false for " + sample.displayName + ".");
                    return;
                }

                _report.checks.Add("PASS Sample.Import: " + sample.displayName);
                AssetDatabase.Refresh();
                _stage.stage = "sample-import-pending";
                SaveStageAndReport();
                RegisterPoll();
            }
            catch (Exception exception)
            {
                Fail("Sample.Import threw: " + exception);
            }
        }

        private static void VerifyImportedSampleNow()
        {
            var snapshot = CaptureSnapshot();
            ValidateRuntimeSnapshot(snapshot.runtime, "imported showcase preset");
            ValidateImportedResources(snapshot);
            ValidateRuntimeDocumentation();
            _report!.observed = snapshot;
            Complete("Showcase sample imported through Sample API and compiled successfully.");
        }

        private static void VerifyUpgradeNow()
        {
            var baseline = ReadBaselineSnapshot();
            var current = CaptureSnapshot();
            _report!.baseline = baseline;
            _report.observed = current;
            CompareSnapshots(baseline, current);

            var beforeSave = CaptureFileHashes(current.sampleRoot);
            AssetDatabase.SaveAssets();
            var afterSave = CaptureFileHashes(current.sampleRoot);
            CompareFileHashes(beforeSave, afterSave, "AssetDatabase.SaveAssets");
            _report.checks.Add("PASS AssetDatabase.SaveAssets: authored sample hashes remained unchanged.");

            Complete("Consumer scene and preset survived the package upgrade with identical GUIDs, bytes, and reflected values.");
        }

        private static AssetSnapshot CaptureSnapshot()
        {
            var root = TryResolveSampleRoot();
            Require(root is not null, "The showcase runtime assembly GUID did not resolve to an imported sample root.");

            var scenePath = root + "/Scenes/Showcase.unity";
            var presetPath = root + "/Examples/ShowcasePreset.asset";
            var sceneFile = ProjectFile(scenePath);
            var presetFile = ProjectFile(presetPath);
            Require(File.Exists(sceneFile), "Showcase scene is missing: " + scenePath);
            Require(File.Exists(presetFile), "Showcase preset is missing: " + presetPath);

            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            var preset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(presetPath);
            Require(scene is not null, "Unity could not load the showcase scene asset: " + scenePath);
            Require(preset is not null, "Unity could not load the showcase preset asset: " + presetPath);

            var snapshot = new AssetSnapshot
            {
                sampleRoot = root!,
                scenePath = scenePath,
                presetPath = presetPath,
                sceneGuid = AssetDatabase.AssetPathToGUID(scenePath),
                presetGuid = AssetDatabase.AssetPathToGUID(presetPath),
                sceneHash = HashFile(sceneFile),
                presetHash = HashFile(presetFile),
                runtime = CapturePresetRuntime(preset!),
                files = CaptureFileHashes(root!),
            };

            Require(!string.IsNullOrEmpty(snapshot.sceneGuid), "Showcase scene has no Unity GUID.");
            Require(!string.IsNullOrEmpty(snapshot.presetGuid), "Showcase preset has no Unity GUID.");
            return snapshot;
        }

        private static RuntimeSnapshot CapturePresetRuntime(UnityEngine.Object preset)
        {
            Require(TryGetMember(preset, "selectedCommand", out var selectedCommand), "ShowcasePreset.selectedCommand is missing.");
            Require(selectedCommand is not null, "ShowcasePreset.selectedCommand is null.");
            var selectedMove = ReadPayload(selectedCommand!, "move");

            var snapshot = new RuntimeSnapshot
            {
                selectedKind = ReadKind(selectedCommand!),
                selectedDestination = ReadVector2(selectedMove, "destination"),
                selectedSpeed = ReadFloat(selectedMove, "speed"),
            };

            Require(TryGetMember(preset, "commands", out var commandsValue), "ShowcasePreset.commands is missing.");
            Require(commandsValue is IList, "ShowcasePreset.commands is not an IList.");
            var commands = (IList)commandsValue!;
            snapshot.commandKinds = new string[commands.Count];
            snapshot.commandCount = commands.Count;

            Require(commands.Count >= 6, "ShowcasePreset.commands has fewer than six authored entries.");
            Require(commands[3] is not null && commands[4] is not null, "The managed-reference command entries are null.");
            Require(TryGetMember(ReadPayload(commands[3]!, "applyEffect"), "effect", out var firstEffect), "ApplyEffect.effect is missing.");
            Require(TryGetMember(ReadPayload(commands[4]!, "applyEffect"), "effect", out var secondEffect), "ApplyEffect.effect is missing.");
            Require(TryGetMember(ReadPayload(commands[5]!, "applyEffect"), "effect", out var nullEffect), "ApplyEffect.effect is missing.");
            snapshot.effectType = firstEffect?.GetType().FullName ?? "";
            snapshot.effectAmount = firstEffect is null ? 0 : ReadInt(firstEffect, "amount");
            snapshot.sharedManagedEffect = ReferenceEquals(firstEffect, secondEffect);
            snapshot.nullManagedEffect = nullEffect is null;

            for (var index = 0; index < commands.Count; index++)
            {
                snapshot.commandKinds[index] = ReadKind(commands[index]!);
            }

            return snapshot;
        }

        private static void ValidateRuntimeSnapshot(RuntimeSnapshot runtime, string label)
        {
            Check(label + ".selectedCommand.kind", runtime.selectedKind == "Move", "expected Move, got " + runtime.selectedKind);
            Check(
                label + ".selectedCommand.destination",
                Approximately(runtime.selectedDestination.x, 3f) && Approximately(runtime.selectedDestination.y, 4f),
                "expected (3, 4), got " + runtime.selectedDestination);
            Check(label + ".selectedCommand.speed", Approximately(runtime.selectedSpeed, 2f), "expected 2, got " + runtime.selectedSpeed);
            Check(label + ".commands.count", runtime.commandCount == 6, "expected 6, got " + runtime.commandCount);
            Check(
                label + ".commands.kinds",
                (runtime.commandKinds ?? Array.Empty<string>()).SequenceEqual(new[] { "Idle", "Move", "Speak", "ApplyEffect", "ApplyEffect", "ApplyEffect" }),
                "unexpected command kind sequence");
            Check(label + ".managedReferences.shared", runtime.sharedManagedEffect, "commands[3] and commands[4] do not share one Effect instance");
            Check(label + ".managedReferences.null", runtime.nullManagedEffect, "commands[5] did not preserve a null Effect");
            Check(
                label + ".managedReferences.damage",
                runtime.effectType.EndsWith("DamageEffect", StringComparison.Ordinal) && runtime.effectAmount == 10,
                "expected DamageEffect(amount: 10), got " + runtime.effectType + "(" + runtime.effectAmount + ")");
        }

        private static void ValidateImportedResources(AssetSnapshot snapshot)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .SingleOrDefault(candidate => candidate.GetName().Name == "Houtamelo.Spire.Showcase");
            Check("imported.sample.assembly", assembly is not null, "Houtamelo.Spire.Showcase was not loaded after import");
            if (assembly is not null)
            {
                Check(
                    "imported.sample.GameplayCommand",
                    assembly.GetType("Houtamelo.Spire.Showcase.GameplayCommand") is not null,
                    "GameplayCommand is missing from Houtamelo.Spire.Showcase");
                Check(
                    "imported.sample.ShowcasePreset",
                    assembly.GetType("Houtamelo.Spire.Showcase.ShowcasePreset") is not null,
                    "ShowcasePreset is missing from Houtamelo.Spire.Showcase");
                Check(
                    "imported.sample.presetType",
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(snapshot.presetPath)!.GetType().FullName ==
                    "Houtamelo.Spire.Showcase.ShowcasePreset",
                    "resolved preset has an unexpected runtime type");
            }

            Check(
                "imported.sample.scene",
                AssetDatabase.LoadAssetAtPath<SceneAsset>(snapshot.scenePath) is not null,
                "resolved scene asset could not be loaded");
            Check(
                "imported.sample.preset",
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(snapshot.presetPath) is not null,
                "resolved preset asset could not be loaded");
        }

        private static void ValidateRuntimeDocumentation()
        {
            var package = TryFindRegisteredPackage();
            Require(package is not null, "The registered Spire package disappeared while validating documentation.");
            var packageRoot = NormalizePath(package!.resolvedPath);
            var candidates = new[]
            {
                Path.Combine(packageRoot, "Runtime", "Houtamelo.Spire.xml"),
                Path.Combine(packageRoot, "Houtamelo.Spire.xml"),
            };
            var documentationPath = candidates.FirstOrDefault(File.Exists);
            Check("package.xml", documentationPath is not null, "Houtamelo.Spire.xml is missing from the resolved package");
            if (documentationPath is null)
                return;

            var documentation = File.ReadAllText(documentationPath);
            Check("package.xml.members", documentation.Contains("<member", StringComparison.Ordinal), "XML documentation has no member entries");
            Check(
                "package.xml.publicApi",
                documentation.Contains("EnforceInitializationAttribute", StringComparison.Ordinal),
                "XML documentation does not contain the public Spire API");
            _report!.documentationPath = documentationPath;
        }

        private static void CompareSnapshots(AssetSnapshot baseline, AssetSnapshot current)
        {
            Check("upgrade.sceneGuid", baseline.sceneGuid == current.sceneGuid, "scene GUID changed");
            Check("upgrade.presetGuid", baseline.presetGuid == current.presetGuid, "preset GUID changed");
            Check("upgrade.sceneHash", baseline.sceneHash == current.sceneHash, "scene bytes changed");
            Check("upgrade.presetHash", baseline.presetHash == current.presetHash, "preset bytes changed");
            CompareRuntime("upgrade.runtime", baseline.runtime, current.runtime);
            CompareFileHashes(baseline.files, current.files, "upgrade.authoredAssets");
            _report!.checks.Add("PASS upgrade.authoredAssets: all tracked scene, preset, source, and meta hashes match.");
        }

        private static void CompareRuntime(string label, RuntimeSnapshot expected, RuntimeSnapshot actual)
        {
            Check(label + ".selectedKind", expected.selectedKind == actual.selectedKind, "selected command kind changed");
            Check(label + ".selectedDestination", expected.selectedDestination == actual.selectedDestination, "selected destination changed");
            Check(label + ".selectedSpeed", Approximately(expected.selectedSpeed, actual.selectedSpeed), "selected speed changed");
            Check(label + ".commandCount", expected.commandCount == actual.commandCount, "command count changed");
            Check(
                label + ".commandKinds",
                (expected.commandKinds ?? Array.Empty<string>()).SequenceEqual(actual.commandKinds ?? Array.Empty<string>()),
                "command kind sequence changed");
            Check(label + ".managedReferenceType", expected.effectType == actual.effectType, "managed-reference type changed");
            Check(label + ".managedReferenceAmount", expected.effectAmount == actual.effectAmount, "managed-reference payload changed");
            Check(label + ".managedReferenceSharing", expected.sharedManagedEffect == actual.sharedManagedEffect, "managed-reference sharing changed");
            Check(label + ".managedReferenceNull", expected.nullManagedEffect == actual.nullManagedEffect, "managed-reference null changed");
        }

        private static void CompareFileHashes(AssetFileSnapshot[]? expected, AssetFileSnapshot[]? actual, string label)
        {
            var expectedMap = (expected ?? Array.Empty<AssetFileSnapshot>())
                .ToDictionary(file => file.relativePath, file => file.sha256, StringComparer.Ordinal);
            var actualMap = (actual ?? Array.Empty<AssetFileSnapshot>())
                .ToDictionary(file => file.relativePath, file => file.sha256, StringComparer.Ordinal);
            var differences = expectedMap.Keys
                .Union(actualMap.Keys, StringComparer.Ordinal)
                .Where(path => !expectedMap.TryGetValue(path, out var expectedHash) ||
                               !actualMap.TryGetValue(path, out var actualHash) ||
                               expectedHash != actualHash)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Take(20)
                .ToArray();
            Check(label, differences.Length == 0, differences.Length == 0 ? "all hashes match" : string.Join(", ", differences));
        }

        private static AssetFileSnapshot[] CaptureFileHashes(string root)
        {
            var fullRoot = ProjectFile(root);
            if (!Directory.Exists(fullRoot))
                return Array.Empty<AssetFileSnapshot>();

            return Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new AssetFileSnapshot
                {
                    relativePath = NormalizePath(path).Substring(NormalizePath(fullRoot).Length).TrimStart('/'),
                    sha256 = HashFile(path),
                })
                .ToArray();
        }

        private static string? TryResolveSampleRoot()
        {
            var assemblyDefinitionPath = AssetDatabase.GUIDToAssetPath(RuntimeAssemblyGuid);
            if (string.IsNullOrEmpty(assemblyDefinitionPath))
                return null;

            var definitionDirectory = Path.GetDirectoryName(assemblyDefinitionPath.Replace('/', Path.DirectorySeparatorChar));
            var root = definitionDirectory is null
                ? null
                : Path.GetDirectoryName(definitionDirectory);
            return root is null || root.Length == 0 ? null : root.Replace(Path.DirectorySeparatorChar, '/');
        }

        private static bool RequireRegisteredPackage()
        {
            var package = TryFindRegisteredPackage();
            if (package is null)
            {
                Fail("Package " + PackageName + " is not registered in this project.");
                return false;
            }

            _report!.packageVersion = package.version;
            _report.resolvedPackage = NormalizePath(package.resolvedPath);
            _report.packageSource = package.source.ToString();
            return true;
        }

        private static UnityEditor.PackageManager.PackageInfo? TryFindRegisteredPackage()
        {
            try
            {
                return UnityEditor.PackageManager.PackageInfo.FindForPackageName(PackageName);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryReadInputs(out string packagePath, out string phase)
        {
            packagePath = NormalizePath(Environment.GetEnvironmentVariable(PackagePathEnvironmentVariable) ?? "");
            phase = (Environment.GetEnvironmentVariable(PhaseEnvironmentVariable) ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(packagePath))
            {
                Fail("SPIRE_CUSTOMER_PACKAGE is required.");
                return false;
            }

            if (phase is not ("baseline" or "candidate"))
            {
                Fail("SPIRE_CUSTOMER_PHASE must be baseline or candidate.");
                return false;
            }

            _report!.requestedPackage = packagePath;
            _report.phase = phase;
            return true;
        }

        private static void StartOperation(string operation)
        {
            UnregisterPoll();
            DeleteStage();
            _addRequest = null;
            _exitRequested = false;
            _report = NewReport(operation, Environment.GetEnvironmentVariable(PhaseEnvironmentVariable) ?? "");
            WriteReport();
        }

        private static AcceptanceReport NewReport(string operation, string phase)
            => new()
            {
                operation = operation,
                phase = phase,
                status = "running",
                unityVersion = Application.unityVersion,
                startedUtc = DateTime.UtcNow.ToString("O"),
            };

        private static AcceptanceStage NewStage(string operation, string stage, string? packagePath)
            => new()
            {
                operation = operation,
                stage = stage,
                phase = _report?.phase ?? "",
                requestedPackage = packagePath ?? "",
                startedUtcTicks = DateTime.UtcNow.Ticks,
            };

        private static void Complete(string message)
        {
            if (_report is null || _exitRequested)
                return;

            _exitRequested = true;
            UnregisterPoll();
            _report.status = "pass";
            _report.success = true;
            _report.message = message;
            _report.details = message;
            _report.stage = "complete";
            _report.completedUtc = DateTime.UtcNow.ToString("O");
            WriteReport();
            DeleteStage();
            Debug.Log("SPIRE_CUSTOMER_ACCEPTANCE_PASS " + message);
            EditorApplication.Exit(0);
        }

        private static void Fail(string message)
        {
            if (_report is null || _exitRequested)
                return;

            _exitRequested = true;
            UnregisterPoll();
            _report.status = "fail";
            _report.success = false;
            _report.message = message;
            _report.error = message;
            _report.details = message;
            _report.stage = _stage?.stage ?? "failed";
            _report.completedUtc = DateTime.UtcNow.ToString("O");
            _report.failures.Add(message);
            WriteReport();
            DeleteStage();
            Debug.LogError("SPIRE_CUSTOMER_ACCEPTANCE_FAIL " + message);
            EditorApplication.Exit(1);
        }

        private static void Check(string name, bool condition, string detail)
        {
            if (_report is null)
                throw new InvalidOperationException("No active acceptance report.");

            _report.checks.Add((condition ? "PASS " : "FAIL ") + name + ": " + detail);
            if (!condition)
                throw new InvalidOperationException(name + ": " + detail);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void SaveStageAndReport()
        {
            if (_stage is not null)
            {
                _stage.stage = _stage.stage.Trim();
                Directory.CreateDirectory(Path.GetDirectoryName(StageFile) ?? ProjectRoot);
                File.WriteAllText(StageFile, JsonUtility.ToJson(_stage, true));
                if (_report is not null)
                    _report.stage = _stage.stage;
            }

            WriteReport();
        }

        private static void WriteReport()
        {
            if (_report is null)
                return;

            Directory.CreateDirectory(ProjectRoot);
            File.WriteAllText(ReportFile, JsonUtility.ToJson(_report, true));
        }

        private static void WriteBaselineSnapshot(AssetSnapshot snapshot)
        {
            var json = JsonUtility.ToJson(snapshot, true);
            File.WriteAllText(Path.Combine(ProjectRoot, BaselineSnapshotName), json);
        }

        private static AssetSnapshot ReadBaselineSnapshot()
        {
            var path = Path.Combine(ProjectRoot, BaselineSnapshotName);
            Require(File.Exists(path), "Baseline capture is missing: expected " + BaselineSnapshotName + " in the project root.");
            var snapshot = JsonUtility.FromJson<AssetSnapshot>(File.ReadAllText(path));
            Require(snapshot is not null, "Baseline capture is not valid JSON: " + path);
            return snapshot!;
        }

        private static AcceptanceStage? ReadStage()
        {
            if (!File.Exists(StageFile))
                return null;

            try
            {
                return JsonUtility.FromJson<AcceptanceStage>(File.ReadAllText(StageFile));
            }
            catch (Exception exception)
            {
                Debug.LogError("SPIRE_CUSTOMER_ACCEPTANCE_FAIL Invalid stage file: " + exception);
                return null;
            }
        }

        private static AcceptanceReport? ReadReport()
        {
            if (!File.Exists(ReportFile))
                return null;

            try
            {
                return JsonUtility.FromJson<AcceptanceReport>(File.ReadAllText(ReportFile));
            }
            catch
            {
                return null;
            }
        }

        private static void DeleteStage()
        {
            if (File.Exists(StageFile))
                File.Delete(StageFile);
        }

        private static bool TryGetMember(object target, string name, out object? value)
        {
            for (var type = target.GetType(); type is not null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field is not null)
                {
                    value = field.GetValue(target);
                    return true;
                }

                var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property is not null && property.GetMethod is not null)
                {
                    value = property.GetValue(target, null);
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static string ReadKind(object value)
        {
            Require(TryGetMember(value, "kind", out var kind), "A generated union value has no kind member.");
            return kind?.ToString() ?? "";
        }

        private static Vector2 ReadVector2(object value, string name)
        {
            Require(TryGetMember(value, name, out var member) && member is Vector2, "Generated Move payload has no Vector2 " + name + " member.");
            return (Vector2)member!;
        }

        private static object ReadPayload(object union, string variant)
        {
            Require(TryGetMember(union, variant, out var payload) && payload is not null,
                "Generated union has no " + variant + " payload accessor.");
            return payload!;
        }

        private static float ReadFloat(object value, string name)
        {
            Require(TryGetMember(value, name, out var member), "Generated payload has no " + name + " member.");
            return Convert.ToSingle(member);
        }

        private static int ReadInt(object value, string name)
        {
            Require(TryGetMember(value, name, out var member), "Managed-reference payload has no " + name + " member.");
            return Convert.ToInt32(member);
        }

        private static string ProjectFile(string assetPath)
            => Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));

        private static string HashFile(string path)
        {
            using var hash = SHA256.Create();
            return ToHex(hash.ComputeHash(File.ReadAllBytes(path)));
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes)
                builder.Append(value.ToString("x2"));
            return builder.ToString();
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "";

            var value = path.Trim();
            if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                value = value.Substring("file:".Length);
            if (value.StartsWith("//", StringComparison.Ordinal))
                value = value.Substring(2);
            if (Path.IsPathRooted(value))
                value = Path.GetFullPath(value);
            value = value.Replace('\\', '/');
            while (value.Length > 1 && value.EndsWith("/", StringComparison.Ordinal))
                value = value.Substring(0, value.Length - 1);
            return value;
        }

        private static bool PathsEqual(string left, string right)
            => string.Equals(
                NormalizePath(left),
                NormalizePath(right),
                Application.platform == RuntimePlatform.WindowsEditor
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);

        private static bool Approximately(float left, float right)
            => Mathf.Abs(left - right) <= 0.0001f;

        private static string ProjectRoot
            => Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;

        private static string ReportFile
            => Path.Combine(ProjectRoot, ReportName);

        private static string StageFile
            => Path.Combine(ProjectRoot, StageName.Replace('/', Path.DirectorySeparatorChar));

        [Serializable]
        private sealed class AcceptanceReport
        {
            public string operation = "";
            public string phase = "";
            public string status = "";
            public bool success;
            public string stage = "";
            public string message = "";
            public string error = "";
            public string details = "";
            public string unityVersion = "";
            public string requestedPackage = "";
            public string resolvedPackage = "";
            public string packageVersion = "";
            public string packageSource = "";
            public string sampleRoot = "";
            public string sampleDisplayName = "";
            public string sampleImportPath = "";
            public string sampleResolvedPath = "";
            public string documentationPath = "";
            public string startedUtc = "";
            public string completedUtc = "";
            public AssetSnapshot? baseline;
            public AssetSnapshot? observed;
            public List<string> checks = new();
            public List<string> failures = new();
        }

        [Serializable]
        private sealed class AcceptanceStage
        {
            public string operation = "";
            public string stage = "";
            public string phase = "";
            public string requestedPackage = "";
            public string lastError = "";
            public long startedUtcTicks;
        }

        [Serializable]
        private sealed class AssetSnapshot
        {
            public string sampleRoot = "";
            public string scenePath = "";
            public string presetPath = "";
            public string sceneGuid = "";
            public string presetGuid = "";
            public string sceneHash = "";
            public string presetHash = "";
            public RuntimeSnapshot runtime = new();
            public AssetFileSnapshot[] files = Array.Empty<AssetFileSnapshot>();
        }

        [Serializable]
        private sealed class RuntimeSnapshot
        {
            public string selectedKind = "";
            public Vector2 selectedDestination;
            public float selectedSpeed;
            public int commandCount;
            public string[] commandKinds = Array.Empty<string>();
            public string effectType = "";
            public int effectAmount;
            public bool sharedManagedEffect;
            public bool nullManagedEffect;
        }

        [Serializable]
        private sealed class AssetFileSnapshot
        {
            public string relativePath = "";
            public string sha256 = "";
        }
    }
}
