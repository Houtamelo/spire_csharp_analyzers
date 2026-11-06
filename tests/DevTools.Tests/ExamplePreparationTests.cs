using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Spire.Tool;
using Xunit;

namespace DevTools.Tests;

public sealed class ExamplePreparationTests
{
    [Fact]
    public void Prepare_FreshSubmoduleCloneAcceptsRemoteComparisonBranchesWithoutMutation()
    {
        using var fixture = ExampleFixture.Create();
        fixture.AddRemoteCloneExample("SubmoduleClone");
        fixture.WriteCatalog(fixture.Entry("SubmoduleClone"));
        var before = fixture.SnapshotAll();

        var prepared = ExamplesWorkflow.Prepare(new ToolContext(fixture.Root));

        Assert.Equal(new[] { fixture.ExamplePath("SubmoduleClone") }, prepared);
        Assert.Equal(before, fixture.SnapshotAll());
    }

    [Fact]
    public void Prepare_ValidGitExamplesReturnsAllAndDoesNotMutateRepositories()
    {
        using var fixture = ExampleFixture.Create();
        fixture.AddDirectoryExample("DirectoryExample");
        fixture.AddWorktreeExample("WorktreeExample");
        fixture.WriteCatalog(fixture.Entry("DirectoryExample"), fixture.Entry("WorktreeExample"));
        fixture.AddDirtyState("DirectoryExample");
        fixture.AddDirtyState("WorktreeExample");
        var before = fixture.SnapshotAll();

        var prepared = ExamplesWorkflow.Prepare(new ToolContext(fixture.Root));

        Assert.Equal(
            new[] { fixture.ExamplePath("DirectoryExample"), fixture.ExamplePath("WorktreeExample") },
            prepared.OrderBy(path => path, StringComparer.Ordinal).ToArray());
        Assert.Equal(before, fixture.SnapshotAll());
        Assert.Equal("with-spire", fixture.CurrentBranch("DirectoryExample"));
        Assert.Equal("with-spire", fixture.CurrentBranch("WorktreeExample"));
    }

    [Fact]
    public void Prepare_WithNameOnlyValidatesSelectedExampleAndDoesNotMaterializeUnselectedExample()
    {
        using var fixture = ExampleFixture.Create();
        fixture.AddDirectoryExample("Selected");
        fixture.WriteCatalog(fixture.Entry("Selected"), fixture.Entry("Missing"));
        var before = fixture.SnapshotAll();

        var prepared = ExamplesWorkflow.Prepare(new ToolContext(fixture.Root), "Selected");

        Assert.Equal(new[] { fixture.ExamplePath("Selected") }, prepared);
        Assert.False(Directory.Exists(fixture.ExamplePath("Missing")));
        Assert.Equal(before, fixture.SnapshotAll());
    }

    [Fact]
    public void Prepare_PreservesDetachedHeadAndDirtyUntrackedFiles()
    {
        using var fixture = ExampleFixture.Create();
        fixture.AddDirectoryExample("Detached");
        fixture.WriteCatalog(fixture.Entry("Detached"));
        fixture.DetachHead("Detached");
        fixture.AddDirtyState("Detached");
        var before = fixture.SnapshotAll();
        var beforeHead = fixture.Head("Detached");

        var prepared = ExamplesWorkflow.Prepare(new ToolContext(fixture.Root));

        Assert.Equal(new[] { fixture.ExamplePath("Detached") }, prepared);
        Assert.Equal(beforeHead, fixture.Head("Detached"));
        Assert.Equal(string.Empty, fixture.CurrentBranch("Detached"));
        Assert.Equal(before, fixture.SnapshotAll());
    }

    [Fact]
    public void Prepare_EmptyCatalogFailsWithoutCreatingExamplesDirectoryEntries()
    {
        using var fixture = ExampleFixture.Create();
        fixture.WriteCatalog();
        var before = fixture.SnapshotAll();

        Assert.ThrowsAny<Exception>(() => ExamplesWorkflow.Prepare(new ToolContext(fixture.Root)));

        Assert.Equal(before, fixture.SnapshotAll());
    }

