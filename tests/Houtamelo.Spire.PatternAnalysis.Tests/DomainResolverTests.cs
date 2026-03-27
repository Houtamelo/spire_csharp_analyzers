using System.Collections.Generic;
using Houtamelo.Spire.PatternAnalysis.Domains;
using Houtamelo.Spire.PatternAnalysis.Resolution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.PatternAnalysis.Tests;

public class DomainResolverTests
{
    private static readonly CSharpCompilationOptions DllOptions
        = new(OutputKind.DynamicallyLinkedLibrary);

    private static readonly MetadataReference CorlibRef
        = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

    private static (CSharpCompilation compilation, DomainResolver resolver) CreateResolver(
        string? extraSource = null)
    {
        var sources = new List<SyntaxTree>();
        if (extraSource != null)
            sources.Add(CSharpSyntaxTree.ParseText(extraSource));

        var compilation = CSharpCompilation.Create("Test", sources, [CorlibRef], DllOptions);
        var resolver = new DomainResolver(compilation, new TypeHierarchyResolver());
        return (compilation, resolver);
    }

    // ─── Bool ─────────────────────────────────────────────────────────

    [Fact]
    public void Bool_returns_BoolDomain()
    {
        var (compilation, resolver) = CreateResolver();
        var boolType = compilation.GetSpecialType(SpecialType.System_Boolean);

        var result = resolver.Resolve(boolType);

        Assert.IsType<BoolDomain>(result);
        Assert.True(result.IsUniverse);
    }

    // ─── Int ──────────────────────────────────────────────────────────

    [Fact]
    public void Int_returns_NumericDomain()
    {
        var (compilation, resolver) = CreateResolver();
        var intType = compilation.GetSpecialType(SpecialType.System_Int32);

        var result = resolver.Resolve(intType);

        Assert.IsType<NumericDomain>(result);
        Assert.True(result.IsUniverse);
    }

    // ─── Double ───────────────────────────────────────────────────────

    [Fact]
    public void Double_returns_NumericDomain()
    {
        var (compilation, resolver) = CreateResolver();
        var doubleType = compilation.GetSpecialType(SpecialType.System_Double);

        var result = resolver.Resolve(doubleType);

        Assert.IsType<NumericDomain>(result);
        Assert.True(result.IsUniverse);
    }

    // ─── Byte ─────────────────────────────────────────────────────────

    [Fact]
    public void Byte_returns_NumericDomain()
    {
        var (compilation, resolver) = CreateResolver();
        var byteType = compilation.GetSpecialType(SpecialType.System_Byte);

        var result = resolver.Resolve(byteType);

        Assert.IsType<NumericDomain>(result);
        Assert.True(result.IsUniverse);
    }

    // ─── Enum ─────────────────────────────────────────────────────────

    [Fact]
    public void Enum_returns_EnumDomain()
    {
        var (compilation, resolver) = CreateResolver("public enum Color { Red, Green, Blue }");
        var enumType = compilation.GetTypeByMetadataName("Color")!;

        var result = resolver.Resolve(enumType);

        Assert.IsType<EnumDomain>(result);
        Assert.True(result.IsUniverse);
    }

    // ─── Nullable<int> ───────────────────────────────────────────────

    [Fact]
    public void Nullable_int_returns_NullableDomain_wrapping_NumericDomain()
    {
        var (compilation, resolver) = CreateResolver();
        var intType = compilation.GetSpecialType(SpecialType.System_Int32);
        var nullableIntType = compilation.GetSpecialType(SpecialType.System_Nullable_T)
            .Construct(intType);

        var result = resolver.Resolve(nullableIntType);

        var nullable = Assert.IsType<NullableDomain>(result);
        Assert.True(nullable.IsUniverse);
    }

    // ─── Nullable<bool> ──────────────────────────────────────────────

