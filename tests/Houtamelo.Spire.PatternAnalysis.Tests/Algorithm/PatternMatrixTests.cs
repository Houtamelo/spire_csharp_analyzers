using System.Collections.Immutable;
using Houtamelo.Spire.PatternAnalysis.Algorithm;
using Houtamelo.Spire.PatternAnalysis.Domains;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.PatternAnalysis.Tests.Algorithm;

public class PatternMatrixTests
{
    static ITypeSymbol GetBoolType()
    {
        var compilation = CSharpCompilation.Create("Test",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation.GetSpecialType(SpecialType.System_Boolean);
    }

    static readonly ITypeSymbol BoolType = GetBoolType();

    static BoolDomain TrueOnly => new(BoolType, hasTrue: true, hasFalse: false);
    static BoolDomain FalseOnly => new(BoolType, hasTrue: false, hasFalse: true);
    static BoolDomain BoolUniverse => BoolDomain.Universe(BoolType);

    // ─── Construction ──────────────────────────────────────────────────

    [Fact]
    public void Construction_two_rows_one_column()
    {
        var column = new Column(
            new SlotIdentifier.TupleSlot(0, BoolType),
            BoolUniverse);

        var rows = ImmutableArray.Create(
            ImmutableArray.Create<Cell>(new Cell.Constraint(TrueOnly)),
            ImmutableArray.Create<Cell>(new Cell.Constraint(FalseOnly)));

        var matrix = new PatternMatrix(rows, ImmutableArray.Create(column));

        Assert.Equal(2, matrix.RowCount);
        Assert.Equal(1, matrix.ColumnCount);
    }

    // ─── Specialize with matching constraint ───────────────────────────

    [Fact]
    public void Specialize_matching_constraint_keeps_row()
    {
        var column = new Column(
            new SlotIdentifier.TupleSlot(0, BoolType),
            BoolUniverse);

        var rows = ImmutableArray.Create(
            ImmutableArray.Create<Cell>(new Cell.Constraint(TrueOnly)));

        var matrix = new PatternMatrix(rows, ImmutableArray.Create(column));
        var result = matrix.Specialize(0, TrueOnly);

        Assert.Equal(1, result.RowCount);
    }

    // ─── Specialize with non-matching constraint ───────────────────────

    [Fact]
    public void Specialize_non_matching_constraint_drops_row()
    {
        var column = new Column(
            new SlotIdentifier.TupleSlot(0, BoolType),
            BoolUniverse);

        var rows = ImmutableArray.Create(
            ImmutableArray.Create<Cell>(new Cell.Constraint(TrueOnly)));

        var matrix = new PatternMatrix(rows, ImmutableArray.Create(column));
        var result = matrix.Specialize(0, FalseOnly);

        Assert.Equal(0, result.RowCount);
    }

    // ─── Specialize keeps wildcards ────────────────────────────────────

    [Fact]
    public void Specialize_keeps_wildcard_rows()
    {
        var column = new Column(
            new SlotIdentifier.TupleSlot(0, BoolType),
            BoolUniverse);

        var rows = ImmutableArray.Create(
            ImmutableArray.Create<Cell>(Cell.Wildcard.Instance));

        var matrix = new PatternMatrix(rows, ImmutableArray.Create(column));
        var result = matrix.Specialize(0, TrueOnly);

        Assert.Equal(1, result.RowCount);
    }

    // ─── Specialize removes column ─────────────────────────────────────

    [Fact]
    public void Specialize_removes_the_specialized_column()
    {
        var column = new Column(
            new SlotIdentifier.TupleSlot(0, BoolType),
            BoolUniverse);

        var rows = ImmutableArray.Create(
            ImmutableArray.Create<Cell>(Cell.Wildcard.Instance));

        var matrix = new PatternMatrix(rows, ImmutableArray.Create(column));
        var result = matrix.Specialize(0, TrueOnly);

        Assert.Equal(0, result.ColumnCount);
    }

    // ─── Mixed rows ────────────────────────────────────────────────────

    [Fact]
    public void Specialize_mixed_rows_keeps_matching_and_wildcards()
    {
        var column = new Column(
            new SlotIdentifier.TupleSlot(0, BoolType),
            BoolUniverse);

        var rows = ImmutableArray.Create(
            ImmutableArray.Create<Cell>(new Cell.Constraint(TrueOnly)),
            ImmutableArray.Create<Cell>(Cell.Wildcard.Instance),
            ImmutableArray.Create<Cell>(new Cell.Constraint(FalseOnly)));

        var matrix = new PatternMatrix(rows, ImmutableArray.Create(column));
        var result = matrix.Specialize(0, TrueOnly);

        // Row 0 (Constraint({true})) matches {true} -> kept
        // Row 1 (Wildcard) -> kept
        // Row 2 (Constraint({false})) does not match {true} -> dropped
        Assert.Equal(2, result.RowCount);
    }

    // ─── Multi-column ──────────────────────────────────────────────────

    [Fact]
    public void Specialize_multi_column_keeps_remaining_columns()
    {
        var col0 = new Column(
            new SlotIdentifier.TupleSlot(0, BoolType),
            BoolUniverse);
        var col1 = new Column(
            new SlotIdentifier.TupleSlot(1, BoolType),
            BoolUniverse);

        var rows = ImmutableArray.Create(
            ImmutableArray.Create<Cell>(
                new Cell.Constraint(TrueOnly),
                new Cell.Constraint(FalseOnly)),
            ImmutableArray.Create<Cell>(
                Cell.Wildcard.Instance,
                new Cell.Constraint(TrueOnly)));

        var matrix = new PatternMatrix(rows, ImmutableArray.Create(col0, col1));

        Assert.Equal(2, matrix.ColumnCount);

        var result = matrix.Specialize(0, TrueOnly);

        // Both rows kept (first matches, second is wildcard)
        Assert.Equal(2, result.RowCount);
        // First column removed, second remains
        Assert.Equal(1, result.ColumnCount);
    }

    // ─── Empty after specialize ────────────────────────────────────────

    [Fact]
    public void Specialize_all_non_matching_gives_empty_matrix()
    {
        var column = new Column(
            new SlotIdentifier.TupleSlot(0, BoolType),
            BoolUniverse);

        var rows = ImmutableArray.Create(
            ImmutableArray.Create<Cell>(new Cell.Constraint(FalseOnly)),
            ImmutableArray.Create<Cell>(new Cell.Constraint(FalseOnly)),
            ImmutableArray.Create<Cell>(new Cell.Constraint(FalseOnly)));

        var matrix = new PatternMatrix(rows, ImmutableArray.Create(column));
        var result = matrix.Specialize(0, TrueOnly);

        Assert.Equal(0, result.RowCount);
    }
}
