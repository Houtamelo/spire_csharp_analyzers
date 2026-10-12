using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests;

public sealed class UnionSourceHintTests : Behavioral.BehavioralTestBase
{
    [Fact]
    public void SameTypeNameInDifferentNamespaces_HaveIndependentGeneratedFactories()
    {
        var assembly = CompileAndLoad("""
            using Houtamelo.Spire;

            namespace HintNamespaces.Left
            {
                [DiscriminatedUnion(Layout.Additive)]
                public partial struct Result
                {
                    [Variant] public static partial Result Some(int value);
                    [Variant] public static partial Result None();
                }
            }

            namespace HintNamespaces.Right
            {
                [DiscriminatedUnion(Layout.Additive)]
                public partial struct Result
                {
                    [Variant] public static partial Result Some(int value);
                    [Variant] public static partial Result None();
                }
            }
            """);

        var left = InvokeFactory(
            GetType(assembly, "HintNamespaces.Left.Result"),
            "Some",
            17);
        var right = InvokeFactory(
            GetType(assembly, "HintNamespaces.Right.Result"),
            "Some",
            23);

        Assert.Equal(17, ReadProperty(left, "value"));
        Assert.Equal(23, ReadProperty(right, "value"));
    }

    [Fact]
    public void SameNestedTypeNameUnderDifferentHosts_HaveIndependentGeneratedFactories()
    {
        var assembly = CompileAndLoad("""
            using Houtamelo.Spire;

            namespace HintNestedHosts
            {
                public partial class FirstHost
                {
                    [DiscriminatedUnion(Layout.Additive)]
                    public partial struct Result
                    {
                        [Variant] public static partial Result Some(int value);
                        [Variant] public static partial Result None();
                    }
                }

                public partial class SecondHost
                {
                    [DiscriminatedUnion(Layout.Additive)]
                    public partial struct Result
                    {
                        [Variant] public static partial Result Some(int value);
                        [Variant] public static partial Result None();
                    }
                }
            }
            """);

        var first = InvokeFactory(
            GetType(assembly, "HintNestedHosts.FirstHost+Result"),
            "Some",
            31);
        var second = InvokeFactory(
            GetType(assembly, "HintNestedHosts.SecondHost+Result"),
            "Some",
            37);

        Assert.Equal(31, ReadProperty(first, "value"));
        Assert.Equal(37, ReadProperty(second, "value"));
    }

    [Fact]
    public void SameContainingHostNameWithDifferentGenericArities_HaveIndependentFactories()
    {
        var assembly = CompileAndLoad("""
            using Houtamelo.Spire;

            namespace HintGenericHosts
            {
                public partial class Container<T>
                {
                    [DiscriminatedUnion(Layout.Additive)]
                    public partial struct Result
                    {
                        [Variant] public static partial Result Some(int value);
                        [Variant] public static partial Result None();
                    }
                }

                public partial class Container<TLeft, TRight>
                {
                    [DiscriminatedUnion(Layout.Additive)]
                    public partial struct Result
                    {
                        [Variant] public static partial Result Some(int value);
                        [Variant] public static partial Result None();
                    }
                }
            }
            """);

        var oneArgumentResult = InvokeFactory(
            GetType(assembly, "HintGenericHosts.Container`1+Result")
                .MakeGenericType(typeof(int)),
            "Some",
            41);
        var twoArgumentResult = InvokeFactory(
            GetType(assembly, "HintGenericHosts.Container`2+Result")
                .MakeGenericType(typeof(int), typeof(string)),
            "Some",
            43);

        Assert.Equal(41, ReadProperty(oneArgumentResult, "value"));
        Assert.Equal(43, ReadProperty(twoArgumentResult, "value"));
    }

    [Fact]
    public void RootUnionArity_IsPartOfGeneratedSourceIdentity()
    {
        var assembly = CompileAndLoad("""
            using Houtamelo.Spire;

            namespace HintRootArity
            {
                [DiscriminatedUnion(Layout.Additive)]
                public partial struct Result
                {
                    [Variant] public static partial Result Some(int value);
                    [Variant] public static partial Result None();
                }

                [DiscriminatedUnion(Layout.Additive)]
                public partial struct Result<T>
                {
                    [Variant] public static partial Result<T> Some(int value);
                    [Variant] public static partial Result<T> None();
                }
            }
            """);

        var nonGeneric = InvokeFactory(
            GetType(assembly, "HintRootArity.Result"),
            "Some",
            47);
        var generic = InvokeFactory(
            GetType(assembly, "HintRootArity.Result`1").MakeGenericType(typeof(string)),
            "Some",
            53);

        Assert.Equal(47, ReadProperty(nonGeneric, "value"));
        Assert.Equal(53, ReadProperty(generic, "value"));
    }

