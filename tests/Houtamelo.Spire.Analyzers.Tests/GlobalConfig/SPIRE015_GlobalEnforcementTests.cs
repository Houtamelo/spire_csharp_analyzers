using System.Collections.Generic;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.Rules;
using Xunit;

namespace Houtamelo.Spire.Analyzers.Tests.GlobalConfig;

public class SPIRE015_GlobalEnforcementTests
{
    private static readonly Dictionary<string, string> GlobalEnabled = new()
    {
        ["build_property.Spire_EnforceExhaustivenessOnAllEnumTypes"] = "true"
    };

    [Fact]
    public async Task GlobalEnabled_UnmarkedEnum_MissingMember_Reports()
    {
        const string source = """
            public enum Color { Red, Green, Blue }
            public class C
            {
                public int M(Color c) => c switch
                {
                    Color.Red => 1,
                    Color.Green => 2,
                };
            }
            """;

        var diagnostics = await GlobalConfigAnalyzerTestHelper
            .GetDiagnosticsAsync<SPIRE015ExhaustiveEnumSwitchAnalyzer>(source, GlobalEnabled);

        Assert.Contains(diagnostics, d => d.Id == "SPIRE015");
    }

    [Fact]
    public async Task GlobalDisabled_UnmarkedEnum_MissingMember_NoReport()
    {
        const string source = """
            public enum Color { Red, Green, Blue }
            public class C
            {
                public int M(Color c) => c switch
                {
                    Color.Red => 1,
                    Color.Green => 2,
                };
            }
            """;

        var diagnostics = await GlobalConfigAnalyzerTestHelper
            .GetDiagnosticsAsync<SPIRE015ExhaustiveEnumSwitchAnalyzer>(source, new Dictionary<string, string>());

        Assert.DoesNotContain(diagnostics, d => d.Id == "SPIRE015");
    }

    [Fact]
    public async Task GlobalEnabled_UnmarkedEnum_AllMembersCovered_NoReport()
    {
        const string source = """
            public enum Color { Red, Green, Blue }
            public class C
            {
                public int M(Color c) => c switch
                {
                    Color.Red => 1,
                    Color.Green => 2,
                    Color.Blue => 3,
                };
            }
            """;

        var diagnostics = await GlobalConfigAnalyzerTestHelper
            .GetDiagnosticsAsync<SPIRE015ExhaustiveEnumSwitchAnalyzer>(source, GlobalEnabled);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SPIRE015");
    }

    [Fact]
    public async Task GlobalEnabled_ExternalEnum_MissingMember_Reports()
    {
        const string source = """
            using System;
            public class C
            {
                public string M(DayOfWeek d) => d switch
                {
                    DayOfWeek.Monday => "Mon",
                    DayOfWeek.Tuesday => "Tue",
                };
            }
            """;

        var diagnostics = await GlobalConfigAnalyzerTestHelper
            .GetDiagnosticsAsync<SPIRE015ExhaustiveEnumSwitchAnalyzer>(source, GlobalEnabled);

        Assert.Contains(diagnostics, d => d.Id == "SPIRE015");
    }
}