    [Fact]
    public void Prepare_UnknownNameFailsWithoutMutation()
    {
        using var fixture = ExampleFixture.Create();
        fixture.AddDirectoryExample("Known");
        fixture.WriteCatalog(fixture.Entry("Known"));
        var before = fixture.SnapshotAll();

        Assert.ThrowsAny<Exception>(() => ExamplesWorkflow.Prepare(new ToolContext(fixture.Root), "Unknown"));

        Assert.Equal(before, fixture.SnapshotAll());
    }

    [Fact]
    public void Prepare_DuplicateNamesFailWithoutMutation()
    {
        using var fixture = ExampleFixture.Create();
        fixture.AddDirectoryExample("Duplicate");
        fixture.WriteCatalog(fixture.Entry("Duplicate"), fixture.Entry("Duplicate"));
        var before = fixture.SnapshotAll();

        Assert.ThrowsAny<Exception>(() => ExamplesWorkflow.Prepare(new ToolContext(fixture.Root)));

        Assert.Equal(before, fixture.SnapshotAll());
    }

    [Fact]
    public void Prepare_InvalidNamesFailWithoutMutation()
    {
        foreach (var name in new[] { "../Outside", "/absolute", "Nested/Child" })
        {
            using var fixture = ExampleFixture.Create();
            fixture.WriteCatalog(fixture.Entry(name));
            var before = fixture.SnapshotAll();

            Assert.ThrowsAny<Exception>(() => ExamplesWorkflow.Prepare(new ToolContext(fixture.Root)));

            Assert.Equal(before, fixture.SnapshotAll());
            Assert.False(Directory.Exists(fixture.ExamplePath("Outside")));
        }
    }

    [Fact]
    public void Prepare_MissingSiblingFailsWithoutCreatingIt()
    {
        using var fixture = ExampleFixture.Create();
        fixture.WriteCatalog(fixture.Entry("Missing"));
        var before = fixture.SnapshotAll();

        Assert.ThrowsAny<Exception>(() => ExamplesWorkflow.Prepare(new ToolContext(fixture.Root)));

        Assert.Equal(before, fixture.SnapshotAll());
        Assert.False(Directory.Exists(fixture.ExamplePath("Missing")));
    }

    [Theory]
    [InlineData("Assets")]
    [InlineData("Packages/manifest.json")]
    [InlineData("ProjectSettings/ProjectVersion.txt")]
    public void Prepare_MissingRequiredUnityFileFailsWithoutMutation(string relativePath)
    {
        using var fixture = ExampleFixture.Create();
        fixture.AddDirectoryExample("Incomplete");
        fixture.WriteCatalog(fixture.Entry("Incomplete"));
        if (relativePath == "Assets")
            Directory.Delete(fixture.ExampleFile("Incomplete", relativePath), recursive: true);
        else
            File.Delete(fixture.ExampleFile("Incomplete", relativePath));
        var before = fixture.SnapshotAll();

        Assert.ThrowsAny<Exception>(() => ExamplesWorkflow.Prepare(new ToolContext(fixture.Root)));

        Assert.Equal(before, fixture.SnapshotAll());
    }

    [Fact]
    public void Prepare_MissingGitRepositoryFailsWithoutMutation()
    {
        using var fixture = ExampleFixture.Create();
        fixture.AddProjectWithoutGit("NoGit");
        fixture.WriteCatalog(fixture.Entry("NoGit"));
        var before = fixture.SnapshotAll();

        Assert.ThrowsAny<Exception>(() => ExamplesWorkflow.Prepare(new ToolContext(fixture.Root)));

        Assert.Equal(before, fixture.SnapshotAll());
    }

    [Theory]
    [InlineData("upstream")]
    [InlineData("without-spire")]
    [InlineData("with-spire")]
    public void Prepare_MissingRequiredRefFailsWithoutMutation(string reference)
    {
        using var fixture = ExampleFixture.Create();
        fixture.AddDirectoryExample("MissingRef");
        fixture.WriteCatalog(fixture.Entry("MissingRef"));
        fixture.DeleteReference("MissingRef", reference);
        var before = fixture.SnapshotAll();

        Assert.ThrowsAny<Exception>(() => ExamplesWorkflow.Prepare(new ToolContext(fixture.Root)));

        Assert.Equal(before, fixture.SnapshotAll());
    }

