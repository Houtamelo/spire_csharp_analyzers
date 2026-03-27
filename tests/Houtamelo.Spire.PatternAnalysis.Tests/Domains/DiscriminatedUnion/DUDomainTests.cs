using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.PatternAnalysis.Domains;
using Houtamelo.Spire.PatternAnalysis.Domains.DiscriminatedUnion;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.PatternAnalysis.Tests.Domains.DiscriminatedUnion;

public class DUDomainTests
{
    const string ShapeSource = @"
namespace Houtamelo.Spire
{
    [System.AttributeUsage(System.AttributeTargets.Struct | System.AttributeTargets.Class)]
    internal sealed class DiscriminatedUnionAttribute : System.Attribute { }
}

[Houtamelo.Spire.DiscriminatedUnion]
public partial struct Shape
{
    public enum Kind : byte { Circle, Rectangle }
    public Kind kind { get; }
    public double radius { get; }
    public double width { get; }
    public double height { get; }
}
";

    static readonly CSharpCompilation Compilation = CreateCompilation();
    static readonly INamedTypeSymbol ShapeType = Compilation.GetTypeByMetadataName("Shape")!;
    static readonly INamedTypeSymbol KindEnumType = (INamedTypeSymbol)ShapeType.GetMembers("Kind").First(m => m is INamedTypeSymbol);
    static readonly IPropertySymbol KindProperty = (IPropertySymbol)ShapeType.GetMembers("kind").First(m => m is IPropertySymbol);
    static readonly ITypeSymbol DoubleType = Compilation.GetSpecialType(SpecialType.System_Double);

    static readonly ImmutableArray<string> VariantNames = ImmutableArray.Create("Circle", "Rectangle");

