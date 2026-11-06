using System.Text.Json.Nodes;

namespace Spire.Tool;

/// <summary>Validates independently versioned Unity demonstration projects.</summary>
public static class ExamplesWorkflow
{
    /// <summary>Validates every example, or one named example, preserving its checkout and local edits.</summary>
    public static IReadOnlyList<string> Prepare(ToolContext context, string? name = null)
    {
        var examples = FileOps.ReadObject(context.PathOf("examples/SpireExamples.json"))["examples"] as JsonArray
            ?? throw new InvalidDataException("Example catalog requires an examples array.");
        var names = examples.Select(example => example?["name"]?.GetValue<string>()
            ?? throw new InvalidDataException("Example catalog entry requires a name.")).ToArray();
        if (names.Length == 0)
            throw new InvalidDataException("Example catalog is empty.");
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length)
            throw new InvalidDataException("Example catalog contains duplicate names.");
        foreach (var entry in names)
            if (string.IsNullOrWhiteSpace(entry) || entry is "." or ".." ||
                Path.IsPathRooted(entry) || entry.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
                throw new InvalidDataException("Example name must be a single directory name: " + entry);
        if (name != null)
        {
            if (!names.Contains(name, StringComparer.Ordinal))
                throw new ArgumentException("Unknown Spire example: " + name);
            names = new[] { name };
        }

        var projects = names.OrderBy(entry => entry, StringComparer.Ordinal)
            .Select(entry => FileOps.FullPath(context.PathOf("examples/" + entry))).ToArray();
        foreach (var project in projects)
        {
            if (!Directory.Exists(project))
                throw new DirectoryNotFoundException("Clone the example Git repository first: " + project);
            foreach (var required in new[] { "Packages/manifest.json", "ProjectSettings/ProjectVersion.txt" })
                FileOps.RequireFile(Path.Combine(project, required));
            if (!Directory.Exists(Path.Combine(project, "Assets")))
                throw new DirectoryNotFoundException("Example Assets directory is missing: " + project);
            var root = Git(project, "rev-parse", "--show-toplevel").Trim();
            if (FileOps.FullPath(root) != project)
                throw new InvalidDataException("Example must be the root of its own Git checkout: " + project);
            Git(project, "rev-parse", "--verify", "refs/tags/upstream^{commit}");
            foreach (var branch in new[] { "without-spire", "with-spire" })
            {
                if (!HasCommit(project, "refs/heads/" + branch) &&
                    !HasCommit(project, "refs/remotes/origin/" + branch))
                    throw new InvalidDataException("Example comparison branch is missing: " + project + ": " + branch);
            }
        }
        return projects;
    }

    private static bool HasCommit(string project, string reference)
    {
        var result = ProcessRunner.RunAsync("git", new[] { "rev-parse", "--verify", "--quiet", reference + "^{commit}" },
            project, TimeSpan.FromSeconds(30), environment: new Dictionary<string, string?> { ["GIT_OPTIONAL_LOCKS"] = "0" })
            .GetAwaiter().GetResult();
        if (result.ExitCode is not (0 or 1))
            result.EnsureSuccess("Validate example comparison branch " + reference);
        return result.ExitCode == 0;
    }

    private static string Git(string project, params string[] arguments)
    {
        var result = ProcessRunner.RunAsync("git", arguments, project, TimeSpan.FromSeconds(30),
            environment: new Dictionary<string, string?> { ["GIT_OPTIONAL_LOCKS"] = "0" })
            .GetAwaiter().GetResult();
        result.EnsureSuccess("Validate example Git repository " + project);
        return result.StandardOutput;
    }
}