    [Fact]
    public void Prepare_NestedDirectoryUsingParentRepositoryFailsWithoutMutation()
    {
        using var fixture = ExampleFixture.Create();
        fixture.AddNestedProjectUsingParentRepository("Nested");
        fixture.WriteCatalog(fixture.Entry("Nested"));
        var before = fixture.SnapshotAll();

        Assert.ThrowsAny<Exception>(() => ExamplesWorkflow.Prepare(new ToolContext(fixture.Root)));

        Assert.Equal(before, fixture.SnapshotAll());
    }

    private sealed class ExampleFixture : IDisposable
    {
        private ExampleFixture(string root)
        {
            Root = root;
            ExamplesRoot = Path.Combine(root, "examples");
            Directory.CreateDirectory(ExamplesRoot);
        }

        public string Root { get; }
        public string ExamplesRoot { get; }

        public static ExampleFixture Create()
            => new(Path.Combine(FindRepositoryRoot(), "tmp", "example-preparation-tests", Guid.NewGuid().ToString("N")));

        public CatalogEntry Entry(string name)
            => new(name, "examples/Original/UnityProject", "upstream");

        public void WriteCatalog(params CatalogEntry[] entries)
        {
            File.WriteAllText(
                Path.Combine(ExamplesRoot, "SpireExamples.json"),
                JsonSerializer.Serialize(new { examples = entries }));
        }

        public void AddDirectoryExample(string name)
        {
            var path = ExamplePath(name);
            AddProjectFiles(path);
            InitializeRepository(path);
        }

        public void AddWorktreeExample(string name)
        {
            var source = Path.Combine(Root, "sources", name);
            AddProjectFiles(source);
            InitializeRepository(source);
            RunGit(source, "checkout", "--detach", "with-spire");
            Directory.CreateDirectory(Path.GetDirectoryName(ExamplePath(name))!);
            RunGit(source, "worktree", "add", ExamplePath(name), "with-spire");
        }

        public void AddRemoteCloneExample(string name)
        {
            var source = Path.Combine(Root, "sources", name);
            AddProjectFiles(source);
            InitializeRepository(source);
            RunGit(Root, "init", "--initial-branch=parent");
            ConfigureRepository(Root);
            RunGit(Root, "-c", "protocol.file.allow=always", "submodule", "add", "--branch", "with-spire", source, ExamplePath(name));
            RunGit(Root, "commit", "-m", "register example submodule");
            RunGit(ExamplePath(name), "checkout", "--detach", "HEAD");
        }

        public void AddProjectWithoutGit(string name)
            => AddProjectFiles(ExamplePath(name));

        public void AddNestedProjectUsingParentRepository(string name)
        {
            RunGit(ExamplesRoot, "init", "--initial-branch=with-spire");
            ConfigureRepository(ExamplesRoot);
            var path = ExamplePath(name);
            AddProjectFiles(path);
            RunGit(ExamplesRoot, "add", "-A");
            RunGit(ExamplesRoot, "commit", "-m", "nested project");
            RunGit(ExamplesRoot, "tag", "upstream");
            RunGit(ExamplesRoot, "branch", "without-spire");
        }

        public void AddDirtyState(string name)
        {
            var path = ExamplePath(name);
            File.WriteAllText(Path.Combine(path, "Assets", "Dirty.cs"), "dirty");
            File.WriteAllText(Path.Combine(path, "Assets", "Untracked.asset"), "untracked");
        }

        public void DetachHead(string name)
            => RunGit(ExamplePath(name), "checkout", "--detach", "HEAD");

        public void DeleteReference(string name, string reference)
        {
            if (reference == "upstream")
                RunGit(ExamplePath(name), "tag", "-d", reference);
            else
            {
                if (reference == "with-spire")
                    RunGit(ExamplePath(name), "checkout", "--detach", "HEAD");
                RunGit(ExamplePath(name), "branch", "-D", reference);
            }
        }

        public string ExamplePath(string name)
            => Path.Combine(ExamplesRoot, name.Replace('/', Path.DirectorySeparatorChar));

        public string ExampleFile(string name, string relative)
            => Path.Combine(ExamplePath(name), relative.Replace('/', Path.DirectorySeparatorChar));

        public string Head(string name)
            => RunGit(ExamplePath(name), "rev-parse", "HEAD");

