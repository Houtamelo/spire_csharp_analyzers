using Houtamelo.Spire;
using Spire.BehavioralTests.Types;
using Xunit;

namespace Spire.BehavioralTests.Tests;

public class InlinerCorrectnessTests
{
    [Fact]
    public void InlinerStruct_EscapedKeywordMethodExecutes()
    {
        var inliner = default(global::Spire.BehavioralTests.Types.@class.eventInliner<int>);
        Assert.Equal(7, inliner.Invoke(7));
    }

    [Fact]
    public void InlinerStruct_EscapedGenericParameterExecutes()
    {
        var inliner = default(global::Spire.BehavioralTests.Types.@class.eventInliner<UnmanagedMarker>);
        var value = new UnmanagedMarker(9);
        Assert.Equal(value.Value, inliner.Invoke(value).Value);
    }

    [Fact]
    public void InlinableTwin_EscapedDelegateExecutes()
    {
        var inliner = default(global::Spire.BehavioralTests.Types.@class.eventInliner<int>);
        Assert.Equal(11, global::Spire.BehavioralTests.Types.@class.@event(11, inliner));
    }

    [Fact]
    public void InlinerStruct_ClassInterfaceNewConstraintExecutes()
    {
        var marker = new Marker { Value = 12 };
        var inliner = default(ConstrainedTargets.CreateInliner<Marker>);
        Assert.Same(marker, inliner.Invoke(marker));
    }

    [Fact]
    public void InlinerStruct_UnmanagedInterfaceConstraintExecutes()
    {
        var value = new UnmanagedMarker(13);
        var inliner = default(ConstrainedTargets.EchoInliner<UnmanagedMarker>);
        Assert.Equal(value.Value, inliner.Invoke(value).Value);
    }

    [Fact]
    public void InlinableTwin_ConstrainedReferenceExecutes()
    {
        var marker = new Marker { Value = 14 };
        var inliner = default(ConstrainedTargets.CreateInliner<Marker>);
        Assert.Same(marker, ConstrainedTargets.Apply(marker, inliner));
    }

    [Fact]
    public void InlinerStruct_NestedGenericReceiverExecutes()
    {
        var receiver = new Box<int>.Inner<Marker>();
        var marker = new Marker { Value = 15 };
        var inliner = default(Box<int>.Inner<Marker>.EchoInliner);
        Assert.Same(marker, inliner.Invoke(receiver, 123, marker));
    }

    [Fact]
    public void InlinerStruct_NamespaceCollisionControlsRemainCallable()
    {
        Assert.Equal(2, default(global::Spire.BehavioralTests.Types.Collision.Alpha.SameName.RunInliner).Invoke(1));
        Assert.Equal(3, default(global::Spire.BehavioralTests.Types.Collision.Beta.SameName.RunInliner).Invoke(1));
    }

    [Fact]
    public void InlinerStruct_NestedTypeCollisionControlsRemainCallable()
    {
        Assert.Equal(4, default(global::Spire.BehavioralTests.Types.Collision.NestedAlpha.Outer.Inner.RunInliner).Invoke(1));
        Assert.Equal(5, default(global::Spire.BehavioralTests.Types.Collision.NestedBeta.Outer.Inner.RunInliner).Invoke(1));
    }

    [Fact]
    public void InlinableTwin_NamespaceCollisionControlsRemainCallable()
    {
        Assert.Equal(7, global::Spire.BehavioralTests.Types.Collision.Alpha.InlinableSameName.Run(default(Increment), 5));
        Assert.Equal(9, global::Spire.BehavioralTests.Types.Collision.Beta.InlinableSameName.Run(default(Increment), 6));
    }

    private readonly struct Increment : IFuncInliner<int, int>
    {
        public int Invoke(int value) => value + 1;
    }
}
