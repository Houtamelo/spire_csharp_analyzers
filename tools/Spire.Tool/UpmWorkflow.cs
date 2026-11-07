using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Spire.Tool;

internal static class UpmWorkflow
{
    private static readonly string[] CredentialNames =
        ["UPM_SERVICE_ACCOUNT_KEY_ID", "UPM_SERVICE_ACCOUNT_KEY_SECRET", "UPM_ORGANIZATION_ID"];

    internal static void Tamper(ToolContext context, string archive, string output)
    {
        archive = context.RequireOutsideExamples(archive);
        output = context.RequireOutsideExamples(output);
        ReadArchive(archive);
        using var source = File.OpenRead(archive);
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var buffer = new MemoryStream();
        gzip.CopyTo(buffer);
        var bytes = buffer.ToArray();
        var changed = false;
        for (var offset = 0; offset + 512 <= bytes.Length && bytes[offset] != 0;)
        {
            var name = Encoding.UTF8.GetString(bytes, offset, 100).TrimEnd('\0');
            var size = Convert.ToInt64(Encoding.ASCII.GetString(bytes, offset + 124, 12).Trim('\0', ' '), 8);
            if (name == "package/README.md")
            {
                Require(size > 0, "README is empty.");
                bytes[offset + 512] ^= 1;
                changed = true;
                break;
            }
            offset = checked(offset + 512 + (int)((size + 511) / 512 * 512));
        }
        Require(changed, "No README payload found to tamper.");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        using var destination = new FileStream(output, FileMode.CreateNew);
        using var compressed = new GZipStream(destination, CompressionLevel.Optimal);
        compressed.Write(bytes);
        Console.WriteLine("TEST ONLY: changed one README byte, preserving original tar headers and signing data: " + output);
    }

