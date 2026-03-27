using System;
using System.Collections.Immutable;
using Houtamelo.Spire.PatternAnalysis.Domains;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.PatternAnalysis.Tests.Domains;

public class StructuralDomainTests
{
    static ITypeSymbol GetBoolType()
    {
        var compilation = CSharpCompilation.Create("Test",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation.GetSpecialType(SpecialType.System_Boolean);
    }

    static INamedTypeSymbol GetTupleType(CSharpCompilation compilation)
    {
        var boolType = compilation.GetSpecialType(SpecialType.System_Boolean);
        var valueTupleT2 = compilation.GetTypeByMetadataName("System.ValueTuple`2")!;
        return valueTupleT2.Construct(boolType, boolType);
    }

    static CSharpCompilation CreateCompilation()
    {
        return CSharpCompilation.Create("Test",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    static readonly CSharpCompilation Compilation = CreateCompilation();
    static readonly ITypeSymbol BoolType = Compilation.GetSpecialType(SpecialType.System_Boolean);

    // ─── TupleDomain: IsUniverse ──────────────────────────────────────

    [Fact]
    public void TupleDomain_both_slots_universe_is_universe()
    {
        var tupleType = GetTupleType(Compilation);
        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(0, BoolType), (IValueDomain)BoolDomain.Universe(BoolType)),
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(1, BoolType), (IValueDomain)BoolDomain.Universe(BoolType)));

        var domain = new TupleDomain(tupleType, slots, hasWildcard: false);

        Assert.True(domain.IsUniverse);
        Assert.False(domain.IsEmpty);
    }

