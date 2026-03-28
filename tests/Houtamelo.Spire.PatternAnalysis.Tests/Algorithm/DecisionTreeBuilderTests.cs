using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.PatternAnalysis.Algorithm;
using Houtamelo.Spire.PatternAnalysis.Domains;
using Houtamelo.Spire.PatternAnalysis.Domains.Numeric;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.PatternAnalysis.Tests.Algorithm;

public class DecisionTreeBuilderTests
{
    // ─── Shared compilation and type helpers ────────────────────────────

    static readonly CSharpCompilation Compilation = CSharpCompilation.Create("Test",
        syntaxTrees:
        [
            CSharpSyntaxTree.ParseText("public enum Color { Red, Green, Blue }")
        ],
        references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
        options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    static readonly ITypeSymbol BoolType = Compilation.GetSpecialType(SpecialType.System_Boolean);
    static readonly ITypeSymbol IntType = Compilation.GetSpecialType(SpecialType.System_Int32);
    static readonly INamedTypeSymbol ColorEnum = Compilation.GetTypeByMetadataName("Color")!;

    static BoolDomain BoolUniverse => BoolDomain.Universe(BoolType);
    static BoolDomain TrueOnly => new(BoolType, hasTrue: true, hasFalse: false);
    static BoolDomain FalseOnly => new(BoolType, hasTrue: false, hasFalse: true);

    static ImmutableHashSet<IFieldSymbol> AllColorMembers =>
        ColorEnum.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.HasConstantValue)
            .ToImmutableHashSet<IFieldSymbol>(SymbolEqualityComparer.Default);

    static EnumDomain ColorUniverse => EnumDomain.Universe(ColorEnum);

    static EnumDomain SingleMember(string name)
    {
        var member = ColorEnum.GetMembers()
            .OfType<IFieldSymbol>()
            .First(f => f.HasConstantValue && f.Name == name);
        var set = ImmutableHashSet.Create<IFieldSymbol>(SymbolEqualityComparer.Default, member);
        return new EnumDomain(ColorEnum, set, AllColorMembers);
    }

