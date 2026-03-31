using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Houtamelo.Spire.Analyzers.Tests.GlobalConfig;

internal sealed class TestGlobalOptions : AnalyzerConfigOptions
{
    private readonly Dictionary<string, string> _values;

    public TestGlobalOptions(Dictionary<string, string> values)
        => _values = values;

    public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
        => _values.TryGetValue(key, out value);
}

internal sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private static readonly AnalyzerConfigOptions Empty = new TestGlobalOptions(new Dictionary<string, string>());

    private readonly TestGlobalOptions _globalOptions;

    public TestAnalyzerConfigOptionsProvider(Dictionary<string, string> globalOptions)
        => _globalOptions = new TestGlobalOptions(globalOptions);

    public override AnalyzerConfigOptions GlobalOptions => _globalOptions;
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;
}
