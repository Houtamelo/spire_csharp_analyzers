using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Spire.UnityAssetPackageAcceptance
{
    public static class AssetPackageAcceptance
    {
        private const string AssetRoot = "Assets/Spire";
        private const string RuntimeDll = AssetRoot + "/Runtime/Houtamelo.Spire.dll";
        private const string FunctionalRuntimeDll = AssetRoot + "/Runtime/Houtamelo.Spire.Functional.dll";
        private static readonly string[] CompilerDlls =
        {
            "Houtamelo.Spire.Analyzers",
            "Houtamelo.Spire.PatternAnalysis",
            "Houtamelo.Spire.CodeFixes",
        };

        [Serializable]
        private sealed class Report
        {
            public bool success;
            public string unityVersion;
            public string importedRoot;
            public int checks;
            public List<string> failures = new List<string>();
            public List<string> passed = new List<string>();
        }

        [MenuItem("Spire/Verify imported unitypackage")]
        public static void VerifyImportedPackage()
        {
            var root = Directory.GetParent(Application.dataPath).FullName;
            var report = new Report
            {
                unityVersion = Application.unityVersion,
                importedRoot = AssetRoot,
            };

            Check(report, "Runtime and compiler plugin import settings", CheckPluginImporters);
            Check(report, "Compiler labels and offline documentation", CheckAssetSurface);
            Check(report, "Generated Unity drawer is present", CheckGeneratedDrawer);
            Check(report, "Generated probe APIs and wrong-variant guard", CheckGeneratedProbe);
            Check(report, "Functional Result and Option runtime API", CheckFunctionalRuntime);
            Check(report, "Native Unity JSON round-trip", CheckNativeSerialization);

            report.success = report.failures.Count == 0;
            File.WriteAllText(
                Path.Combine(root, "unitypackage-verify.json"),
                JsonUtility.ToJson(report, true));
            Debug.Log("SPIRE_UNITYPACKAGE_ACCEPTANCE " + report.success + " checks=" + report.checks);
            EditorApplication.Exit(report.success ? 0 : 1);
        }

        public static void Verify() => VerifyImportedPackage();

        private static void Check(Report report, string name, Action action)
        {
            report.checks++;
            try
            {
                action();
                report.passed.Add(name);
            }
            catch (Exception error)
            {
                report.failures.Add(name + ": " + error);
            }
        }

        private static void CheckPluginImporters()
        {
            var runtime = AssetImporter.GetAtPath(RuntimeDll) as PluginImporter;
            Ensure(runtime != null, "The runtime DLL importer was not found.");
            Ensure(runtime.GetCompatibleWithAnyPlatform(), "The runtime DLL is not enabled for Any Platform.");
            Ensure(runtime.GetCompatibleWithEditor(), "The runtime DLL is not enabled for the Editor.");

            var functional = AssetImporter.GetAtPath(FunctionalRuntimeDll) as PluginImporter;
            Ensure(functional != null, "The Functional runtime DLL importer was not found.");
            Ensure(functional.GetCompatibleWithAnyPlatform(), "The Functional runtime DLL is not enabled for Any Platform.");
            Ensure(functional.GetCompatibleWithEditor(), "The Functional runtime DLL is not enabled for the Editor.");

            foreach (var name in CompilerDlls)
            {
                var path = AssetRoot + "/Analyzers/" + name + ".dll";
                var importer = AssetImporter.GetAtPath(path) as PluginImporter;
                Ensure(importer != null, "Compiler DLL importer was not found: " + name);
                Ensure(!importer.GetCompatibleWithAnyPlatform(), "Compiler DLL is enabled for Any Platform: " + name);
                Ensure(!importer.GetCompatibleWithEditor(), "Compiler DLL is enabled for the Editor: " + name);
            }
        }

        private static void CheckAssetSurface()
        {
            foreach (var name in CompilerDlls)
            {
                var path = AssetRoot + "/Analyzers/" + name + ".dll";
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                Ensure(asset != null, "Compiler DLL asset was not imported: " + name);
                Ensure(AssetDatabase.GetLabels(asset).Contains("RoslynAnalyzer"),
                    "Compiler DLL lost its RoslynAnalyzer label: " + name);
            }

            CheckDeterministicPluginGuids();

            foreach (var path in new[]
            {
                AssetRoot + "/README.txt",
                AssetRoot + "/LICENSE.md",
                AssetRoot + "/Third Party Notices.md",
                AssetRoot + "/CHANGELOG.md",
                AssetRoot + "/Documentation/GettingStarted.md",
                AssetRoot + "/Runtime/Houtamelo.Spire.Functional.xml",
            })
                Ensure(File.Exists(Path.GetFullPath(path)), "Imported documentation file is missing: " + path);

            Ensure(!Directory.Exists(Path.GetFullPath(AssetRoot + "/Samples~")),
                "A UPM Samples~ directory leaked into the .unitypackage export.");
            Ensure(!File.Exists(Path.GetFullPath(AssetRoot + "/package.json")),
                "The UPM package manifest leaked into the .unitypackage export.");
        }

        private static void CheckDeterministicPluginGuids()
        {
            var relativePaths = new List<string>
            {
                "Runtime/Houtamelo.Spire.dll",
                "Runtime/Houtamelo.Spire.Functional.dll",
            };
            relativePaths.AddRange(CompilerDlls.Select(name => "Analyzers/" + name + ".dll"));
            foreach (var relative in relativePaths)
            {
                var metadataPath = Path.GetFullPath(AssetRoot + "/" + relative + ".meta");
                Ensure(File.Exists(metadataPath), "Imported plugin metadata is missing: " + relative);
                var guid = File.ReadAllLines(metadataPath)
                    .SingleOrDefault(line => line.StartsWith("guid: ", StringComparison.Ordinal));
                Ensure(guid != null, "Imported plugin metadata has no GUID: " + relative);
                var expected = Sha256("Houtamelo.Spire.Unity/" + relative).Substring(0, 32);
                Ensure(guid.Substring("guid: ".Length) == expected,
                    "Imported plugin GUID changed: " + relative);
            }
        }

        private static void CheckGeneratedDrawer()
        {
            var command = FindType("Spire.AssetPackageProbe.Command");
            Ensure(command != null, "The positive probe Command type was not compiled.");
            var assembly = command.Assembly;
            var drawer = assembly.GetTypes().FirstOrDefault(type =>
                type.Name.IndexOf("__SpireUnityPropertyDrawer", StringComparison.Ordinal) >= 0 &&
                type.Name.IndexOf("Command", StringComparison.Ordinal) >= 0);
            Ensure(drawer != null, "The generated drawer for Command was not compiled.");
            Ensure(typeof(UnityEditor.PropertyDrawer).IsAssignableFrom(drawer),
                "The generated Command drawer does not derive from PropertyDrawer.");
            Ensure(!drawer.IsAbstract, "The generated Command drawer is abstract.");
        }

        private static void CheckGeneratedProbe()
        {
            var command = FindType("Spire.AssetPackageProbe.Command");
            var probe = FindType("Spire.AssetPackageProbe.ProbeComponent");
            Ensure(command != null && probe != null, "The positive probe types were not compiled.");

            var makeMove = probe.GetMethod("MakeMove", BindingFlags.Public | BindingFlags.Static);
            var describe = probe.GetMethod("Describe", BindingFlags.Public | BindingFlags.Static);
            Ensure(makeMove != null && describe != null, "The positive probe methods were not found.");
            var move = makeMove.Invoke(null, null);
            var description = (string)describe.Invoke(null, new[] { move });
            Ensure(description == "Move:3,4,5:2.5", "Generated API returned the wrong move payload: " + description);

            var speak = command.GetMethod("Speak", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { "Imported" });
            var wrongAccessor = command.GetProperty("move", BindingFlags.Public | BindingFlags.Instance);
            Ensure(wrongAccessor != null, "The generated move accessor was not found.");
            var rejected = false;
            Application.LogCallback callback = (message, stack, type) =>
            {
                if (type == LogType.Assert &&
                    message.Contains("Accessing the `Move` variant's fields") &&
                    message.Contains("Actual kind: Speak"))
                    rejected = true;
            };
            Application.logMessageReceived += callback;
            try
            {
                wrongAccessor.GetValue(speak);
            }
            finally
            {
                Application.logMessageReceived -= callback;
            }
            Ensure(rejected, "Wrong-variant payload access did not emit the generated Unity assertion.");
        }

        private static void CheckFunctionalRuntime()
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(value => value.GetName().Name == "Houtamelo.Spire.Functional");
            Ensure(assembly != null, "The Houtamelo.Spire.Functional assembly was not loaded.");

            var probe = FindType("Spire.AssetPackageProbe.FunctionalProbe");
            var exercise = probe?.GetMethod("ExerciseFunctional", BindingFlags.Public | BindingFlags.Static);
            Ensure(exercise != null, "The Functional source probe was not compiled.");
            Ensure((int)exercise.Invoke(null, null) == 44,
                "The Functional source probe returned the wrong result.");

            var resultType = assembly.GetType("Houtamelo.Spire.Functional.Result`1")
                ?.MakeGenericType(typeof(int));
            Ensure(resultType != null, "The Functional Result<T> type was not found.");
            var okFactory = resultType.GetMethod(
                "Ok",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(int) },
                modifiers: null);
            Ensure(okFactory != null, "The Functional Result<T>.Ok factory was not found.");
            var result = okFactory.Invoke(null, new object[] { 42 });
            Ensure(result != null, "Result<T>.Ok returned null.");
            Ensure((bool)resultType.GetProperty("IsOk").GetValue(result), "Result<T>.Ok was not successful.");
            Ensure((int)resultType.GetMethod("unwrap").Invoke(result, null) == 42,
                "Result<T>.unwrap returned the wrong value.");

            var optionType = assembly.GetType("Houtamelo.Spire.Functional.OptionImpl");
            Ensure(optionType != null, "The Functional OptionImpl type was not found.");
            var someFactory = optionType.GetMethod("Some", BindingFlags.Public | BindingFlags.Static)
                ?.MakeGenericMethod(typeof(int));
            var noneFactory = optionType.GetMethod("None", BindingFlags.Public | BindingFlags.Static)
                ?.MakeGenericMethod(typeof(int));
            Ensure(someFactory != null && noneFactory != null, "The Functional Option factories were not found.");
            var some = someFactory.Invoke(null, new object[] { 7 });
            var none = noneFactory.Invoke(null, null);
            Ensure(some is int && (int)some == 7, "OptionImpl.Some returned the wrong value.");
            Ensure(none == null, "OptionImpl.None returned a value.");
        }

        private static void CheckNativeSerialization()
        {
            var probe = FindType("Spire.AssetPackageProbe.ProbeComponent");
            var command = FindType("Spire.AssetPackageProbe.Command");
            Ensure(probe != null && command != null, "The positive probe types were not compiled.");
            var makeSpeak = probe.GetMethod("MakeSpeak", BindingFlags.Public | BindingFlags.Static);
            var makeIdle = probe.GetMethod("MakeIdle", BindingFlags.Public | BindingFlags.Static);
            var describe = probe.GetMethod("Describe", BindingFlags.Public | BindingFlags.Static);
            var field = probe.GetField("command", BindingFlags.NonPublic | BindingFlags.Instance);
            Ensure(makeSpeak != null && makeIdle != null && describe != null && field != null,
                "The serializable probe surface was not found.");

            var gameObject = new GameObject("Spire unitypackage acceptance");
            try
            {
                var component = gameObject.AddComponent(probe);
                field.SetValue(component, makeSpeak.Invoke(null, null));
                var serialized = EditorJsonUtility.ToJson(component);
                field.SetValue(component, makeIdle.Invoke(null, null));
                EditorJsonUtility.FromJsonOverwrite(serialized, component);
                var restored = field.GetValue(component);
                var description = (string)describe.Invoke(null, new[] { restored });
                Ensure(description == "Speak:Imported", "Native Unity JSON restored the wrong probe value: " + description);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null)
                    return type;
            }
            return null;
        }

        private static void Ensure(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static string Sha256(string text)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text)))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
        }
    }
}