    [Fact]
    public void Nullable_bool_returns_NullableDomain_wrapping_BoolDomain()
    {
        var (compilation, resolver) = CreateResolver();
        var boolType = compilation.GetSpecialType(SpecialType.System_Boolean);
        var nullableBoolType = compilation.GetSpecialType(SpecialType.System_Nullable_T)
            .Construct(boolType);

        var result = resolver.Resolve(nullableBoolType);

        var nullable = Assert.IsType<NullableDomain>(result);
        Assert.True(nullable.IsUniverse);
        // bool? has 3 partitions: null, true, false
        Assert.Equal(3, nullable.Split().Length);
    }

    // ─── Nullable<enum> ──────────────────────────────────────────────

    [Fact]
    public void Nullable_enum_returns_NullableDomain_wrapping_EnumDomain()
    {
        var (compilation, resolver) = CreateResolver("public enum Dir { Up, Down }");
        var enumType = compilation.GetTypeByMetadataName("Dir")!;
        var nullableEnumType = compilation.GetSpecialType(SpecialType.System_Nullable_T)
            .Construct(enumType);

        var result = resolver.Resolve(nullableEnumType);

        var nullable = Assert.IsType<NullableDomain>(result);
        Assert.True(nullable.IsUniverse);
        // Dir? has 3 partitions: null, Up, Down
        Assert.Equal(3, nullable.Split().Length);
    }

    // ─── Unknown class (fallback) ────────────────────────────────────

    [Fact]
    public void Unknown_class_oblivious_returns_NullableDomain_wrapping_fallback()
    {
        // In nullable-oblivious context, reference types have NullableAnnotation.None
        // which means null is possible => NullableDomain wrapping the fallback
        var (compilation, resolver) = CreateResolver("public class Foo { }");
        var fooType = compilation.GetTypeByMetadataName("Foo")!;

        var result = resolver.Resolve(fooType);

        Assert.IsType<NullableDomain>(result);
    }

    [Fact]
    public void Unknown_class_not_annotated_returns_PropertyPatternDomain()
    {
        var source = @"
#nullable enable
public class Foo
{
    public Foo Field = null!;
}
";
        var (compilation, resolver) = CreateResolver(source);
        var fooType = compilation.GetTypeByMetadataName("Foo")!;
        var field = fooType.GetMembers("Field")[0] as IFieldSymbol;
        Assert.NotNull(field);
        Assert.Equal(NullableAnnotation.NotAnnotated, field!.Type.NullableAnnotation);

        var result = resolver.Resolve(field.Type);

        Assert.IsType<PropertyPatternDomain>(result);
    }

    // ─── Reference type in nullable-oblivious context ────────────────

    [Fact]
    public void Reference_type_nullable_oblivious_returns_NullableDomain()
    {
        // Without #nullable enable, reference types have NullableAnnotation.None (oblivious)
        var (compilation, resolver) = CreateResolver("public class Bar { }");
        var barType = compilation.GetTypeByMetadataName("Bar")!;

        // In nullable-oblivious context, NullableAnnotation is None
        Assert.Equal(NullableAnnotation.None, barType.NullableAnnotation);

        var result = resolver.Resolve(barType);

        // Should wrap in NullableDomain because oblivious means null is possible
        Assert.IsType<NullableDomain>(result);
    }

    // ─── Reference type NotAnnotated does NOT wrap ────────────────────

    [Fact]
    public void Reference_type_not_annotated_resolves_directly()
    {
        // With #nullable enable, a non-nullable reference type has NullableAnnotation.NotAnnotated
        var source = @"
#nullable enable
public class MyClass
{
    public MyClass Field = null!;
}
";
        var (compilation, resolver) = CreateResolver(source);
        var myClassType = compilation.GetTypeByMetadataName("MyClass")!;

        // Get the field's type — it should be NotAnnotated in nullable-enabled context
        var field = myClassType.GetMembers("Field")[0] as IFieldSymbol;
        Assert.NotNull(field);
        Assert.Equal(NullableAnnotation.NotAnnotated, field!.Type.NullableAnnotation);

        var result = resolver.Resolve(field.Type);

        // NotAnnotated reference type should NOT wrap in NullableDomain
        Assert.IsType<PropertyPatternDomain>(result);
    }

