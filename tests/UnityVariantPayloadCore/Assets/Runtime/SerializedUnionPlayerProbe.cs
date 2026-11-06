#nullable enable

using System;
using UnityEngine;

namespace Spire.UnityVariantPayloadCore
{

    public static class SerializedUnionPlayerProbe
    {
        private const string FixtureResource = "SerializedUnionPayloadCore/SerializedUnionFixture";
        private const string TextureResource = "SerializedUnionPayloadCore/SerializedUnionTexture";
        private const string AliasResource = "SerializedUnionPayloadCore/SerializedUnionPlayerAlias";
        private const string NullResource = "SerializedUnionPayloadCore/SerializedUnionPlayerNull";

        public static void RunChecks(Action<bool, string> require)
        {
            var asset = Resources.Load<SerializedUnionAsset>(FixtureResource);
            require(asset != null, "Player could not load the canonical serialized union asset.");
            if (asset == null)
                return;

            if (asset.shape.kind != SerializedShape.Kind.Move)
            {
                require(false, "Player did not reconstruct the fielded payload variant.");
                return;
            }
            require(
                asset.shape.move.destination == new Vector2(2f, 3f) &&
                asset.shape.move.speed == 7.5f,
                "Player did not reconstruct the fielded payload.");
            require(asset.empty.kind == SerializedShape.Kind.Empty,
                "Player did not reconstruct the fieldless payload.");
            if (asset.nested.kind != SerializedShape.Kind.Rich)
            {
                require(false, "Player did not reconstruct the nested payload variant.");
                return;
            }

            var rich = asset.nested.rich;
            var texture = Resources.Load<Texture2D>(TextureResource);
            if (texture is null || asset.sharedTexture is null || rich.icon is null)
            {
                require(false, "Player did not restore UnityEngine.Object references.");
            }
            else
            {
                require(ReferenceEquals(texture, asset.sharedTexture) && ReferenceEquals(texture, rich.icon) &&
#if UNITY_6000_4_OR_NEWER
                    texture.GetEntityId() == asset.sharedTexture.GetEntityId() &&
                    texture.GetEntityId() == rich.icon.GetEntityId(),
#else
                    texture.GetInstanceID() == asset.sharedTexture.GetInstanceID() &&
                    texture.GetInstanceID() == rich.icon.GetInstanceID(),
#endif
                    "Player did not preserve UnityEngine.Object identity.");
            }
            if (rich.helper.command.kind != SerializedNestedOwner.Command.Kind.Run)
            {
                require(false, "Player did not reconstruct the nested command variant.");
                return;
            }
            if (rich.helper.nested.kind != SerializedShape.Kind.Move)
            {
                require(false, "Player did not reconstruct the nested helper variant.");
                return;
            }
            require(rich.helper.command.run.speed == 3.5f &&
                rich.helper.nested.move.speed == 1.25f,
                "Player did not reconstruct the nested inline-helper unions.");
            require(rich.children.Length == 2 &&
                rich.children[0].kind == SerializedChildUnion.Kind.Leaf &&
                rich.children[1].kind == SerializedChildUnion.Kind.Command &&
                rich.childList.Count == 2 &&
                rich.childList[0].kind == SerializedChildUnion.Kind.Empty &&
                rich.childList[1].kind == SerializedChildUnion.Kind.Leaf,
                "Player did not reconstruct nested union collections.");
            require(asset.shapeArray.Length == 3 && asset.shapeList.Count == 3,
                "Player did not reconstruct top-level union collections.");

            if (SerializedUnionNativeCapabilities.GenericUnionSerializationSupported)
            {
                if (asset.generic.kind != SerializedGeneric<int>.Kind.Value)
                {
                    require(false, "Player did not reconstruct the generic union variant.");
                    return;
                }
                require(asset.generic.value.value == 42 && asset.generic.value.label == "generic",
                    "Player did not reconstruct the generic union closure.");
            }
            else
            {
                Debug.Log(
                    "SerializedUnionPlayerProbe: generic union native serialization is " +
                    SerializedUnionNativeCapabilities.GenericUnionSerializationApplicability +
                    "; runtime generic coverage remains in CorePlayerProbe.");
            }

            require(asset.graphRoot != null && rich.graph != null &&
                ReferenceEquals(asset.graphRoot, rich.graph),
                "Player did not preserve managed-reference graph sharing.");
            if (asset.graphRoot != null)
            {
                require(asset.graphRoot.next != null && asset.graphRoot.next.next != null &&
                    ReferenceEquals(asset.graphRoot, asset.graphRoot.next.next),
                    "Player did not preserve the managed-reference cycle.");
            }

            var alias = Resources.Load<SerializedUnionAsset>(AliasResource);
            require(alias != null, "Player could not load the former-name alias asset.");
            if (alias != null)
            {
                if (alias.alias.kind != SerializedAliasUnion.Kind.Current)
                    throw new InvalidOperationException("Player did not reconstruct the former-name alias variant.");
                require(alias.alias.current.amount == 99 && alias.alias.current.label == "alias",
                    "Player did not reconstruct the former-name alias payload.");
            }

            var invalid = Resources.Load<SerializedUnionAsset>(NullResource);
            if (invalid != null)
            {
                var persistedFallback = false;
                if (invalid.shape.kind != SerializedShape.Kind.Move)
                {
                    Debug.Log("SerializedUnionPlayerProbe: invalid-payload asset was canonicalized to a non-Move variant; native invalid-asset load not claimed.");
                }
                else
                {
                    persistedFallback = invalid.shape.move.destination == Vector2.zero && invalid.shape.move.speed == 0f;
                }
                if (persistedFallback)
                    Debug.Log("SerializedUnionPlayerProbe: persisted invalid-payload fallback loaded.");
                else if (invalid.shape.kind == SerializedShape.Kind.Move)
                    Debug.Log("SerializedUnionPlayerProbe: invalid-payload asset was canonicalized during build; native invalid-asset load not claimed.");
            }
            else
                Debug.Log("SerializedUnionPlayerProbe: invalid-payload asset was unavailable after build; native invalid-asset load not claimed.");

            CheckDirectInvalidPayloadFallback(require);
        }

        private static void CheckDirectInvalidPayloadFallback(Action<bool, string> require)
        {
            var fallback = SerializedShape.__SpireFromSerialized(null);
            if (fallback.kind != SerializedShape.Kind.Move)
            {
                require(false, "Direct invalid-payload recovery did not select the first variant.");
                return;
            }

            require(fallback.move.destination == Vector2.zero && fallback.move.speed == 0f,
                "Direct invalid-payload recovery did not use default first-variant fields.");
        }
    }

}