    static EnumDomain Members(params string[] names)
    {
        var members = ColorEnum.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.HasConstantValue && names.Contains(f.Name))
            .ToImmutableHashSet<IFieldSymbol>(SymbolEqualityComparer.Default);
        return new EnumDomain(ColorEnum, members, AllColorMembers);
    }

    static SlotIdentifier Slot(int index, ITypeSymbol type) =>
        new SlotIdentifier.TupleSlot(index, type);

    // ─── 1. Empty matrix (0 rows, 1 bool column) → not exhaustive ──────

    [Fact]
    public void EmptyMatrix_one_bool_column_reports_missing_case()
    {
        var matrix = new PatternMatrix(
            ImmutableArray<ImmutableArray<Cell>>.Empty,
            [new Column(Slot(0, BoolType), BoolUniverse)]);

        var result = DecisionTreeBuilder.Check(matrix);

        // Expansion splits bool universe into 2 leaf-level missing cases
        Assert.Equal(2, result.MissingCases.Length);
        foreach (var missing in result.MissingCases)
        {
            Assert.Single(missing.Constraints);
            Assert.False(missing.Constraints[0].Remaining.IsEmpty);
        }
    }

    // ─── 2. Single wildcard row → exhaustive ────────────────────────────

    [Fact]
    public void SingleWildcard_row_is_exhaustive()
    {
        var column = new Column(Slot(0, BoolType), BoolUniverse);
        var rows = ImmutableArray.Create(
            ImmutableArray.Create<Cell>(Cell.Wildcard.Instance));

        var matrix = new PatternMatrix(rows, ImmutableArray.Create(column));
        var result = DecisionTreeBuilder.Check(matrix);

        Assert.Empty(result.MissingCases);
    }

    // ─── 3. Bool: 2 rows (true, false) → exhaustive ────────────────────

    [Fact]
    public void Bool_true_and_false_is_exhaustive()
    {
        var column = new Column(Slot(0, BoolType), BoolUniverse);
        var rows = ImmutableArray.Create(
            ImmutableArray.Create<Cell>(new Cell.Constraint(TrueOnly)),
            ImmutableArray.Create<Cell>(new Cell.Constraint(FalseOnly)));

        var matrix = new PatternMatrix(rows, ImmutableArray.Create(column));
        var result = DecisionTreeBuilder.Check(matrix);

        Assert.Empty(result.MissingCases);
    }

    // ─── 4. Bool: 1 row (true) → not exhaustive, missing {false} ───────

    [Fact]
    public void Bool_true_only_reports_missing_false()
    {
        var column = new Column(Slot(0, BoolType), BoolUniverse);
        var rows = ImmutableArray.Create(
            ImmutableArray.Create<Cell>(new Cell.Constraint(TrueOnly)));

        var matrix = new PatternMatrix(rows, ImmutableArray.Create(column));
        var result = DecisionTreeBuilder.Check(matrix);

        Assert.Single(result.MissingCases);
        var missing = result.MissingCases[0];
        Assert.Single(missing.Constraints);

        // The missing domain should be {false}
        var remaining = (BoolDomain)missing.Constraints[0].Remaining;
        Assert.False(remaining.IsEmpty);
        Assert.False(remaining.IsUniverse);
        // The remaining domain, when intersected with FalseOnly, should not be empty
        var intersected = (BoolDomain)remaining.Intersect(FalseOnly);
        Assert.False(intersected.IsEmpty);
        // And when intersected with TrueOnly, should be empty (true is covered)
        var intersectedTrue = (BoolDomain)remaining.Intersect(TrueOnly);
        Assert.True(intersectedTrue.IsEmpty);
    }

    // ─── 5. Enum (3 members): all covered → exhaustive ─────────────────

    [Fact]
    public void Enum_all_members_covered_is_exhaustive()
    {
        var column = new Column(Slot(0, ColorEnum), ColorUniverse);
        var rows = ImmutableArray.Create(
            ImmutableArray.Create<Cell>(new Cell.Constraint(SingleMember("Red"))),
            ImmutableArray.Create<Cell>(new Cell.Constraint(SingleMember("Green"))),
            ImmutableArray.Create<Cell>(new Cell.Constraint(SingleMember("Blue"))));

        var matrix = new PatternMatrix(rows, ImmutableArray.Create(column));
        var result = DecisionTreeBuilder.Check(matrix);

        Assert.Empty(result.MissingCases);
    }

    // ─── 6. Enum: missing one → reports correct missing member ──────────

    [Fact]
    public void Enum_missing_blue_reports_it()
    {
        var column = new Column(Slot(0, ColorEnum), ColorUniverse);
        var rows = ImmutableArray.Create(
            ImmutableArray.Create<Cell>(new Cell.Constraint(SingleMember("Red"))),
            ImmutableArray.Create<Cell>(new Cell.Constraint(SingleMember("Green"))));

        var matrix = new PatternMatrix(rows, ImmutableArray.Create(column));
        var result = DecisionTreeBuilder.Check(matrix);

        Assert.Single(result.MissingCases);
        var missing = result.MissingCases[0];
        Assert.Single(missing.Constraints);

        // The remaining domain should contain only Blue
        var remaining = (EnumDomain)missing.Constraints[0].Remaining;
        Assert.False(remaining.IsEmpty);
        // Intersecting with Blue singleton should be non-empty
        var blueIntersect = (EnumDomain)remaining.Intersect(SingleMember("Blue"));
        Assert.False(blueIntersect.IsEmpty);
        // Intersecting with Red should be empty
        var redIntersect = (EnumDomain)remaining.Intersect(SingleMember("Red"));
        Assert.True(redIntersect.IsEmpty);
    }

    // ─── 7. 2-column (bool, bool): 4 rows → exhaustive ─────────────────

    [Fact]
    public void TwoBoolColumns_all_four_combos_is_exhaustive()
    {
        var col0 = new Column(Slot(0, BoolType), BoolUniverse);
        var col1 = new Column(Slot(1, BoolType), BoolUniverse);

        var rows = ImmutableArray.Create(
            ImmutableArray.Create<Cell>(new Cell.Constraint(TrueOnly), new Cell.Constraint(TrueOnly)),
            ImmutableArray.Create<Cell>(new Cell.Constraint(TrueOnly), new Cell.Constraint(FalseOnly)),
            ImmutableArray.Create<Cell>(new Cell.Constraint(FalseOnly), new Cell.Constraint(TrueOnly)),
            ImmutableArray.Create<Cell>(new Cell.Constraint(FalseOnly), new Cell.Constraint(FalseOnly)));

        var matrix = new PatternMatrix(rows, ImmutableArray.Create(col0, col1));
        var result = DecisionTreeBuilder.Check(matrix);

        Assert.Empty(result.MissingCases);
    }

    // ─── 8. 2-column (bool, bool): 3 rows → missing one combo ──────────

    [Fact]
    public void TwoBoolColumns_missing_false_false_reports_it()
    {
        var col0 = new Column(Slot(0, BoolType), BoolUniverse);
        var col1 = new Column(Slot(1, BoolType), BoolUniverse);

        // Missing: (false, false)
        var rows = ImmutableArray.Create(
            ImmutableArray.Create<Cell>(new Cell.Constraint(TrueOnly), new Cell.Constraint(TrueOnly)),
            ImmutableArray.Create<Cell>(new Cell.Constraint(TrueOnly), new Cell.Constraint(FalseOnly)),
            ImmutableArray.Create<Cell>(new Cell.Constraint(FalseOnly), new Cell.Constraint(TrueOnly)));

        var matrix = new PatternMatrix(rows, ImmutableArray.Create(col0, col1));
        var result = DecisionTreeBuilder.Check(matrix);

        Assert.Single(result.MissingCases);
        var missing = result.MissingCases[0];
        Assert.Equal(2, missing.Constraints.Length);
    }

    // ─── 9. 2-column (enum{A,B}, bool): wildcard covers partition ───────

    [Fact]
    public void EnumBool_wildcard_in_second_column_covers_partition()
    {
        // Using a 2-member enum for simplicity (Red, Green from Color — pretend Blue doesn't exist)
        // Actually, let's use the full Color enum but with 3 rows:
        // Row 0: Red, true
        // Row 1: Red, false
        // Row 2: Green, _
        // Row 3: Blue, _
        // This should be exhaustive (wildcard covers both true and false for Green and Blue)

        var col0 = new Column(Slot(0, ColorEnum), ColorUniverse);
        var col1 = new Column(Slot(1, BoolType), BoolUniverse);

        var rows = ImmutableArray.Create(
            ImmutableArray.Create<Cell>(new Cell.Constraint(SingleMember("Red")), new Cell.Constraint(TrueOnly)),
            ImmutableArray.Create<Cell>(new Cell.Constraint(SingleMember("Red")), new Cell.Constraint(FalseOnly)),
            ImmutableArray.Create<Cell>(new Cell.Constraint(SingleMember("Green")), Cell.Wildcard.Instance),
            ImmutableArray.Create<Cell>(new Cell.Constraint(SingleMember("Blue")), Cell.Wildcard.Instance));

        var matrix = new PatternMatrix(rows, ImmutableArray.Create(col0, col1));
        var result = DecisionTreeBuilder.Check(matrix);

        Assert.Empty(result.MissingCases);
    }

    // ─── 10. Numeric: two complementary ranges → exhaustive ─────────────

    [Fact]
    public void Numeric_complementary_ranges_exhaustive()
    {
        // int: [int.MinValue, 0] and (0, int.MaxValue]
        var intMin = (double)int.MinValue;
        var intMax = (double)int.MaxValue;

        var leftRange = new NumericDomain(IntType,
            IntervalSet.Single(new Interval(intMin, 0, true, true)),
            intMin, intMax);
        var rightRange = new NumericDomain(IntType,
            IntervalSet.Single(new Interval(0, intMax, false, true)),
            intMin, intMax);

        var column = new Column(Slot(0, IntType), NumericDomain.Universe(IntType));

        var rows = ImmutableArray.Create(
            ImmutableArray.Create<Cell>(new Cell.Constraint(leftRange)),
            ImmutableArray.Create<Cell>(new Cell.Constraint(rightRange)));

        var matrix = new PatternMatrix(rows, ImmutableArray.Create(column));
        var result = DecisionTreeBuilder.Check(matrix);

        Assert.Empty(result.MissingCases);
    }

    // ─── 11. Numeric: gap at zero → not exhaustive ──────────────────────

    [Fact]
    public void Numeric_gap_at_zero_reports_missing()
    {
        // int: [int.MinValue, 0) and (0, int.MaxValue] — missing exactly {0}
        var intMin = (double)int.MinValue;
        var intMax = (double)int.MaxValue;

        var leftRange = new NumericDomain(IntType,
            IntervalSet.Single(new Interval(intMin, 0, true, false)),
            intMin, intMax);
        var rightRange = new NumericDomain(IntType,
            IntervalSet.Single(new Interval(0, intMax, false, true)),
            intMin, intMax);

        var column = new Column(Slot(0, IntType), NumericDomain.Universe(IntType));

        var rows = ImmutableArray.Create(
            ImmutableArray.Create<Cell>(new Cell.Constraint(leftRange)),
            ImmutableArray.Create<Cell>(new Cell.Constraint(rightRange)));

        var matrix = new PatternMatrix(rows, ImmutableArray.Create(column));
        var result = DecisionTreeBuilder.Check(matrix);

        Assert.NotEmpty(result.MissingCases);
        // There should be at least one missing case containing a numeric domain around 0
        var allConstraints = result.MissingCases.SelectMany(mc => mc.Constraints).ToArray();
        Assert.NotEmpty(allConstraints);
        // At least one remaining domain should be non-empty (the gap at 0)
        Assert.Contains(allConstraints, c => !c.Remaining.IsEmpty);
    }
}
