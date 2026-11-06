#nullable enable

using Serializable = global::System.SerializableAttribute;
using SerializeField = global::UnityEngine.SerializeField;
using Preserve = global::UnityEngine.Scripting.PreserveAttribute;

namespace Houtamelo.Spire.Generated {
[@Preserve]
internal sealed class __SpireReconstructedPayload : global::Houtamelo.Spire.UnitySerializationPayloadBase {
    internal static readonly __SpireReconstructedPayload Instance = new __SpireReconstructedPayload();

    [@Preserve]
    __SpireReconstructedPayload() {}
}

[@Serializable]
[@Preserve]
internal class SpireSerializedPayload<T> : global::Houtamelo.Spire.UnitySerializationPayloadBase {
    [@Preserve]
    public SpireSerializedPayload() {}

    [@SerializeField] public T value = default!;
}
}