    static CSharpCompilation CreateCompilation()
    {
        var tree = CSharpSyntaxTree.ParseText(ShapeSource);
        return CSharpCompilation.Create("Test", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    static EnumDomain KindUniverse() => EnumDomain.Universe(KindEnumType);

    static EnumDomain KindSingleton(string name)
    {
        var allMembers = KindEnumType.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.HasConstantValue)
            .ToImmutableHashSet<IFieldSymbol>(SymbolEqualityComparer.Default);

        var member = allMembers.First(m => m.Name == name);
        var singleton = ImmutableHashSet.Create<IFieldSymbol>(SymbolEqualityComparer.Default, member);
        return new EnumDomain(KindEnumType, singleton, allMembers);
    }

    // ─── DUTupleDomain: Construction + IsUniverse ──────────────────

    [Fact]
    public void DUTupleDomain_with_all_variants_is_universe()
    {
        var kindDomain = KindUniverse();
        var fieldDomain = BoolDomain.Universe(DoubleType); // placeholder for variant field

        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(0, KindEnumType), (IValueDomain)kindDomain),
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(1, DoubleType), (IValueDomain)fieldDomain));

        var domain = new DUTupleDomain(ShapeType, KindEnumType, VariantNames, slots, hasWildcard: false);

        Assert.True(domain.IsUniverse);
        Assert.False(domain.IsEmpty);
    }

    // ─── DUTupleDomain: Split partitions on Kind ───────────────────

    [Fact]
    public void DUTupleDomain_split_returns_one_domain_per_variant()
    {
        var kindDomain = KindUniverse();
        var fieldDomain = BoolDomain.Universe(DoubleType);

        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(0, KindEnumType), (IValueDomain)kindDomain),
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(1, DoubleType), (IValueDomain)fieldDomain));

        var domain = new DUTupleDomain(ShapeType, KindEnumType, VariantNames, slots, hasWildcard: false);

        // DU split always partitions on Kind (element[0]), even when all slots are universe
        var parts = domain.Split();
        Assert.Equal(2, parts.Length);
        Assert.All(parts, p => Assert.IsType<DUTupleDomain>(p));
    }

    [Fact]
    public void DUTupleDomain_split_partitions_have_singleton_kind()
    {
        var kindDomain = KindUniverse();
        var fieldDomain = BoolDomain.Universe(DoubleType);

        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(0, KindEnumType), (IValueDomain)kindDomain),
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(1, DoubleType), (IValueDomain)fieldDomain));

        var domain = new DUTupleDomain(ShapeType, KindEnumType, VariantNames, slots, hasWildcard: false);

        var parts = domain.Split();
        // Each partition's kind slot is a singleton, so it is not universe and not empty
        foreach (var part in parts)
        {
            Assert.False(part.IsUniverse);
            Assert.False(part.IsEmpty);
        }
    }

    // ─── DUTupleDomain: Subtract Circle leaves Rectangle ───────────

    [Fact]
    public void DUTupleDomain_subtract_circle_from_kind_leaves_rectangle()
    {
        var kindAfterSubtract = (EnumDomain)KindUniverse().Subtract(KindSingleton("Circle"));
        var fieldDomain = BoolDomain.Universe(DoubleType);

        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(0, KindEnumType), (IValueDomain)kindAfterSubtract),
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(1, DoubleType), (IValueDomain)fieldDomain));

        var domain = new DUTupleDomain(ShapeType, KindEnumType, VariantNames, slots, hasWildcard: false);

        // Kind has only Rectangle left, so split produces 1 partition
        var parts = domain.Split();
        Assert.Single(parts);
    }

    // ─── DUTupleDomain: IsEmpty after removing all variants ────────

    [Fact]
    public void DUTupleDomain_is_empty_when_kind_exhausted()
    {
        var emptyKind = (EnumDomain)KindUniverse().Subtract(KindUniverse());
        var fieldDomain = BoolDomain.Universe(DoubleType);

        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(0, KindEnumType), (IValueDomain)emptyKind),
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(1, DoubleType), (IValueDomain)fieldDomain));

        var domain = new DUTupleDomain(ShapeType, KindEnumType, VariantNames, slots, hasWildcard: false);

        Assert.True(domain.IsEmpty);
    }

    // ─── DUPropertyPatternDomain: Construction + IsUniverse ────────

    [Fact]
    public void DUPropertyPatternDomain_with_all_variants_is_universe()
    {
        var kindDomain = KindUniverse();
        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.PropertySlot(KindProperty), (IValueDomain)kindDomain));

        var domain = new DUPropertyPatternDomain(ShapeType, KindProperty, KindEnumType, VariantNames, slots, hasWildcard: false);

        Assert.True(domain.IsUniverse);
        Assert.False(domain.IsEmpty);
    }

    // ─── DUPropertyPatternDomain: Split partitions on kind ─────────

    [Fact]
    public void DUPropertyPatternDomain_split_returns_one_domain_per_variant()
    {
        var kindDomain = KindUniverse();
        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.PropertySlot(KindProperty), (IValueDomain)kindDomain));

        var domain = new DUPropertyPatternDomain(ShapeType, KindProperty, KindEnumType, VariantNames, slots, hasWildcard: false);

        var parts = domain.Split();
        Assert.Equal(2, parts.Length);
        Assert.All(parts, p => Assert.IsType<DUPropertyPatternDomain>(p));
    }

    // ─── DUPropertyPatternDomain: Subtract variant ─────────────────

    [Fact]
    public void DUPropertyPatternDomain_subtract_circle_kind_leaves_rectangle()
    {
        var kindAfterSubtract = (EnumDomain)KindUniverse().Subtract(KindSingleton("Circle"));
        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.PropertySlot(KindProperty), (IValueDomain)kindAfterSubtract));

        var domain = new DUPropertyPatternDomain(ShapeType, KindProperty, KindEnumType, VariantNames, slots, hasWildcard: false);

        var parts = domain.Split();
        Assert.Single(parts);
    }

    // ─── DUPropertyPatternDomain: IsEmpty ──────────────────────────

    [Fact]
    public void DUPropertyPatternDomain_empty_kind_is_empty()
    {
        var emptyKind = (EnumDomain)KindUniverse().Subtract(KindUniverse());
        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.PropertySlot(KindProperty), (IValueDomain)emptyKind));

        var domain = new DUPropertyPatternDomain(ShapeType, KindProperty, KindEnumType, VariantNames, slots, hasWildcard: false);

        Assert.True(domain.IsEmpty);
    }
}
