using System;
using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.PatternAnalysis.Domains;
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

    /// A pattern operation the analyzer cannot model. It is deliberately
    /// kept separate from Wildcard: unsupported syntax must never prove that
    /// the covered set is exhaustive.
    internal sealed class Unknown : Cell
    {
        public static readonly Unknown Instance = new();
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
    /// Rows containing an unsupported operation. The marker is kept separate
    /// from cells because specialization removes columns, including the cell
    /// that carried the marker.
    internal ImmutableArray<bool> UnknownRows { get; }
    public int RowCount => Rows.Length;
    public int ColumnCount => Columns.Length;

    public PatternMatrix(
        ImmutableArray<ImmutableArray<Cell>> rows,
        ImmutableArray<Column> columns,
        ImmutableArray<bool> unknownRows = default)
    {
        Rows = rows;
        Columns = columns;
        if (unknownRows.IsDefault)
        {
            var inferred = ImmutableArray.CreateBuilder<bool>(rows.Length);
            foreach (var row in rows)
            {
                bool unknown = false;
                foreach (var cell in row)
                {
                    if (cell is Cell.Unknown)
                    {
                        unknown = true;
                        break;
                    }
                }

                inferred.Add(unknown);
            }

            UnknownRows = inferred.MoveToImmutable();
        }
        else
        {
            UnknownRows = unknownRows;
        }
    }

    /// Specialize: keep rows whose cell at columnIndex intersects the partition.
    /// Remove the column. Wildcard cells are kept in all partitions.
    public PatternMatrix Specialize(int columnIndex, IValueDomain partition)
    {
        var rowBuilder = ImmutableArray.CreateBuilder<ImmutableArray<Cell>>();
        var unknownBuilder = ImmutableArray.CreateBuilder<bool>(Rows.Length);

        for (int rowIndex = 0; rowIndex < Rows.Length; rowIndex++)
        {
            var row = Rows[rowIndex];
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
            else if (cell is Cell.Unknown)
            {
                // An unsupported cell may match this partition, but it does
                // not itself cover it. Keep the marker so the decision tree
                // can report an indeterminate result if no known row covers
                // the resulting region.
                keep = true;
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
            unknownBuilder.Add(UnknownRows[rowIndex] || cell is Cell.Unknown);
        }

        // Build new columns with the specialized column removed
        var colBuilder = ImmutableArray.CreateBuilder<Column>(Columns.Length - 1);
        for (int c = 0; c < Columns.Length; c++)
        {
            if (c != columnIndex)
                colBuilder.Add(Columns[c]);
        }

        return new PatternMatrix(
            rowBuilder.ToImmutable(),
            colBuilder.MoveToImmutable(),
            unknownBuilder.ToImmutable());
    }

    // ─── Subject type resolution ────────────────────────────────────────

    /// Resolves the switch subject type with correct NullableAnnotation.
    /// For most symbols (parameters, fields, properties, explicitly-typed locals), the symbol's
    /// Type property has the correct annotation. However, var-declared locals report Annotated
    /// even when the inferred type is non-nullable. For those, we fall back to IOperation.Type
    /// which reflects the flow-analyzed nullability correctly.
    static ITypeSymbol? ResolveSubjectType(IOperation value)
    {
        var type = value.Type;
        if (type == null || type.IsValueType)
            return type;

        var declaredType = value switch
        {
            ILocalReferenceOperation localRef when IsVarLocal(localRef.Local)
                => type, // var locals: symbol annotation is unreliable, use operation type
            IParameterReferenceOperation paramRef => paramRef.Parameter.Type,
            ILocalReferenceOperation localRef => localRef.Local.Type,
            IFieldReferenceOperation fieldRef => fieldRef.Field.Type,
            IPropertyReferenceOperation propRef => propRef.Property.Type,
            _ => null,
        };

        return declaredType ?? type;
    }

    static bool IsVarLocal(ILocalSymbol local)
    {
        foreach (var syntaxRef in local.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is VariableDeclaratorSyntax
                { Parent: VariableDeclarationSyntax varDecl } && varDecl.Type.IsVar)
                return true;
        }
        return false;
    }

    // ─── Build from switch expression ────────────────────────────────────

    /// Build a PatternMatrix from a switch expression's arms.
    /// Arms with when guards are excluded (conservative — treated as not covering anything).
    public static PatternMatrix Build(ISwitchExpressionOperation switchExpr, DomainResolver resolver)
    {
        var subjectType = ResolveSubjectType(switchExpr.Value);
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
        var subjectType = ResolveSubjectType(switchStmt.Value);
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
            return Cell.Unknown.Instance;

        var constantValue = value.ConstantValue;
        if (!constantValue.HasValue)
            return Cell.Unknown.Instance;

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
            return new Cell.Constraint(new EmptyDomain(domain.Type));
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

        // Numeric constants. Keep Roslyn's typed ConstantValue intact until
        // NumericDomain chooses its exact integral or legacy floating path.
        if (innerCell == null && effectiveDomain is NumericDomain numericDomain)
        {
            if (TryGetNumericConstant(value, numericDomain, out var numericValue) &&
                numericDomain.TryCreatePoint(numericValue, out var point))
            {
                innerCell = new Cell.Constraint(point);
            }
            else
            {
                // An unsupported typed numeric constant cannot prove coverage.
                innerCell = new Cell.Constraint(numericDomain.EmptyLike());
            }
        }

        if (innerCell == null)
            return Cell.Unknown.Instance;

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
            {
                if (HasPropertyPatterns(patterns))
                    return BuildNullableStructDUPropertyMatrix(subjectType, kindEnum, patterns, resolver);
                return BuildNullableStructDUMatrix(subjectType, kindEnum, patterns, resolver);
            }

            if (HasPropertyPatterns(patterns))
                return BuildStructDUPropertyMatrix(unwrapped, kindEnum, patterns, resolver);
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
        {
            if (HasPropertyPatterns(patterns))
                return BuildTypePropertyMatrix(subjectType, patterns, resolver, resolved);
            return BuildTypeDomainMatrix(subjectType, patterns, resolved);
        }

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
            if (ContainsPropertyPattern(pattern))
                return true;
        }
        return false;
    }

    static bool ContainsPropertyPattern(IPatternOperation pattern)
    {
        switch (pattern)
        {
            case IRecursivePatternOperation recursive:
                if (!recursive.PropertySubpatterns.IsEmpty)
                    return true;
                foreach (var subpattern in recursive.DeconstructionSubpatterns)
                {
                    if (ContainsPropertyPattern(subpattern))
                        return true;
                }
                return false;
            case IBinaryPatternOperation binary:
                return ContainsPropertyPattern(binary.LeftPattern)
                    || ContainsPropertyPattern(binary.RightPattern);
            case INegatedPatternOperation negated:
                return ContainsPropertyPattern(negated.Pattern);
            default:
                return false;
        }
    }

    /// Build a multi-column matrix from property patterns.
    /// Collects all distinct properties mentioned across all arms,
    /// creates a column for each, and maps each arm's subpatterns.
    static PatternMatrix BuildPropertyMatrix(
        ITypeSymbol subjectType,
        ImmutableArray<IPatternOperation> patterns,
        DomainResolver resolver)
    {
        return BuildPropertyMatrixCore(subjectType, patterns, resolver, resolver.Resolve(subjectType));
    }

    /// Builds a root column plus one column for every distinct property path.
    /// Keeping the parent path as a column is what prevents a child property
    /// from being considered when its containing reference is null or a
    /// different runtime type.
    static PatternMatrix BuildPropertyMatrixCore(
        ITypeSymbol subjectType,
        ImmutableArray<IPatternOperation> patterns,
        DomainResolver resolver,
        IValueDomain rootDomain)
    {
        var paths = new System.Collections.Generic.List<ImmutableArray<ISymbol>>();
        foreach (var pattern in patterns)
        {
            if (pattern is IRecursivePatternOperation recursive)
                CollectPropertyPaths(recursive, ImmutableArray<ISymbol>.Empty, paths);
        }

        var columns = ImmutableArray.CreateBuilder<Column>(paths.Count + 1);
        columns.Add(new Column(new SlotIdentifier.RootSlot(subjectType), rootDomain));
        foreach (var path in paths)
        {
            var member = path[path.Length - 1];
            var memberType = member switch
            {
                IPropertySymbol property => property.Type,
                IFieldSymbol field => field.Type,
                _ => subjectType,
            };
            columns.Add(new Column(new SlotIdentifier.PropertySlot(path), resolver.Resolve(memberType)));
        }

        var cols = columns.MoveToImmutable();
        var rows = ImmutableArray.CreateBuilder<ImmutableArray<Cell>>(patterns.Length);
        foreach (var pattern in patterns)
            rows.Add(ConvertPropertyRow(pattern, cols, paths, resolver, isStructDU: false, rootDomain));

        return new PatternMatrix(rows.ToImmutable(), cols);
    }

    static void CollectPropertyPaths(
        IRecursivePatternOperation recursive,
        ImmutableArray<ISymbol> parent,
        System.Collections.Generic.List<ImmutableArray<ISymbol>> paths)
    {
        foreach (var propSub in recursive.PropertySubpatterns)
        {
            var memberPath = GetMemberPath(propSub);
            if (memberPath.IsDefaultOrEmpty)
                continue;

            var path = parent.AddRange(memberPath);
            AddPathPrefixes(paths, path);

            if (propSub.Pattern is IRecursivePatternOperation nested)
                CollectPropertyPaths(nested, path, paths);
        }
    }

    static bool ContainsPath(
        System.Collections.Generic.List<ImmutableArray<ISymbol>> paths,
        ImmutableArray<ISymbol> candidate)
    {
        foreach (var path in paths)
        {
            if (PathsEqual(path, candidate))
                return true;
        }

        return false;
    }

    static void AddPathPrefixes(
        System.Collections.Generic.List<ImmutableArray<ISymbol>> paths,
        ImmutableArray<ISymbol> fullPath)
    {
        for (int length = 1; length <= fullPath.Length; length++)
        {
            var prefix = fullPath.Take(length).ToImmutableArray();
            if (!ContainsPath(paths, prefix))
                paths.Add(prefix);
        }
    }

    static bool PathsEqual(ImmutableArray<ISymbol> left, ImmutableArray<ISymbol> right)
    {
        if (left.Length != right.Length)
            return false;

        for (int i = 0; i < left.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(left[i], right[i]))
                return false;
        }

        return true;
    }

    /// Convert one arm into a row. Unknown recursive operations remain marked
    /// as Unknown so the decision tree can avoid claiming proof.
    static ImmutableArray<Cell> ConvertPropertyRow(
        IPatternOperation pattern,
        ImmutableArray<Column> columns,
        System.Collections.Generic.List<ImmutableArray<ISymbol>> paths,
        DomainResolver resolver,
        bool isStructDU,
        IValueDomain rootDomain)
    {
        var cells = ImmutableArray.CreateBuilder<Cell>(columns.Length);
        for (int i = 0; i < columns.Length; i++)
            cells.Add(Cell.Wildcard.Instance);

        if (pattern is IDiscardPatternOperation
            || pattern is IDeclarationPatternOperation { Syntax: VarPatternSyntax })
            return cells.MoveToImmutable();

        cells[0] = ConvertRootPattern(pattern, rootDomain, isStructDU);

        if (pattern is IRecursivePatternOperation recursive)
        {
            ApplyPropertySubpatterns(
                recursive,
                ImmutableArray<ISymbol>.Empty,
                cells,
                columns,
                paths,
                resolver,
                isStructDU);
        }

        return cells.MoveToImmutable();
    }

    static Cell ConvertRootPattern(IPatternOperation pattern, IValueDomain rootDomain, bool isStructDU)
    {
        if (pattern is IConstantPatternOperation constant)
            return ConvertConstantPattern(constant, rootDomain);

        if (pattern is IDeclarationPatternOperation declaration)
        {
            if (declaration.Syntax is VarPatternSyntax)
                return Cell.Wildcard.Instance;
            return declaration.MatchedType is not null
                ? ConvertTypePattern(declaration.MatchedType, rootDomain)
                : Cell.Unknown.Instance;
        }

        if (pattern is ITypePatternOperation typePattern)
            return ConvertTypePattern(typePattern.MatchedType, rootDomain);

        if (pattern is IRecursivePatternOperation recursive)
        {
            if (!recursive.DeconstructionSubpatterns.IsEmpty)
                return Cell.Unknown.Instance;

            if (isStructDU)
                return Cell.Wildcard.Instance;

            if (recursive.MatchedType is not null)
                return ConvertTypePattern(recursive.MatchedType, rootDomain);

            // A property pattern on a nullable root proves that the root is
            // non-null. For non-nullable roots this is simply the universe.
            return NonNullCell(rootDomain);
        }

        if (pattern is IBinaryPatternOperation binary)
            return ConvertBinaryPattern(binary, rootDomain);
        if (pattern is INegatedPatternOperation negated)
            return ConvertNegatedPattern(negated, rootDomain);

        return ConvertPattern(pattern, rootDomain);
    }

    static Cell NonNullCell(IValueDomain domain)
    {
        if (domain is NullableDomain nullable)
            return new Cell.Constraint(new NullableDomain(domain.Type, nullable.Inner, hasNull: false));
        return Cell.Wildcard.Instance;
    }

    static void ApplyPropertySubpatterns(
        IRecursivePatternOperation recursive,
        ImmutableArray<ISymbol> parent,
        ImmutableArray<Cell>.Builder cells,
        ImmutableArray<Column> columns,
        System.Collections.Generic.List<ImmutableArray<ISymbol>> paths,
        DomainResolver resolver,
        bool isStructDU)
    {
        foreach (var propSub in recursive.PropertySubpatterns)
        {
            var memberPath = GetMemberPath(propSub);
            if (memberPath.IsDefaultOrEmpty)
                continue;

            if (isStructDU && parent.Length == 0 && GetMemberName(propSub.Member) == "kind")
                continue;

            var path = parent.AddRange(memberPath);
            int index = FindPathIndex(paths, path);
            if (index < 0)
                continue;

            // Extended property syntax (`Left.Flag`) has no nested
            // recursive operation to establish that `Left` is non-null.
            // Constrain every containing prefix before applying the leaf.
            for (int prefixLength = 1; prefixLength < path.Length; prefixLength++)
            {
                var prefix = path.Take(prefixLength).ToImmutableArray();
                int prefixIndex = FindPathIndex(paths, prefix);
                if (prefixIndex < 0)
                    continue;

                var prefixDomain = columns[prefixIndex + 1].Domain;
                cells[prefixIndex + 1] = IntersectCells(
                    cells[prefixIndex + 1],
                    NonNullCell(prefixDomain),
                    prefixDomain);
            }

            var childDomain = columns[index + 1].Domain;
            var childPattern = propSub.Pattern;
            var childCell = ConvertNestedPattern(childPattern, childDomain);
            cells[index + 1] = childCell;

            if (childPattern is IRecursivePatternOperation nested)
            {
                // The containing value must exist before any leaf can match.
                if (childCell is Cell.Wildcard && nested.MatchedType is null)
                    cells[index + 1] = NonNullCell(childDomain);

                ApplyPropertySubpatterns(
                    nested,
                    path,
                    cells,
                    columns,
                    paths,
                    resolver,
                    isStructDU: false);
            }
        }
    }

    static Cell ConvertNestedPattern(IPatternOperation pattern, IValueDomain domain)
    {
        if (pattern is IRecursivePatternOperation recursive)
        {
            if (recursive.DeconstructionSubpatterns.Length > 0)
                return Cell.Unknown.Instance;
            if (recursive.MatchedType is not null)
                return ConvertTypePattern(recursive.MatchedType, domain);
            return NonNullCell(domain);
        }

        return ConvertPattern(pattern, domain);
    }

    static int FindPathIndex(
        System.Collections.Generic.List<ImmutableArray<ISymbol>> paths,
        ImmutableArray<ISymbol> path)
    {
        for (int i = 0; i < paths.Count; i++)
        {
            if (PathsEqual(paths[i], path))
                return i;
        }

        return -1;
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

        var rowBuilder = ImmutableArray.CreateBuilder<ImmutableArray<Cell>>();
        foreach (var pattern in patterns)
        {
            var cell = ExtractStructDUKindCell(pattern, kindDomain, kindEnum);
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

        var rowBuilder = ImmutableArray.CreateBuilder<ImmutableArray<Cell>>();
        foreach (var pattern in patterns)
        {
            var cell = ConvertNullableStructDUPattern(pattern, nullableKindDomain, kindDomain, kindEnum);
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
        INamedTypeSymbol kindEnum)
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
            var leftCell = ConvertNullableStructDUPattern(binaryAnd.LeftPattern, nullableKindDomain, kindDomain, kindEnum);
            var rightCell = ConvertNullableStructDUPattern(binaryAnd.RightPattern, nullableKindDomain, kindDomain, kindEnum);
            return IntersectCells(leftCell, rightCell, nullableKindDomain);
        }

        // Binary `or` pattern — extract inner Kind cell and wrap in NullableDomain
        if (pattern is IBinaryPatternOperation binaryOr && binaryOr.OperatorKind == BinaryOperatorKind.Or)
        {
            var leftCell = ConvertNullableStructDUPattern(binaryOr.LeftPattern, nullableKindDomain, kindDomain, kindEnum);
            var rightCell = ConvertNullableStructDUPattern(binaryOr.RightPattern, nullableKindDomain, kindDomain, kindEnum);
            return UnionCells(leftCell, rightCell, nullableKindDomain);
        }

        // DU kind-matching patterns (deconstruct/property/constant) → wrap in non-null NullableDomain
        var innerCell = ExtractStructDUKindCell(pattern, kindDomain, kindEnum);
        if (innerCell is Cell.Constraint innerConstraint)
            return new Cell.Constraint(new NullableDomain(nullableKindDomain.Type, innerConstraint.MatchedValues, hasNull: false));

        if (innerCell is Cell.Unknown)
            return Cell.Unknown.Instance;

        // An unrestricted non-null recursive pattern does not match null.
        return new Cell.Constraint(new NullableDomain(nullableKindDomain.Type, kindDomain, hasNull: false));
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

    static PatternMatrix BuildTypePropertyMatrix(
        ITypeSymbol subjectType,
        ImmutableArray<IPatternOperation> patterns,
        DomainResolver resolver,
        IValueDomain domain)
        => BuildPropertyMatrixCore(subjectType, patterns, resolver, domain);

    /// Struct DU property patterns have a Kind enum at the root and payload
    /// members below it. The kind is kept as its own stable column so payload
    /// leaves remain correlated with the selected variant.
    static PatternMatrix BuildStructDUPropertyMatrix(
        ITypeSymbol subjectType,
        INamedTypeSymbol kindEnum,
        ImmutableArray<IPatternOperation> patterns,
        DomainResolver resolver)
        => BuildStructDUPropertyMatrixCore(
            subjectType,
            kindEnum,
            patterns,
            resolver,
            EnumDomain.Universe(kindEnum),
            nullableRoot: false);

    static PatternMatrix BuildStructDUPropertyMatrixCore(
        ITypeSymbol subjectType,
        INamedTypeSymbol kindEnum,
        ImmutableArray<IPatternOperation> patterns,
        DomainResolver resolver,
        IValueDomain rootDomain,
        bool nullableRoot)
    {
        var paths = new System.Collections.Generic.List<ImmutableArray<ISymbol>>();
        foreach (var pattern in patterns)
        {
            if (pattern is IRecursivePatternOperation recursive)
                CollectStructDUPropertyPaths(recursive, ImmutableArray<ISymbol>.Empty, paths);
        }

        var columns = ImmutableArray.CreateBuilder<Column>(paths.Count + 1);
        columns.Add(new Column(new SlotIdentifier.RootSlot(rootDomain.Type), rootDomain));
        foreach (var path in paths)
        {
            var member = path[path.Length - 1];
            var memberType = member switch
            {
                IPropertySymbol property => property.Type,
                IFieldSymbol field => field.Type,
                _ => subjectType,
            };
            columns.Add(new Column(new SlotIdentifier.PropertySlot(path), resolver.Resolve(memberType)));
        }

        var cols = columns.MoveToImmutable();
        var rows = ImmutableArray.CreateBuilder<ImmutableArray<Cell>>(patterns.Length);
        foreach (var pattern in patterns)
        {
            var cells = ImmutableArray.CreateBuilder<Cell>(cols.Length);
            for (int i = 0; i < cols.Length; i++)
                cells.Add(Cell.Wildcard.Instance);

            cells[0] = nullableRoot
                ? ConvertNullableStructDUPropertyPattern(
                    pattern,
                    (NullableDomain)cols[0].Domain,
                    (EnumDomain)((NullableDomain)cols[0].Domain).Inner,
                    kindEnum)
                : ExtractStructDUPropertyKindCell(
                    pattern,
                    (EnumDomain)cols[0].Domain,
                    kindEnum);

            // A logical pattern containing nested properties cannot be
            // represented by the Kind-only extractor. Keep it indeterminate
            // rather than silently dropping the payload restriction.
            if (pattern is not IRecursivePatternOperation
                && ContainsPropertyPattern(pattern))
                cells[0] = Cell.Unknown.Instance;
            else if (pattern is IRecursivePatternOperation recursivePattern
                     && !recursivePattern.DeconstructionSubpatterns.IsEmpty
                     && !recursivePattern.PropertySubpatterns.IsEmpty)
                cells[0] = Cell.Unknown.Instance;

            if (pattern is IRecursivePatternOperation recursive)
            {
                ApplyPropertySubpatterns(
                    recursive,
                    ImmutableArray<ISymbol>.Empty,
                    cells,
                    cols,
                    paths,
                    resolver,
                    isStructDU: true);
            }

            rows.Add(cells.MoveToImmutable());
        }

        return new PatternMatrix(rows.ToImmutable(), cols);
    }

    static void CollectStructDUPropertyPaths(
        IRecursivePatternOperation recursive,
        ImmutableArray<ISymbol> parent,
        System.Collections.Generic.List<ImmutableArray<ISymbol>> paths)
    {
        foreach (var propSub in recursive.PropertySubpatterns)
        {
            if (parent.Length == 0 && GetMemberName(propSub.Member) == "kind")
                continue;

            var memberPath = GetMemberPath(propSub);
            if (memberPath.IsDefaultOrEmpty)
                continue;

            var path = parent.AddRange(memberPath);
            AddPathPrefixes(paths, path);

            if (propSub.Pattern is IRecursivePatternOperation nested)
                CollectStructDUPropertyPaths(nested, path, paths);
        }
    }

    static PatternMatrix BuildNullableStructDUPropertyMatrix(
        ITypeSymbol nullableType,
        INamedTypeSymbol kindEnum,
        ImmutableArray<IPatternOperation> patterns,
        DomainResolver resolver)
        => BuildStructDUPropertyMatrixCore(
            nullableType,
            kindEnum,
            patterns,
            resolver,
            NullableDomain.Universe(nullableType, EnumDomain.Universe(kindEnum)),
            nullableRoot: true);

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
                return Cell.Unknown.Instance;

            case IRecursivePatternOperation recursive:
                // Record DU variant: `Option<int>.Some { Value: var v }` has MatchedType = Some
                if (!recursive.DeconstructionSubpatterns.IsEmpty)
                    return Cell.Unknown.Instance;
                if (recursive.MatchedType != null)
                    return ConvertTypePattern(recursive.MatchedType, domain);
                // Positional/structural pattern without a modeled type
                // narrowing cannot prove coverage here.
                return Cell.Unknown.Instance;

            case ITypePatternOperation typePattern:
                return ConvertTypePattern(typePattern.MatchedType, domain);

            case IConstantPatternOperation constPattern:
                return ConvertConstantPattern(constPattern, domain);

            case IBinaryPatternOperation binaryPattern:
                return ConvertTypeDomainBinaryPattern(binaryPattern, domain);

            case INegatedPatternOperation negPattern:
                return ConvertNegatedPattern(negPattern, domain);

            default:
                return Cell.Unknown.Instance;
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
                return Cell.Unknown.Instance;
        }
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

    static Cell ExtractStructDUPropertyKindCell(
        IPatternOperation pattern,
        EnumDomain kindDomain,
        INamedTypeSymbol kindEnum)
    {
        switch (pattern)
        {
            case IDiscardPatternOperation:
                return Cell.Wildcard.Instance;
            case IDeclarationPatternOperation { Syntax: VarPatternSyntax }:
                return Cell.Wildcard.Instance;
            case IRecursivePatternOperation recursive:
                if (!recursive.DeconstructionSubpatterns.IsEmpty)
                    return Cell.Unknown.Instance;
                foreach (var propSub in recursive.PropertySubpatterns)
                {
                    if (GetMemberName(propSub.Member) == "kind")
                        return ConvertPattern(propSub.Pattern, kindDomain);
                }
                return Cell.Wildcard.Instance;
            case IBinaryPatternOperation:
                // Logical combinations containing payload properties are
                // intentionally left to the conservative unknown path.
                return Cell.Unknown.Instance;
            case IConstantPatternOperation constant:
                return ConvertConstantPattern(constant, kindDomain);
            default:
                return Cell.Unknown.Instance;
        }
    }

    static Cell ConvertNullableStructDUPropertyPattern(
        IPatternOperation pattern,
        NullableDomain nullableKindDomain,
        EnumDomain kindDomain,
        INamedTypeSymbol kindEnum)
    {
        if (pattern is IConstantPatternOperation constant
            && constant.Value?.ConstantValue is { HasValue: true, Value: null })
        {
            return ConvertNullableStructDUPattern(
                pattern,
                nullableKindDomain,
                kindDomain,
                kindEnum);
        }

        var inner = ExtractStructDUPropertyKindCell(pattern, kindDomain, kindEnum);
        if (inner is Cell.Constraint constraint)
            return new Cell.Constraint(new NullableDomain(nullableKindDomain.Type, constraint.MatchedValues, hasNull: false));
        if (inner is Cell.Unknown)
            return Cell.Unknown.Instance;
        return new Cell.Constraint(new NullableDomain(nullableKindDomain.Type, kindDomain, hasNull: false));
    }

    /// Convert a single arm pattern to a Kind enum cell for a struct DU.
    static Cell ExtractStructDUKindCell(
        IPatternOperation pattern,
        EnumDomain kindDomain,
        INamedTypeSymbol kindEnum)
    {
        switch (pattern)
        {
            case IDiscardPatternOperation:
                return Cell.Wildcard.Instance;

            case IDeclarationPatternOperation declPat when declPat.Syntax is VarPatternSyntax:
                return Cell.Wildcard.Instance;

            case IBinaryPatternOperation binaryPat:
                return ConvertBinaryStructDUPattern(binaryPat, kindDomain, kindEnum);

            case IRecursivePatternOperation recursive:
                return ExtractKindFromRecursive(recursive, kindDomain, kindEnum);

            case IConstantPatternOperation constPat:
                // Direct Kind constant (unlikely at top level, but handle it)
                return ConvertConstantPattern(constPat, kindDomain);

            default:
                return Cell.Unknown.Instance;
        }
    }

    /// Extract Kind from a recursive pattern (deconstruct or property).
    static Cell ExtractKindFromRecursive(
        IRecursivePatternOperation recursive,
        EnumDomain kindDomain,
        INamedTypeSymbol kindEnum)
    {
        // Deconstruct: (Kind.Circle, ...) — element[0] is the Kind
        if (!recursive.DeconstructionSubpatterns.IsEmpty)
        {
            for (int i = 1; i < recursive.DeconstructionSubpatterns.Length; i++)
            {
                if (!IsUnrestrictedPayloadPattern(recursive.DeconstructionSubpatterns[i]))
                    return Cell.Unknown.Instance;
            }

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
                {
                    foreach (var other in recursive.PropertySubpatterns)
                    {
                        if (ReferenceEquals(other, propSub))
                            continue;
                        if (!IsUnrestrictedPayloadPattern(other.Pattern))
                            return Cell.Unknown.Instance;
                    }

                    return ConvertPattern(propSub.Pattern, kindDomain);
                }
            }
        }

        // Empty recursive pattern like `{ }` or a type-check pattern — wildcard
        return Cell.Wildcard.Instance;
    }

    static bool IsUnrestrictedPayloadPattern(IPatternOperation pattern)
        => pattern is IDiscardPatternOperation
            || pattern is IDeclarationPatternOperation { Syntax: VarPatternSyntax };

    /// Handle binary (or/and) patterns for struct DU Kind extraction.
    static Cell ConvertBinaryStructDUPattern(
        IBinaryPatternOperation binaryPat,
        EnumDomain kindDomain,
        INamedTypeSymbol kindEnum)
    {
        var left = ExtractStructDUKindCell(binaryPat.LeftPattern, kindDomain, kindEnum);
        var right = ExtractStructDUKindCell(binaryPat.RightPattern, kindDomain, kindEnum);

        switch (binaryPat.OperatorKind)
        {
            case BinaryOperatorKind.Or:
                return UnionCells(left, right, kindDomain);
            case BinaryOperatorKind.And:
                return IntersectCells(left, right, kindDomain);
            default:
                return Cell.Unknown.Instance;
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
                // Recursive property/deconstruction patterns are expanded by
                // the structural matrix builders. Reaching this fallback
                // means the operation could not be modeled at this level.
                return Cell.Unknown.Instance;

            default:
                // Unknown pattern — never treat unsupported syntax as a
                // proven wildcard.
                return Cell.Unknown.Instance;
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

        // A recursive property pattern commonly reports the containing type
        // as MatchedType (for example `{ Child: { Flag: true } }`). This is
        // not an unsupported type test; it simply proves that the containing
        // value is non-null.
        if (SymbolEqualityComparer.Default.Equals(matchedType, effectiveDomain.Type))
            return isNullable ? NonNullCell(domain) : Cell.Wildcard.Instance;

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

        // Fallback — the type is not part of a finite known hierarchy. Do not
        // claim that an unsupported type pattern covers the whole domain.
        return Cell.Unknown.Instance;
    }

    static Cell ConvertConstantPattern(IConstantPatternOperation constPattern, IValueDomain domain)
    {
        var value = constPattern.Value;
        if (value == null)
            return Cell.Unknown.Instance;

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

            // Null cannot match a non-nullable domain.
            return new Cell.Constraint(new EmptyDomain(domain.Type));
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

        // Numeric constants use the same typed-constant helper as traditional
        // case labels and relational patterns.
        if (innerCell == null && effectiveDomain is NumericDomain numericDomain)
        {
            if (TryGetNumericConstant(value, numericDomain, out var numericValue) &&
                numericDomain.TryCreatePoint(numericValue, out var point))
            {
                innerCell = new Cell.Constraint(point);
            }
            else
            {
                // An unsupported typed numeric constant cannot prove coverage.
                innerCell = new Cell.Constraint(numericDomain.EmptyLike());
            }
        }

        if (innerCell == null)
            return Cell.Unknown.Instance;

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

        if (effectiveDomain is not NumericDomain numericDomain)
            return Cell.Unknown.Instance;

        // Keep the typed Roslyn constant intact. Integral domains convert it
        // directly to BigInteger; floating domains preserve the existing
        // invariant-culture double conversion.
        if (!TryGetNumericConstant(relPattern.Value, numericDomain, out var numericValue) ||
            !numericDomain.TryCreateRange(numericValue, relPattern.OperatorKind, out var innerConstraint))
        {
            var unsupported = numericDomain.EmptyLike();
            if (isNullable)
            {
                return new Cell.Constraint(
                    new NullableDomain(domain.Type, unsupported, hasNull: false));
            }

            return new Cell.Constraint(unsupported);
        }

        if (isNullable)
        {
            // Wrap in NullableDomain covering only the non-null portion
            return new Cell.Constraint(new NullableDomain(domain.Type, innerConstraint, hasNull: false));
        }

        return new Cell.Constraint(innerConstraint);
    }

    /// Preserve the operation's typed constant until NumericDomain dispatches
    /// to exact integral or legacy floating arithmetic. Unsupported numeric
    /// constants return false so callers can represent an empty constraint,
    /// never a wildcard that would falsely prove exhaustiveness.
    static bool TryGetNumericConstant(
        IOperation value,
        NumericDomain domain,
        out object constant)
    {
        var constantValue = value.ConstantValue;
        if (!constantValue.HasValue || constantValue.Value == null)
        {
            constant = null!;
            return false;
        }

        constant = constantValue.Value;
        if (domain.IsIntegral)
            return NumericDomain.IsIntegralConstant(constant);

        return constant is IConvertible;
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
                return Cell.Unknown.Instance;
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

        if (inner is Cell.Unknown)
            return Cell.Unknown.Instance;

        if (inner is Cell.Wildcard)
        {
            // Complement of a proven wildcard is the empty set.
            return new Cell.Constraint(new EmptyDomain(domain.Type));
        }

        if (inner is Cell.Constraint constraint)
        {
            if (constraint.MatchedValues is EmptyDomain)
                return Cell.Wildcard.Instance;

            var complemented = constraint.MatchedValues.Complement();
            if (complemented.IsEmpty)
                return new Cell.Constraint(new EmptyDomain(domain.Type));
            return new Cell.Constraint(complemented);
        }

        return Cell.Unknown.Instance;
    }

    /// Union two cells: wildcard absorbs everything; otherwise union the domains.
    static Cell UnionCells(Cell left, Cell right, IValueDomain domain)
    {
        if (left is Cell.Wildcard || right is Cell.Wildcard)
            return Cell.Wildcard.Instance;

        if (left is Cell.Unknown || right is Cell.Unknown)
            return Cell.Unknown.Instance;

        if (left is Cell.Constraint leftC && right is Cell.Constraint rightC)
        {
            if (leftC.MatchedValues.IsEmpty)
                return right;
            if (rightC.MatchedValues.IsEmpty)
                return left;

            if (leftC.MatchedValues is NullableDomain leftNullable
                && rightC.MatchedValues is NullableDomain rightNullable)
            {
                if (!TryUnionDomains(leftNullable.Inner, rightNullable.Inner, out var innerUnion))
                    return Cell.Unknown.Instance;

                return new Cell.Constraint(new NullableDomain(
                    leftNullable.Type,
                    innerUnion,
                    leftNullable.HasNull || rightNullable.HasNull));
            }

            if (!TryUnionDomains(leftC.MatchedValues, rightC.MatchedValues, out var union))
                return Cell.Unknown.Instance;
            return new Cell.Constraint(union);
        }

        return Cell.Unknown.Instance;
    }

    static bool TryUnionDomains(
        IValueDomain left,
        IValueDomain right,
        out IValueDomain union)
    {
        if (left.IsEmpty)
        {
            union = right;
            return true;
        }

        if (right.IsEmpty)
        {
            union = left;
            return true;
        }

        try
        {
            union = left.Complement().Intersect(right.Complement()).Complement();
            return true;
        }
        catch (NotSupportedException)
        {
            union = left;
            return false;
        }
    }

    /// Intersect two cells: wildcard is identity; otherwise intersect the domains.
    static Cell IntersectCells(Cell left, Cell right, IValueDomain domain)
    {
        if (left is Cell.Wildcard)
            return right;
        if (right is Cell.Wildcard)
            return left;

        if (left is Cell.Unknown || right is Cell.Unknown)
            return Cell.Unknown.Instance;

        if (left is Cell.Constraint leftC && right is Cell.Constraint rightC)
        {
            var intersected = leftC.MatchedValues.Intersect(rightC.MatchedValues);
            return new Cell.Constraint(intersected);
        }

        return Cell.Wildcard.Instance;
    }

    /// Extract the full member path from a property subpattern. Extended
    /// property syntax (`{ Left.Flag: true }`) stores the receiver chain in
    /// the operation tree; retaining it prevents sibling leaves from aliasing.
    static ImmutableArray<ISymbol> GetMemberPath(IPropertySubpatternOperation propSub)
    {
        var symbols = ImmutableArray.CreateBuilder<ISymbol>();
        CollectMemberPath(propSub.Member, symbols);
        return symbols.ToImmutable();
    }

    static void CollectMemberPath(
        IOperation? operation,
        ImmutableArray<ISymbol>.Builder symbols)
    {
        switch (operation)
        {
            case IPropertyReferenceOperation property:
                CollectMemberPath(property.Instance, symbols);
                symbols.Add(property.Property);
                break;
            case IFieldReferenceOperation field:
                CollectMemberPath(field.Instance, symbols);
                symbols.Add(field.Field);
                break;
        }
    }
}
