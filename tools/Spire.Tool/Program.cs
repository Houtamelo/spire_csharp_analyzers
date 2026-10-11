using System.Text.Json.Nodes;
using Spire.Tool;

try
{
    var options = Options.Parse(args);
    if (options.Help)
    {
        Console.WriteLine("Spire.Tool: package | unity-integration | unity-behavioral | unity-harness | unity-asmdefs | jetbrains | hook");
        Console.WriteLine("Usage: dotnet run --project tools/Spire.Tool -- <command> [options]");
        Console.WriteLine("Common options: --root PATH --configuration Debug|Release --no-build --help");
        Console.WriteLine("package: --output PATH");
        Console.WriteLine("Unity tests: --editor PATH --project PATH --timeout SECONDS");
        Console.WriteLine("unity-integration: --graphics --method NAME");
        Console.WriteLine("unity-behavioral, unity-harness, unity-asmdefs: --prepare-only");
        Console.WriteLine("unity-behavioral: --backend Mono|IL2CPP (Mono runs in EditMode; IL2CPP builds a Linux player)");
        Console.WriteLine("unity-harness: --probe");
        Console.WriteLine("unity-asmdefs: --rider PATH");
        Console.WriteLine("jetbrains: --rider PATH --cli PATH --stage all|inspect|quick-fix|recompile");
        Console.WriteLine("hook [SessionStart|Stop|SessionEnd]: read the event JSON from standard input");
        return 0;
    }
    var backend = options.Value("backend", "Mono");
    if (options.Flag("backend") && options.Command != "unity-behavioral")
        throw new ArgumentException("Option --backend is only supported by unity-behavioral.");
    if (backend is not ("Mono" or "IL2CPP"))
        throw new ArgumentException("Backend must be Mono or IL2CPP.");
    var root = options.Root ?? FindRoot();
    var context = new ToolContext(root);
    var configuration = options.Value("configuration", "Release");
    if (configuration is not ("Debug" or "Release")) throw new ArgumentException("Configuration must be Debug or Release.");
    var editor = options.Value("editor", ToolContext.DefaultEditor);
    var rider = options.Value("rider", ToolContext.DefaultRider);
    var timeout = TimeSpan.FromSeconds(options.Integer("timeout", options.Command == "unity-integration" ? 240 : backend == "IL2CPP" ? 1200 : 900));
    var build = !options.Flag("no-build");
    var prepare = options.Flag("prepare-only");
    string Project(string name) => options.Value("project", context.PathOf("tmp/" + name));
    switch (options.Command)
    {
        case "package":
            var output = await PackageWorkflow.BuildAsync(context, options.Value("output", context.PathOf("tmp/unity-package/com.houtamelo.spire")), configuration, build);
            Console.WriteLine("Unity package: " + output);
            return 0;
        case "unity-integration":
            return await IntegrationWorkflow.RunAsync(context, Project("unity-integration"), editor, configuration, build, options.Flag("graphics"),
                options.Value("method", "Spire.UnityIntegration.SpireUnityIntegration.Run"), timeout);
        case "unity-behavioral":
            return await BehavioralWorkflow.RunAsync(context, Project(backend == "IL2CPP" ? "unity-il2cpp-behavioral" : "unity-behavioral"), editor, configuration, build, prepare, timeout, backend);
        case "unity-harness":
            return await HarnessWorkflow.RunAsync(context, Project("unity-compiler-tests"), editor, configuration, build, prepare, options.Flag("probe"), timeout);
        case "unity-asmdefs":
            return await AsmdefWorkflow.RunAsync(context, Project("unity-asmdefs"), editor, rider, configuration, build, prepare, timeout);
        case "jetbrains":
            var stage = options.Value("stage", "all");
            if (stage is not ("all" or "inspect" or "quick-fix" or "recompile")) throw new ArgumentException("Invalid JetBrains stage: " + stage);
            return await JetBrainsWorkflow.RunAsync(context, Project("unity-asmdefs"), rider, options.Value("cli", context.PathOf("tmp/jetbrains-cli/jb")), editor, stage, timeout);
        case "hook":
            var input = JsonNode.Parse(await Console.In.ReadToEndAsync()) as JsonObject ?? throw new ArgumentException("Expected hook event JSON.");
            if (options.Event != null && input["hook_event_name"]?.GetValue<string>() != options.Event) throw new ArgumentException("Hook event name differs from the command.");
            var result = await LifecycleWorkflow.RunAsync(context, input);
            if (result != null) Console.WriteLine(result.ToJsonString());
            return 0;
        default: throw new ArgumentException("Unknown command: " + options.Command);
    }
}
catch (ArgumentException error) { Console.Error.WriteLine(error.Message); return 2; }
catch (TimeoutException error) { Console.Error.WriteLine(error.Message); return 124; }
catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }

static string FindRoot()
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Spire.Analyzers.slnx"))) return directory.FullName;
    throw new ArgumentException("Could not locate the repository; specify --root.");
}

internal sealed class Options
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);
    public string Command { get; private set; } = "";
    public string? Event { get; private set; }
    public string? Root => _values.TryGetValue("root", out var root) ? root : null;
    public bool Help => Flag("help") || Command == "" && _values.Count == 0;
    public bool Flag(string name) => _values.ContainsKey(name);
    public string Value(string name, string fallback) => _values.TryGetValue(name, out var value) ? value! : fallback;
    public int Integer(string name, int fallback)
    {
        if (!_values.TryGetValue(name, out var value)) return fallback;
        if (!int.TryParse(value, out var result) || result <= 0) throw new ArgumentException("Option --" + name + " must be a positive integer.");
        return result;
    }
    public static Options Parse(string[] args)
    {
        var options = new Options();
        var flags = new HashSet<string>(new[] { "help", "no-build", "prepare-only", "graphics", "probe" }, StringComparer.Ordinal);
        var values = new HashSet<string>(new[] { "root", "configuration", "editor", "project", "output", "timeout", "rider", "cli", "stage", "method", "backend" }, StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument == "-h") argument = "--help";
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                if (options.Command == "") options.Command = argument;
                else if (options.Command == "hook" && options.Event == null) options.Event = argument;
                else throw new ArgumentException("Unexpected argument: " + argument);
                continue;
            }
            var parts = argument.Substring(2).Split('=', 2);
            var name = parts[0];
            if (options._values.ContainsKey(name)) throw new ArgumentException("Duplicate option: --" + name);
            if (flags.Contains(name))
            {
                if (parts.Length != 1) throw new ArgumentException("Option --" + name + " does not accept a value.");
                options._values[name] = null;
            }
            else if (values.Contains(name))
            {
                if (parts.Length == 2) options._values[name] = parts[1];
                else if (++index < args.Length && !args[index].StartsWith("--", StringComparison.Ordinal)) options._values[name] = args[index];
                else throw new ArgumentException("Option --" + name + " requires a value.");
            }
            else throw new ArgumentException("Unknown option: --" + name);
        }
        return options;
    }
}
