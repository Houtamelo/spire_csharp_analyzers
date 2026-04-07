using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Xunit;

namespace Houtamelo.Spire.Analyzers.Tests.GlobalConfig;

public class CS8509SuppressorTests
{
    private static readonly Dictionary<string, string> GlobalEnabled = new()
    {
        ["build_property.Spire_EnforceExhaustivenessOnAllEnumTypes"] = "true"
    };

    private static async Task AssertSwitchIsSuppressed(string source, Dictionary<string, string> globalOptions)
    {
        var compilerDiags = await GlobalConfigAnalyzerTestHelper.GetCompilerDiagnosticsAsync(source, globalOptions);
        Assert.Contains(compilerDiags, d => d.Id is "CS8509" or "CS8524");

        var allDiags = await GlobalConfigAnalyzerTestHelper
            .GetAllDiagnosticsAsync<CS8509Suppressor>(source, globalOptions);
        var unsuppressed = allDiags.Where(d => d.Id is "CS8509" or "CS8524" && !d.IsSuppressed).ToList();
        Assert.Empty(unsuppressed);
    }

    private static async Task AssertSwitchIsNotSuppressed(string source, Dictionary<string, string> globalOptions)
    {
        var allDiags = await GlobalConfigAnalyzerTestHelper
            .GetAllDiagnosticsAsync<CS8509Suppressor>(source, globalOptions);
        var unsuppressed = allDiags.Where(d => d.Id is "CS8509" or "CS8524" && !d.IsSuppressed).ToList();
        Assert.NotEmpty(unsuppressed);
    }

    [Fact]
    public async Task PlainEnum_AllNamedMembersCovered_Suppresses()
    {
        const string source = """
            public enum Color { Red, Green, Blue }
            public class C
            {
                public int M(Color c) => c switch
                {
                    Color.Red   => 1,
                    Color.Green => 2,
                    Color.Blue  => 3,
                };
            }
            """;

        await AssertSwitchIsSuppressed(source, new Dictionary<string, string>());
    }

    [Fact]
    public async Task GlobalEnabled_PlainEnum_AllNamedMembersCovered_Suppresses()
    {
        const string source = """
            public enum Stone { Rock, Paper, Scissors }
            public class C
            {
                public string M(Stone s) => s switch
                {
                    Stone.Rock     => "rock",
                    Stone.Paper    => "paper",
                    Stone.Scissors => "scissors",
                };
            }
            """;

        await AssertSwitchIsSuppressed(source, GlobalEnabled);
    }

    [Fact]
    public async Task TupleOfUnmarkedEnums_AllCombinationsCovered_Suppresses()
    {
        const string source = """
            public enum Bit { Zero, One }
            public class C
            {
                public int M(Bit a, Bit b) => (a, b) switch
                {
                    (Bit.Zero, Bit.Zero) => 0,
                    (Bit.Zero, Bit.One)  => 1,
                    (Bit.One,  Bit.Zero) => 2,
                    (Bit.One,  Bit.One)  => 3,
                };
            }
            """;

        await AssertSwitchIsSuppressed(source, new Dictionary<string, string>());
    }

    [Fact]
    public async Task GlobalEnabled_TupleOfEnums_AllCombinationsCovered_Suppresses()
    {
        const string source = """
            public enum Stone { Rock, Paper, Scissors }
            public class C
            {
                public string M(Stone a, Stone b) => (a, b) switch
                {
                    (Stone.Rock,     Stone.Rock)     => "RR",
                    (Stone.Rock,     Stone.Paper)    => "RP",
                    (Stone.Rock,     Stone.Scissors) => "RS",
                    (Stone.Paper,    Stone.Rock)     => "PR",
                    (Stone.Paper,    Stone.Paper)    => "PP",
                    (Stone.Paper,    Stone.Scissors) => "PS",
                    (Stone.Scissors, Stone.Rock)     => "SR",
                    (Stone.Scissors, Stone.Paper)    => "SP",
                    (Stone.Scissors, Stone.Scissors) => "SS",
                };
            }
            """;

        await AssertSwitchIsSuppressed(source, GlobalEnabled);
    }

    [Fact]
    public async Task NullableEnum_AllNamedMembersCoveredWithNullArm_Suppresses()
    {
        const string source = """
            public enum Dir { North, South, East, West }
            public class C
            {
                public string M(Dir? d) => d switch
                {
                    null      => "none",
                    Dir.North => "N",
                    Dir.South => "S",
                    Dir.East  => "E",
                    Dir.West  => "W",
                };
            }
            """;

        await AssertSwitchIsSuppressed(source, new Dictionary<string, string>());
    }

    [Fact]
    public async Task TupleOfEnumAndBool_AllCombinationsCovered_Suppresses()
    {
        const string source = """
            public enum Op { Add, Remove }
            public class C
            {
                public string M(Op op, bool enabled) => (op, enabled) switch
                {
                    (Op.Add,    true)  => "add-on",
                    (Op.Add,    false) => "add-off",
                    (Op.Remove, true)  => "rm-on",
                    (Op.Remove, false) => "rm-off",
                };
            }
            """;

        await AssertSwitchIsSuppressed(source, new Dictionary<string, string>());
    }

    [Fact]
    public async Task PlainEnum_MissingMember_NotSuppressed()
    {
        const string source = """
            public enum Color { Red, Green, Blue }
            public class C
            {
                public int M(Color c) => c switch
                {
                    Color.Red   => 1,
                    Color.Green => 2,
                };
            }
            """;

        await AssertSwitchIsNotSuppressed(source, new Dictionary<string, string>());
    }

    [Fact]
    public async Task TupleOfEnums_MissingCombination_NotSuppressed()
    {
        const string source = """
            public enum Bit { Zero, One }
            public class C
            {
                public int M(Bit a, Bit b) => (a, b) switch
                {
                    (Bit.Zero, Bit.Zero) => 0,
                    (Bit.Zero, Bit.One)  => 1,
                    (Bit.One,  Bit.Zero) => 2,
                };
            }
            """;

        await AssertSwitchIsNotSuppressed(source, new Dictionary<string, string>());
    }

    [Fact]
    public async Task GlobalEnabled_PlainEnum_MissingMember_NotSuppressed()
    {
        const string source = """
            public enum Color { Red, Green, Blue }
            public class C
            {
                public int M(Color c) => c switch
                {
                    Color.Red   => 1,
                    Color.Green => 2,
                };
            }
            """;

        await AssertSwitchIsNotSuppressed(source, GlobalEnabled);
    }
}
