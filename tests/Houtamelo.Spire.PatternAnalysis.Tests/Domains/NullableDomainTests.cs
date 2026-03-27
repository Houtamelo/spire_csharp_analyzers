using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.PatternAnalysis.Domains;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.PatternAnalysis.Tests.Domains;

public class NullableDomainTests
{
    static (ITypeSymbol nullableBoolType, ITypeSymbol boolType, BoolDomain boolUniverse) GetBoolSetup()
    {
        var compilation = CSharpCompilation.Create("Test",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var boolType = compilation.GetSpecialType(SpecialType.System_Boolean);
        var nullableBoolType = compilation.GetSpecialType(SpecialType.System_Nullable_T)
            .Construct(boolType);
        return (nullableBoolType, boolType, BoolDomain.Universe(boolType));
    }

    static (ITypeSymbol nullableEnumType, INamedTypeSymbol enumType) GetEnumSetup()
    {
        var tree = CSharpSyntaxTree.ParseText("public enum Color { Red, Green, Blue }");
        var compilation = CSharpCompilation.Create("Test", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var enumType = compilation.GetTypeByMetadataName("Color")!;
        var nullableEnumType = compilation.GetSpecialType(SpecialType.System_Nullable_T)
            .Construct(enumType);
        return (nullableEnumType, enumType);
    }

    // ─── Universe ────────────────────────────────────────────────────

    [Fact]
    public void Universe_is_universe()
    {
        var (nullableBoolType, _, boolUniverse) = GetBoolSetup();
        var domain = NullableDomain.Universe(nullableBoolType, boolUniverse);

        Assert.True(domain.IsUniverse);
        Assert.False(domain.IsEmpty);
    }

    // ─── Subtract ────────────────────────────────────────────────────

    [Fact]
    public void Subtract_null_leaves_inner_universe()
    {
        var (nullableBoolType, boolType, boolUniverse) = GetBoolSetup();
        var universe = NullableDomain.Universe(nullableBoolType, boolUniverse);

        // Subtract a null-only domain (empty inner, hasNull=true)
        var nullOnly = new NullableDomain(nullableBoolType,
            new BoolDomain(boolType, hasTrue: false, hasFalse: false), hasNull: true);
        var result = (NullableDomain)universe.Subtract(nullOnly);

        Assert.False(result.IsUniverse);
        Assert.False(result.IsEmpty);

        // Should behave like {true, false} — Split should give 2 partitions
        var parts = result.Split();
        Assert.Equal(2, parts.Length);
    }

    [Fact]
    public void Subtract_true_leaves_null_and_false()
    {
        var (nullableBoolType, boolType, boolUniverse) = GetBoolSetup();
        var universe = NullableDomain.Universe(nullableBoolType, boolUniverse);

        // Subtract {true} (no null, inner has true only)
        var trueOnly = new NullableDomain(nullableBoolType,
            new BoolDomain(boolType, hasTrue: true, hasFalse: false), hasNull: false);
        var result = (NullableDomain)universe.Subtract(trueOnly);

        Assert.False(result.IsUniverse);
        Assert.False(result.IsEmpty);

        // Should be {null, false} — Split should give 2 partitions
        var parts = result.Split();
        Assert.Equal(2, parts.Length);
    }

    [Fact]
    public void Subtract_all_leaves_empty()
    {
        var (nullableBoolType, _, boolUniverse) = GetBoolSetup();
        var universe = NullableDomain.Universe(nullableBoolType, boolUniverse);

        var result = universe.Subtract(universe);
        Assert.True(result.IsEmpty);
    }

    // ─── Split ───────────────────────────────────────────────────────

    [Fact]
    public void Split_universe_returns_three_partitions()
    {
        var (nullableBoolType, _, boolUniverse) = GetBoolSetup();
        var domain = NullableDomain.Universe(nullableBoolType, boolUniverse);

        // bool? universe = {null, true, false} => 3 partitions
        var parts = domain.Split();
        Assert.Equal(3, parts.Length);

        // Each partition should be non-empty and non-universe
        foreach (var part in parts)
        {
            Assert.False(part.IsEmpty);
            Assert.False(part.IsUniverse);
        }
    }

    [Fact]
    public void Split_without_null_returns_inner_partitions()
    {
        var (nullableBoolType, boolType, _) = GetBoolSetup();

        // {true, false} without null
        var domain = new NullableDomain(nullableBoolType,
            new BoolDomain(boolType, hasTrue: true, hasFalse: true), hasNull: false);

        var parts = domain.Split();
        Assert.Equal(2, parts.Length);
    }

    [Fact]
    public void Split_null_only_returns_single_partition()
    {
        var (nullableBoolType, boolType, _) = GetBoolSetup();

        var nullOnly = new NullableDomain(nullableBoolType,
            new BoolDomain(boolType, hasTrue: false, hasFalse: false), hasNull: true);

        var parts = nullOnly.Split();
        Assert.Single(parts);
    }

    [Fact]
    public void Split_empty_returns_empty_array()
    {
        var (nullableBoolType, boolType, _) = GetBoolSetup();

        var empty = new NullableDomain(nullableBoolType,
            new BoolDomain(boolType, hasTrue: false, hasFalse: false), hasNull: false);

        var parts = empty.Split();
        Assert.True(parts.IsEmpty);
    }

    // ─── Complement ──────────────────────────────────────────────────

    [Fact]
    public void Complement_of_null_only_is_true_false()
    {
        var (nullableBoolType, boolType, _) = GetBoolSetup();

        var nullOnly = new NullableDomain(nullableBoolType,
            new BoolDomain(boolType, hasTrue: false, hasFalse: false), hasNull: true);

        var result = (NullableDomain)nullOnly.Complement();

        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);

        // Complement of {null} in bool? universe = {true, false}
        var parts = result.Split();
        Assert.Equal(2, parts.Length);
    }

    [Fact]
    public void Complement_of_true_is_null_and_false()
    {
        var (nullableBoolType, boolType, _) = GetBoolSetup();

        // {true} without null
        var trueOnly = new NullableDomain(nullableBoolType,
            new BoolDomain(boolType, hasTrue: true, hasFalse: false), hasNull: false);

        var result = (NullableDomain)trueOnly.Complement();

        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);

        // Complement of {true} in bool? universe = {null, false}
        var parts = result.Split();
        Assert.Equal(2, parts.Length);
    }