    internal static async Task SignAsync(ToolContext context, string package, string output, string upm, TimeSpan timeout)
    {
        package = context.RequireOutsideExamples(package);
        output = context.RequireOutsideExamples(output);
        var manifest = FileOps.ReadObject(Path.Combine(package, "package.json"));
        Require(manifest["name"]?.GetValue<string>() == "com.houtamelo.spire", "Expected a Spire package.");
        Require(Path.GetFileName(package) == "com.houtamelo.spire", "Package folder must match its technical name.");
        var credentials = CredentialNames.ToDictionary(name => name, name => Environment.GetEnvironmentVariable(name));
        foreach (var item in credentials)
            Require(!string.IsNullOrWhiteSpace(item.Value), "Required environment variable is missing: " + item.Key);
        Require(!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any(), "Signing output must be empty; preserve earlier artifacts in a separate directory.");
        var source = Directory.EnumerateFiles(package, "*", SearchOption.AllDirectories)
            .ToDictionary(path => FileOps.Relative(package, path), FileOps.Hash, StringComparer.Ordinal);
        Directory.CreateDirectory(output);
        // The CLI's output can contain service-account identifiers. Never stream or persist it unredacted.
        var result = await ProcessRunner.RunAsync(upm,
            ["pack", package, "--organization-id", credentials["UPM_ORGANIZATION_ID"]!, "--destination", output],
            context.Root, timeout).ConfigureAwait(false);
        var text = result.StandardOutput + result.StandardError;
        foreach (var value in credentials.Values.Where(value => !string.IsNullOrEmpty(value)))
            text = text.Replace(value!, "[REDACTED]", StringComparison.Ordinal);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials[CredentialNames[0]] + ":" + credentials[CredentialNames[1]]));
        text = text.Replace(basic, "[REDACTED]", StringComparison.Ordinal);
        text = Regex.Replace(text, @"(?i)(Basic|Bearer)\s+[A-Za-z0-9._~+/=-]+", "$1 [REDACTED]");
        FileOps.Write(Path.Combine(output, "signing.log"), text);
        Require(result.ExitCode == 0, "UPM signing failed (exit " + result.ExitCode + "); see the redacted signing.log.");
        var archive = Path.Combine(output, "com.houtamelo.spire-" + manifest["version"]!.GetValue<string>() + ".tgz");
        var payload = ReadArchive(archive);
        var packed = JsonNode.Parse(payload["package.json"])!.AsObject();
        var sourceManifest = manifest.DeepClone().AsObject();
        if (!sourceManifest.ContainsKey("repository") && packed.ContainsKey("repository"))
        {
            var repository = packed["repository"]!.AsObject();
            Require(repository["type"]?.GetValue<string>() == "git" &&
                repository["url"]?.GetValue<string>() == "https://github.com/Houtamelo/spire_csharp_analyzers.git" &&
                Regex.IsMatch(repository["revision"]?.GetValue<string>() ?? "", "^[a-f0-9]{40}$"), "Unexpected CLI repository metadata.");
            packed.Remove("repository");
        }
        Require(JsonNode.DeepEquals(sourceManifest, packed), "Packing changed package manifest values.");
        Require(payload.Keys.Where(path => path != ".attestation.p7m").Order().SequenceEqual(source.Keys.Order()), "Packing changed the payload inventory.");
        foreach (var item in source.Where(item => item.Key != "package.json"))
            Require(FileOps.HashTextBytes(payload[item.Key]) == item.Value, "Packing changed payload bytes: " + item.Key);
        Require(payload.ContainsKey(".attestation.p7m"), "Signed archive has no attestation.");
        // These keys belong only in the signing process. Unity processes must never inherit them.
        FileOps.WriteJson(Path.Combine(output, "artifact.json"), Inputs(archive, payload));
        Console.WriteLine("Signed artifact: " + archive);
    }

    internal static async Task<int> ValidateAsync(ToolContext context, string archive, string project, string editor,
        bool prepareOnly, bool allowUnsupportedSignature, TimeSpan timeout)
    {
        archive = context.RequireOutsideExamples(archive);
        project = context.RequireOutsideExamples(project);
        var payload = ReadArchive(archive);
        var inputs = Inputs(archive, payload);
        inputs["allowUnsupportedSignature"] = allowUnsupportedSignature;
        var fixture = context.PathOf("tests/UnityTarballAcceptance");
        project = FileOps.Disposable(project, fixture, ".spire-unity-tarball", "Spire signed tarball acceptance\n");
        foreach (var folder in new[] { "Assets", "Packages", "ProjectSettings" })
        {
            FileOps.ResetDirectory(Path.Combine(project, folder));
            FileOps.CopyDirectory(Path.Combine(fixture, folder), Path.Combine(project, folder));
        }
        FileOps.WriteJson(Path.Combine(project, "tarball-inputs.json"), inputs);
        var manifest = FileOps.ReadObject(Path.Combine(project, "Packages/manifest.json"));
        manifest["dependencies"]!["com.houtamelo.spire"] = "file:" + archive.Replace('\\', '/');
        FileOps.WriteJson(Path.Combine(project, "Packages/manifest.json"), manifest);
        if (prepareOnly) return 0;
        FileOps.RequireFile(editor);
        var version = Directory.GetParent(Path.GetDirectoryName(editor)!)!.Name;
        FileOps.Write(Path.Combine(project, "ProjectSettings/ProjectVersion.txt"), "m_EditorVersion: " + version + "\n");
        var environment = CredentialNames.ToDictionary(name => name, _ => (string?)null);
        foreach (var stage in new[] { "Install", "Verify" })
        {
            var report = Path.Combine(project, "tarball-" + stage.ToLowerInvariant() + ".json");
            FileOps.Archive(project, [report, Path.Combine(project, stage + ".log")]);
            var run = await UnityRunner.MethodAsync(context, project, editor,
                "Spire.UnityTarballAcceptance.TarballAcceptance." + stage,
                Path.Combine(project, stage + ".log"), timeout, environment: environment).ConfigureAwait(false);
            FileOps.Write(Path.Combine(project, stage + ".process.log"), run.StandardOutput + run.StandardError);
            Require(File.Exists(report), "Unity exited " + run.ExitCode + " without an acceptance report; see " + Path.Combine(project, stage + ".process.log"));
            FileOps.RequireFile(report);
            var result = FileOps.ReadObject(report);
            Require(run.ExitCode == 0 && result["success"]?.GetValue<bool>() == true,
                "Tarball " + stage + " failed; see " + report);
            var installed = JsonNode.Parse(result["installedManifest"]!.GetValue<string>())!.AsObject();
            if (installed.Remove("_fingerprint", out var fingerprint))
            {
                var pattern = result["unityVersion"]!.GetValue<string>().StartsWith("2023.2.", StringComparison.Ordinal)
                    ? "^[a-f0-9]{12}$" : "^[a-f0-9]{32}$";
                Require(Regex.IsMatch(fingerprint!.GetValue<string>(), pattern), "Unexpected installed fingerprint.");
            }
            Require(JsonNode.DeepEquals(JsonNode.Parse(payload["package.json"]), installed), "Installed package manifest differs from signed artifact.");
        }
        Require(FileOps.Hash(archive) == inputs["artifactSha256"]!.GetValue<string>(), "Artifact changed during validation.");
        File.Copy(Path.Combine(project, "Packages/packages-lock.json"), Path.Combine(project, "accepted-packages-lock.json"), true);
        Console.WriteLine("Signed tarball acceptance passed: " + project);
        return 0;
    }

    internal static Dictionary<string, byte[]> ReadArchive(string archive)
    {
        Require(archive.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase), "Expected a .tgz archive.");
        using var file = File.OpenRead(archive);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);
        var payload = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        long total = 0;
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) != null)
        {
            Require(entry.Name.StartsWith("package/", StringComparison.Ordinal), "Archive entries must be under package/.");
            var name = entry.Name[8..].TrimEnd('/');
            Require(name.Length > 0 && !name.Contains('\\') && !name.Contains(':') &&
                name.Split('/').All(part => part is not ("" or "." or "..")), "Invalid archive path: " + entry.Name);
            Require(entry.EntryType is TarEntryType.Directory or TarEntryType.RegularFile or TarEntryType.V7RegularFile,
                "Unsupported archive entry: " + entry.Name);
            if (entry.EntryType == TarEntryType.Directory) continue;
            total += entry.Length;
            Require(total <= 550L * 1024 * 1024, "Package exceeds the supported 550 MiB payload limit.");
            using var buffer = new MemoryStream();
            entry.DataStream!.CopyTo(buffer);
            Require(payload.TryAdd(name, buffer.ToArray()), "Duplicate archive path: " + name);
        }
        Require(payload.TryGetValue("package.json", out var json), "Archive has no package.json.");
        var manifest = JsonNode.Parse(json!)!.AsObject();
        Require(manifest["name"]?.GetValue<string>() == "com.houtamelo.spire", "Archive is not a Spire package.");
        foreach (var assembly in PackageWorkflow.Assemblies)
            foreach (var suffix in new[] { ".dll", ".dll.meta" })
                Require(payload.ContainsKey(assembly.Folder + "/" + assembly.Name + suffix), "Archive is missing a product assembly or metadata.");
        return payload;
    }

    private static JsonObject Inputs(string archive, Dictionary<string, byte[]> payload) => new()
    {
        ["artifact"] = Path.GetFullPath(archive),
        ["artifactSha256"] = FileOps.Hash(archive),
        ["attestationSha256"] = payload.TryGetValue(".attestation.p7m", out var attestation) ? FileOps.HashTextBytes(attestation) : null,
        ["version"] = JsonNode.Parse(payload["package.json"])!["version"]!.GetValue<string>(),
        ["files"] = new JsonArray(payload.Where(item => item.Key != ".attestation.p7m").OrderBy(item => item.Key).Select(item => (JsonNode)new JsonObject
        { ["path"] = item.Key, ["sha256"] = FileOps.HashTextBytes(item.Value) }).ToArray()),
    };

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