        public string CurrentBranch(string name)
            => RunGitOptional(ExamplePath(name), "symbolic-ref", "--short", "-q", "HEAD");

        public string SnapshotAll()
        {
            var entries = new List<string>();
            foreach (var path in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
                         .Where(path => !IsGitMetadata(path)))
            {
                var relative = Path.GetRelativePath(Root, path).Replace(Path.DirectorySeparatorChar, '/');
                entries.Add(relative + "=" + Hash(path));
            }

            foreach (var directory in Directory.EnumerateDirectories(Root, "*", SearchOption.AllDirectories)
                         .Where(path => Path.GetFileName(path) == ".git"))
            {
                var relative = Path.GetRelativePath(Root, directory).Replace(Path.DirectorySeparatorChar, '/');
                entries.Add(relative + "/<git-directory>");
            }

            foreach (var project in Directory.EnumerateDirectories(Root, "*", SearchOption.AllDirectories)
                         .Where(path => File.Exists(Path.Combine(path, ".git")) || Directory.Exists(Path.Combine(path, ".git"))))
            {
                var relative = Path.GetRelativePath(Root, project).Replace(Path.DirectorySeparatorChar, '/');
                var head = RunGit(project, "rev-parse", "HEAD");
                var branch = RunGitOptional(project, "symbolic-ref", "--short", "-q", "HEAD");
                var refs = RunGit(project, "for-each-ref", "--format=%(refname)=%(objectname)");
                var status = RunGit(project, "status", "--porcelain=v1", "--untracked-files=all");
                entries.Add(relative + "/<git-state>=" + Normalize(head) + ";" + Normalize(branch) + ";" + Normalize(refs) + ";" + Normalize(status));
            }

            entries.Sort(StringComparer.Ordinal);
            return string.Join("\n", entries);
        }

        public void Dispose()
        {
            TestDirectories.DeleteOwned(Root);
        }

        private static void AddProjectFiles(string path)
        {
            Directory.CreateDirectory(Path.Combine(path, "Assets"));
            Directory.CreateDirectory(Path.Combine(path, "Packages"));
            Directory.CreateDirectory(Path.Combine(path, "ProjectSettings"));
            File.WriteAllText(Path.Combine(path, "Assets", "Example.cs"), "without spire");
            File.WriteAllText(Path.Combine(path, "Packages", "manifest.json"), "manifest");
            File.WriteAllText(Path.Combine(path, "ProjectSettings", "ProjectVersion.txt"), "version");
        }

        private static void InitializeRepository(string path)
        {
            RunGit(path, "init", "--initial-branch=with-spire");
            ConfigureRepository(path);
            RunGit(path, "add", "-A");
            RunGit(path, "commit", "-m", "without Spire");
            RunGit(path, "tag", "upstream");
            RunGit(path, "branch", "without-spire");
            File.WriteAllText(Path.Combine(path, "Assets", "Example.cs"), "with spire");
            File.WriteAllText(Path.Combine(path, "Assets", "SpireIntegration.cs"), "Spire integration");
            RunGit(path, "add", "-A");
            RunGit(path, "commit", "-m", "with Spire");
        }

        private static void ConfigureRepository(string path)
        {
            RunGit(path, "config", "user.name", "Spire Tests");
            RunGit(path, "config", "user.email", "spire-tests@example.invalid");
        }

        private static string RunGit(string workingDirectory, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start git.");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error}");
            return output.Trim();
        }

        private static string RunGitOptional(string workingDirectory, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start git.");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0 && (output.Length != 0 || error.Length != 0))
                throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error}");
            return output.Trim();
        }

        private static bool IsGitMetadata(string path)
        {
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return parts.Any(part => part == ".git");
        }

        private static string Hash(string path)
        {
            using var sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty);
        }

        private static string Normalize(string value)
            => value.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

        private static string FindRepositoryRoot()
        {
            foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                var directory = new DirectoryInfo(start);
                while (directory is not null)
                {
                    if (File.Exists(Path.Combine(directory.FullName, "Spire.Analyzers.slnx")))
                        return directory.FullName;
                    directory = directory.Parent;
                }
            }

            throw new InvalidOperationException("Could not locate the repository root.");
        }
    }

    private sealed record CatalogEntry(string name, string baseline, string revision);
}