    [Fact]
    public void Complement_of_universe_is_empty()
    {
        var (nullableBoolType, _, boolUniverse) = GetBoolSetup();
        var universe = NullableDomain.Universe(nullableBoolType, boolUniverse);

        var result = universe.Complement();
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Complement_of_empty_is_universe()
    {
        var (nullableBoolType, boolType, _) = GetBoolSetup();
        var empty = new NullableDomain(nullableBoolType,
            new BoolDomain(boolType, hasTrue: false, hasFalse: false), hasNull: false);

        var result = empty.Complement();
        Assert.True(result.IsUniverse);
    }

    // ─── Intersect ───────────────────────────────────────────────────

    [Fact]
    public void Intersect_null_true_with_null_false_is_null_only()
    {
        var (nullableBoolType, boolType, _) = GetBoolSetup();

        var nullTrue = new NullableDomain(nullableBoolType,
            new BoolDomain(boolType, hasTrue: true, hasFalse: false), hasNull: true);
        var nullFalse = new NullableDomain(nullableBoolType,
            new BoolDomain(boolType, hasTrue: false, hasFalse: true), hasNull: true);

        var result = (NullableDomain)nullTrue.Intersect(nullFalse);

        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);

        // Intersection: null is common, true and false are disjoint => {null}
        var parts = result.Split();
        Assert.Single(parts);
    }

    [Fact]
    public void Intersect_with_universe_returns_same()
    {
        var (nullableBoolType, boolType, boolUniverse) = GetBoolSetup();
        var universe = NullableDomain.Universe(nullableBoolType, boolUniverse);

        var nullTrue = new NullableDomain(nullableBoolType,
            new BoolDomain(boolType, hasTrue: true, hasFalse: false), hasNull: true);

        var result = (NullableDomain)nullTrue.Intersect(universe);

        // {null, true} intersect {null, true, false} = {null, true}
        var parts = result.Split();
        Assert.Equal(2, parts.Length);
    }

    // ─── IsEmpty ─────────────────────────────────────────────────────

    [Fact]
    public void IsEmpty_when_all_subtracted()
    {
        var (nullableBoolType, _, boolUniverse) = GetBoolSetup();
        var universe = NullableDomain.Universe(nullableBoolType, boolUniverse);

        var result = universe.Subtract(universe);
        Assert.True(result.IsEmpty);
        Assert.Empty(result.Split());
    }

