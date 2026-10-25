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
    [DiscriminatedUnion]
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
    [DiscriminatedUnion]
    public partial struct UnitySerializationFootprintCollisionUnion
    {
        [Variant]
        public static partial UnitySerializationFootprintCollisionUnion Count(int value);

        [Variant]
        public static partial UnitySerializationFootprintCollisionUnion Text(string value);

        public static string Describe(UnitySerializationFootprintCollisionUnion value)
        {
            if (value.kind == Kind.Count)
                return "count:" + value.count.value;
            if (value.kind == Kind.Text)
                return "text:" + value.text.value;
            return "invalid";
        }
    }

    [Serializable]
    public sealed class UnitySerializationFootprintEffect
    {
        public int marker;
    }

    [Serializable]
    [DiscriminatedUnion]
    public partial struct UnitySerializationFootprintNullableUnion
    {
        [Variant]
        public static partial UnitySerializationFootprintNullableUnion Apply(
            [UnitySerializeReference] UnitySerializationFootprintEffect? effect);

        [Variant]
        public static partial UnitySerializationFootprintNullableUnion Remove(
            [UnitySerializeReference] UnitySerializationFootprintEffect effect);

        public static int ReadEffect(UnitySerializationFootprintNullableUnion value)
        {
            if (value.kind == Kind.Apply)
                return value.apply.effect?.marker ?? -1;
            if (value.kind == Kind.Remove)
                return value.remove.effect?.marker ?? -1;
            return -1;
        }
    }

}
