#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Houtamelo.Spire;
using UnityEngine;

namespace Spire.UnityIntegration
{
    public sealed class StabilityPlayerHost : MonoBehaviour, ISerializationCallbackReceiver
    {
        public StabilityAdditiveUnion additive;
        public StabilityBoxedFieldsUnion boxedFields;
        public StabilityBoxedTupleUnion boxedTuple;
        public StabilityUnsafeOverlapUnion unsafeOverlap;
        public StabilityOverlapUnion overlap;
        public StabilityNestedParentUnion parent;
        public StabilityNestedParentUnion[] parentArray = Array.Empty<StabilityNestedParentUnion>();
        public List<StabilityNestedParentUnion> parentList = new List<StabilityNestedParentUnion>();
        public StabilityGenericOwnerInt genericInt;
        public StabilityGenericOwnerString genericString;
        public StabilityGenericUnion<int>[] genericArray = Array.Empty<StabilityGenericUnion<int>>();
        public StabilityCallbackChildHelper callbackHelper = new StabilityCallbackChildHelper();
        public Texture2D reference = null!;

        [SerializeReference]
        public UnitySerializationPayloadBase?[] opaqueFormerPayloads = Array.Empty<UnitySerializationPayloadBase?>();

        [NonSerialized]
        public WeakReference[] callbackPayloadWeakReferences = Array.Empty<WeakReference>();

        [NonSerialized]
        public bool lifetimeProbeValuesValid;

        [NonSerialized]
        public bool lifetimeProbeCacheCleared;

        [NonSerialized]
        public int lifetimeProbeAmount;

        [NonSerialized]
        public string lifetimeProbeLabel = string.Empty;

        [NonSerialized]
        public int beforeSerializeCount;

        [NonSerialized]
        public int afterDeserializeCount;

        public void OnBeforeSerialize()
        {
            beforeSerializeCount++;
        }

        public void OnAfterDeserialize()
        {
            afterDeserializeCount++;
        }

        public void RunLifetimeWeakReferenceProbe()
        {
            var probe = StabilityAdditiveUnion.First(123, "lifetime-probe");
            probe = probe.__SpirePrepareForSerialization();
            var payload = GetPayload(probe);
            if (payload == null)
            {
                callbackPayloadWeakReferences = Array.Empty<WeakReference>();
                lifetimeProbeValuesValid = false;
                lifetimeProbeCacheCleared = false;
                return;
            }

            callbackPayloadWeakReferences = new[] { new WeakReference(payload) };
            probe.OnAfterDeserialize();
            lifetimeProbeAmount = probe.amount;
            lifetimeProbeLabel = probe.label ?? string.Empty;
            lifetimeProbeValuesValid = probe.kind == StabilityAdditiveUnion.Kind.First &&
                lifetimeProbeAmount == 123 &&
                lifetimeProbeLabel == "lifetime-probe";
            var marker = GetPayload(probe);
            lifetimeProbeCacheCleared = marker != null && !IsConcretePayload(probe, marker);
            payload = null;
        }

        private static object? GetPayload<T>(T value)
        {
            var field = value!.GetType().GetField(
                "_serializedPayload",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(value);
        }

        private static bool IsConcretePayload<T>(T value, object payload)
        {
            return value!.GetType()
                .GetNestedTypes(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(type => type.IsClass && !type.IsAbstract &&
                    type.IsDefined(typeof(SerializableAttribute), inherit: false))
                .Any(type => type.IsInstanceOfType(payload));
        }

    }

}
