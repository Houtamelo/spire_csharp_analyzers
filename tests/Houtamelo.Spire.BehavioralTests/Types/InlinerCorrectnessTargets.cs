using System;
using Houtamelo.Spire;

namespace Spire.BehavioralTests.Types
{
    public interface IMarker { }

    public sealed class Marker : IMarker
    {
        public Marker() { }
        public int Value { get; set; }
    }

    public readonly struct UnmanagedMarker : IMarker
    {
        public UnmanagedMarker(int value) => Value = value;
        public int Value { get; }
    }

    public static partial class @class
    {
        [InlinerStruct]
        public static @delegate @event<@delegate>(@delegate @value)
            where @delegate : unmanaged
            => @value;

        public static @struct @event<@struct>(
            @struct @value,
            [Inlinable] Func<@struct, @struct> @delegate)
            where @struct : unmanaged
            => @delegate(@value);
    }

    public static partial class ConstrainedTargets
    {
        [InlinerStruct]
        public static T Create<T>(T value)
            where T : class, IMarker, new()
            => value;

        [InlinerStruct]
        public static T Echo<T>(T value)
            where T : unmanaged, IMarker
            => value;

        public static T Apply<T>(T value, [Inlinable] Func<T, T> callback)
            where T : class, IMarker, new()
            => callback(value);
    }

    public partial class Box<TOuter>
        where TOuter : unmanaged
    {
        public partial class Inner<TInner>
            where TInner : class, IMarker, new()
        {
            [InlinerStruct]
            public TInner Echo(TOuter outer, TInner inner) => inner;
        }
    }
}

namespace Spire.BehavioralTests.Types.Collision.Alpha
{
    public static partial class SameName
    {
        [InlinerStruct]
        public static int Run(int value) => value + 1;
    }

    public static partial class InlinableSameName
    {
        public static int Run([Inlinable] Func<int, int> callback, int value)
            => callback(value + 1);
    }
}

namespace Spire.BehavioralTests.Types.Collision.Beta
{
    public static partial class SameName
    {
        [InlinerStruct]
        public static int Run(int value) => value + 2;
    }

    public static partial class InlinableSameName
    {
        public static int Run([Inlinable] Func<int, int> callback, int value)
            => callback(value + 2);
    }
}

namespace Spire.BehavioralTests.Types.Collision.NestedAlpha
{
    public partial class Outer
    {
        public partial class Inner
        {
            [InlinerStruct]
            public static int Run(int value) => value + 3;
        }
    }
}

namespace Spire.BehavioralTests.Types.Collision.NestedBeta
{
    public partial class Outer
    {
        public partial class Inner
        {
            [InlinerStruct]
            public static int Run(int value) => value + 4;
        }
    }
}
