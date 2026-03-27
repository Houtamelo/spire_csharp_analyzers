using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.PatternAnalysis.Resolution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.PatternAnalysis.Tests.Resolution;

public class TypeHierarchyResolverTests
{
    private static readonly CSharpCompilationOptions DllOptions
        = new(OutputKind.DynamicallyLinkedLibrary);

    private static readonly MetadataReference CorlibRef
        = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

    private static (Compilation compilation, INamedTypeSymbol baseType) Compile(
        string source, string baseTypeName)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("Test", [tree], [CorlibRef], DllOptions);
        var baseType = compilation.GetTypeByMetadataName(baseTypeName)!;
        return (compilation, baseType);
    }

    // ─── Sealed class ─────────────────────────────────────────────────

    [Fact]
    public void Sealed_class_returns_empty()
    {
        var (compilation, baseType) = Compile(
            "public sealed class Foo { }", "Foo");

        var resolver = new TypeHierarchyResolver();
        var result = resolver.Resolve(baseType, compilation);

        Assert.True(result.IsEmpty);
    }

    // ─── Abstract class with 2 concrete subclasses ────────────────────

    [Fact]
    public void Abstract_class_with_two_concrete_subclasses_returns_both()
    {
        var (compilation, baseType) = Compile(@"
public abstract class Animal { }
public class Dog : Animal { }
public class Cat : Animal { }
", "Animal");

        var resolver = new TypeHierarchyResolver();
        var result = resolver.Resolve(baseType, compilation);

        Assert.Equal(2, result.Length);
        var names = result.Select(t => t.Name).OrderBy(n => n).ToArray();
        Assert.Equal("Cat", names[0]);
        Assert.Equal("Dog", names[1]);
    }

    // ─── Interface with 3 implementors ────────────────────────────────

    [Fact]
    public void Interface_with_three_implementors_returns_all()
    {
        var (compilation, baseType) = Compile(@"
public interface IShape { }
public class Circle : IShape { }
public class Square : IShape { }
public class Triangle : IShape { }
", "IShape");

        var resolver = new TypeHierarchyResolver();
        var result = resolver.Resolve(baseType, compilation);

        Assert.Equal(3, result.Length);
        var names = result.Select(t => t.Name).OrderBy(n => n).ToArray();
        Assert.Equal("Circle", names[0]);
        Assert.Equal("Square", names[1]);
        Assert.Equal("Triangle", names[2]);
    }

    // ─── Abstract intermediate skipped ────────────────────────────────

    [Fact]
    public void Abstract_intermediate_is_skipped_only_concrete_returned()
    {
        var (compilation, baseType) = Compile(@"
public abstract class Base { }
public abstract class Middle : Base { }
public class Leaf1 : Middle { }
public class Leaf2 : Base { }
", "Base");

        var resolver = new TypeHierarchyResolver();
        var result = resolver.Resolve(baseType, compilation);

        Assert.Equal(2, result.Length);
        var names = result.Select(t => t.Name).OrderBy(n => n).ToArray();
        Assert.Equal("Leaf1", names[0]);
        Assert.Equal("Leaf2", names[1]);
    }

    // ─── Internal class ──────────────────────────────────────────────

    [Fact]
    public void Internal_class_only_searches_declaring_assembly()
    {
        var (compilation, baseType) = Compile(@"
internal abstract class InternalBase { }
internal class Impl1 : InternalBase { }
internal class Impl2 : InternalBase { }
", "InternalBase");

        var resolver = new TypeHierarchyResolver();
        var result = resolver.Resolve(baseType, compilation);

        // Both concrete implementors found within the declaring assembly
        Assert.Equal(2, result.Length);
        var names = result.Select(t => t.Name).OrderBy(n => n).ToArray();
        Assert.Equal("Impl1", names[0]);
        Assert.Equal("Impl2", names[1]);
    }

    // ─── Private nested class ─────────────────────────────────────────

    [Fact]
    public void Private_nested_class_only_searches_parent_nested_types()
    {
        var (compilation, baseType) = Compile(@"
public class Outer
{
    private abstract class PrivateBase { }
    private class Impl1 : PrivateBase { }
    private class Impl2 : PrivateBase { }
}
", "Outer+PrivateBase");

        var resolver = new TypeHierarchyResolver();
        var result = resolver.Resolve(baseType, compilation);

        Assert.Equal(2, result.Length);
        var names = result.Select(t => t.Name).OrderBy(n => n).ToArray();
        Assert.Equal("Impl1", names[0]);
        Assert.Equal("Impl2", names[1]);
    }

    // ─── Class with only private constructors ─────────────────────────

    [Fact]
    public void Class_with_only_private_ctors_only_searches_nested_types()
    {
        var (compilation, baseType) = Compile(@"
public abstract class Restricted
{
    private Restricted() { }
    public class Sub1 : Restricted { private Sub1() : base() { } }
    public class Sub2 : Restricted { private Sub2() : base() { } }
}

// This should NOT be found because Restricted has only private ctors,
// so only nested types can derive from it.
// (C# would reject this at compile time, but we verify the resolver's logic.)
", "Restricted");

        var resolver = new TypeHierarchyResolver();
        var result = resolver.Resolve(baseType, compilation);

        Assert.Equal(2, result.Length);
        var names = result.Select(t => t.Name).OrderBy(n => n).ToArray();
        Assert.Equal("Sub1", names[0]);
        Assert.Equal("Sub2", names[1]);
    }

    // ─── Generic base type ────────────────────────────────────────────

    [Fact]
    public void Generic_base_type_found_via_original_definition()
    {
        var (compilation, baseType) = Compile(@"
public abstract class Foo<T> { }
public class Bar : Foo<int> { }
public class Baz : Foo<string> { }
", "Foo`1");

        var resolver = new TypeHierarchyResolver();
        var result = resolver.Resolve(baseType, compilation);

        Assert.Equal(2, result.Length);
        var names = result.Select(t => t.Name).OrderBy(n => n).ToArray();
        Assert.Equal("Bar", names[0]);
        Assert.Equal("Baz", names[1]);
    }

    // ─── No implementors ─────────────────────────────────────────────

    [Fact]
    public void No_implementors_returns_empty()
    {
        var (compilation, baseType) = Compile(
            "public abstract class Lonely { }", "Lonely");

        var resolver = new TypeHierarchyResolver();
        var result = resolver.Resolve(baseType, compilation);

        Assert.True(result.IsEmpty);
    }

    // ─── Caching ──────────────────────────────────────────────────────

    [Fact]
    public void Second_resolve_returns_cached_result()
    {
        var (compilation, baseType) = Compile(@"
public abstract class Base { }
public class Sub : Base { }
", "Base");

        var resolver = new TypeHierarchyResolver();
        var result1 = resolver.Resolve(baseType, compilation);
        var result2 = resolver.Resolve(baseType, compilation);

        // Same immutable array instance returned from cache
        Assert.True(result1 == result2);
    }

    // ─── Interface — abstract implementor skipped ─────────────────────

    [Fact]
    public void Interface_abstract_implementor_skipped()
    {
        var (compilation, baseType) = Compile(@"
public interface IHandler { }
public abstract class AbstractHandler : IHandler { }
public class ConcreteHandler : AbstractHandler { }
", "IHandler");

        var resolver = new TypeHierarchyResolver();
        var result = resolver.Resolve(baseType, compilation);

        // Only ConcreteHandler — AbstractHandler is abstract
        Assert.Single(result);
        Assert.Equal("ConcreteHandler", result[0].Name);
    }

    // ─── Nested + protected base ─────────────────────────────────────

    [Fact]
    public void Nested_protected_class_only_searches_parent_nested_types()
    {
        var (compilation, baseType) = Compile(@"
public class Outer
{
    protected abstract class ProtectedBase { }
    protected class Impl : ProtectedBase { }
}
", "Outer+ProtectedBase");

        var resolver = new TypeHierarchyResolver();
        var result = resolver.Resolve(baseType, compilation);

        Assert.Single(result);
        Assert.Equal("Impl", result[0].Name);
    }

    // ─── Multi-assembly: public type + public ctor ────────────────────

    [Fact]
    public void Public_type_searches_dependent_assemblies()
    {
        // Assembly A: declares public abstract class
        var treeA = CSharpSyntaxTree.ParseText(@"
public abstract class Vehicle { }
public class Car : Vehicle { }
");
        var compilationA = CSharpCompilation.Create("AssemblyA", [treeA], [CorlibRef], DllOptions);

        // Assembly B: depends on A, has another subclass
        var refA = compilationA.ToMetadataReference();
        var treeB = CSharpSyntaxTree.ParseText(@"
public class Truck : Vehicle { }
");
        var compilationB = CSharpCompilation.Create("AssemblyB", [treeB], [CorlibRef, refA], DllOptions);

        // Resolve from compilation B (which sees both A and B)
        var baseType = compilationB.GetTypeByMetadataName("Vehicle")!;
        var resolver = new TypeHierarchyResolver();
        var result = resolver.Resolve(baseType, compilationB);

        // Should find Car (from A) and Truck (from B)
        Assert.Equal(2, result.Length);
        var names = result.Select(t => t.Name).OrderBy(n => n).ToArray();
        Assert.Equal("Car", names[0]);
        Assert.Equal("Truck", names[1]);
    }

    // ─── Internal type not found in dependent assembly ────────────────

    [Fact]
    public void Internal_type_not_found_in_dependent_assembly()
    {
        // Assembly A: declares internal abstract class + internal subclass
        var treeA = CSharpSyntaxTree.ParseText(@"
internal abstract class InternalBase { }
internal class ImplA : InternalBase { }
");
        var compilationA = CSharpCompilation.Create("AssemblyA", [treeA], [CorlibRef], DllOptions);

        // Assembly B: depends on A (but can't see internal types)
        var refA = compilationA.ToMetadataReference();
        var treeB = CSharpSyntaxTree.ParseText("public class Unused { }");
        var compilationB = CSharpCompilation.Create("AssemblyB", [treeB], [CorlibRef, refA], DllOptions);

        // Resolve from compilation A (internal scope)
        var baseType = compilationA.GetTypeByMetadataName("InternalBase")!;
        var resolver = new TypeHierarchyResolver();
        var result = resolver.Resolve(baseType, compilationA);

        // Only ImplA found — internal scope limited to declaring assembly
        Assert.Single(result);
        Assert.Equal("ImplA", result[0].Name);
    }

    // ─── Internal ctor limits to declaring assembly ───────────────────

    [Fact]
    public void Internal_ctor_limits_to_declaring_assembly()
    {
        var (compilation, baseType) = Compile(@"
public abstract class RestrictedCtor
{
    internal RestrictedCtor() { }
}
public class Sub1 : RestrictedCtor { }
", "RestrictedCtor");

        var resolver = new TypeHierarchyResolver();
        var result = resolver.Resolve(baseType, compilation);

        // Sub1 found within the declaring assembly
        Assert.Single(result);
        Assert.Equal("Sub1", result[0].Name);
    }

    // ─── Interface skips constructor rules ─────────────────────────────

    [Fact]
    public void Interface_skips_constructor_rules()
    {
        // Interfaces have no constructors — should fall through to rule 7 (public type search)
        var treeA = CSharpSyntaxTree.ParseText(@"
public interface IPlugin { }
public class PluginA : IPlugin { }
");
        var compilationA = CSharpCompilation.Create("AssemblyA", [treeA], [CorlibRef], DllOptions);

        var refA = compilationA.ToMetadataReference();
        var treeB = CSharpSyntaxTree.ParseText(@"
public class PluginB : IPlugin { }
");
        var compilationB = CSharpCompilation.Create("AssemblyB", [treeB], [CorlibRef, refA], DllOptions);

        var baseType = compilationB.GetTypeByMetadataName("IPlugin")!;
        var resolver = new TypeHierarchyResolver();
        var result = resolver.Resolve(baseType, compilationB);

        // Should find PluginA (from A) and PluginB (from B)
        Assert.Equal(2, result.Length);
        var names = result.Select(t => t.Name).OrderBy(n => n).ToArray();
        Assert.Equal("PluginA", names[0]);
        Assert.Equal("PluginB", names[1]);
    }
}