    [Fact]
    public void DistinctTypeNames_ControlRemainsCompilableAndInvokable()
    {
        var assembly = CompileAndLoad("""
            using Houtamelo.Spire;

            namespace HintDistinctNames
            {
                [DiscriminatedUnion(Layout.Additive)]
                public partial struct LeftResult
                {
                    [Variant] public static partial LeftResult Some(int value);
                    [Variant] public static partial LeftResult None();
                }

                [DiscriminatedUnion(Layout.Additive)]
                public partial struct RightResult
                {
                    [Variant] public static partial RightResult Some(int value);
                    [Variant] public static partial RightResult None();
                }
            }
            """);

        var left = InvokeFactory(
            GetType(assembly, "HintDistinctNames.LeftResult"),
            "Some",
            59);
        var right = InvokeFactory(
            GetType(assembly, "HintDistinctNames.RightResult"),
            "Some",
            61);

        Assert.Equal(59, ReadProperty(left, "value"));
        Assert.Equal(61, ReadProperty(right, "value"));
    }

    [Fact]
    public void CombiningMarkNamespace_DoesNotCollideWithItsEscapedHintText()
    {
        const string combiningMark = "\u0301";
        var assembly = CompileAndLoad($$"""
            using Houtamelo.Spire;

            namespace HintEscapes.Cafe{{combiningMark}}
            {
                [DiscriminatedUnion(Layout.Additive)]
                public partial struct Result
                {
                    [Variant] public static partial Result Some(int value);
                    [Variant] public static partial Result None();
                }
            }

            namespace HintEscapes.Cafe_u0301_
            {
                [DiscriminatedUnion(Layout.Additive)]
                public partial struct Result
                {
                    [Variant] public static partial Result Some(int value);
                    [Variant] public static partial Result None();
                }
            }
            """);

        var combiningResult = InvokeFactory(
            GetType(assembly, "HintEscapes.Cafe\u0301.Result"),
            "Some",
            79);
        var escapedTextResult = InvokeFactory(
            GetType(assembly, "HintEscapes.Cafe_u0301_.Result"),
            "Some",
            83);

        Assert.Equal(79, ReadProperty(combiningResult, "value"));
        Assert.Equal(83, ReadProperty(escapedTextResult, "value"));
    }

    [Fact]
    public void UnitySerializedNestedHostArities_CompileAgainstRealUnityReferences()
    {
        var result = UnityGeneratorTestHarness.Run("""
            #nullable enable
            using System;
            using Houtamelo.Spire;

            namespace HintUnityGenericHosts
            {
                public partial class Container<T>
                {
                    [Serializable]
                    [DiscriminatedUnion(Layout.Additive)]
                    public partial struct Result
                    {
                        [Variant] public static partial Result Some(int value);
                        [Variant] public static partial Result None();
                    }
                }

                public partial class Container<TLeft, TRight>
                {
                    [Serializable]
                    [DiscriminatedUnion(Layout.Additive)]
                    public partial struct Result
                    {
                        [Variant] public static partial Result Some(int value);
                        [Variant] public static partial Result None();
                    }
                }
            }
            """);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        var assembly = UnityGeneratorTestHarness.EmitAndLoad(
            Assert.IsType<CSharpCompilation>(result.OutputCompilation),
            "Unity nested host arity hint identity");

        var oneArgumentResult = InvokeFactory(
            GetType(assembly, "HintUnityGenericHosts.Container`1+Result")
                .MakeGenericType(typeof(int)),
            "Some",
            67);
        var twoArgumentResult = InvokeFactory(
            GetType(assembly, "HintUnityGenericHosts.Container`2+Result")
                .MakeGenericType(typeof(int), typeof(string)),
            "Some",
            71);

        Assert.Equal(67, ReadProperty(oneArgumentResult, "value"));
        Assert.Equal(71, ReadProperty(twoArgumentResult, "value"));
    }

}
