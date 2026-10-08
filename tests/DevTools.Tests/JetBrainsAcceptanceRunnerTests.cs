using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spire.Tool;
using Xunit;

namespace DevTools.Tests;

public sealed class JetBrainsAcceptanceRunnerTests
{
    [Fact]
    public void IssueRows_ReadsInspectCodeSarifAndPreservesDiagnosticLocations()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = AcceptanceFixture.Create(repositoryRoot);
        var report = Path.Combine(fixture.Root, "jetbrains-inspections.sarif");
        var generatedReport = Path.Combine(repositoryRoot, "tmp", "unity-asmdefs", "jetbrains-inspections.sarif");
        var usesGeneratedReport = File.Exists(generatedReport);
        if (usesGeneratedReport)
            File.Copy(generatedReport, report);
        else
            File.WriteAllText(report, SarifFixture, Encoding.UTF8);

        var rows = JetBrainsWorkflow.IssueRows(report);
        Assert.NotEmpty(rows);

        var spire002 = rows.OfType<JsonObject>()
            .Where(row => GetString(row, "id").EndsWith("SPIRE002", StringComparison.Ordinal) &&
                NormalizePath(GetString(row, "uri")).EndsWith("Assets/RuntimeAuto/RuntimeAuto.cs", StringComparison.Ordinal))
            .ToArray();
        var spire016 = rows.OfType<JsonObject>()
            .Where(row => GetString(row, "id").EndsWith("SPIRE016", StringComparison.Ordinal) &&
                NormalizePath(GetString(row, "uri")).EndsWith("Assets/RuntimeAuto/_rider.cs", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(spire002);
        Assert.Single(spire016);

        if (!usesGeneratedReport)
        {
            Assert.Equal(11, GetInt(GetObject(spire002[0], "region"), "startLine"));
            Assert.Equal(7, GetInt(GetObject(spire016[0], "region"), "startLine"));
            Assert.Equal(18, GetInt(GetObject(spire016[0], "region"), "startColumn"));
        }
    }

    [Fact]
    public void IssueRows_RejectsAResultWithoutAPhysicalLocation()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = AcceptanceFixture.Create(repositoryRoot);
        var report = Path.Combine(fixture.Root, "malformed.sarif");
        File.WriteAllText(
            report,
            "{\"version\":\"2.1.0\",\"runs\":[{\"tool\":{\"driver\":{\"name\":\"InspectCode\"}},\"results\":[{\"ruleId\":\"SPIRE002\",\"message\":{\"text\":\"missing location\"},\"locations\":[]}]}]}",
            Encoding.UTF8);

        var error = Assert.ThrowsAny<Exception>(() => JetBrainsWorkflow.IssueRows(report));
        Assert.Contains("physical location", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IssueRows_RejectsMalformedSarifJson()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = AcceptanceFixture.Create(repositoryRoot);
        var report = Path.Combine(fixture.Root, "malformed-json.sarif");
        File.WriteAllText(report, "{ this is not SARIF", Encoding.UTF8);

        Assert.ThrowsAny<Exception>(() => JetBrainsWorkflow.IssueRows(report));
    }

    [Fact]
    public void IssueRows_RejectsEmptyPhysicalLocationUriLineAndWrongSchema()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = AcceptanceFixture.Create(repositoryRoot);
        var malformedReports = new[]
        {
            (Name: "empty-physical-location", Json: """
                {
                  "version": "2.1.0",
                  "runs": [{ "results": [{ "locations": [{ "physicalLocation": {} }] }] }]
                }
                """),
            (Name: "missing-uri", Json: """
                {
                  "version": "2.1.0",
                  "runs": [{ "results": [{ "locations": [{ "physicalLocation": {
                    "artifactLocation": {}, "region": { "startLine": 1 }
                  } }] }] }]
                }
                """),
            (Name: "missing-line", Json: """
                {
                  "version": "2.1.0",
                  "runs": [{ "results": [{ "locations": [{ "physicalLocation": {
                    "artifactLocation": { "uri": "file:///workspace/Assets/RuntimeAuto/RuntimeAuto.cs" },
                    "region": {}
                  } }] }] }]
                }
                """),
            (Name: "wrong-schema", Json: """
                {
                  "version": "2.0.0",
                  "runs": [{ "results": [{ "locations": [{ "physicalLocation": {
                    "artifactLocation": { "uri": "file:///workspace/Assets/RuntimeAuto/RuntimeAuto.cs" },
                    "region": { "startLine": 1 }
                  } }] }] }]
                }
                """),
        };

        foreach (var malformed in malformedReports)
        {
            var report = Path.Combine(fixture.Root, malformed.Name + ".sarif");
            File.WriteAllText(report, malformed.Json, Encoding.UTF8);
            Assert.ThrowsAny<Exception>(() => JetBrainsWorkflow.IssueRows(report));
        }
    }

    private const string SarifFixture = """
        {
          "$schema": "https://json.schemastore.org/sarif-2.1.0.json",
          "version": "2.1.0",
          "runs": [
            {
              "tool": {
                "driver": {
                  "name": "InspectCode",
                  "version": "2026.2.3.1"
                }
              },
              "results": [
                {
                  "ruleId": "SPIRE002",
                  "level": "warning",
                  "message": { "text": "The type requires initialization." },
                  "locations": [
                    {
                      "physicalLocation": {
                        "artifactLocation": { "uri": "file:///workspace/Assets/RuntimeAuto/RuntimeAuto.cs" },
                        "region": { "startLine": 11, "startColumn": 9, "endLine": 11, "endColumn": 20 }
                      }
                    }
                  ]
                },
                {
                  "ruleId": "SPIRE016",
                  "level": "warning",
                  "message": { "text": "Use SpireEnum<T>.From(value)." },
                  "locations": [
                    {
                      "physicalLocation": {
                        "artifactLocation": { "uri": "file:///workspace/Assets/RuntimeAuto/_rider.cs" },
                        "region": { "startLine": 7, "startColumn": 18, "endLine": 7, "endColumn": 42 }
                      }
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private static JsonObject GetObject(JsonObject parent, string property)
        => parent[property]?.AsObject() ?? throw new Xunit.Sdk.XunitException($"JSON object property '{property}' was missing.");

    private static string GetString(JsonObject parent, string property)
        => parent[property]?.GetValue<string>() ?? throw new Xunit.Sdk.XunitException($"JSON string property '{property}' was missing.");

    private static int GetInt(JsonObject parent, string property)
        => parent[property]?.GetValue<int>() ?? throw new Xunit.Sdk.XunitException($"JSON integer property '{property}' was missing.");

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static string FindRepositoryRoot()
    {
        foreach (var startingPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "tools", "Spire.Tool.Core", "Spire.Tool.Core.csproj")) &&
                    File.Exists(Path.Combine(directory.FullName, "src", "Houtamelo.Spire", "Houtamelo.Spire.csproj")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root containing the Spire.Tool implementation.");
    }

    private sealed class AcceptanceFixture : IDisposable
    {
        private AcceptanceFixture(string root)
        {
            Root = root;
            Directory.CreateDirectory(root);
        }

        public string Root { get; }

        public static AcceptanceFixture Create(string repositoryRoot)
            => new(Path.Combine(repositoryRoot, "tmp", "devtools-jetbrains-acceptance-tests", Guid.NewGuid().ToString("N")));

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
