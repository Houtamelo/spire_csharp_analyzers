namespace UnityStructCatalog;

internal sealed record Options(string Source, string Output, string[] References, string? Framework, bool PublicOnly, bool ExcludeGenerics, int MinimumInstanceFields)
{
    internal static Options Parse(string[] arguments)
    {
        string? source = null;
        string? output = null;
        string? framework = null;
        var publicOnly = false;
        var excludeGenerics = false;
        var minimumInstanceFields = 0;
        var references = new List<string>();
        for (var index = 0; index < arguments.Length; index++)
        {
            var option = arguments[index];
            switch (option)
            {
                case "--public-only": publicOnly = true; continue;
                case "--exclude-generics": excludeGenerics = true; continue;
                case "--source" or "--output" or "--reference" or "--framework" or "--min-instance-fields": break;
                default: throw new ArgumentException($"Unknown option: {option}.");
            }
            if (index + 1 == arguments.Length || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Missing value for {option}.");
            var argument = arguments[++index];
            if (option == "--min-instance-fields")
            {
                if (!int.TryParse(argument, out minimumInstanceFields) || minimumInstanceFields < 0)
                    throw new ArgumentException("--min-instance-fields requires a nonnegative integer.");
                continue;
            }
            var value = Path.GetFullPath(argument);
            switch (option)
            {
                case "--source": source = value; break;
                case "--output": output = value; break;
                case "--reference": references.Add(value); break;
                case "--framework": framework = value; break;
            }
        }

        if (source is null || output is null)
            throw new ArgumentException("Both --source and --output are required. Use --help for usage.");
        if (!Directory.Exists(source))
            throw new ArgumentException($"Source directory does not exist: {source}");
        foreach (var reference in references)
            if (!File.Exists(reference) && !Directory.Exists(reference))
                throw new ArgumentException($"Reference does not exist: {reference}");
        if (framework is not null && !Directory.Exists(framework))
            throw new ArgumentException($"Framework directory does not exist: {framework}");
        return new Options(source, output, references.ToArray(), framework, publicOnly, excludeGenerics, minimumInstanceFields);
    }
}