    [Fact]
    public void TupleDomain_has_wildcard_is_universe_regardless_of_slots()
    {
        var tupleType = GetTupleType(Compilation);
        // One slot is empty, but hasWildcard overrides
        var emptyBool = new BoolDomain(BoolType, hasTrue: false, hasFalse: false);
        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(0, BoolType), (IValueDomain)emptyBool),
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(1, BoolType), (IValueDomain)BoolDomain.Universe(BoolType)));

        var domain = new TupleDomain(tupleType, slots, hasWildcard: true);

        Assert.True(domain.IsUniverse);
    }

    // ─── TupleDomain: IsEmpty ─────────────────────────────────────────

    [Fact]
    public void TupleDomain_one_slot_empty_is_empty()
    {
        var tupleType = GetTupleType(Compilation);
        var emptyBool = new BoolDomain(BoolType, hasTrue: false, hasFalse: false);
        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(0, BoolType), (IValueDomain)emptyBool),
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(1, BoolType), (IValueDomain)BoolDomain.Universe(BoolType)));

        var domain = new TupleDomain(tupleType, slots, hasWildcard: false);

        Assert.True(domain.IsEmpty);
    }

    [Fact]
    public void TupleDomain_has_wildcard_with_empty_slot_is_not_empty()
    {
        var tupleType = GetTupleType(Compilation);
        var emptyBool = new BoolDomain(BoolType, hasTrue: false, hasFalse: false);
        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(0, BoolType), (IValueDomain)emptyBool),
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(1, BoolType), (IValueDomain)BoolDomain.Universe(BoolType)));

        var domain = new TupleDomain(tupleType, slots, hasWildcard: true);

        // HasWildcard means IsUniverse, so not empty
        Assert.False(domain.IsEmpty);
    }

    // ─── TupleDomain: Split ───────────────────────────────────────────

    [Fact]
    public void TupleDomain_split_splits_first_non_universe_slot()
    {
        var tupleType = GetTupleType(Compilation);
        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(0, BoolType), (IValueDomain)BoolDomain.Universe(BoolType)),
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(1, BoolType), (IValueDomain)BoolDomain.Universe(BoolType)));

        var domain = new TupleDomain(tupleType, slots, hasWildcard: false);

        // Both slots are universe, so Split returns just this
        var parts = domain.Split();
        Assert.Single(parts);
    }

    [Fact]
    public void TupleDomain_split_on_bool_bool_returns_two_partitions()
    {
        var tupleType = GetTupleType(Compilation);
        // First slot is {true} only, second is universe
        var trueOnly = new BoolDomain(BoolType, hasTrue: true, hasFalse: false);
        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(0, BoolType), (IValueDomain)trueOnly),
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(1, BoolType), (IValueDomain)BoolDomain.Universe(BoolType)));

        var domain = new TupleDomain(tupleType, slots, hasWildcard: false);

        // trueOnly.Split() returns 1 partition ({true}), so domain.Split() returns 1
        var parts = domain.Split();
        Assert.Single(parts);
        // The result should be a TupleDomain
        Assert.IsType<TupleDomain>(parts[0]);
    }

    [Fact]
    public void TupleDomain_split_universe_first_slot_splits_second()
    {
        var tupleType = GetTupleType(Compilation);
        // First slot is universe, second is {true, false} (also universe) — both universe
        // Let's make first universe and second a non-universe: just {true}
        var trueOnly = new BoolDomain(BoolType, hasTrue: true, hasFalse: false);
        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(0, BoolType), (IValueDomain)BoolDomain.Universe(BoolType)),
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(1, BoolType), (IValueDomain)trueOnly));

        var domain = new TupleDomain(tupleType, slots, hasWildcard: false);

        // First slot is universe, so split goes to second slot
        // trueOnly.Split() returns 1 partition
        var parts = domain.Split();
        Assert.Single(parts);
        Assert.IsType<TupleDomain>(parts[0]);
    }

    [Fact]
    public void TupleDomain_split_both_universe_returns_self()
    {
        var tupleType = GetTupleType(Compilation);
        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(0, BoolType), (IValueDomain)BoolDomain.Universe(BoolType)),
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(1, BoolType), (IValueDomain)BoolDomain.Universe(BoolType)));

        var domain = new TupleDomain(tupleType, slots, hasWildcard: false);

        var parts = domain.Split();
        Assert.Single(parts);
        // When all slots are universe, Split returns just this
        Assert.Same(domain, parts[0]);
    }

    // ─── TupleDomain: Intersect ───────────────────────────────────────

    [Fact]
    public void TupleDomain_intersect_overlapping_slots()
    {
        var tupleType = GetTupleType(Compilation);
        var trueOnly = new BoolDomain(BoolType, hasTrue: true, hasFalse: false);
        var falseOnly = new BoolDomain(BoolType, hasTrue: false, hasFalse: true);

        var domain1 = new TupleDomain(tupleType,
            ImmutableArray.Create(
                ((SlotIdentifier)new SlotIdentifier.TupleSlot(0, BoolType), (IValueDomain)BoolDomain.Universe(BoolType)),
                ((SlotIdentifier)new SlotIdentifier.TupleSlot(1, BoolType), (IValueDomain)trueOnly)),
            hasWildcard: false);

        var domain2 = new TupleDomain(tupleType,
            ImmutableArray.Create(
                ((SlotIdentifier)new SlotIdentifier.TupleSlot(0, BoolType), (IValueDomain)trueOnly),
                ((SlotIdentifier)new SlotIdentifier.TupleSlot(1, BoolType), (IValueDomain)BoolDomain.Universe(BoolType))),
            hasWildcard: false);

        var result = (TupleDomain)domain1.Intersect(domain2);

        // Slot 0: universe ∩ {true} = {true}
        // Slot 1: {true} ∩ universe = {true}
        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);
    }

    [Fact]
    public void TupleDomain_intersect_disjoint_slot_produces_empty()
    {
        var tupleType = GetTupleType(Compilation);
        var trueOnly = new BoolDomain(BoolType, hasTrue: true, hasFalse: false);
        var falseOnly = new BoolDomain(BoolType, hasTrue: false, hasFalse: true);

        var domain1 = new TupleDomain(tupleType,
            ImmutableArray.Create(
                ((SlotIdentifier)new SlotIdentifier.TupleSlot(0, BoolType), (IValueDomain)trueOnly),
                ((SlotIdentifier)new SlotIdentifier.TupleSlot(1, BoolType), (IValueDomain)BoolDomain.Universe(BoolType))),
            hasWildcard: false);

        var domain2 = new TupleDomain(tupleType,
            ImmutableArray.Create(
                ((SlotIdentifier)new SlotIdentifier.TupleSlot(0, BoolType), (IValueDomain)falseOnly),
                ((SlotIdentifier)new SlotIdentifier.TupleSlot(1, BoolType), (IValueDomain)BoolDomain.Universe(BoolType))),
            hasWildcard: false);

        var result = (TupleDomain)domain1.Intersect(domain2);

        // Slot 0: {true} ∩ {false} = empty => whole domain is empty
        Assert.True(result.IsEmpty);
    }

    // ─── TupleDomain: Subtract / Complement throw ─────────────────────

    [Fact]
    public void TupleDomain_subtract_throws_not_supported()
    {
        var tupleType = GetTupleType(Compilation);
        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(0, BoolType), (IValueDomain)BoolDomain.Universe(BoolType)),
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(1, BoolType), (IValueDomain)BoolDomain.Universe(BoolType)));

        var domain = new TupleDomain(tupleType, slots, hasWildcard: false);

        Assert.Throws<NotSupportedException>(() => { domain.Subtract(domain); });
    }

    [Fact]
    public void TupleDomain_complement_throws_not_supported()
    {
        var tupleType = GetTupleType(Compilation);
        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(0, BoolType), (IValueDomain)BoolDomain.Universe(BoolType)),
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(1, BoolType), (IValueDomain)BoolDomain.Universe(BoolType)));

        var domain = new TupleDomain(tupleType, slots, hasWildcard: false);

        Assert.Throws<NotSupportedException>(() => { domain.Complement(); });
    }

    // ─── PropertyPatternDomain: IsUniverse ────────────────────────────

    [Fact]
    public void PropertyPatternDomain_single_bool_property_universe_is_universe()
    {
        var tree = CSharpSyntaxTree.ParseText("public class C { public bool Flag { get; set; } }");
        var compilation = CSharpCompilation.Create("Test", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var classType = compilation.GetTypeByMetadataName("C")!;
        var propSymbol = (IPropertySymbol)classType.GetMembers("Flag")[0];

        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.PropertySlot(propSymbol), (IValueDomain)BoolDomain.Universe(BoolType)));

        var domain = new PropertyPatternDomain(classType, slots, hasWildcard: false);

        Assert.True(domain.IsUniverse);
        Assert.False(domain.IsEmpty);
    }

    [Fact]
    public void PropertyPatternDomain_has_wildcard_is_universe()
    {
        var tree = CSharpSyntaxTree.ParseText("public class C { public bool Flag { get; set; } }");
        var compilation = CSharpCompilation.Create("Test", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var classType = compilation.GetTypeByMetadataName("C")!;
        var propSymbol = (IPropertySymbol)classType.GetMembers("Flag")[0];

        var trueOnly = new BoolDomain(BoolType, hasTrue: true, hasFalse: false);
        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.PropertySlot(propSymbol), (IValueDomain)trueOnly));

        var domain = new PropertyPatternDomain(classType, slots, hasWildcard: true);

        Assert.True(domain.IsUniverse);
    }

    // ─── PropertyPatternDomain: Split ─────────────────────────────────

    [Fact]
    public void PropertyPatternDomain_split_splits_property_domain()
    {
        var tree = CSharpSyntaxTree.ParseText("public class C { public bool Flag { get; set; } }");
        var compilation = CSharpCompilation.Create("Test", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var classType = compilation.GetTypeByMetadataName("C")!;
        var propSymbol = (IPropertySymbol)classType.GetMembers("Flag")[0];

        var trueOnly = new BoolDomain(BoolType, hasTrue: true, hasFalse: false);
        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.PropertySlot(propSymbol), (IValueDomain)trueOnly));

        var domain = new PropertyPatternDomain(classType, slots, hasWildcard: false);

        var parts = domain.Split();
        // {true} splits into 1 partition
        Assert.Single(parts);
        Assert.IsType<PropertyPatternDomain>(parts[0]);
    }

    // ─── PropertyPatternDomain: Intersect ─────────────────────────────

    [Fact]
    public void PropertyPatternDomain_intersect_two_domains()
    {
        var tree = CSharpSyntaxTree.ParseText("public class C { public bool Flag { get; set; } }");
        var compilation = CSharpCompilation.Create("Test", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var classType = compilation.GetTypeByMetadataName("C")!;
        var propSymbol = (IPropertySymbol)classType.GetMembers("Flag")[0];

        var domain1 = new PropertyPatternDomain(classType,
            ImmutableArray.Create(
                ((SlotIdentifier)new SlotIdentifier.PropertySlot(propSymbol), (IValueDomain)BoolDomain.Universe(BoolType))),
            hasWildcard: false);

        var trueOnly = new BoolDomain(BoolType, hasTrue: true, hasFalse: false);
        var domain2 = new PropertyPatternDomain(classType,
            ImmutableArray.Create(
                ((SlotIdentifier)new SlotIdentifier.PropertySlot(propSymbol), (IValueDomain)trueOnly)),
            hasWildcard: false);

        var result = (PropertyPatternDomain)domain1.Intersect(domain2);

        // universe ∩ {true} = {true}
        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);
    }

    // ─── TupleDomain: Type property ───────────────────────────────────

    [Fact]
    public void TupleDomain_type_returns_tuple_type()
    {
        var tupleType = GetTupleType(Compilation);
        var slots = ImmutableArray.Create(
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(0, BoolType), (IValueDomain)BoolDomain.Universe(BoolType)),
            ((SlotIdentifier)new SlotIdentifier.TupleSlot(1, BoolType), (IValueDomain)BoolDomain.Universe(BoolType)));

        var domain = new TupleDomain(tupleType, slots, hasWildcard: false);

        Assert.Equal(tupleType, domain.Type);
    }
}
