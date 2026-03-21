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

// Combine all github-markdown reports into RESULTS.md
if (Directory.Exists(resultsDir))
{
    var mdFiles = Directory.GetFiles(resultsDir, "*-report-github.md")
        .OrderBy(f => f)
        .ToArray();

    if (mdFiles.Length > 0)
    {
        using var writer = new StreamWriter("RESULTS.md");
        writer.WriteLine("# Benchmark Results");
        writer.WriteLine();
        writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
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
                // Find columns to drop from header row
                var headerCells = tableLines[0].Split('|');
                var dropIndices = new HashSet<int>();
                for (int i = 1; i < headerCells.Length - 1; i++)
                {
                    var name2 = headerCells[i].Trim();
                    if (name2 is "Error" or "StdDev" or "RatioSD" or "Median")
                        dropIndices.Add(i);
                }

                foreach (var line in tableLines)
                {
                    if (dropIndices.Count > 0)
                        writer.WriteLine(StripColumns(line, dropIndices));
                    else
                        writer.WriteLine(line);
                }
            }

            writer.WriteLine();
        }

        Console.WriteLine($"Combined {mdFiles.Length} reports into RESULTS.md");
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
