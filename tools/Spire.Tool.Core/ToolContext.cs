using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Spire.Tool;

public sealed class ToolContext
{
    public ToolContext(string root) => Root = FileOps.FullPath(root);
    public string Root { get; }
    public string Dotnet => Environment.GetEnvironmentVariable("SPIRE_DOTNET_HOST")
                            ?? Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
    public string PathOf(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
    internal string RequireOutsideExamples(string path)
    {
        var examples = Path.Combine(Root, "examples");
        var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        void Check(string candidate)
        {
            if (string.Equals(candidate, examples, comparison) ||
                candidate.StartsWith(examples + Path.DirectorySeparatorChar, comparison))
                throw new InvalidOperationException("Validation inputs and outputs must stay outside the repository examples directory.");
        }

        var full = Path.GetFullPath(path);
        Check(full);
        full = FileOps.FullPath(full);
        Check(full);
        return full;
    }
    public static string DefaultEditor => Environment.GetEnvironmentVariable("SPIRE_UNITY_EDITOR")
        ?? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Unity/Hub/Editor/6000.5.6f1/Editor/Unity"));
    public static string StandaloneTarget => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? "StandaloneWindows64" : "StandaloneLinux64";
    public static string DefaultRider => Environment.GetEnvironmentVariable("SPIRE_RIDER_ROOT")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/JetBrains/Toolbox/apps/rider-2");

    public async Task BuildAsync(string project, string configuration, params string[] extra)
    {
        var result = await ProcessRunner.RunAsync(Dotnet,
            new[] { "build", PathOf(project), "-c", configuration, "--nologo", "-p:EnableSourceControlManagerQueries=false" }.Concat(extra), Root,
            TimeSpan.FromMinutes(10), environment: new Dictionary<string, string?>
            {
                // Shared MSBuild nodes can outlive test hosts and leave nested CLI builds waiting.
                ["MSBUILDDISABLENODEREUSE"] = "1",
            }).ConfigureAwait(false);
        Console.Write(result.StandardOutput);
        Console.Error.Write(result.StandardError);
        result.EnsureSuccess("Build " + project);
    }
}

internal static class FileOps
{
    internal static string HashTextBytes(byte[] bytes)
    {
        using var hash = SHA256.Create();
        return DevTools.ProcessCompatibility.ToHexString(hash.ComputeHash(bytes)).ToLowerInvariant();
    }
    internal static readonly UTF8Encoding Utf8 = new(false);
    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    internal static string FullPath(string path)
    {
        var full = Path.GetFullPath(path);
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return full;
        var suffix = new Stack<string>();
        var current = full;
        while (true)
        {
            var resolved = RealPath(current, IntPtr.Zero);
            if (resolved != IntPtr.Zero)
            {
                try
                {
                    current = Marshal.PtrToStringAnsi(resolved)!;
                    foreach (var part in suffix) current = Path.Combine(current, part);
                    return current;
                }
                finally { Free(resolved); }
            }
            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || parent == current) return full;
            suffix.Push(Path.GetFileName(current));
            current = parent;
        }
    }

    [DllImport("libc", EntryPoint = "realpath", SetLastError = true)]
    private static extern IntPtr RealPath(string path, IntPtr buffer);
    [DllImport("libc", EntryPoint = "free")]
    private static extern void Free(IntPtr pointer);

    internal static string ReadSharedText(string path)
    {
        // Unity can leave its log writer open briefly after the Editor exits.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    internal static JsonObject ReadObject(string path) => JsonNode.Parse(File.ReadAllText(path)) as JsonObject
        ?? throw new InvalidDataException("Expected a JSON object: " + path);
    internal static void WriteJson(string path, JsonNode data) => Write(path, data.ToJsonString(JsonOptions) + "\n");
    internal static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, text, Utf8);
    }
    internal static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Hex(sha.ComputeHash(stream));
    }
    internal static string HashText(string text)
    {
        using var sha = SHA256.Create();
        return Hex(sha.ComputeHash(Utf8.GetBytes(text)));
    }
    private static string Hex(byte[] bytes) => string.Concat(bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    internal static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
    internal static JsonArray Array(IEnumerable<string> values) => new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
    internal static string[] Strings(JsonNode? values) => values?.AsArray().Select(value => value!.GetValue<string>()).ToArray() ?? System.Array.Empty<string>();

    internal static string Disposable(string project, string fixture, string markerName, string markerContent)
    {
        project = FullPath(project);
        fixture = FullPath(fixture);
        if (project == fixture || project.StartsWith(fixture + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Use a disposable project; the tracked fixture must remain unchanged.");
        var marker = Path.Combine(project, markerName);
        if (Directory.Exists(project) && Directory.EnumerateFileSystemEntries(project).Any() && !File.Exists(marker))
            throw new InvalidOperationException("The project contains unrelated files; choose an empty disposable directory.");
        if (File.Exists(marker) && File.ReadAllText(marker) != markerContent)
            throw new InvalidOperationException("The project ownership marker is invalid; choose an empty disposable directory.");
        Directory.CreateDirectory(project);
        Write(marker, markerContent);
        return project;
    }
    internal static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        foreach (var directory in Directory.EnumerateDirectories(source)) CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }
    internal static void ResetDirectory(string directory)
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        Directory.CreateDirectory(directory);
    }
    internal static void Archive(string project, IEnumerable<string> paths)
    {
        var existing = paths.Where(path => File.Exists(path) || Directory.Exists(path)).Distinct().ToArray();
        if (existing.Length == 0) return;
        var archive = Path.Combine(project, "TestResults", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(archive);
        foreach (var path in existing)
        {
            var target = Path.Combine(archive, Path.GetFileName(path));
            if (Directory.Exists(path)) Directory.Move(path, target);
            else File.Move(path, target);
        }
    }
    internal static JsonObject Input(string root, string path) => new() { ["source"] = Relative(root, path), ["sha256"] = Hash(path) };
    internal static void RequireFile(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Required file is missing: " + path, path);
    }
    internal static void DeleteFile(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
