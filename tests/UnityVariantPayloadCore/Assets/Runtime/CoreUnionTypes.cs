#nullable enable

using System;
using Houtamelo.Spire;
using UnityEngine;

namespace Spire.UnityVariantPayloadCore
{

    public struct UnknownBlock
    {
        public long first;
        public byte marker;
    }

    public struct ManagedPair
    {
        public string? label;
        public int count;
    }

    public sealed class RefA
    {
        public int value;
    }

    public sealed class RefB
    {
        public string? value;
    }

    public sealed class RefC
    {
        public float value;
    }

    public sealed class RefD
    {
        public long value;
    }

    [DiscriminatedUnion]
    public partial struct CoreShape
    {
        [Variant] public static partial CoreShape Circle(Vector2 position, float radius);
        [Variant] public static partial CoreShape Triangle(Vector3 first, Vector3 second, Vector3 third);
        [Variant] public static partial CoreShape Empty();
    }

    [DiscriminatedUnion]
    public partial struct GenericCore<T> where T : unmanaged
    {
        [Variant] public static partial GenericCore<T> Value(T value, float scale);
        [Variant] public static partial GenericCore<T> Empty();
    }

    [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]
    public partial struct TypedReferences
    {
        [Variant] public static partial TypedReferences Pair(ManagedPair pair);
        [Variant] public static partial TypedReferences Text(string text);
        [Variant] public static partial TypedReferences Numbers(int[] numbers);
        [Variant] public static partial TypedReferences Both(string left, string right);
    }

    [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Shared)]
    public partial struct SharedReferences
    {
        [Variant] public static partial SharedReferences Pair(ManagedPair pair);
        [Variant] public static partial SharedReferences Text(string text);
        [Variant] public static partial SharedReferences Numbers(int[] numbers);
        [Variant] public static partial SharedReferences Empty();
    }

    [DiscriminatedUnion]
    public partial struct AutoReferences
    {
        [Variant] public static partial AutoReferences A(RefA value);
        [Variant] public static partial AutoReferences B(RefB value);
        [Variant] public static partial AutoReferences C(RefC value);
        [Variant] public static partial AutoReferences D(RefD value);
    }

    [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Shared)]
    public partial struct SharedTupleReferences
    {
        [Variant] public static partial SharedTupleReferences First((string? label, int count) value);
        [Variant] public static partial SharedTupleReferences Second((string title, int amount) value);
        [Variant]
        public static partial SharedTupleReferences Both(
            (string? leftLabel, int leftCount) left,
            (string? rightLabel, int rightCount) right);
    }

    [DiscriminatedUnion]
    public partial struct UnknownBlockUnion
    {
        [Variant] public static partial UnknownBlockUnion Block(UnknownBlock value);
        [Variant] public static partial UnknownBlockUnion Count(int value);
    }

    public static partial class NestedOwner
    {
        [DiscriminatedUnion]
        public partial struct Command
        {
            [Variant] public static partial Command Run(float speed);
            [Variant] public static partial Command Stop();
        }
    }

}
