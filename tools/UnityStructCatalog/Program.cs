using System.Text.Json;
using UnityStructCatalog;

try
{
    if (args is ["--help"] or ["-h"])
    {
        Console.WriteLine("Usage: dotnet run --project tools/UnityStructCatalog -- --source <source-root> --output <output-directory> [--framework <runtime-assembly-directory>] [--reference <dll-or-directory>]... [--public-only] [--exclude-generics] [--min-instance-fields <count>]");
        Console.WriteLine("Scans every C# source file and all preprocessor branches. Writes all.json, unmanaged.json, managed.json, unresolved.json, unmanaged.tsv, and summary.json.");
        Console.WriteLine("--framework replaces host framework references. Use Unity's runtime implementation assemblies (e.g. MonoBleedingEdge/lib/mono/unityjit-linux), not API-only reference assemblies.");
        Console.WriteLine("--public-only requires public accessibility through every enclosing type. --exclude-generics also excludes non-generic types nested in generic types.");
        Console.WriteLine("--min-instance-fields 2 excludes empty and single-field structs. Counts instance storage fields, including generated backing fields, rather than measuring byte size.");
        return 0;
    }

    var options = Options.Parse(args);
    var result = Catalog.Scan(options);
    Directory.CreateDirectory(options.Output);
    var jsonOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    foreach (var status in new[] { "all", "unmanaged", "managed", "unresolved" })
    {
        var entries = status == "all" ? result.Structs : result.Structs.Where(entry => entry.Status == status).ToArray();
        File.WriteAllText(Path.Combine(options.Output, status + ".json"), JsonSerializer.Serialize(entries, jsonOptions) + "\n");
    }

    File.WriteAllText(Path.Combine(options.Output, "summary.json"), JsonSerializer.Serialize(result.Summary, jsonOptions) + "\n");
    File.WriteAllLines(Path.Combine(options.Output, "unmanaged.tsv"), new[] { "type\tgeneric\tref-like\tdeclarations" }.Concat(
        result.Structs.Where(entry => entry.Status == "unmanaged").Select(entry =>
            $"{entry.Name}\t{entry.IsGeneric}\t{entry.IsRefLike}\t{string.Join(";", entry.Declarations.Select(declaration => $"{declaration.File}:{declaration.Line}"))}")));
    Console.WriteLine($"Scanned {result.Summary.SourceFiles} C# files: {result.Summary.ScannedStructs} structs; retained {result.Summary.TotalStructs} ({result.Summary.Unmanaged} unmanaged, {result.Summary.Managed} managed, {result.Summary.Unresolved} unresolved).");
    Console.WriteLine($"Inventory: {options.Output}");
    return 0;
}
catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or BadImageFormatException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
