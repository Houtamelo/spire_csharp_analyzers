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
    /// Handles pattern case clauses (C# 7+ pattern syntax), single-value case clauses
    /// (traditional `case value:` syntax), and default case clauses (`default:`).
    /// Clauses with when guards are excluded.
    public static PatternMatrix Build(ISwitchOperation switchStmt, DomainResolver resolver)
    {
        var subjectType = switchStmt.Value.Type;
        if (subjectType == null)
            return Empty();

        // Collect patterns from pattern case clauses (excluding guarded ones)
        var patterns = ImmutableArray.CreateBuilder<IPatternOperation>();

        // Track whether we have non-pattern clauses that need direct cell conversion
        var directCells = ImmutableArray.CreateBuilder<Cell>();
        bool hasNonPatternClauses = false;

        foreach (var switchCase in switchStmt.Cases)
        {
            foreach (var clause in switchCase.Clauses)
            {
                switch (clause)
                {
                    case IPatternCaseClauseOperation patternClause:
                        if (patternClause.Guard != null)
                            continue;
                        patterns.Add(patternClause.Pattern);
                        break;

                    case IDefaultCaseClauseOperation:
                        hasNonPatternClauses = true;
                        directCells.Add(Cell.Wildcard.Instance);
                        break;

                    case ISingleValueCaseClauseOperation singleValue:
                        hasNonPatternClauses = true;
                        directCells.Add(ConvertSingleValueClause(singleValue, subjectType, resolver));
                        break;
                }
            }
        }

        if (!hasNonPatternClauses)
            return BuildCore(subjectType, patterns.ToImmutable(), resolver);

        // Mix pattern-derived rows with directly-converted rows.
        // Build the base matrix from pattern clauses (handles tuple/property expansion).
        var baseMatrix = BuildCore(subjectType, patterns.ToImmutable(), resolver);

        // For direct cells (single-value and default), we only support single-column matrices.
        // Traditional case labels don't use tuple/property decomposition.
        if (baseMatrix.ColumnCount <= 1)
        {
            // Resolve domain for the single column
            var domain = resolver.Resolve(subjectType);
            var slot = new SlotIdentifier.RootSlot(subjectType);
            var column = new Column(slot, domain);
            var columns = ImmutableArray.Create(column);

            // Re-resolve direct cells against the proper domain
            // (ConvertSingleValueClause used the type, but we need domain-aware conversion)
            var rowBuilder = ImmutableArray.CreateBuilder<ImmutableArray<Cell>>();

            // Add pattern-derived rows
            foreach (var row in baseMatrix.Rows)
                rowBuilder.Add(row);

            // Add directly-converted rows
            foreach (var cell in directCells)
                rowBuilder.Add(ImmutableArray.Create(cell));

            return new PatternMatrix(rowBuilder.ToImmutable(), columns);
        }

        // Multi-column case: direct cells become wildcard rows (they can't decompose)
        var multiRowBuilder = ImmutableArray.CreateBuilder<ImmutableArray<Cell>>();
        foreach (var row in baseMatrix.Rows)
            multiRowBuilder.Add(row);
        foreach (var _ in directCells)
            multiRowBuilder.Add(WildcardRow(baseMatrix.ColumnCount));

        return new PatternMatrix(multiRowBuilder.ToImmutable(), baseMatrix.Columns);
    }

    /// Convert a traditional `case value:` clause to a Cell.
    static Cell ConvertSingleValueClause(
        ISingleValueCaseClauseOperation clause,
        ITypeSymbol subjectType,
        DomainResolver resolver)
    {
        var value = clause.Value;
        if (value == null)
            return Cell.Wildcard.Instance;

        var constantValue = value.ConstantValue;
        if (!constantValue.HasValue)
            return Cell.Wildcard.Instance;

        // Reuse the same logic as ConvertConstantPattern by resolving the domain
        var domain = resolver.Resolve(subjectType);

        // Null constant
        if (constantValue.Value == null)
        {
            if (domain is NullableDomain nullableDomain)
            {
                var emptyInner = new EmptyDomain(nullableDomain.Inner.Type);
                return new Cell.Constraint(new NullableDomain(domain.Type, emptyInner, hasNull: true));
            }
            return Cell.Wildcard.Instance;
        }

        // Unwrap nullable for non-null values
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
                // Conversion failed
            }
        }

        if (innerCell == null)
            return Cell.Wildcard.Instance;

        if (isNullable && innerCell is Cell.Constraint innerConstraint)
        {
            var wrapped = new NullableDomain(domain.Type, innerConstraint.MatchedValues, hasNull: false);
            return new Cell.Constraint(wrapped);
        }

        return innerCell;
    }

    static PatternMatrix Empty() =>
        new(ImmutableArray<ImmutableArray<Cell>>.Empty, ImmutableArray<Column>.Empty);

    /// Core build logic. Detects struct DU, tuple subjects, and property patterns,
    /// expanding them into multi-column matrices.
    static PatternMatrix BuildCore(
        ITypeSymbol subjectType,
        ImmutableArray<IPatternOperation> patterns,
        DomainResolver resolver)
    {
        // Unwrap Nullable<T> so struct DU detection works on the inner type
        var unwrapped = subjectType;
        bool isNullableValueType = false;
        if (subjectType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullableNamed)
        {
            unwrapped = nullableNamed.TypeArguments[0];
            isNullableValueType = true;
        }

        // Struct discriminated union → single-column Kind enum matrix
        var kindEnum = resolver.TryGetStructDUKindEnum(unwrapped);
        if (kindEnum != null)
        {
            if (isNullableValueType)
                return BuildNullableStructDUMatrix(subjectType, kindEnum, patterns, resolver);
            return BuildStructDUMatrix(unwrapped, kindEnum, patterns, resolver);
        }

        // Tuple subject → multi-column matrix with one column per element
        if (subjectType is INamedTypeSymbol { IsTupleType: true } tupleType)
            return BuildTupleMatrix(tupleType, patterns, resolver);

        // Record DU / EnforceExhaustive → single-column type domain matrix.
        // Must check before HasPropertyPatterns, otherwise property subpatterns
        // cause the matrix to ignore root-type discrimination.
        var resolved = resolver.Resolve(subjectType);
        var effectiveDomain = resolved is NullableDomain nd ? nd.Inner : resolved;
        if (effectiveDomain is EnforceExhaustiveDomain)
            return BuildTypeDomainMatrix(subjectType, patterns, resolved);

        // Check if any arm uses property subpatterns → multi-column property matrix
        if (HasPropertyPatterns(patterns))
            return BuildPropertyMatrix(subjectType, patterns, resolver);

        // Default: single-column matrix
        var domain = resolved;
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
        // Flatten nested tuple types into a single column list.
        // e.g., ((bool, bool), bool) → [bool, bool, bool]
        var columns = ImmutableArray.CreateBuilder<Column>();
        FlattenTupleColumns(tupleType, columns, resolver);

        var cols = columns.ToImmutable();
        var rowBuilder = ImmutableArray.CreateBuilder<ImmutableArray<Cell>>();

        foreach (var pattern in patterns)
        {
            var cellBuilder = ImmutableArray.CreateBuilder<Cell>(cols.Length);
            FlattenTupleRow(pattern, cellBuilder, cols, 0);

            // Pad remaining columns with wildcards (handles discard/var/fallback patterns)
            while (cellBuilder.Count < cols.Length)
                cellBuilder.Add(Cell.Wildcard.Instance);

            rowBuilder.Add(cellBuilder.ToImmutable());
        }

        return new PatternMatrix(rowBuilder.ToImmutable(), cols);
    }

    /// Recursively flatten a tuple type's elements into columns.
    /// Nested tuples are expanded inline.
    static void FlattenTupleColumns(
        INamedTypeSymbol tupleType,
        ImmutableArray<Column>.Builder columns,
        DomainResolver resolver)
    {
        var elements = tupleType.TupleElements;
        for (int i = 0; i < elements.Length; i++)
        {
            var elemType = elements[i].Type;
            if (elemType is INamedTypeSymbol { IsTupleType: true } nestedTuple)
            {
                FlattenTupleColumns(nestedTuple, columns, resolver);
            }
            else
            {
                var domain = resolver.Resolve(elemType);
                var slot = new SlotIdentifier.TupleSlot(columns.Count, elemType);
                columns.Add(new Column(slot, domain));
            }
        }
    }

    /// Recursively flatten a single arm's pattern into cells for a flattened tuple matrix.
    /// Returns the number of flat columns consumed.
    static int FlattenTupleRow(
        IPatternOperation pattern,
        ImmutableArray<Cell>.Builder cells,
        ImmutableArray<Column> columns,
        int startCol)
    {
        // Discard/var patterns → wildcards for all columns this pattern covers
        if (pattern is IDiscardPatternOperation)
        {
            // Must determine how many flat columns this sub-tuple would expand to.
            // The caller knows the answer via the column structure.
            // We can't determine count from the pattern alone, so the caller handles this.
            return 0; // Sentinel — handled by the caller.
        }

        if (pattern is IDeclarationPatternOperation declPat && declPat.Syntax is VarPatternSyntax)
            return 0; // Same as discard.

        // Recursive pattern with deconstruction subpatterns
        if (pattern is IRecursivePatternOperation recursive
            && !recursive.DeconstructionSubpatterns.IsEmpty)
        {
            int col = startCol;
            for (int i = 0; i < recursive.DeconstructionSubpatterns.Length; i++)
            {
                var subPattern = recursive.DeconstructionSubpatterns[i];

                // Check if this subpattern itself is a nested tuple deconstruction
                if (subPattern is IRecursivePatternOperation nestedRecursive
                    && !nestedRecursive.DeconstructionSubpatterns.IsEmpty
                    && nestedRecursive.InputType is INamedTypeSymbol { IsTupleType: true })
                {
                    int consumed = FlattenTupleRow(subPattern, cells, columns, col);
                    col += consumed;
                }
                else if (subPattern is IDiscardPatternOperation
                         || (subPattern is IDeclarationPatternOperation dp && dp.Syntax is VarPatternSyntax))
                {
                    // Wildcard sub-element — need to figure out how many flat columns it covers.
                    // Look at the original tuple structure to determine span.
                    // For simplicity: a wildcard at leaf level covers 1 column.
                    // For nested tuple wildcards, we need the original type info.
                    if (col < columns.Length)
                    {
                        // Check if the subpattern's input type is a nested tuple
                        var subInputType = GetSubPatternInputType(recursive, i);
                        if (subInputType is INamedTypeSymbol { IsTupleType: true } nestedTupleType)
                        {
                            int span = CountFlatColumns(nestedTupleType);
                            for (int j = 0; j < span && col < columns.Length; j++)
                            {
                                cells.Add(Cell.Wildcard.Instance);
                                col++;
                            }
                        }
                        else
                        {
                            cells.Add(Cell.Wildcard.Instance);
                            col++;
                        }
                    }
                }
                else
                {
                    // Leaf-level pattern → convert to cell
                    if (col < columns.Length)
                    {
                        cells.Add(ConvertPattern(subPattern, columns[col].Domain));
                        col++;
                    }
                }
            }

            return col - startCol;
        }

        // Fallback: single wildcard (not a tuple decomposition)
        return 0;
    }

    /// Count how many flat (leaf) columns a tuple type expands to.
    static int CountFlatColumns(INamedTypeSymbol tupleType)
    {
        int count = 0;
        foreach (var elem in tupleType.TupleElements)
        {
            if (elem.Type is INamedTypeSymbol { IsTupleType: true } nested)
                count += CountFlatColumns(nested);
            else
                count++;
        }
        return count;
    }

    /// Get the input type for a subpattern at a given index within a recursive pattern.
    /// Uses the deconstructed type's tuple elements.
    static ITypeSymbol? GetSubPatternInputType(IRecursivePatternOperation recursive, int index)
    {
        // The input type is the type being deconstructed.
        // For a tuple pattern, this is the tuple type.
        var inputType = recursive.InputType ?? recursive.NarrowedType;
        if (inputType is INamedTypeSymbol { IsTupleType: true } tupleType)
        {
            var elements = tupleType.TupleElements;
            if (index < elements.Length)
                return elements[index].Type;
        }
        return null;
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

    // ─── Struct DU matrix ─────────────────────────────────────────────────

    /// Build a single-column Kind enum matrix for a struct discriminated union.
    /// Exhaustiveness is determined solely by coverage of all Kind enum members.
    static PatternMatrix BuildStructDUMatrix(
        ITypeSymbol subjectType,
        INamedTypeSymbol kindEnum,
        ImmutableArray<IPatternOperation> patterns,
        DomainResolver resolver)
    {
        var kindDomain = EnumDomain.Universe(kindEnum);
        var slot = new SlotIdentifier.RootSlot(kindEnum);
        var column = new Column(slot, kindDomain);

        // Detect majority pattern style to guide Kind extraction
        var style = DetectStructDUStyle(patterns);

        var rowBuilder = ImmutableArray.CreateBuilder<ImmutableArray<Cell>>();
        foreach (var pattern in patterns)
        {
            var cell = ExtractStructDUKindCell(pattern, kindDomain, kindEnum, style);
            rowBuilder.Add(ImmutableArray.Create(cell));
        }

        return new PatternMatrix(rowBuilder.ToImmutable(), ImmutableArray.Create(column));
    }

    /// Build a single-column matrix for a nullable struct DU (e.g., Shape?).
    /// The domain is NullableDomain(EnumDomain(Kind)), so null patterns and
    /// Kind-matching patterns are both handled in one column.
    static PatternMatrix BuildNullableStructDUMatrix(
        ITypeSymbol nullableType,
        INamedTypeSymbol kindEnum,
        ImmutableArray<IPatternOperation> patterns,
        DomainResolver resolver)
    {
        var kindDomain = EnumDomain.Universe(kindEnum);
        var nullableKindDomain = NullableDomain.Universe(nullableType, kindDomain);
        var slot = new SlotIdentifier.RootSlot(nullableType);
        var column = new Column(slot, nullableKindDomain);

        var style = DetectStructDUStyle(patterns);

        var rowBuilder = ImmutableArray.CreateBuilder<ImmutableArray<Cell>>();
        foreach (var pattern in patterns)
        {
            var cell = ConvertNullableStructDUPattern(pattern, nullableKindDomain, kindDomain, kindEnum, style);
            rowBuilder.Add(ImmutableArray.Create(cell));
        }

        return new PatternMatrix(rowBuilder.ToImmutable(), ImmutableArray.Create(column));
    }

    /// Convert a pattern in a nullable struct DU switch.
    /// Handles null constants, DU kind patterns, and wildcards.
    static Cell ConvertNullableStructDUPattern(
        IPatternOperation pattern,
        NullableDomain nullableKindDomain,
        EnumDomain kindDomain,
        INamedTypeSymbol kindEnum,
        StructDUStyle style)
    {
        // Wildcard / var → covers everything (null + all variants)
        if (pattern is IDiscardPatternOperation)
            return Cell.Wildcard.Instance;
        if (pattern is IDeclarationPatternOperation declPat && declPat.Syntax is VarPatternSyntax)
            return Cell.Wildcard.Instance;

        // Null constant → covers only null partition
        if (pattern is IConstantPatternOperation constPat)
        {
            var cv = constPat.Value?.ConstantValue;
            if (cv is { HasValue: true, Value: null })
            {
                var emptyInner = new EmptyDomain(kindDomain.Type);
                return new Cell.Constraint(new NullableDomain(nullableKindDomain.Type, emptyInner, hasNull: true));
            }
        }

        // Negated pattern: `not null` covers all non-null values
        if (pattern is INegatedPatternOperation negated
            && negated.Pattern is IConstantPatternOperation negConst)
        {
            var nv = negConst.Value?.ConstantValue;
            if (nv is { HasValue: true, Value: null })
            {
                // `not null` → covers all Kind variants but not null
                return new Cell.Constraint(new NullableDomain(nullableKindDomain.Type, kindDomain, hasNull: false));
            }
        }

        // Binary `and` pattern: `not null and (Kind.X, ...)` — intersect both sides
        if (pattern is IBinaryPatternOperation binaryAnd && binaryAnd.OperatorKind == BinaryOperatorKind.And)
        {
            var leftCell = ConvertNullableStructDUPattern(binaryAnd.LeftPattern, nullableKindDomain, kindDomain, kindEnum, style);
            var rightCell = ConvertNullableStructDUPattern(binaryAnd.RightPattern, nullableKindDomain, kindDomain, kindEnum, style);

            // Wildcards don't narrow: if one side is wildcard, return the other
            if (leftCell is Cell.Wildcard)
                return rightCell;
            if (rightCell is Cell.Wildcard)
                return leftCell;

            // Both are constraints — intersect them
            if (leftCell is Cell.Constraint lc && rightCell is Cell.Constraint rc)
            {
                var intersection = lc.MatchedValues.Intersect(rc.MatchedValues);
                return intersection.IsEmpty ? new Cell.Constraint(intersection) : new Cell.Constraint(intersection);
            }

            return leftCell;
        }

        // Binary `or` pattern — extract inner Kind cell and wrap in NullableDomain
        if (pattern is IBinaryPatternOperation binaryOr && binaryOr.OperatorKind == BinaryOperatorKind.Or)
        {
            var innerOrCell = ConvertBinaryStructDUPattern(binaryOr, kindDomain, kindEnum, style);
            if (innerOrCell is Cell.Constraint innerOrConstraint)
                return new Cell.Constraint(new NullableDomain(nullableKindDomain.Type, innerOrConstraint.MatchedValues, hasNull: false));
            return Cell.Wildcard.Instance;
        }

        // DU kind-matching patterns (deconstruct/property/constant) → wrap in non-null NullableDomain
        var innerCell = ExtractStructDUKindCell(pattern, kindDomain, kindEnum, style);
        if (innerCell is Cell.Constraint innerConstraint)
            return new Cell.Constraint(new NullableDomain(nullableKindDomain.Type, innerConstraint.MatchedValues, hasNull: false));

        // Wildcard from ExtractStructDUKindCell → treat as covering everything
        return Cell.Wildcard.Instance;
    }

    // ─── Record / EnforceExhaustive type domain matrix ────────────────────

    /// Build a single-column matrix for a type discriminated by EnforceExhaustiveDomain.
    /// Handles record DUs and [EnforceExhaustiveness] hierarchies.
    /// The domain may be wrapped in NullableDomain for nullable reference types.
    static PatternMatrix BuildTypeDomainMatrix(
        ITypeSymbol subjectType,
        ImmutableArray<IPatternOperation> patterns,
        IValueDomain domain)
    {
        var slot = new SlotIdentifier.RootSlot(subjectType);
        var column = new Column(slot, domain);

        var rowBuilder = ImmutableArray.CreateBuilder<ImmutableArray<Cell>>();
        foreach (var pattern in patterns)
        {
            var cell = ConvertTypeDomainPattern(pattern, domain);
            rowBuilder.Add(ImmutableArray.Create(cell));
        }

        return new PatternMatrix(rowBuilder.ToImmutable(), ImmutableArray.Create(column));
    }

    /// Convert a pattern for a type-discriminated domain (record DU / enforce-exhaustive).
    /// Handles recursive patterns with MatchedType, type patterns, declaration patterns,
    /// constants (null), and wildcards.
    static Cell ConvertTypeDomainPattern(IPatternOperation pattern, IValueDomain domain)
    {
        switch (pattern)
        {
            case IDiscardPatternOperation:
                return Cell.Wildcard.Instance;

            case IDeclarationPatternOperation declPat:
                if (declPat.Syntax is VarPatternSyntax)
                    return Cell.Wildcard.Instance;
                if (declPat.MatchedType != null)
                    return ConvertTypePattern(declPat.MatchedType, domain);
                return Cell.Wildcard.Instance;

            case IRecursivePatternOperation recursive:
                // Record DU variant: `Option<int>.Some { Value: var v }` has MatchedType = Some
                if (recursive.MatchedType != null)
                    return ConvertTypePattern(recursive.MatchedType, domain);
                // Positional pattern without type narrowing — wildcard
                return Cell.Wildcard.Instance;

            case ITypePatternOperation typePattern:
                return ConvertTypePattern(typePattern.MatchedType, domain);

            case IConstantPatternOperation constPattern:
                return ConvertConstantPattern(constPattern, domain);

            case IBinaryPatternOperation binaryPattern:
                return ConvertTypeDomainBinaryPattern(binaryPattern, domain);

            case INegatedPatternOperation negPattern:
                return ConvertNegatedPattern(negPattern, domain);

            default:
                return Cell.Wildcard.Instance;
        }
    }

    /// Binary pattern for type-discriminated domains.
    /// Uses ConvertTypeDomainPattern recursively (not ConvertPattern) so that
    /// IRecursivePatternOperation with MatchedType is handled correctly.
    static Cell ConvertTypeDomainBinaryPattern(IBinaryPatternOperation binaryPattern, IValueDomain domain)
    {
        var left = ConvertTypeDomainPattern(binaryPattern.LeftPattern, domain);
        var right = ConvertTypeDomainPattern(binaryPattern.RightPattern, domain);

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

    enum StructDUStyle { Deconstruct, Property }

    /// Scan patterns to detect whether deconstruct or property style dominates.
    static StructDUStyle DetectStructDUStyle(ImmutableArray<IPatternOperation> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (pattern is IRecursivePatternOperation recursive)
            {
                if (!recursive.DeconstructionSubpatterns.IsEmpty)
                    return StructDUStyle.Deconstruct;

                if (!recursive.PropertySubpatterns.IsEmpty && HasKindSubpattern(recursive))
                    return StructDUStyle.Property;
            }
        }

        // Default to property mode
        return StructDUStyle.Property;
    }

    /// Check if a recursive pattern has a property subpattern for the 'kind' member.
    static bool HasKindSubpattern(IRecursivePatternOperation recursive)
    {
        foreach (var propSub in recursive.PropertySubpatterns)
        {
            var memberName = GetMemberName(propSub.Member);
            if (memberName == "kind")
                return true;
        }
        return false;
    }

    /// Extract the member name from a property subpattern's Member operation.
    static string? GetMemberName(IOperation? memberOp)
    {
        if (memberOp is IPropertyReferenceOperation propRef)
            return propRef.Property.Name;
        if (memberOp is IFieldReferenceOperation fieldRef)
            return fieldRef.Field.Name;
        return null;
    }

    /// Convert a single arm pattern to a Kind enum cell for a struct DU.
    static Cell ExtractStructDUKindCell(
        IPatternOperation pattern,
        EnumDomain kindDomain,
        INamedTypeSymbol kindEnum,
        StructDUStyle style)
    {
        switch (pattern)
        {
            case IDiscardPatternOperation:
                return Cell.Wildcard.Instance;

            case IDeclarationPatternOperation declPat when declPat.Syntax is VarPatternSyntax:
                return Cell.Wildcard.Instance;

            case IBinaryPatternOperation binaryPat:
                return ConvertBinaryStructDUPattern(binaryPat, kindDomain, kindEnum, style);

            case IRecursivePatternOperation recursive:
                return ExtractKindFromRecursive(recursive, kindDomain, kindEnum, style);

            case IConstantPatternOperation constPat:
                // Direct Kind constant (unlikely at top level, but handle it)
                return ConvertConstantPattern(constPat, kindDomain);

            default:
                return Cell.Wildcard.Instance;
        }
    }

    /// Extract Kind from a recursive pattern (deconstruct or property).
    static Cell ExtractKindFromRecursive(
        IRecursivePatternOperation recursive,
        EnumDomain kindDomain,
        INamedTypeSymbol kindEnum,
        StructDUStyle style)
    {
        // Deconstruct: (Kind.Circle, ...) — element[0] is the Kind
        if (!recursive.DeconstructionSubpatterns.IsEmpty)
        {
            var firstSub = recursive.DeconstructionSubpatterns[0];
            return ConvertPattern(firstSub, kindDomain);
        }

        // Property: { kind: Kind.Circle, ... } — find the 'kind' subpattern
        if (!recursive.PropertySubpatterns.IsEmpty)
        {
            foreach (var propSub in recursive.PropertySubpatterns)
            {
                var memberName = GetMemberName(propSub.Member);
                if (memberName == "kind")
                    return ConvertPattern(propSub.Pattern, kindDomain);
            }
        }

        // Empty recursive pattern like `{ }` or a type-check pattern — wildcard
        return Cell.Wildcard.Instance;
    }

    /// Handle binary (or/and) patterns for struct DU Kind extraction.
    static Cell ConvertBinaryStructDUPattern(
        IBinaryPatternOperation binaryPat,
        EnumDomain kindDomain,
        INamedTypeSymbol kindEnum,
        StructDUStyle style)
    {
        var left = ExtractStructDUKindCell(binaryPat.LeftPattern, kindDomain, kindEnum, style);
        var right = ExtractStructDUKindCell(binaryPat.RightPattern, kindDomain, kindEnum, style);

        switch (binaryPat.OperatorKind)
        {
            case BinaryOperatorKind.Or:
                return UnionCells(left, right, kindDomain);
            case BinaryOperatorKind.And:
                return IntersectCells(left, right, kindDomain);
            default:
                return Cell.Wildcard.Instance;
        }
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

            case ITypePatternOperation typePattern:
                return ConvertTypePattern(typePattern.MatchedType, domain);

            case IRecursivePatternOperation:
                // Structural decomposition — conservative wildcard for now.
                // Nested tuples are handled by BuildTupleMatrix flattening.
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

        // Type-narrowing declaration (e.g., `Dog d`) — treat like a type pattern
        if (declPattern.MatchedType != null)
            return ConvertTypePattern(declPattern.MatchedType, domain);

        return Cell.Wildcard.Instance;
    }

    /// Converts a type pattern (matched type) into a Cell.
    /// For EnforceExhaustiveDomain, creates a singleton constraint for the matched type.
    /// For other domains, falls back to wildcard (conservative).
    static Cell ConvertTypePattern(ITypeSymbol matchedType, IValueDomain domain)
    {
        // Unwrap NullableDomain to check the inner domain
        var isNullable = domain is NullableDomain;
        var effectiveDomain = isNullable ? ((NullableDomain)domain).Inner : domain;

        if (effectiveDomain is EnforceExhaustiveDomain exhaustiveDomain
            && matchedType is INamedTypeSymbol namedMatchedType)
        {
            // Find the matched type in the domain's known types
            INamedTypeSymbol? found = null;
            foreach (var candidate in exhaustiveDomain.AllTypes)
            {
                if (SymbolEqualityComparer.Default.Equals(candidate, namedMatchedType) ||
                    SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, namedMatchedType.OriginalDefinition))
                {
                    found = candidate;
                    break;
                }
            }

            if (found != null)
            {
                var singleton = ImmutableHashSet.Create<INamedTypeSymbol>(SymbolEqualityComparer.Default, found);
                var constraint = new EnforceExhaustiveDomain(effectiveDomain.Type, singleton, exhaustiveDomain.AllTypes);

                if (isNullable)
                    return new Cell.Constraint(new NullableDomain(domain.Type, constraint, hasNull: false));

                return new Cell.Constraint(constraint);
            }
        }

        // Fallback — conservative wildcard
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
        // Unwrap NullableDomain — relational patterns match non-null values
        var isNullable = domain is NullableDomain;
        var effectiveDomain = isNullable ? ((NullableDomain)domain).Inner : domain;

        if (effectiveDomain is not NumericDomain)
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

        var innerConstraint = new NumericDomain(effectiveDomain.Type, IntervalSet.Single(interval), double.MinValue, double.MaxValue);

        if (isNullable)
        {
            // Wrap in NullableDomain covering only the non-null portion
            return new Cell.Constraint(new NullableDomain(domain.Type, innerConstraint, hasNull: false));
        }

        return new Cell.Constraint(innerConstraint);
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
