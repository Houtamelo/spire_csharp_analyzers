using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Spire.UnityTarballAcceptance;

public static class TarballAcceptance
{
    [Serializable]
    private sealed class Inputs
    {
        public string artifactSha256;
        public string version;
        public bool allowUnsupportedSignature;
        public FileInput[] files;
    }

    [Serializable]
    private sealed class FileInput
    {
        public string path;
        public string sha256;
    }

    [Serializable]
    private sealed class Report
    {
        public bool success;
        public string unityVersion;
        public string packageSource;
        public string packageVersion;
        public string signatureStatus;
        public bool signatureVerified;
        public string signatureReason;
        public string installedManifest;
        public string samplePath;
        public int verifiedFiles;
        public int generatedApiChecks;
        public string error;
        public List<string> checks = new List<string>();
    }

    public static void Install() => Run(false);
    public static void Verify() => Run(true);

    private static void Run(bool verify)
    {
        var root = Directory.GetParent(Application.dataPath).FullName;
        var report = new Report { unityVersion = Application.unityVersion };
        try
        {
            var input = JsonUtility.FromJson<Inputs>(File.ReadAllText(Path.Combine(root, "tarball-inputs.json")));
            var package = PackageInfo.FindForPackageName("individual.antonio-pedro-goncalves-ferreira.spire");
            Require(package != null, "Package Manager registered Spire", report);
            report.packageSource = package.source.ToString();
            report.packageVersion = package.version;
            Require(report.packageSource == "LocalTarball", "Source is LocalTarball", report);
            Require(package.version == input.version, "Package version matches artifact", report);
            var signatureProperty = typeof(PackageInfo).GetProperty("signature", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (signatureProperty == null)
            {
                report.signatureStatus = "Unsupported";
                Require(input.allowUnsupportedSignature && int.Parse(Application.unityVersion.Split('.')[0]) < 6000,
                    "Signature inspection can only be omitted explicitly on older Editors", report);
            }
            else
            {
                var signature = signatureProperty.GetValue(package);
                Require(signature != null, "Package signature information is present", report);
                report.signatureStatus = signature.GetType().GetProperty("status").GetValue(signature).ToString();
                report.signatureReason = signature.GetType().GetProperty("reason").GetValue(signature) as string;
                report.signatureVerified = report.signatureStatus == "Valid";
                var legacy = input.allowUnsupportedSignature && int.Parse(Application.unityVersion.Split('.')[0]) < 6000 &&
                    (report.signatureStatus == "Unsigned" || report.signatureStatus == "Unchecked");
                Require(report.signatureVerified || legacy,
                    legacy ? "Older Editor does not verify the modern signature; artifact hashes remain required" : "Unity validates the package signature", report);
            }
            Require(package.errors.Length == 0, "Package Manager reports no package errors", report);
            Require(input.files != null && input.files.Length > 0, "Independent artifact inventory is present", report);
            foreach (var file in input.files)
            {
                var installed = Path.Combine(package.resolvedPath, file.path);
                Require(File.Exists(installed), "Installed file exists: " + file.path, report);
                if (file.path == "package.json")
                    report.installedManifest = File.ReadAllText(installed);
                else
                    Require(Hash(installed) == file.sha256, "Installed bytes match artifact: " + file.path, report);
                report.verifiedFiles++;
            }
            foreach (var name in new[] { "Houtamelo.Spire.Analyzers", "Houtamelo.Spire.PatternAnalysis", "Houtamelo.Spire.CodeFixes" })
            {
                var asset = "Packages/individual.antonio-pedro-goncalves-ferreira.spire/Analyzers/" + name + ".dll";
                var importer = AssetImporter.GetAtPath(asset) as PluginImporter;
                Require(importer != null && !importer.GetCompatibleWithAnyPlatform() && !importer.GetCompatibleWithEditor(),
                    "Compiler DLL excluded from plugin imports: " + name, report);
                Require(AssetDatabase.GetLabels(AssetDatabase.LoadMainAssetAtPath(asset)).Contains("RoslynAnalyzer"),
                    "Compiler DLL retains RoslynAnalyzer: " + name, report);
            }
            var sample = Sample.FindByPackage(package.name, package.version).Single(item => item.displayName == "Quick Start");
            report.samplePath = sample.importPath;
            if (!verify)
            {
                Require(!sample.isImported, "Quick Start has not already been imported", report);
                Require(sample.Import(), "Package Manager imports Quick Start", report);
            }
            else
            {
                Require(sample.isImported, "Package Manager records Quick Start import", report);
                foreach (var file in input.files.Where(item => item.path.StartsWith("Samples~/QuickStart/", StringComparison.Ordinal)))
                {
                    var imported = Path.Combine(sample.importPath, file.path.Substring("Samples~/QuickStart/".Length));
                    Require(File.Exists(imported), "Sample file imported: " + file.path, report);
                    if (file.path.EndsWith(".meta", StringComparison.Ordinal))
                        Require(GuidLine(imported) == GuidLine(Path.Combine(package.resolvedPath, file.path)), "Sample GUID preserved: " + file.path, report);
                    else
                        Require(Hash(imported) == file.sha256, "Sample bytes match artifact: " + file.path, report);
                }
                var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(item => item.GetName().Name == "Houtamelo.Spire.QuickStart");
                var command = assembly.GetType("Houtamelo.Spire.QuickStart.Command", true);
                var component = assembly.GetType("Houtamelo.Spire.QuickStart.QuickStart", true);
                var idle = command.GetMethod("Idle").Invoke(null, null);
                var move = command.GetMethod("Move").Invoke(null, new object[] { new Vector2(2, 3), 4f });
                var speak = command.GetMethod("Speak").Invoke(null, new object[] { "Signed sample" });
                var describe = component.GetMethod("Describe");
                Require((string)describe.Invoke(null, new[] { idle }) == "Idle", "Generated empty variant is usable", report);
                Require((string)describe.Invoke(null, new[] { move }) == "Move at speed 4", "Generated value payload is usable", report);
                Require((string)describe.Invoke(null, new[] { speak }) == "Signed sample", "Generated reference payload is usable", report);
                report.generatedApiChecks = 3;
                var rejected = false;
                Application.LogCallback assertion = (message, stack, type) =>
                {
                    if (type == LogType.Assert && message.Contains("Accessing the `Speak` variant's fields") && message.Contains("Actual kind: Move"))
                        rejected = true;
                };
                Application.logMessageReceived += assertion;
                try { command.GetProperty("speak").GetValue(move); }
                finally { Application.logMessageReceived -= assertion; }
                Require(rejected, "Wrong-variant accessor emits the generated Unity assertion", report);
                report.generatedApiChecks++;
                var go = new GameObject("Spire Quick Start persistence");
                try
                {
                    var instance = go.AddComponent(component);
                    var field = component.GetField("command", BindingFlags.Instance | BindingFlags.NonPublic);
                    field.SetValue(instance, speak);
                    var json = EditorJsonUtility.ToJson(instance);
                    field.SetValue(instance, idle);
                    EditorJsonUtility.FromJsonOverwrite(json, instance);
                    Require((string)describe.Invoke(null, new[] { field.GetValue(instance) }) == "Signed sample",
                        "Imported component round-trips native Unity JSON serialization", report);
                    report.generatedApiChecks++;
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            report.success = true;
        }
        catch (Exception error) { report.error = error.ToString(); }
        File.WriteAllText(Path.Combine(root, verify ? "tarball-verify.json" : "tarball-install.json"), JsonUtility.ToJson(report, true));
        Debug.Log("SPIRE_TARBALL_ACCEPTANCE " + report.success + " signature=" + report.signatureStatus + " files=" + report.verifiedFiles);
        EditorApplication.Exit(report.success ? 0 : 1);
    }

    private static string Hash(string path)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }

    private static string GuidLine(string path) => File.ReadAllLines(path).Single(line => line.StartsWith("guid: ", StringComparison.Ordinal));

    private static void Require(bool condition, string message, Report report)
    {
        if (!condition) throw new InvalidOperationException(message);
        report.checks.Add(message);
    }
}
