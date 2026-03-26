using System.Collections.Immutable;
using Houtamelo.Spire.PatternAnalysis.Domains;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.PatternAnalysis.Tests.Domains;

public class BoolDomainTests
{
    static ITypeSymbol GetBoolType()
    {
        var compilation = CSharpCompilation.Create("Test",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation.GetSpecialType(SpecialType.System_Boolean);
    }

    static readonly ITypeSymbol BoolType = GetBoolType();

    // ─── Universe ────────────────────────────────────────────────────

    [Fact]
    public void Universe_has_both_values()
    {
        var domain = BoolDomain.Universe(BoolType);
        Assert.True(domain.IsUniverse);
        Assert.False(domain.IsEmpty);
    }

    // ─── Subtract ────────────────────────────────────────────────────

    [Fact]
    public void Subtract_true_leaves_false()
    {
        var universe = BoolDomain.Universe(BoolType);
        var trueOnly = new BoolDomain(BoolType, hasTrue: true, hasFalse: false);
        var result = (BoolDomain)universe.Subtract(trueOnly);

        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);

        // Split should give us exactly one domain containing {false}
        var parts = result.Split();
        Assert.Single(parts);
    }

    [Fact]
    public void Subtract_both_leaves_empty()
    {
        var universe = BoolDomain.Universe(BoolType);
        var result = universe.Subtract(universe);
        Assert.True(result.IsEmpty);
    }

    // ─── Complement ──────────────────────────────────────────────────

    [Fact]
    public void Complement_of_true_is_false()
    {
        var trueOnly = new BoolDomain(BoolType, hasTrue: true, hasFalse: false);
        var result = (BoolDomain)trueOnly.Complement();

        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);

        // The complement of {true} is {false}
        var parts = result.Split();
        Assert.Single(parts);
    }

    [Fact]
    public void Complement_of_universe_is_empty()
    {
        var universe = BoolDomain.Universe(BoolType);
        var result = universe.Complement();
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Complement_of_empty_is_universe()
    {
        var empty = new BoolDomain(BoolType, hasTrue: false, hasFalse: false);
        var result = empty.Complement();
        Assert.True(result.IsUniverse);
    }

    // ─── Split ───────────────────────────────────────────────────────

    [Fact]
    public void Split_universe_returns_two_singletons()
    {
        var universe = BoolDomain.Universe(BoolType);
        var parts = universe.Split();
        Assert.Equal(2, parts.Length);
        // Each partition should be non-empty and non-universe
        foreach (var part in parts)
        {
            Assert.False(part.IsEmpty);
            Assert.False(part.IsUniverse);
        }
    }

    [Fact]
    public void Split_singleton_returns_itself()
    {
        var trueOnly = new BoolDomain(BoolType, hasTrue: true, hasFalse: false);
        var parts = trueOnly.Split();
        Assert.Single(parts);
        Assert.False(parts[0].IsEmpty);
    }

    [Fact]
    public void Split_empty_returns_empty_array()
    {
        var empty = new BoolDomain(BoolType, hasTrue: false, hasFalse: false);
        var parts = empty.Split();
        Assert.True(parts.IsEmpty);
    }

    // ─── IsEmpty ─────────────────────────────────────────────────────

    [Fact]
    public void IsEmpty_after_subtracting_all()
    {
        var universe = BoolDomain.Universe(BoolType);
        var result = universe.Subtract(universe);
        Assert.True(result.IsEmpty);
    }

    // ─── Intersect ───────────────────────────────────────────────────

    [Fact]
    public void Intersect_true_and_false_is_empty()
    {
        var trueOnly = new BoolDomain(BoolType, hasTrue: true, hasFalse: false);
        var falseOnly = new BoolDomain(BoolType, hasTrue: false, hasFalse: true);
        var result = trueOnly.Intersect(falseOnly);
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Intersect_universe_and_singleton_is_singleton()
    {
        var universe = BoolDomain.Universe(BoolType);
        var trueOnly = new BoolDomain(BoolType, hasTrue: true, hasFalse: false);
        var result = (BoolDomain)universe.Intersect(trueOnly);

        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);
        Assert.Single(result.Split());
    }

    [Fact]
    public void Intersect_same_returns_same()
    {
        var trueOnly = new BoolDomain(BoolType, hasTrue: true, hasFalse: false);
        var result = (BoolDomain)trueOnly.Intersect(trueOnly);
        Assert.False(result.IsEmpty);
        Assert.Single(result.Split());
    }

    // ─── Type property ───────────────────────────────────────────────

    [Fact]
    public void Type_returns_bool_type()
    {
        var domain = BoolDomain.Universe(BoolType);
        Assert.Equal(BoolType, domain.Type);
    }

    // ─── Double complement roundtrip ─────────────────────────────────

    [Fact]
    public void Double_complement_returns_original()
    {
        var trueOnly = new BoolDomain(BoolType, hasTrue: true, hasFalse: false);
        var result = (BoolDomain)trueOnly.Complement().Complement();
        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);
        Assert.Single(result.Split());
    }
}