    // ─── Type property ───────────────────────────────────────────────

    [Fact]
    public void Type_returns_nullable_type()
    {
        var (nullableBoolType, _, boolUniverse) = GetBoolSetup();
        var domain = NullableDomain.Universe(nullableBoolType, boolUniverse);
        Assert.Equal(nullableBoolType, domain.Type);
    }

    // ─── Double complement roundtrip ─────────────────────────────────

    [Fact]
    public void Double_complement_returns_original()
    {
        var (nullableBoolType, boolType, _) = GetBoolSetup();

        var nullTrue = new NullableDomain(nullableBoolType,
            new BoolDomain(boolType, hasTrue: true, hasFalse: false), hasNull: true);

        var result = (NullableDomain)nullTrue.Complement().Complement();

        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);
        Assert.Equal(2, result.Split().Length);
    }

    // ─── Subtract/Intersect with bare inner domain ─────────────────

    [Fact]
    public void Subtract_bare_inner_subtracts_from_inner_keeps_null()
    {
        var (nullableBoolType, boolType, boolUniverse) = GetBoolSetup();
        var universe = NullableDomain.Universe(nullableBoolType, boolUniverse);

        // Subtract a bare BoolDomain {true} — not wrapped in NullableDomain
        var bareTrue = new BoolDomain(boolType, hasTrue: true, hasFalse: false);
        var result = (NullableDomain)universe.Subtract(bareTrue);

        // null is preserved, only {true} removed from inner => {null, false}
        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);
        var parts = result.Split();
        Assert.Equal(2, parts.Length);
    }

    [Fact]
    public void Intersect_bare_inner_drops_null_intersects_inner()
    {
        var (nullableBoolType, boolType, boolUniverse) = GetBoolSetup();
        var universe = NullableDomain.Universe(nullableBoolType, boolUniverse);

        // Intersect with bare BoolDomain {true} — not wrapped in NullableDomain
        var bareTrue = new BoolDomain(boolType, hasTrue: true, hasFalse: false);
        var result = (NullableDomain)universe.Intersect(bareTrue);

        // No null in bare domain => null dropped, inner intersected => {true}
        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);
        var parts = result.Split();
        Assert.Single(parts);
    }

    // ─── Works with EnumDomain ───────────────────────────────────────

    [Fact]
    public void Works_with_enum_domain_as_inner()
    {
        var (nullableEnumType, enumType) = GetEnumSetup();
        var enumUniverse = EnumDomain.Universe(enumType);

        var domain = NullableDomain.Universe(nullableEnumType, enumUniverse);

        Assert.True(domain.IsUniverse);
        Assert.False(domain.IsEmpty);

        // Color? universe = {null, Red, Green, Blue} => 4 partitions
        var parts = domain.Split();
        Assert.Equal(4, parts.Length);
    }

    [Fact]
    public void Subtract_null_from_nullable_enum_leaves_three()
    {
        var (nullableEnumType, enumType) = GetEnumSetup();
        var enumUniverse = EnumDomain.Universe(enumType);
        var universe = NullableDomain.Universe(nullableEnumType, enumUniverse);

        var emptyEnum = new EnumDomain(enumType,
            ImmutableHashSet<IFieldSymbol>.Empty,
            enumType.GetMembers().OfType<IFieldSymbol>()
                .Where(f => f.HasConstantValue)
                .ToImmutableHashSet<IFieldSymbol>(SymbolEqualityComparer.Default));
        var nullOnly = new NullableDomain(nullableEnumType, emptyEnum, hasNull: true);

        var result = (NullableDomain)universe.Subtract(nullOnly);

        // {Red, Green, Blue} without null => 3 partitions
        var parts = result.Split();
        Assert.Equal(3, parts.Length);
    }

    [Fact]
    public void Subtract_all_from_nullable_enum_is_empty()
    {
        var (nullableEnumType, enumType) = GetEnumSetup();
        var enumUniverse = EnumDomain.Universe(enumType);
        var universe = NullableDomain.Universe(nullableEnumType, enumUniverse);

        var result = universe.Subtract(universe);
        Assert.True(result.IsEmpty);
    }
}
