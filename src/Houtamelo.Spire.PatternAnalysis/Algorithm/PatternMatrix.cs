using System.Collections.Immutable;
using Houtamelo.Spire.PatternAnalysis.Domains;

namespace Houtamelo.Spire.PatternAnalysis.Algorithm;

/// A single cell in the pattern matrix — either a wildcard or a constrained domain.
internal abstract class Cell
{
    private Cell() { }

    /// Matches any value in the slot's domain.
    internal sealed class Wildcard : Cell
    {
        public static readonly Wildcard Instance = new();
    }

    /// Matches a specific subset of the slot's domain.
    internal sealed class Constraint : Cell
    {
        public IValueDomain MatchedValues { get; }

        public Constraint(IValueDomain matchedValues)
        {
            MatchedValues = matchedValues;
        }
    }
}

/// A column in the pattern matrix — associates a slot identifier with its full domain.
internal readonly struct Column
{
    public SlotIdentifier Slot { get; }
    public IValueDomain Domain { get; }

    public Column(SlotIdentifier slot, IValueDomain domain)
    {
        Slot = slot;
        Domain = domain;
    }
}

/// The pattern matrix: rows of cells against typed columns.
/// Core data structure for the Maranget exhaustiveness algorithm.
internal sealed class PatternMatrix
{
    public ImmutableArray<ImmutableArray<Cell>> Rows { get; }
    public ImmutableArray<Column> Columns { get; }
    public int RowCount => Rows.Length;
    public int ColumnCount => Columns.Length;

    public PatternMatrix(
        ImmutableArray<ImmutableArray<Cell>> rows,
        ImmutableArray<Column> columns)
    {
        Rows = rows;
        Columns = columns;
    }

    /// Specialize: keep rows whose cell at columnIndex intersects the partition.
    /// Remove the column. Wildcard cells are kept in all partitions.
    public PatternMatrix Specialize(int columnIndex, IValueDomain partition)
    {
        var rowBuilder = ImmutableArray.CreateBuilder<ImmutableArray<Cell>>();

        foreach (var row in Rows)
        {
            var cell = row[columnIndex];

            bool keep;
            if (cell is Cell.Wildcard)
            {
                keep = true;
            }
            else if (cell is Cell.Constraint constraint)
            {
                var intersection = constraint.MatchedValues.Intersect(partition);
                keep = !intersection.IsEmpty;
            }
            else
            {
                keep = false;
            }

            if (!keep)
                continue;

            // Build the new row with the specialized column removed
            var cellBuilder = ImmutableArray.CreateBuilder<Cell>(row.Length - 1);
            for (int c = 0; c < row.Length; c++)
            {
                if (c != columnIndex)
                    cellBuilder.Add(row[c]);
            }

            rowBuilder.Add(cellBuilder.MoveToImmutable());
        }

        // Build new columns with the specialized column removed
        var colBuilder = ImmutableArray.CreateBuilder<Column>(Columns.Length - 1);
        for (int c = 0; c < Columns.Length; c++)
        {
            if (c != columnIndex)
                colBuilder.Add(Columns[c]);
        }

        return new PatternMatrix(rowBuilder.ToImmutable(), colBuilder.MoveToImmutable());
    }
}
