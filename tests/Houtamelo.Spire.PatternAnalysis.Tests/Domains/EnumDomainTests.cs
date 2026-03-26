using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.PatternAnalysis.Domains;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.PatternAnalysis.Tests.Domains;

public class EnumDomainTests
{
    static INamedTypeSymbol GetEnumType()
    {
        var tree = CSharpSyntaxTree.ParseText("public enum Color { Red, Green, Blue }");
        var compilation = CSharpCompilation.Create("Test", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation.GetTypeByMetadataName("Color")!;
    }

    static readonly INamedTypeSymbol ColorEnum = GetEnumType();

    static ImmutableHashSet<IFieldSymbol> GetMembers(params string[] names)
    {
        var allFields = ColorEnum.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.HasConstantValue);

        return allFields
            .Where(f => names.Contains(f.Name))
            .ToImmutableHashSet<IFieldSymbol>(SymbolEqualityComparer.Default);
    }

    static ImmutableHashSet<IFieldSymbol> AllMembers =>
        ColorEnum.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.HasConstantValue)
            .ToImmutableHashSet<IFieldSymbol>(SymbolEqualityComparer.Default);

    // ─── Universe ────────────────────────────────────────────────────

    [Fact]
    public void Universe_contains_all_three_members()
    {
        var domain = EnumDomain.Universe(ColorEnum);
        Assert.True(domain.IsUniverse);
        Assert.False(domain.IsEmpty);
        Assert.Equal(3, domain.Split().Length);
    }

    // ─── Subtract ────────────────────────────────────────────────────

    [Fact]
    public void Subtract_red_leaves_green_and_blue()
    {
        var universe = EnumDomain.Universe(ColorEnum);
        var redOnly = new EnumDomain(ColorEnum, GetMembers("Red"), AllMembers);
        var result = (EnumDomain)universe.Subtract(redOnly);

        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);
        Assert.Equal(2, result.Split().Length);
    }

    [Fact]
    public void Subtract_all_leaves_empty()
    {
        var universe = EnumDomain.Universe(ColorEnum);
        var result = universe.Subtract(universe);
        Assert.True(result.IsEmpty);
    }

    // ─── Complement ──────────────────────────────────────────────────

    [Fact]
    public void Complement_of_red_is_green_blue()
    {
        var redOnly = new EnumDomain(ColorEnum, GetMembers("Red"), AllMembers);
        var result = (EnumDomain)redOnly.Complement();

        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);
        Assert.Equal(2, result.Split().Length);
    }

    [Fact]
    public void Complement_of_universe_is_empty()
    {
        var universe = EnumDomain.Universe(ColorEnum);
        var result = universe.Complement();
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Complement_of_empty_is_universe()
    {
        var empty = new EnumDomain(ColorEnum, ImmutableHashSet<IFieldSymbol>.Empty, AllMembers);
        var result = empty.Complement();
        Assert.True(result.IsUniverse);
    }

    // ─── Split ───────────────────────────────────────────────────────

    [Fact]
    public void Split_universe_returns_three_singletons()
    {
        var universe = EnumDomain.Universe(ColorEnum);
        var parts = universe.Split();
        Assert.Equal(3, parts.Length);
        foreach (var part in parts)
        {
            Assert.False(part.IsEmpty);
            Assert.False(part.IsUniverse);
            // Each singleton should split to just itself
            Assert.Single(part.Split());
        }
    }

    [Fact]
    public void Split_singleton_returns_itself()
    {
        var redOnly = new EnumDomain(ColorEnum, GetMembers("Red"), AllMembers);
        var parts = redOnly.Split();
        Assert.Single(parts);
    }

    [Fact]
    public void Split_empty_returns_empty_array()
    {
        var empty = new EnumDomain(ColorEnum, ImmutableHashSet<IFieldSymbol>.Empty, AllMembers);
        var parts = empty.Split();
        Assert.True(parts.IsEmpty);
    }

    // ─── IsEmpty ─────────────────────────────────────────────────────

    [Fact]
    public void IsEmpty_when_all_subtracted()
    {
        var universe = EnumDomain.Universe(ColorEnum);
        var result = universe.Subtract(universe);
        Assert.True(result.IsEmpty);
    }

    // ─── Intersect ───────────────────────────────────────────────────

    [Fact]
    public void Intersect_red_green_with_green_blue_is_green()
    {
        var redGreen = new EnumDomain(ColorEnum, GetMembers("Red", "Green"), AllMembers);
        var greenBlue = new EnumDomain(ColorEnum, GetMembers("Green", "Blue"), AllMembers);
        var result = (EnumDomain)redGreen.Intersect(greenBlue);

        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);
        Assert.Single(result.Split());
    }

    [Fact]
    public void Intersect_disjoint_is_empty()
    {
        var redOnly = new EnumDomain(ColorEnum, GetMembers("Red"), AllMembers);
        var blueOnly = new EnumDomain(ColorEnum, GetMembers("Blue"), AllMembers);
        var result = redOnly.Intersect(blueOnly);
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Intersect_with_universe_returns_same()
    {
        var redGreen = new EnumDomain(ColorEnum, GetMembers("Red", "Green"), AllMembers);
        var universe = EnumDomain.Universe(ColorEnum);
        var result = (EnumDomain)redGreen.Intersect(universe);
        Assert.Equal(2, result.Split().Length);
    }

    // ─── Type property ───────────────────────────────────────────────

    [Fact]
    public void Type_returns_enum_type()
    {
        var domain = EnumDomain.Universe(ColorEnum);
        Assert.Equal(ColorEnum, domain.Type);
    }

    // ─── Double complement roundtrip ─────────────────────────────────

    [Fact]
    public void Double_complement_returns_original()
    {
        var redOnly = new EnumDomain(ColorEnum, GetMembers("Red"), AllMembers);
        var result = (EnumDomain)redOnly.Complement().Complement();
        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);
        Assert.Single(result.Split());
    }
}
