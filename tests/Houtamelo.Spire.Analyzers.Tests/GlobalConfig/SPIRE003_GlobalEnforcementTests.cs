using System.Collections.Generic;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.Rules;
using Xunit;

namespace Houtamelo.Spire.Analyzers.Tests.GlobalConfig;

public class SPIRE003_GlobalEnforcementTests
{
    private static readonly Dictionary<string, string> GlobalEnabled = new()
    {
        ["build_property.Spire_EnforceExhaustivenessOnAllEnumTypes"] = "true"
    };

    [Fact]
    public async Task GlobalEnabled_NoZeroMember_DefaultReports()
    {
        const string source = """
            public enum Status { Active = 1, Inactive = 2 }
            public class C
            {
                public Status M() => default(Status);
            }
            """;

        var diagnostics = await GlobalConfigAnalyzerTestHelper
            .GetDiagnosticsAsync<SPIRE003DefaultOfEnforceInitializationStructAnalyzer>(source, GlobalEnabled);

        Assert.Contains(diagnostics, d => d.Id == "SPIRE003");
    }

    [Fact]
    public async Task GlobalDisabled_NoZeroMember_DefaultNoReport()
    {
        const string source = """
            public enum Status { Active = 1, Inactive = 2 }
            public class C
            {
                public Status M() => default(Status);
            }
            """;

        var diagnostics = await GlobalConfigAnalyzerTestHelper
            .GetDiagnosticsAsync<SPIRE003DefaultOfEnforceInitializationStructAnalyzer>(source, new Dictionary<string, string>());

        Assert.DoesNotContain(diagnostics, d => d.Id == "SPIRE003");
    }

    [Fact]
    public async Task GlobalEnabled_WithZeroMember_DefaultNoReport()
    {
        const string source = """
            public enum Color { Red = 0, Green = 1, Blue = 2 }
            public class C
            {
                public Color M() => default(Color);
            }
            """;

        var diagnostics = await GlobalConfigAnalyzerTestHelper
            .GetDiagnosticsAsync<SPIRE003DefaultOfEnforceInitializationStructAnalyzer>(source, GlobalEnabled);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SPIRE003");
    }
}
