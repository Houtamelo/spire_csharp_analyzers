using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.PatternAnalysis.Domains;
using Houtamelo.Spire.PatternAnalysis.Resolution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.PatternAnalysis.Tests.Domains;

public class EnforceExhaustiveDomainTests
{
    private static (Compilation compilation, INamedTypeSymbol baseType) Compile(string source, string baseTypeName)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("Test", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var baseType = compilation.GetTypeByMetadataName(baseTypeName)!;
        return (compilation, baseType);
    }

    private const string TwoSubtypesSource = @"
public abstract class Shape { }
public class Circle : Shape { }
public class Rectangle : Shape { }
";

    // ─── Create ──────────────────────────────────────────────────────

    [Fact]
    public void Create_universe_has_two_types()
    {
        var (compilation, baseType) = Compile(TwoSubtypesSource, "Shape");
        var resolver = new TypeHierarchyResolver();

        var domain = EnforceExhaustiveDomain.Create(baseType, resolver, compilation);

        Assert.False(domain.IsEmpty);
        Assert.True(domain.IsUniverse);
        Assert.Equal(2, domain.Split().Length);
    }

    // ─── Subtract ────────────────────────────────────────────────────

    [Fact]
    public void Subtract_one_type_leaves_one_remaining()
    {
        var (compilation, baseType) = Compile(TwoSubtypesSource, "Shape");
        var resolver = new TypeHierarchyResolver();
        var universe = EnforceExhaustiveDomain.Create(baseType, resolver, compilation);

        var circleType = compilation.GetTypeByMetadataName("Circle")!;
        var circleSingleton = new EnforceExhaustiveDomain(
            baseType,
            ImmutableHashSet.Create<INamedTypeSymbol>(SymbolEqualityComparer.Default, circleType),
            ((EnforceExhaustiveDomain)universe).AllTypes);

        var result = (EnforceExhaustiveDomain)universe.Subtract(circleSingleton);

        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);
        Assert.Single(result.Split());
    }

    [Fact]
    public void Subtract_both_types_leaves_empty()
    {
        var (compilation, baseType) = Compile(TwoSubtypesSource, "Shape");
        var resolver = new TypeHierarchyResolver();
        var universe = EnforceExhaustiveDomain.Create(baseType, resolver, compilation);

        var result = universe.Subtract(universe);

        Assert.True(result.IsEmpty);
    }

    // ─── Split ───────────────────────────────────────────────────────

    [Fact]
    public void Split_returns_two_singletons()
    {
        var (compilation, baseType) = Compile(TwoSubtypesSource, "Shape");
        var resolver = new TypeHierarchyResolver();
        var universe = EnforceExhaustiveDomain.Create(baseType, resolver, compilation);

        var parts = universe.Split();

        Assert.Equal(2, parts.Length);
        foreach (var part in parts)
        {
            Assert.False(part.IsEmpty);
            Assert.False(part.IsUniverse);
            Assert.Single(part.Split());
        }
    }

    // ─── Complement ──────────────────────────────────────────────────

    [Fact]
    public void Complement_of_one_type_is_the_other()
    {
        var (compilation, baseType) = Compile(TwoSubtypesSource, "Shape");
        var resolver = new TypeHierarchyResolver();
        var universe = EnforceExhaustiveDomain.Create(baseType, resolver, compilation);

        var circleType = compilation.GetTypeByMetadataName("Circle")!;
        var circleSingleton = new EnforceExhaustiveDomain(
            baseType,
            ImmutableHashSet.Create<INamedTypeSymbol>(SymbolEqualityComparer.Default, circleType),
            ((EnforceExhaustiveDomain)universe).AllTypes);

        var complement = (EnforceExhaustiveDomain)circleSingleton.Complement();

        Assert.False(complement.IsEmpty);
        Assert.False(complement.IsUniverse);
        Assert.Single(complement.Split());
    }

    // ─── Intersect ───────────────────────────────────────────────────

    [Fact]
    public void Intersect_disjoint_singletons_is_empty()
    {
        var (compilation, baseType) = Compile(TwoSubtypesSource, "Shape");
        var resolver = new TypeHierarchyResolver();
        var universe = EnforceExhaustiveDomain.Create(baseType, resolver, compilation);
        var allTypes = ((EnforceExhaustiveDomain)universe).AllTypes;

        var circleType = compilation.GetTypeByMetadataName("Circle")!;
        var rectangleType = compilation.GetTypeByMetadataName("Rectangle")!;

        var circleOnly = new EnforceExhaustiveDomain(
            baseType,
            ImmutableHashSet.Create<INamedTypeSymbol>(SymbolEqualityComparer.Default, circleType),
            allTypes);
        var rectangleOnly = new EnforceExhaustiveDomain(
            baseType,
            ImmutableHashSet.Create<INamedTypeSymbol>(SymbolEqualityComparer.Default, rectangleType),
            allTypes);

        var result = circleOnly.Intersect(rectangleOnly);

        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Intersect_universe_with_singleton_is_singleton()
    {
        var (compilation, baseType) = Compile(TwoSubtypesSource, "Shape");
        var resolver = new TypeHierarchyResolver();
        var universe = EnforceExhaustiveDomain.Create(baseType, resolver, compilation);
        var allTypes = ((EnforceExhaustiveDomain)universe).AllTypes;

        var circleType = compilation.GetTypeByMetadataName("Circle")!;
        var circleOnly = new EnforceExhaustiveDomain(
            baseType,
            ImmutableHashSet.Create<INamedTypeSymbol>(SymbolEqualityComparer.Default, circleType),
            allTypes);

        var result = (EnforceExhaustiveDomain)universe.Intersect(circleOnly);

        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);
        Assert.Single(result.Split());
    }

    // ─── IsEmpty ─────────────────────────────────────────────────────

    [Fact]
    public void IsEmpty_when_all_subtracted()
    {
        var (compilation, baseType) = Compile(TwoSubtypesSource, "Shape");
        var resolver = new TypeHierarchyResolver();
        var universe = EnforceExhaustiveDomain.Create(baseType, resolver, compilation);

        var result = universe.Subtract(universe);
        Assert.True(result.IsEmpty);
        Assert.True(result.Split().IsEmpty);
    }

    // ─── Type property ───────────────────────────────────────────────

    [Fact]
    public void Type_returns_base_type()
    {
        var (compilation, baseType) = Compile(TwoSubtypesSource, "Shape");
        var resolver = new TypeHierarchyResolver();
        var domain = EnforceExhaustiveDomain.Create(baseType, resolver, compilation);

        Assert.True(SymbolEqualityComparer.Default.Equals(baseType, domain.Type));
    }

    // ─── Double complement roundtrip ─────────────────────────────────

    [Fact]
    public void Double_complement_returns_original()
    {
        var (compilation, baseType) = Compile(TwoSubtypesSource, "Shape");
        var resolver = new TypeHierarchyResolver();
        var universe = EnforceExhaustiveDomain.Create(baseType, resolver, compilation);
        var allTypes = ((EnforceExhaustiveDomain)universe).AllTypes;

        var circleType = compilation.GetTypeByMetadataName("Circle")!;
        var circleOnly = new EnforceExhaustiveDomain(
            baseType,
            ImmutableHashSet.Create<INamedTypeSymbol>(SymbolEqualityComparer.Default, circleType),
            allTypes);

        var roundTrip = (EnforceExhaustiveDomain)circleOnly.Complement().Complement();

        Assert.False(roundTrip.IsEmpty);
        Assert.False(roundTrip.IsUniverse);
        Assert.Single(roundTrip.Split());
    }

    // ─── Complement of universe/empty ────────────────────────────────

    [Fact]
    public void Complement_of_universe_is_empty()
    {
        var (compilation, baseType) = Compile(TwoSubtypesSource, "Shape");
        var resolver = new TypeHierarchyResolver();
        var universe = EnforceExhaustiveDomain.Create(baseType, resolver, compilation);

        var result = universe.Complement();
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Complement_of_empty_is_universe()
    {
        var (compilation, baseType) = Compile(TwoSubtypesSource, "Shape");
        var resolver = new TypeHierarchyResolver();
        var universe = EnforceExhaustiveDomain.Create(baseType, resolver, compilation);

        var empty = universe.Subtract(universe);
        var result = empty.Complement();
        Assert.True(result.IsUniverse);
    }

    // ─── Split empty returns empty array ────────────────────────────

    [Fact]
    public void Split_empty_returns_empty_array()
    {
        var (compilation, baseType) = Compile(TwoSubtypesSource, "Shape");
        var resolver = new TypeHierarchyResolver();
        var universe = EnforceExhaustiveDomain.Create(baseType, resolver, compilation);

        var empty = universe.Subtract(universe);
        var parts = empty.Split();
        Assert.True(parts.IsEmpty);
    }
}
