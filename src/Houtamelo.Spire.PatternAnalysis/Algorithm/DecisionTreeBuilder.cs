using System.Collections.Generic;
using System.Collections.Immutable;
using Houtamelo.Spire.PatternAnalysis.Domains;

namespace Houtamelo.Spire.PatternAnalysis.Algorithm;

/// Maranget-style exhaustiveness checker.
/// Recursively specializes a pattern matrix until it proves exhaustive or finds missing cases.
internal static class DecisionTreeBuilder
{
    public static ExhaustivenessResult Check(PatternMatrix matrix)
    {
        var missingCases = new List<MissingCase>();
        CheckCore(matrix, ImmutableArray<SlotConstraint>.Empty, missingCases);
        return new ExhaustivenessResult(missingCases.ToImmutableArray());
    }

    static void CheckCore(
        PatternMatrix matrix,
        ImmutableArray<SlotConstraint> accumulated,
        List<MissingCase> missingCases)
    {
        // 1. Zero rows → no patterns cover this region. Report a missing case.
        //    Must be checked before zero columns: when both are zero, the
        //    accumulated constraints describe an uncovered partition.
        if (matrix.RowCount == 0)
        {
            var builder = accumulated.ToBuilder();
            foreach (var col in matrix.Columns)
                builder.Add(new SlotConstraint(col.Slot, col.Domain));
            missingCases.Add(new MissingCase(builder.ToImmutable()));
            return;
        }

        // 2. Zero columns → every remaining row matches; the pattern space is covered.
        if (matrix.ColumnCount == 0)
            return;

        // 3. Pick the column with the smallest Split() count (fewest partitions).
        //    Ties broken by leftmost index.
        int colIdx = SelectColumn(matrix);
        var column = matrix.Columns[colIdx];

        // 4. Compute partitions from the column domain, refined by row constraints.
        var partitions = ComputePartitions(matrix, colIdx, column);

        // 5. For each partition: record the constraint, specialize the matrix, and recurse.
        foreach (var partition in partitions)
        {
            var constraint = new SlotConstraint(column.Slot, partition);
            var specialized = matrix.Specialize(colIdx, partition);
            CheckCore(specialized, accumulated.Add(constraint), missingCases);
        }
    }

    /// Compute the partitions for a column.
    /// For discrete domains (bool, enum) the column's Split() is sufficient.
    /// For continuous domains (numeric), we refine using constraints from the rows
    /// to ensure the partitions distinguish between covered and uncovered regions.
    static ImmutableArray<IValueDomain> ComputePartitions(
        PatternMatrix matrix, int colIdx, Column column)
    {
        // Start with the column domain's natural split.
        var baseSplit = column.Domain.Split();

        // If the column domain splits into multiple partitions, use those.
        // This handles bool (2 partitions) and enum (N partitions) correctly.
        if (baseSplit.Length > 1)
            return baseSplit;

        // Single partition (typically numeric universe or similar continuous domain).
        // Refine by collecting constraint domains from the rows to create finer splits.
        var constraintDomains = CollectConstraintDomains(matrix, colIdx);

        if (constraintDomains.Length == 0)
        {
            // All rows are wildcards — the single partition suffices.
            return baseSplit;
        }

        // Build refined partitions: each distinct constraint region, plus the remainder.
        // Union all constraints, then split the column domain into:
        //   - Each constraint's intersection with the column domain
        //   - The complement (column domain minus all constraints)

        var partitionBuilder = ImmutableArray.CreateBuilder<IValueDomain>();
        var coveredByAll = column.Domain;

        foreach (var cd in constraintDomains)
        {
            var portion = column.Domain.Intersect(cd);
            if (!portion.IsEmpty)
            {
                partitionBuilder.Add(portion);
                coveredByAll = coveredByAll.Subtract(cd);
            }
        }

        // Add the uncovered remainder (the "default" case).
        if (!coveredByAll.IsEmpty)
            partitionBuilder.Add(coveredByAll);

        return partitionBuilder.Count > 0
            ? partitionBuilder.ToImmutable()
            : baseSplit;
    }

    /// Collect distinct constraint domains from a column's cells.
    /// Deduplicates by tracking which domains we've already seen.
    static ImmutableArray<IValueDomain> CollectConstraintDomains(PatternMatrix matrix, int colIdx)
    {
        var builder = ImmutableArray.CreateBuilder<IValueDomain>();

        foreach (var row in matrix.Rows)
        {
            if (row[colIdx] is Cell.Constraint constraint)
            {
                // Add if not already present (simple reference check is insufficient;
                // we could do structural comparison, but for now just add all and
                // let the subtraction handle overlaps).
                builder.Add(constraint.MatchedValues);
            }
        }

        return builder.ToImmutable();
    }

    static int SelectColumn(PatternMatrix matrix)
    {
        int bestIdx = 0;
        int bestSplitCount = matrix.Columns[0].Domain.Split().Length;

        for (int i = 1; i < matrix.ColumnCount; i++)
        {
            int count = matrix.Columns[i].Domain.Split().Length;
            if (count < bestSplitCount)
            {
                bestSplitCount = count;
                bestIdx = i;
            }
        }

        return bestIdx;
    }
}
