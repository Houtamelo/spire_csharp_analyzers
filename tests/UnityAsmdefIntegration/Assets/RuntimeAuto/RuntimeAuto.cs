#nullable enable
using System;
using Houtamelo.Spire;

namespace Spire.UnityAsmdefIntegration.RuntimeAuto
{
    [Serializable]
    [DiscriminatedUnion]
    public partial struct RuntimeAutoUnion
    {
        [Variant]
        public static partial RuntimeAutoUnion Empty();

        [Variant]
        public static partial RuntimeAutoUnion Value(int value);
    }

    public readonly struct RuntimeAutoPoint
    {
        public readonly int x;
        public readonly int y;

        public RuntimeAutoPoint(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

    [DiscriminatedUnion]
    public readonly partial struct RuntimeAutoOverlapUnion
    {
        [Variant]
        public static partial RuntimeAutoOverlapUnion First(int x, int y);

        [Variant]
        public static partial RuntimeAutoOverlapUnion Second(int left, int right);

        [Variant]
        public static partial RuntimeAutoOverlapUnion Text(string? text);

        [Variant]
        public static partial RuntimeAutoOverlapUnion Object(object detail);

        [Variant]
        public static partial RuntimeAutoOverlapUnion Point(RuntimeAutoPoint point);

        [Variant]
        public static partial RuntimeAutoOverlapUnion Empty();
    }

    [EnforceInitialization]
    public struct InitializationProbe
    {
        public int value;

        public InitializationProbe(int value)
        {
            this.value = value;
        }
    }

    [EnforceInitialization]
    public struct WarningOnlyType
    {
    }

    public static class AsmdefProbe
    {
        public static InitializationProbe[] Initialized(int value)
        {
            return new InitializationProbe[]
            {
                new InitializationProbe(value),
            };
        }

#pragma warning disable SPIRE014
        public static bool Verify()
        {
            var union = RuntimeAutoUnion.Value(42);
            var first = RuntimeAutoOverlapUnion.First(11, 22);
            var second = RuntimeAutoOverlapUnion.Second(33, 44);
            var text = RuntimeAutoOverlapUnion.Text("text");
            var detail = new object();
            var objectValue = RuntimeAutoOverlapUnion.Object(detail);
            var point = RuntimeAutoOverlapUnion.Point(new RuntimeAutoPoint(55, 66));
            var empty = RuntimeAutoOverlapUnion.Empty();

            return union.kind == RuntimeAutoUnion.Kind.Value
                && union.value.value == 42
                && first.kind == RuntimeAutoOverlapUnion.Kind.First
                && first.first.x == 11
                && first.first.y == 22
                && second.kind == RuntimeAutoOverlapUnion.Kind.Second
                && second.second.left == 33
                && second.second.right == 44
                && text.kind == RuntimeAutoOverlapUnion.Kind.Text
                && text.text.text == "text"
                && objectValue.kind == RuntimeAutoOverlapUnion.Kind.Object
                && object.ReferenceEquals(objectValue.@object.detail, detail)
                && point.kind == RuntimeAutoOverlapUnion.Kind.Point
                && point.point.point.x == 55
                && point.point.point.y == 66
                && empty.kind == RuntimeAutoOverlapUnion.Kind.Empty
                && Initialized(42)[0].value == 42;
        }
#pragma warning restore SPIRE014
    }
}
