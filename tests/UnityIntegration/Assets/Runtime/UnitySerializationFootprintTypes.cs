#nullable enable

using System;
using Houtamelo.Spire;
using UnityEngine;

namespace Spire.UnityIntegration
{
    [Serializable]
    public sealed class UnitySerializationFootprintEqualsAlways
    {
        public int serializedValue;

        public override bool Equals(object? obj)
            => obj is UnitySerializationFootprintEqualsAlways;

        public override int GetHashCode()
            => 0;
    }

    [Serializable]
    [DiscriminatedUnion(Layout.Additive)]
    public partial struct UnitySerializationFootprintUnion
    {
        [Variant]
        public static partial UnitySerializationFootprintUnion Value(
            UnitySerializationFootprintEqualsAlways value,
            int marker);

        [Variant]
        public static partial UnitySerializationFootprintUnion Empty();
    }

    [Serializable]
    [DiscriminatedUnion(Layout.Additive)]
    public partial struct UnitySerializationFootprintCollisionUnion
    {
        [Variant]
        public static partial UnitySerializationFootprintCollisionUnion Count(int value);

        [Variant]
        public static partial UnitySerializationFootprintCollisionUnion Text(string value);

        public static string Describe(UnitySerializationFootprintCollisionUnion value)
            => value switch
            {
                { kind: Kind.Count, count_value: var count } => "count:" + count,
                { kind: Kind.Text, text_value: var text } => "text:" + text,
                _ => "invalid",
            };
    }

    [Serializable]
    public sealed class UnitySerializationFootprintEffect
    {
        public int marker;
    }

    [Serializable]
    [DiscriminatedUnion(Layout.Additive)]
    public partial struct UnitySerializationFootprintNullableUnion
    {
        [Variant]
        public static partial UnitySerializationFootprintNullableUnion Apply(
            [UnitySerializeReference] UnitySerializationFootprintEffect? effect);

        [Variant]
        public static partial UnitySerializationFootprintNullableUnion Remove(
            [UnitySerializeReference] UnitySerializationFootprintEffect effect);

        public static int ReadEffect(UnitySerializationFootprintNullableUnion value)
            => value switch
            {
                { kind: Kind.Apply, effect: var effect } => effect?.marker ?? -1,
                { kind: Kind.Remove, effect: var effect } => effect?.marker ?? -1,
                _ => -1,
            };
    }

}
