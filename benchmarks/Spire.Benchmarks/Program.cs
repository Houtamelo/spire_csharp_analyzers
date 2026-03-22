using BenchmarkDotNet.Running;

// Run all:           dotnet run -c Release -- --filter '*' --job Dry
// Single class:      dotnet run -c Release -- --filter *Construct*
// Micro with disasm: dotnet run -c Release -- --filter *MicroConstruct*
// JSON only:         dotnet run -c Release -- --filter *Json*

// Clear stale results before running
var resultsDir = Path.Combine("BenchmarkDotNet.Artifacts", "results");
if (Directory.Exists(resultsDir))
    Directory.Delete(resultsDir, recursive: true);

var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

// Detect job suffix from args (--job Dry → _dry, --job Short → _short, default → _default)
var jobSuffix = "_default";
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i].Equals("--job", StringComparison.OrdinalIgnoreCase))
    {
        jobSuffix = "_" + args[i + 1].ToLowerInvariant();
        break;
    }
}

// Output to docs/benchmark-results/ (resolve relative to solution root)
var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var outputDir = Path.Combine(solutionRoot, "docs", "benchmark-results");
Directory.CreateDirectory(outputDir);
var outputFile = Path.Combine(outputDir, $"RESULTS{jobSuffix}.md");

if (Directory.Exists(resultsDir))
{
    var mdFiles = Directory.GetFiles(resultsDir, "*-report-github.md")
        .OrderBy(f => f)
        .ToArray();

    if (mdFiles.Length > 0)
    {
        using var writer = new StreamWriter(outputFile);
        writer.WriteLine("# Benchmark Results");
        writer.WriteLine();
        writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}  ");
        writer.WriteLine($"Job: {jobSuffix.TrimStart('_')} | N: {Spire.Benchmarks.BenchN.Default}  ");
        writer.WriteLine($"Runtime: .NET {Environment.Version}  ");
        writer.WriteLine();

        foreach (var file in mdFiles)
        {
            var name = Path.GetFileNameWithoutExtension(file)
                .Replace("-report-github", "")
                .Replace("Spire.Benchmarks.", "");
            writer.WriteLine($"## {name}");
            writer.WriteLine();

            var tableLines = File.ReadLines(file)
                .Where(l => l.StartsWith("|"))
                .ToList();

            if (tableLines.Count >= 2)
            {
                var headerCells = tableLines[0].Split('|');
                var dropIndices = new HashSet<int>();
                for (int i = 1; i < headerCells.Length - 1; i++)
                {
                    var col = headerCells[i].Trim();
                    if (col is "Error" or "StdDev" or "RatioSD" or "Median")
                        dropIndices.Add(i);
                }

                foreach (var line in tableLines)
                {
                    writer.WriteLine(dropIndices.Count > 0 ? StripColumns(line, dropIndices) : line);
                }
            }

            writer.WriteLine();
        }

        Console.WriteLine($"Combined {mdFiles.Length} reports into {outputFile}");
    }
}

static string StripColumns(string line, HashSet<int> dropIndices)
{
    var cells = line.Split('|');
    var kept = new List<string>();
    for (int i = 0; i < cells.Length; i++)
    {
        if (!dropIndices.Contains(i))
            kept.Add(cells[i]);
    }
    return string.Join("|", kept);
}