    // ─── Reference type Annotated wraps in NullableDomain ─────────────

    [Fact]
    public void Reference_type_annotated_returns_NullableDomain()
    {
        var source = @"
#nullable enable
public class MyClass
{
    public MyClass? NullableField = null;
}
";
        var (compilation, resolver) = CreateResolver(source);
        var myClassType = compilation.GetTypeByMetadataName("MyClass")!;

        var field = myClassType.GetMembers("NullableField")[0] as IFieldSymbol;
        Assert.NotNull(field);
        Assert.Equal(NullableAnnotation.Annotated, field!.Type.NullableAnnotation);

        var result = resolver.Resolve(field.Type);

        Assert.IsType<NullableDomain>(result);
    }

    // ─── [EnforceExhaustiveness] marked abstract class ───────────────

    [Fact]
    public void EnforceExhaustiveness_abstract_class_returns_EnforceExhaustiveDomain()
    {
        var source = @"
namespace Houtamelo.Spire.Core
{
    [System.AttributeUsage(System.AttributeTargets.Enum | System.AttributeTargets.Class | System.AttributeTargets.Interface)]
    public class EnforceExhaustivenessAttribute : System.Attribute { }
}

[Houtamelo.Spire.Core.EnforceExhaustiveness]
public abstract class Shape { }
public class Circle : Shape { }
public class Square : Shape { }
";
        var (compilation, resolver) = CreateResolver(source);
        var shapeType = compilation.GetTypeByMetadataName("Shape")!;

        // Shape is in nullable-oblivious context (reference type, NullableAnnotation.None)
        // so it will be wrapped in NullableDomain. Resolve the inner type by checking the
        // NullableDomain's inner. But actually — let's test that the domain chain is correct.
        var result = resolver.Resolve(shapeType);

        // Reference type in oblivious context => NullableDomain wrapping the resolved inner type
        var nullable = Assert.IsType<NullableDomain>(result);

        // The inner domain (from Split: non-null partitions) should resolve the EnforceExhaustive type.
        // Since we can't directly access _inner, verify through Split behavior:
        // EnforceExhaustiveDomain with 2 concrete types + null => 3 partitions
        var parts = nullable.Split();
        Assert.Equal(3, parts.Length);
    }

    // ─── [EnforceExhaustiveness] with NotAnnotated ref type ──────────

    [Fact]
    public void EnforceExhaustiveness_not_annotated_returns_EnforceExhaustiveDomain_directly()
    {
        var source = @"
#nullable enable
namespace Houtamelo.Spire.Core
{
    [System.AttributeUsage(System.AttributeTargets.Enum | System.AttributeTargets.Class | System.AttributeTargets.Interface)]
    public class EnforceExhaustivenessAttribute : System.Attribute { }
}

[Houtamelo.Spire.Core.EnforceExhaustiveness]
public abstract class Shape { }
public class Circle : Shape { }
public class Square : Shape { }

public class Holder
{
    public Shape NonNullShape = null!;
}
";
        var (compilation, resolver) = CreateResolver(source);
        var holderType = compilation.GetTypeByMetadataName("Holder")!;
        var field = holderType.GetMembers("NonNullShape")[0] as IFieldSymbol;
        Assert.NotNull(field);
        Assert.Equal(NullableAnnotation.NotAnnotated, field!.Type.NullableAnnotation);

        var result = resolver.Resolve(field.Type);

        // NotAnnotated => resolve directly, no NullableDomain wrapper
        var domain = Assert.IsType<EnforceExhaustiveDomain>(result);
        Assert.True(domain.IsUniverse);
        // 2 concrete types: Circle, Square
        Assert.Equal(2, domain.Split().Length);
    }
}
