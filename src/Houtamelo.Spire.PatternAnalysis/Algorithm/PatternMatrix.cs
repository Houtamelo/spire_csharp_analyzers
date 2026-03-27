using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Houtamelo.Spire.PatternAnalysis.Domains;
using Houtamelo.Spire.PatternAnalysis.Domains.Numeric;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

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

    // ─── Build from switch expression ────────────────────────────────────

    /// Build a PatternMatrix from a switch expression's arms.
    /// Arms with when guards are excluded (conservative — treated as not covering anything).
    public static PatternMatrix Build(ISwitchExpressionOperation switchExpr, DomainResolver resolver)
    {
        var subjectType = switchExpr.Value.Type;
        if (subjectType == null)
            return Empty();

        // Collect patterns (excluding guarded arms)
        var patterns = ImmutableArray.CreateBuilder<IPatternOperation>();
        foreach (var arm in switchExpr.Arms)
        {
            if (arm.Guard != null)
                continue;
            patterns.Add(arm.Pattern);
        }

        return BuildCore(subjectType, patterns.ToImmutable(), resolver);
    }

    /// Build a PatternMatrix from a switch statement's cases.
    /// Clauses with when guards are excluded.
    public static PatternMatrix Build(ISwitchOperation switchStmt, DomainResolver resolver)
    {
        var subjectType = switchStmt.Value.Type;
        if (subjectType == null)
            return Empty();

        // Collect patterns (excluding guarded clauses)
        var patterns = ImmutableArray.CreateBuilder<IPatternOperation>();
        foreach (var switchCase in switchStmt.Cases)
        {
            foreach (var clause in switchCase.Clauses)
            {
                if (clause is not IPatternCaseClauseOperation patternClause)
                    continue;
                if (patternClause.Guard != null)
                    continue;
                patterns.Add(patternClause.Pattern);
            }
        }

        return BuildCore(subjectType, patterns.ToImmutable(), resolver);
    }

    static PatternMatrix Empty() =>
        new(ImmutableArray<ImmutableArray<Cell>>.Empty, ImmutableArray<Column>.Empty);

    /// Core build logic. Detects tuple subjects and property patterns,
    /// expanding them into multi-column matrices.
    static PatternMatrix BuildCore(
        ITypeSymbol subjectType,
        ImmutableArray<IPatternOperation> patterns,
        DomainResolver resolver)
    {
        // Tuple subject → multi-column matrix with one column per element
        if (subjectType is INamedTypeSymbol { IsTupleType: true } tupleType)
            return BuildTupleMatrix(tupleType, patterns, resolver);

        // Check if any arm uses property subpatterns → multi-column property matrix
        if (HasPropertyPatterns(patterns))
            return BuildPropertyMatrix(subjectType, patterns, resolver);

        // Default: single-column matrix
        var domain = resolver.Resolve(subjectType);
        var slot = new SlotIdentifier.RootSlot(subjectType);
        var column = new Column(slot, domain);

        var rowBuilder = ImmutableArray.CreateBuilder<ImmutableArray<Cell>>();
        foreach (var pattern in patterns)
        {
            var cell = ConvertPattern(pattern, domain);
            rowBuilder.Add(ImmutableArray.Create(cell));
        }

        return new PatternMatrix(rowBuilder.ToImmutable(), ImmutableArray.Create(column));
    }

    // ─── Tuple matrix ────────────────────────────────────────────────────

    static PatternMatrix BuildTupleMatrix(
        INamedTypeSymbol tupleType,
        ImmutableArray<IPatternOperation> patterns,
        DomainResolver resolver)
    {
        var elements = tupleType.TupleElements;
        var columns = ImmutableArray.CreateBuilder<Column>(elements.Length);

        for (int i = 0; i < elements.Length; i++)
        {
            var elemType = elements[i].Type;
            var domain = resolver.Resolve(elemType);
            var slot = new SlotIdentifier.TupleSlot(i, elemType);
            columns.Add(new Column(slot, domain));
        }

        var cols = columns.MoveToImmutable();
        var rowBuilder = ImmutableArray.CreateBuilder<ImmutableArray<Cell>>();

        foreach (var pattern in patterns)
        {
            var row = ConvertTupleRow(pattern, cols, resolver);
            rowBuilder.Add(row);
        }

        return new PatternMatrix(rowBuilder.ToImmutable(), cols);
    }

    /// Convert a single arm's pattern into a row of cells for a tuple matrix.
    static ImmutableArray<Cell> ConvertTupleRow(
        IPatternOperation pattern,
        ImmutableArray<Column> columns,
        DomainResolver resolver)
    {
        // Discard/var patterns → all wildcards
        if (pattern is IDiscardPatternOperation)
            return WildcardRow(columns.Length);

        if (pattern is IDeclarationPatternOperation declPat && declPat.Syntax is VarPatternSyntax)
            return WildcardRow(columns.Length);

        // Recursive pattern with deconstruction subpatterns → map each subpattern to a column
        if (pattern is IRecursivePatternOperation recursive
            && !recursive.DeconstructionSubpatterns.IsEmpty)
        {
            var cellBuilder = ImmutableArray.CreateBuilder<Cell>(columns.Length);
            for (int i = 0; i < columns.Length; i++)
            {
                if (i < recursive.DeconstructionSubpatterns.Length)
                {
                    var subPattern = recursive.DeconstructionSubpatterns[i];
                    cellBuilder.Add(ConvertPattern(subPattern, columns[i].Domain));
                }
                else
                {
                    cellBuilder.Add(Cell.Wildcard.Instance);
                }
            }
            return cellBuilder.MoveToImmutable();
        }

        // Fallback: wildcard for all columns
        return WildcardRow(columns.Length);
    }

    static ImmutableArray<Cell> WildcardRow(int count)
    {
        var builder = ImmutableArray.CreateBuilder<Cell>(count);
        for (int i = 0; i < count; i++)
            builder.Add(Cell.Wildcard.Instance);
        return builder.MoveToImmutable();
    }

    // ─── Property-pattern matrix ──────────────────────────────────────────

    /// Check if any pattern in the set uses property subpatterns.
    static bool HasPropertyPatterns(ImmutableArray<IPatternOperation> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (pattern is IRecursivePatternOperation recursive
                && !recursive.PropertySubpatterns.IsEmpty)
                return true;
        }
        return false;
    }

    /// Build a multi-column matrix from property patterns.
    /// Collects all distinct properties mentioned across all arms,
    /// creates a column for each, and maps each arm's subpatterns.
    static PatternMatrix BuildPropertyMatrix(
        ITypeSymbol subjectType,
        ImmutableArray<IPatternOperation> patterns,
        DomainResolver resolver)
    {
        // Collect all distinct property symbols mentioned across arms
        var propertySet = new System.Collections.Generic.Dictionary<IPropertySymbol, int>(
            SymbolEqualityComparer.Default);

        foreach (var pattern in patterns)
        {
            if (pattern is not IRecursivePatternOperation recursive)
                continue;

            foreach (var propSub in recursive.PropertySubpatterns)
            {
                var prop = GetPropertyFromSubpattern(propSub);
                if (prop != null && !propertySet.ContainsKey(prop))
                    propertySet[prop] = propertySet.Count;
            }
        }

        if (propertySet.Count == 0)
        {
            // No properties found — fall back to single-column
            var domain = resolver.Resolve(subjectType);
            var slot = new SlotIdentifier.RootSlot(subjectType);
            var column = new Column(slot, domain);
            var rowBuilder2 = ImmutableArray.CreateBuilder<ImmutableArray<Cell>>();
            foreach (var pattern in patterns)
            {
                var cell = ConvertPattern(pattern, domain);
                rowBuilder2.Add(ImmutableArray.Create(cell));
            }
            return new PatternMatrix(rowBuilder2.ToImmutable(), ImmutableArray.Create(column));
        }

        // Build columns from collected properties
        var columns = ImmutableArray.CreateBuilder<Column>(propertySet.Count);
        var propList = new IPropertySymbol[propertySet.Count];
        foreach (var kvp in propertySet)
            propList[kvp.Value] = kvp.Key;

        for (int i = 0; i < propList.Length; i++)
        {
            var prop = propList[i];
            var propDomain = resolver.Resolve(prop.Type);
            var slot = new SlotIdentifier.PropertySlot(prop);
            columns.Add(new Column(slot, propDomain));
        }

        var cols = columns.MoveToImmutable();
        var rowBuilder = ImmutableArray.CreateBuilder<ImmutableArray<Cell>>();

        foreach (var pattern in patterns)
        {
            var row = ConvertPropertyRow(pattern, cols, propList, propertySet, resolver);
            rowBuilder.Add(row);
        }

        return new PatternMatrix(rowBuilder.ToImmutable(), cols);
    }

    /// Convert a single arm's pattern into a row of cells for a property matrix.
    static ImmutableArray<Cell> ConvertPropertyRow(
        IPatternOperation pattern,
        ImmutableArray<Column> columns,
        IPropertySymbol[] propList,
        System.Collections.Generic.Dictionary<IPropertySymbol, int> propertySet,
        DomainResolver resolver)
    {
        // Discard/var/wildcard → all wildcards
        if (pattern is IDiscardPatternOperation)
            return WildcardRow(columns.Length);

        if (pattern is IDeclarationPatternOperation declPat && declPat.Syntax is VarPatternSyntax)
            return WildcardRow(columns.Length);

        if (pattern is IRecursivePatternOperation recursive)
        {
            // Start with all wildcards
            var cellBuilder = ImmutableArray.CreateBuilder<Cell>(columns.Length);
            for (int i = 0; i < columns.Length; i++)
                cellBuilder.Add(Cell.Wildcard.Instance);

            // Fill in property subpatterns
            foreach (var propSub in recursive.PropertySubpatterns)
            {
                var prop = GetPropertyFromSubpattern(propSub);
                if (prop != null && propertySet.TryGetValue(prop, out int idx))
                {
                    cellBuilder[idx] = ConvertPattern(propSub.Pattern, columns[idx].Domain);
                }
            }

            return cellBuilder.MoveToImmutable();
        }

        return WildcardRow(columns.Length);
    }

    // ─── Pattern-to-Cell conversion ──────────────────────────────────────

    /// Convert a Roslyn IPatternOperation to a Cell for the given column domain.
    static Cell ConvertPattern(IPatternOperation pattern, IValueDomain domain)
    {
        switch (pattern)
        {
            case IDiscardPatternOperation:
                return Cell.Wildcard.Instance;

            case IDeclarationPatternOperation declPattern:
                return ConvertDeclarationPattern(declPattern, domain);

            case IConstantPatternOperation constPattern:
                return ConvertConstantPattern(constPattern, domain);

            case IRelationalPatternOperation relPattern:
                return ConvertRelationalPattern(relPattern, domain);

            case IBinaryPatternOperation binaryPattern:
                return ConvertBinaryPattern(binaryPattern, domain);

            case INegatedPatternOperation negPattern:
                return ConvertNegatedPattern(negPattern, domain);

            case ITypePatternOperation:
                // Type patterns narrow by type — conservative wildcard for now
                return Cell.Wildcard.Instance;

            case IRecursivePatternOperation:
                // Structural decomposition — conservative wildcard for now
                return Cell.Wildcard.Instance;

            default:
                // Unknown pattern — conservative wildcard
                return Cell.Wildcard.Instance;
        }
    }

    static Cell ConvertDeclarationPattern(IDeclarationPatternOperation declPattern, IValueDomain domain)
    {
        // `var x` patterns match everything
        if (declPattern.Syntax is VarPatternSyntax)
            return Cell.Wildcard.Instance;

        // Type-narrowing declaration — conservative wildcard for now
        return Cell.Wildcard.Instance;
    }

    static Cell ConvertConstantPattern(IConstantPatternOperation constPattern, IValueDomain domain)
    {
        var value = constPattern.Value;
        if (value == null)
            return Cell.Wildcard.Instance;

        var constantValue = value.ConstantValue;
        if (!constantValue.HasValue)
            return Cell.Wildcard.Instance;

        // Null constant — matches only the null partition of a NullableDomain
        if (constantValue.Value == null)
        {
            if (domain is NullableDomain nullableDomain)
            {
                // Create a NullableDomain that covers only null (empty inner)
                var emptyInner = new EmptyDomain(nullableDomain.Inner.Type);
                return new Cell.Constraint(new NullableDomain(domain.Type, emptyInner, hasNull: true));
            }

            // Null against a non-nullable domain — no-op wildcard
            return Cell.Wildcard.Instance;
        }

        // Unwrap NullableDomain for non-null constant patterns
        var effectiveDomain = domain;
        var isNullable = domain is NullableDomain;
        if (isNullable)
            effectiveDomain = ((NullableDomain)domain).Inner;

        Cell? innerCell = null;

        // Bool literals
        if (constantValue.Value is bool boolVal && effectiveDomain is BoolDomain)
        {
            var constraint = new BoolDomain(effectiveDomain.Type, hasTrue: boolVal, hasFalse: !boolVal);
            innerCell = new Cell.Constraint(constraint);
        }

        // Enum constants
        if (innerCell == null && effectiveDomain is EnumDomain && value.Type?.TypeKind == TypeKind.Enum)
        {
            var matchedField = FindEnumField(value);
            if (matchedField != null)
            {
                var singleton = ImmutableHashSet.Create<IFieldSymbol>(SymbolEqualityComparer.Default, matchedField);
                var allMembers = ((INamedTypeSymbol)effectiveDomain.Type).GetMembers()
                    .OfType<IFieldSymbol>()
                    .Where(f => f.HasConstantValue)
                    .ToImmutableHashSet<IFieldSymbol>(SymbolEqualityComparer.Default);
                var constraint = new EnumDomain(effectiveDomain.Type, singleton, allMembers);
                innerCell = new Cell.Constraint(constraint);
            }
        }

        // Numeric constants
        if (innerCell == null && effectiveDomain is NumericDomain && constantValue.Value is IConvertible convertible)
        {
            try
            {
                var doubleVal = convertible.ToDouble(CultureInfo.InvariantCulture);
                var point = new NumericDomain(effectiveDomain.Type,
                    IntervalSet.Single(new Interval(doubleVal, doubleVal, true, true)),
                    double.MinValue, double.MaxValue);
                innerCell = new Cell.Constraint(point);
            }
            catch
            {
                // Conversion failed — fallback to wildcard
            }
        }

        if (innerCell == null)
            return Cell.Wildcard.Instance;

        // Wrap back in NullableDomain if the column domain is nullable
        if (isNullable && innerCell is Cell.Constraint innerConstraint)
        {
            var wrapped = new NullableDomain(domain.Type, innerConstraint.MatchedValues, hasNull: false);
            return new Cell.Constraint(wrapped);
        }

        return innerCell;
    }

    static IFieldSymbol? FindEnumField(IOperation value)
    {
        // Walk through conversions to find the underlying field reference
        var current = value;
        while (current is IConversionOperation conversion)
            current = conversion.Operand;

        if (current is IFieldReferenceOperation fieldRef && fieldRef.Field.HasConstantValue)
            return fieldRef.Field;

        return null;
    }

    static Cell ConvertRelationalPattern(IRelationalPatternOperation relPattern, IValueDomain domain)
    {
        if (domain is not NumericDomain)
            return Cell.Wildcard.Instance;

        var constValue = relPattern.Value.ConstantValue;
        if (!constValue.HasValue || constValue.Value is not IConvertible convertible)
            return Cell.Wildcard.Instance;

        double bound;
        try
        {
            bound = convertible.ToDouble(CultureInfo.InvariantCulture);
        }
        catch
        {
            return Cell.Wildcard.Instance;
        }

        Interval interval;
        switch (relPattern.OperatorKind)
        {
            case BinaryOperatorKind.LessThan:
                interval = new Interval(double.MinValue, bound, true, false);
                break;
            case BinaryOperatorKind.LessThanOrEqual:
                interval = new Interval(double.MinValue, bound, true, true);
                break;
            case BinaryOperatorKind.GreaterThan:
                interval = new Interval(bound, double.MaxValue, false, true);
                break;
            case BinaryOperatorKind.GreaterThanOrEqual:
                interval = new Interval(bound, double.MaxValue, true, true);
                break;
            default:
                return Cell.Wildcard.Instance;
        }

        var constraint = new NumericDomain(domain.Type, IntervalSet.Single(interval), double.MinValue, double.MaxValue);
        return new Cell.Constraint(constraint);
    }

    static Cell ConvertBinaryPattern(IBinaryPatternOperation binaryPattern, IValueDomain domain)
    {
        var left = ConvertPattern(binaryPattern.LeftPattern, domain);
        var right = ConvertPattern(binaryPattern.RightPattern, domain);

        switch (binaryPattern.OperatorKind)
        {
            case BinaryOperatorKind.Or:
                return UnionCells(left, right, domain);
            case BinaryOperatorKind.And:
                return IntersectCells(left, right, domain);
            default:
                return Cell.Wildcard.Instance;
        }
    }

    static Cell ConvertNegatedPattern(INegatedPatternOperation negPattern, IValueDomain domain)
    {
        // Special case: `not null` against a NullableDomain.
        // Directly construct the not-null partition instead of relying on Complement,
        // which would fail for structural inner domains.
        if (negPattern.Pattern is IConstantPatternOperation constInner
            && constInner.Value?.ConstantValue is { HasValue: true, Value: null }
            && domain is NullableDomain nullableDomain)
        {
            return new Cell.Constraint(
                new NullableDomain(domain.Type, nullableDomain.Inner, hasNull: false));
        }

        var inner = ConvertPattern(negPattern.Pattern, domain);

        if (inner is Cell.Wildcard)
        {
            // Complement of wildcard is empty — nothing matches.
            // This is unusual; treat as wildcard to be conservative.
            return Cell.Wildcard.Instance;
        }

        if (inner is Cell.Constraint constraint)
        {
            var complemented = constraint.MatchedValues.Complement();
            if (complemented.IsEmpty)
                return Cell.Wildcard.Instance;
            return new Cell.Constraint(complemented);
        }

        return Cell.Wildcard.Instance;
    }

    /// Union two cells: wildcard absorbs everything; otherwise union the domains.
    static Cell UnionCells(Cell left, Cell right, IValueDomain domain)
    {
        if (left is Cell.Wildcard || right is Cell.Wildcard)
            return Cell.Wildcard.Instance;

        if (left is Cell.Constraint leftC && right is Cell.Constraint rightC)
        {
            // Union = complement of (complement(left) intersect complement(right))
            var leftComp = leftC.MatchedValues.Complement();
            var rightComp = rightC.MatchedValues.Complement();
            var intersection = leftComp.Intersect(rightComp);
            var union = intersection.Complement();
            return new Cell.Constraint(union);
        }

        return Cell.Wildcard.Instance;
    }

    /// Intersect two cells: wildcard is identity; otherwise intersect the domains.
    static Cell IntersectCells(Cell left, Cell right, IValueDomain domain)
    {
        if (left is Cell.Wildcard)
            return right;
        if (right is Cell.Wildcard)
            return left;

        if (left is Cell.Constraint leftC && right is Cell.Constraint rightC)
        {
            var intersected = leftC.MatchedValues.Intersect(rightC.MatchedValues);
            return new Cell.Constraint(intersected);
        }

        return Cell.Wildcard.Instance;
    }

    /// Extract the IPropertySymbol from a property subpattern's Member operation.
    /// Member is an IOperation (IPropertyReferenceOperation or IFieldReferenceOperation),
    /// not a symbol directly.
    static IPropertySymbol? GetPropertyFromSubpattern(IPropertySubpatternOperation propSub)
    {
        if (propSub.Member is IPropertyReferenceOperation propRef)
            return propRef.Property;

        return null;
    }
}
