using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Spire.Tool;
using Xunit;

namespace DevTools.Tests;

public sealed class UpmReleaseTests
{
    [Fact]
    public async Task TamperControlChangesExactlyOnePayloadByteAndPreservesTheTarStream()
    {
        using var fixture = new Fixture();
        await fixture.Package();
        fixture.Archive();
        var output = Path.Combine(fixture.Root, "tampered.tgz");
        var result = await fixture.Run("package-tamper", "--package", fixture.Tgz, "--output", output);
        Assert.Equal(0, result.ExitCode);
        byte[] Inflate(string path)
        {
            using var file = File.OpenRead(path);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var buffer = new MemoryStream();
            gzip.CopyTo(buffer);
            return buffer.ToArray();
        }
        var original = Inflate(fixture.Tgz);
        var changed = Inflate(output);
        Assert.Equal(original.Length, changed.Length);
        Assert.Equal(1, original.Zip(changed, (left, right) => left != right).Count(value => value));
        var second = await fixture.Run("package-tamper", "--package", fixture.Tgz, "--output", output);
        Assert.NotEqual(0, second.ExitCode);
        Assert.Equal(changed, Inflate(output));
    }

    [Fact]
    public async Task TarballPreparationUsesArchiveBytesAndDoesNotRequireEditorOrCredentials()
    {
        using var fixture = new Fixture();
        await fixture.Package();
        fixture.Archive();
        var result = await fixture.Run("unity-tarball", "--package", fixture.Tgz, "--project", fixture.Project, "--prepare-only", "--editor", "missing-editor");
        Assert.Equal(0, result.ExitCode);
        using var input = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.Project, "tarball-inputs.json")));
        Assert.Equal(Directory.GetFiles(fixture.PackagePath, "*", SearchOption.AllDirectories).Length, input.RootElement.GetProperty("files").GetArrayLength());
        Assert.False(input.RootElement.GetProperty("allowUnsupportedSignature").GetBoolean());
        Assert.False(Directory.Exists(Path.Combine(fixture.Project, "Assets", "Samples")));
        Assert.DoesNotContain("render-pipelines", File.ReadAllText(Path.Combine(fixture.Project, "Packages", "manifest.json")));
    }

    [Theory]
    [InlineData("traversal")]
    [InlineData("duplicate")]
    [InlineData("missing-dll")]
    [InlineData("wrong-name")]
    public async Task InvalidArchiveFailsBeforeProjectMutation(string kind)
    {
        using var fixture = new Fixture();
        await fixture.Package();
        fixture.Archive(kind);
        var result = await fixture.Run("unity-tarball", "--package", fixture.Tgz, "--project", fixture.Project, "--prepare-only");
        Assert.NotEqual(0, result.ExitCode);
        Assert.False(Directory.Exists(fixture.Project));
    }

    [Fact]
    public async Task SigningWithoutCredentialsFailsBeforeCreatingOutput()
    {
        using var fixture = new Fixture();
        await fixture.Package();
        var output = Path.Combine(fixture.Root, "signed");
        var result = await fixture.Run("package-sign", "--package", fixture.PackagePath, "--output", output, "--upm", "must-not-launch");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("UPM_SERVICE_ACCOUNT_KEY_ID", result.StandardError);
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public async Task TarballPreparationPreservesUnrelatedProjectFiles()
    {
        using var fixture = new Fixture();
        await fixture.Package();
        fixture.Archive();
        Directory.CreateDirectory(fixture.Project);
        var sentinel = Path.Combine(fixture.Project, "keep.txt");
        File.WriteAllText(sentinel, "keep");
        var result = await fixture.Run("unity-tarball", "--package", fixture.Tgz, "--project", fixture.Project, "--prepare-only");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal("keep", File.ReadAllText(sentinel));
        Assert.Single(Directory.GetFiles(fixture.Project));
    }

    [Fact]
    public async Task QuickStartExportPreservesAuthoredBytesAndIncludesReleaseDocumentation()
    {
        using var fixture = new Fixture();
        await fixture.Package();
        foreach (var relative in File.ReadAllLines(Path.Combine(fixture.Repo, "tests/UnityQuickStart/sample-files.txt")))
            Assert.Equal(File.ReadAllBytes(Path.Combine(fixture.Repo, "tests/UnityQuickStart/Assets/QuickStart", relative)),
                File.ReadAllBytes(Path.Combine(fixture.PackagePath, "Samples~/QuickStart", relative)));
        Assert.Equal(File.ReadAllBytes(Path.Combine(fixture.Repo, "LICENSE")), File.ReadAllBytes(Path.Combine(fixture.PackagePath, "LICENSE.md")));
        Assert.Contains("release candidate", File.ReadAllText(Path.Combine(fixture.PackagePath, "CHANGELOG.md")));
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly string Repo;
        internal readonly string Root;
        internal string PackagePath => Path.Combine(Root, "com.houtamelo.spire");
        internal string Project => Path.Combine(Root, "customer");
        internal string Tgz => Path.Combine(Root, "package.tgz");
        private readonly string configuration;

        internal Fixture()
        {
            string? repository = null;
            foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                for (var current = new DirectoryInfo(start); current != null; current = current.Parent)
                    if (File.Exists(Path.Combine(current.FullName, "Spire.Analyzers.slnx")))
                    {
                        repository = current.FullName;
                        break;
                    }
                if (repository != null) break;
            }
            Repo = repository ?? throw new InvalidOperationException("Repository not found.");
            Root = Path.Combine(Repo, "tmp", "upm-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            configuration = Environment.GetEnvironmentVariable("SPIRE_UNITY_TEST_CONFIGURATION") ??
                (AppContext.BaseDirectory.Contains(Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar) ? "Release" : "Debug");
        }

        internal async Task Package()
        {
            var result = await Run("package", "--output", PackagePath, "--configuration", configuration, "--no-build");
            Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
        }

        internal Task<ProcessResult> Run(params string[] args) => ProcessRunner.RunAsync(new ToolContext(Repo).Dotnet,
            new[] { Path.Combine(Repo, "tools/Spire.Tool/bin", configuration, "net10.0/Spire.Tool.dll") }.Concat(args),
            Repo, TimeSpan.FromMinutes(2), new Dictionary<string, string?>
            {
                ["UPM_SERVICE_ACCOUNT_KEY_ID"] = null,
                ["UPM_SERVICE_ACCOUNT_KEY_SECRET"] = null,
                ["UPM_ORGANIZATION_ID"] = null,
            });

        internal void Archive(string kind = "valid")
        {
            using var file = File.Create(Tgz);
            using var stream = new GZipStream(file, CompressionLevel.Optimal);
            foreach (var path in Directory.GetFiles(PackagePath, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(PackagePath, path).Replace('\\', '/');
                if (kind == "missing-dll" && relative == "Runtime/Houtamelo.Spire.dll") continue;
                var bytes = File.ReadAllBytes(path);
                if (kind == "wrong-name" && relative == "package.json")
                    bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("com.houtamelo.spire", "com.example.other"));
                Entry("package/" + relative, bytes);
            }
            if (kind == "duplicate") Entry("package/package.json", File.ReadAllBytes(Path.Combine(PackagePath, "package.json")));
            if (kind == "traversal") Entry("package/../outside.txt", Encoding.UTF8.GetBytes("must not escape"));
            stream.Write(new byte[1024], 0, 1024);

            void Entry(string name, byte[] bytes)
            {
                var header = new byte[512];
                var encoded = Encoding.UTF8.GetBytes(name);
                Array.Copy(encoded, 0, header, 0, encoded.Length);
                void Octal(int offset, int length, long value)
                {
                    var text = Encoding.ASCII.GetBytes(Convert.ToString(value, 8).PadLeft(length - 1, '0') + "\0");
                    Array.Copy(text, 0, header, offset, length);
                }
                Octal(100, 8, 420); Octal(108, 8, 0); Octal(116, 8, 0); Octal(124, 12, bytes.Length); Octal(136, 12, 0);
                for (var i = 148; i < 156; i++) header[i] = 32;
                header[156] = (byte)'0';
                Octal(148, 8, header.Sum(value => (int)value));
                stream.Write(header, 0, header.Length);
                stream.Write(bytes, 0, bytes.Length);
                var padding = (512 - bytes.Length % 512) % 512;
                stream.Write(new byte[padding], 0, padding);
            }
        }

        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
}
