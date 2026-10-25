#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Spire.UnityIntegration
{

    public sealed class UnityIntegrationPlayerHook : MonoBehaviour
    {
        public bool managedReferenceOnly;
        public UnityIntegrationFixtureProfile fixtureProfile = UnityIntegrationFixtureProfile.Full;
        public string expectedBackend = string.Empty;

        public const string ExpectedLabel = "player";
        public const string ExpectedBoxedFieldsLabel = "boxed-player";
        public const int ExpectedBoxedTupleValue = 26;
        public const int ExpectedBoxedGenericValue = 42;
        public const string ExpectedBoxedGenericLabel = "boxed-generic-player";
        public const uint ExpectedUnsafeRaw = 123;
        public const float ExpectedUnsafeAmount = 2.5f;
        public const string ExpectedUnsafeLabel = "unsafe-player";
        public const int ExpectedUnsafeGenericValue = 37;
        public const uint ExpectedUnsafeGenericCode = 19;
        public const float ExpectedUnsafeGenericWeight = 1.75f;
        public const string ExpectedUnsafeGenericLabel = "unsafe-generic-player";
        public const float ExpectedOverlapWidth = 2.5f;
        public const float ExpectedOverlapHeight = 4.5f;
        public const string ExpectedOverlapLabel = "overlap-player";
        public const string ExpectedStabilityLabel = "stability-player";
        public const string ExpectedStabilityNestedLabel = "stability-nested-player";
        public const string ExpectedStabilityArrayLabel = "stability-array-player";
        public const string ExpectedStabilityListLabel = "stability-list-player";
        public const string ExpectedStabilityGenericString = "stability-generic-player";
        public const int ExpectedStabilityGenericInt = 73;
        public const int ExpectedStabilityMarker = 91;
        public const int ExpectedLegacyBoxedFieldsValue = 81;
        public const int ExpectedLegacyBoxedTupleValue = 82;
        public const int ExpectedLegacyUnsafeOverlapValue = 83;
        public const int ExpectedLegacyOverlapValue = 84;
        public const string ExpectedLegacyBoxedFieldsLabel = "legacy-boxed-fields-player";
        public const string ExpectedLegacyBoxedTupleLabel = "legacy-boxed-tuple-player";
        public const string ExpectedLegacyUnsafeOverlapLabel = "legacy-unsafe-overlap-player";
        public const string ExpectedLegacyOverlapLabel = "legacy-overlap-player";
        public const string ReportArgument = "-spire-report";
        public const string ReportFileName = "spire-unity-integration-player.json";

        private void Start()
        {
            var host = GetComponent<UnitySerializationMono>();
            var mainFixturePresent = host != null;
            var stabilityFixturePresent = GameObject.Find("SpireStabilityPlayerHost") != null;
            var managedReferenceFixturePresent = GameObject.Find("SpireManagedReferencePlayerHost") != null;
            var profileKnown = Enum.IsDefined(typeof(UnityIntegrationFixtureProfile), fixtureProfile);
            var expectedMainFixture = ProfileExpectsMainFixture(fixtureProfile);
            var expectedStabilityFixture = ProfileExpectsStabilityFixture(fixtureProfile);
            var expectedManagedReferenceFixture = ProfileExpectsManagedReferenceFixture(fixtureProfile);
            var loaded = host != null;
            var kind = loaded ? host!.single.kind.ToString() : "MissingHost";
#if ENABLE_IL2CPP
            const string backend = "IL2CPP";
#else
            const string backend = "Mono";
#endif
            var label = string.Empty;
            var boxedFieldsLabel = string.Empty;
            var boxedTupleValue = 0;
            var boxedGenericValue = 0;
            var boxedGenericLabel = string.Empty;
            var unsafeRaw = 0u;
            var unsafeAmount = 0f;
            var unsafeLabel = string.Empty;
            var unsafeGenericValue = 0;
            var unsafeGenericCode = 0u;
            var unsafeGenericWeight = 0f;
            var unsafeGenericLabel = string.Empty;
            var overlapWidth = 0f;
            var overlapHeight = 0f;
            var overlapLabel = string.Empty;
            if (host != null)
            {
                var union = host.single;
                if (union.kind == UnityShapeSerializable.Kind.Label)
                    label = union.label.text;
                var boxedFields = host.boxedFields;
                if (boxedFields.kind == UnityBoxedFieldsUnion.Kind.Text)
                    boxedFieldsLabel = boxedFields.text.label;
                var boxedTuple = host.boxedTuple;
                if (boxedTuple.kind == UnityBoxedTupleUnion.Kind.Number)
                    boxedTupleValue = boxedTuple.number.value;
                var boxedGeneric = host.boxedFieldsGenericInt;
                if (boxedGeneric.kind == UnityBoxedFieldsGeneric<int>.Kind.Some)
                    boxedGenericValue = boxedGeneric.some.value;
                var boxedGenericString = host.boxedTupleGenericString;
                if (boxedGenericString.kind == UnityBoxedTupleGeneric<string>.Kind.Some)
                    boxedGenericLabel = boxedGenericString.some.value;
                var unsafeUnion = host.unsafeUnion;
                if (unsafeUnion.kind == UnityUnsafeOverlapUnion.Kind.Mixed)
                {
                    unsafeRaw = unsafeUnion.mixed.raw;
                    unsafeAmount = unsafeUnion.mixed.amount;
                    unsafeLabel = unsafeUnion.mixed.label;
                }
                var unsafeInt = host.unsafeGenericInt;
                if (unsafeInt.kind == UnityUnsafeOverlapGeneric<int>.Kind.Some)
                {
                    unsafeGenericValue = unsafeInt.some.value;
                    unsafeGenericCode = unsafeInt.some.code;
                    unsafeGenericWeight = unsafeInt.some.weight;
                }
                var unsafeString = host.unsafeGenericString;
                if (unsafeString.kind == UnityUnsafeOverlapGeneric<string>.Kind.Some)
                    unsafeGenericLabel = unsafeString.some.value;
                var overlap = host.overlapUnion;
                if (overlap.kind == UnityOverlapUnion.Kind.Rectangle)
                {
                    overlapWidth = overlap.rectangle.width;
                    overlapHeight = overlap.rectangle.height;
                }
                var overlapReference = host.overlapReference;
                if (overlapReference.kind == UnityOverlapReferenceUnion.Kind.Text)
                    overlapLabel = overlapReference.text.label;
            }
            var stability = ProbeStabilityPlayer();
            var managedReferences = ProbeManagedReferencePlayer();
            var report = new PlayerReport
            {
                unityVersion = Application.unityVersion,
                backend = backend,
                runtimePlatform = Application.platform.ToString(),
                loaded = loaded,
                kind = kind,
                label = label,
                boxedFieldsLabel = boxedFieldsLabel,
                boxedTupleValue = boxedTupleValue,
                boxedGenericValue = boxedGenericValue,
                boxedGenericLabel = boxedGenericLabel,
                unsafeRaw = unsafeRaw,
                unsafeAmount = unsafeAmount,
                unsafeLabel = unsafeLabel,
                unsafeGenericValue = unsafeGenericValue,
                unsafeGenericCode = unsafeGenericCode,
                unsafeGenericWeight = unsafeGenericWeight,
                unsafeGenericLabel = unsafeGenericLabel,
                overlapWidth = overlapWidth,
                overlapHeight = overlapHeight,
                overlapLabel = overlapLabel,
                stability = stability,
                managedReferences = managedReferences,
                runtimeAssembly = typeof(UnityShapeSerializable).Assembly.FullName ?? string.Empty,
                fixtureProfile = fixtureProfile.ToString(),
                expectedBackend = expectedBackend,
                profileKnown = profileKnown,
                expectedMainFixture = expectedMainFixture,
                expectedStabilityFixture = expectedStabilityFixture,
                expectedManagedReferenceFixture = expectedManagedReferenceFixture,
                mainFixturePresent = mainFixturePresent,
                stabilityFixturePresent = stabilityFixturePresent,
                managedReferenceFixturePresent = managedReferenceFixturePresent,
                actualBackendMatchesExpected = string.Equals(backend, expectedBackend, StringComparison.Ordinal),
            };

            var path = ResolveReportPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            var mainPresenceValid = expectedMainFixture
                ? mainFixturePresent
                : !mainFixturePresent;
            var stabilityPresenceValid = expectedStabilityFixture
                ? stabilityFixturePresent
                : !stabilityFixturePresent;
            var managedReferencePresenceValid = expectedManagedReferenceFixture
                ? managedReferenceFixturePresent
                : !managedReferenceFixturePresent;
            var mainValid = !expectedMainFixture ||
                (report.loaded &&
                    kind == UnityShapeSerializable.Kind.Label.ToString() &&
                    label == ExpectedLabel &&
                    boxedFieldsLabel == ExpectedBoxedFieldsLabel &&
                    boxedTupleValue == ExpectedBoxedTupleValue &&
                    boxedGenericValue == ExpectedBoxedGenericValue &&
                    boxedGenericLabel == ExpectedBoxedGenericLabel &&
                    unsafeRaw == ExpectedUnsafeRaw &&
                    unsafeAmount == ExpectedUnsafeAmount &&
                    unsafeLabel == ExpectedUnsafeLabel &&
                    unsafeGenericValue == ExpectedUnsafeGenericValue &&
                    unsafeGenericCode == ExpectedUnsafeGenericCode &&
                    unsafeGenericWeight == ExpectedUnsafeGenericWeight &&
                    unsafeGenericLabel == ExpectedUnsafeGenericLabel &&
                    overlapWidth == ExpectedOverlapWidth &&
                    overlapHeight == ExpectedOverlapHeight &&
                    overlapLabel == ExpectedOverlapLabel);
            var stabilityValid = !expectedStabilityFixture ||
                (stability.valuesValid &&
                    stability.cloneValuesValid &&
                    stability.sourceCacheCleared &&
                    stability.sourcePayloadCreatedForFirstClone &&
                    stability.sourcePayloadReusedAcrossClones &&
                    stability.cloneCacheCleared &&
                    stability.cloneDoesNotRetainSourcePayload &&
                    stability.reconstructionMarkerShared &&
                    stability.cloneAfterDeserializeCount > 0 &&
                    stability.reflectionSurfaceAvailable &&
                    stability.copyMutationIsolationVerified &&
                    stability.legacyPayloadValuesValid &&
                    stability.cloneLegacyPayloadValuesValid &&
                    stability.lifetimeProbeValuesValid &&
                    stability.lifetimeProbeCacheCleared &&
                    stability.callbackPayloadCaptured &&
                    stability.callbackPayloadCollected);
            var managedReferenceValid = !expectedManagedReferenceFixture || managedReferences.valid;
            var profileValid = profileKnown &&
                mainPresenceValid &&
                stabilityPresenceValid &&
                managedReferencePresenceValid &&
                report.actualBackendMatchesExpected;
            report.mainPresenceValid = mainPresenceValid;
            report.stabilityPresenceValid = stabilityPresenceValid;
            report.managedReferencePresenceValid = managedReferencePresenceValid;
            report.mainAssertionsValid = mainValid;
            report.stabilityAssertionsValid = stabilityValid;
            report.managedReferenceAssertionsValid = managedReferenceValid;
            report.profileValid = profileValid;
            report.requiredAssertionsValid = profileValid && mainValid && stabilityValid && managedReferenceValid;
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            var valid = report.requiredAssertionsValid;
            Application.Quit(valid ? 0 : 1);
        }

        private static bool ProfileExpectsMainFixture(UnityIntegrationFixtureProfile profile)
            => profile == UnityIntegrationFixtureProfile.Full ||
                profile == UnityIntegrationFixtureProfile.BaselineOnly ||
                profile == UnityIntegrationFixtureProfile.MainBaselineAndManaged;

        private static bool ProfileExpectsStabilityFixture(UnityIntegrationFixtureProfile profile)
            => profile == UnityIntegrationFixtureProfile.Full ||
                profile == UnityIntegrationFixtureProfile.BaselineOnly ||
                profile == UnityIntegrationFixtureProfile.StabilityBaselineAndManaged;

        private static bool ProfileExpectsManagedReferenceFixture(UnityIntegrationFixtureProfile profile)
            => profile == UnityIntegrationFixtureProfile.Full ||
                profile == UnityIntegrationFixtureProfile.ManagedReferenceOnly ||
                profile == UnityIntegrationFixtureProfile.ManagedReferenceNullOnly ||
                profile == UnityIntegrationFixtureProfile.ManagedReferenceSharedOnly ||
                profile == UnityIntegrationFixtureProfile.ManagedReferenceCycleOnly ||
                profile == UnityIntegrationFixtureProfile.MainBaselineAndManaged ||
                profile == UnityIntegrationFixtureProfile.StabilityBaselineAndManaged;

        private static StabilityPlayerProbeResult ProbeStabilityPlayer()
        {
            var result = new StabilityPlayerProbeResult();
            var sourceObject = GameObject.Find("SpireStabilityPlayerHost");
            var source = sourceObject == null
                ? null
                : sourceObject.GetComponent<StabilityPlayerHost>();
            if (source == null)
            {
                result.error = "SpireStabilityPlayerHost was unavailable.";
                return result;
            }

            result.sourceAfterDeserializeCount = source.afterDeserializeCount;
            result.valuesValid = StabilityValuesMatch(source);
            result.sourceCacheCleared = StabilityCachesCleared(source);
            result.legacyPayloadValuesValid = LegacyPayloadValuesMatch(source);

            var sourcePayloadBeforeFirstClone = GetMemberValue(source.additive, "_serializedPayload");
            var firstCloneObject = UnityEngine.Object.Instantiate(sourceObject);
            var firstClone = firstCloneObject.GetComponent<StabilityPlayerHost>();
            if (firstClone == null)
            {
                UnityEngine.Object.Destroy(firstCloneObject);
                result.error = "The first stability player clone did not contain StabilityPlayerHost.";
                return result;
            }

            var sourcePayloadAfterFirstClone = GetMemberValue(source.additive, "_serializedPayload");
            var firstClonePayload = GetMemberValue(firstClone.additive, "_serializedPayload");
            result.sourcePayloadBeforeCloneId = Identity(sourcePayloadBeforeFirstClone);
            result.sourcePayloadAfterCloneId = Identity(sourcePayloadAfterFirstClone);
            result.sourcePayloadCreatedForFirstClone =
                IsReconstructionMarker(source.additive, sourcePayloadBeforeFirstClone) &&
                sourcePayloadAfterFirstClone != null &&
                !IsReconstructionMarker(source.additive, sourcePayloadAfterFirstClone);
            result.cloneAfterDeserializeCount = firstClone.afterDeserializeCount;
            result.cloneBeforeSerializeCount = firstClone.beforeSerializeCount;
            result.cloneValuesValid = StabilityValuesMatch(firstClone);
            result.cloneCacheCleared = StabilityCachesCleared(firstClone);
            result.cloneDoesNotRetainSourcePayload = sourcePayloadAfterFirstClone != null &&
                firstClonePayload != null &&
                !ReferenceEquals(sourcePayloadAfterFirstClone, firstClonePayload) &&
                IsReconstructionMarker(firstClone.additive, firstClonePayload) &&
                result.cloneCacheCleared;
            result.cloneLegacyPayloadValuesValid = LegacyPayloadValuesMatch(firstClone);
            result.reflectionSurfaceAvailable = HasCacheReflection(source.additive) &&
                HasCacheReflection(firstClone.additive);
            var sourceParentLabel = source.parent.child.child.text.label;
            firstClone.parent = StabilityNestedParentUnion.Child(
                StabilityNestedChildUnion.Text(99, "clone-only"),
                ExpectedStabilityMarker + 99);
            var cloneArray = firstClone.parentArray;
            cloneArray[0] = StabilityNestedParentUnion.Child(
                StabilityNestedChildUnion.Text(100, "clone-array-only"),
                ExpectedStabilityMarker + 100);
            firstClone.parentArray = cloneArray;
            result.copyMutationIsolationVerified =
                source.parent.child.child.text.label == sourceParentLabel &&
                firstClone.parent.child.child.text.label == "clone-only" &&
                source.parentArray[0].child.child.text.label == ExpectedStabilityArrayLabel &&
                firstClone.parentArray[0].child.child.text.label == "clone-array-only";

            var sourcePayloadBeforeSecondClone = GetMemberValue(source.additive, "_serializedPayload");
            var secondCloneObject = UnityEngine.Object.Instantiate(sourceObject);
            var secondClone = secondCloneObject.GetComponent<StabilityPlayerHost>();
            if (secondClone == null)
            {
                UnityEngine.Object.Destroy(secondCloneObject);
                UnityEngine.Object.Destroy(firstCloneObject);
                result.error = "The second stability player clone did not contain StabilityPlayerHost.";
                return result;
            }

            var secondClonePayload = GetMemberValue(secondClone.additive, "_serializedPayload");
            result.reconstructionMarkerShared =
                IsReconstructionMarker(firstClone.additive, firstClonePayload) &&
                IsReconstructionMarker(secondClone.additive, secondClonePayload) &&
                ReferenceEquals(firstClonePayload, secondClonePayload);
            var sourcePayloadAfterSecondClone = GetMemberValue(source.additive, "_serializedPayload");
            result.sourcePayloadBeforeSecondCloneId = Identity(sourcePayloadBeforeSecondClone);
            result.sourcePayloadAfterSecondCloneId = Identity(sourcePayloadAfterSecondClone);
            result.sourcePayloadReusedAcrossClones = sourcePayloadAfterFirstClone != null &&
                sourcePayloadBeforeSecondClone != null &&
                sourcePayloadAfterSecondClone != null &&
                ReferenceEquals(sourcePayloadAfterFirstClone, sourcePayloadBeforeSecondClone) &&
                ReferenceEquals(sourcePayloadAfterFirstClone, sourcePayloadAfterSecondClone);

            firstClone.RunLifetimeWeakReferenceProbe();
            result.lifetimeProbeAmount = firstClone.lifetimeProbeAmount;
            result.lifetimeProbeLabel = firstClone.lifetimeProbeLabel;
            result.lifetimeProbeValuesValid = firstClone.lifetimeProbeValuesValid;
            result.lifetimeProbeCacheCleared = firstClone.lifetimeProbeCacheCleared;
            result.callbackPayloadCount = firstClone.callbackPayloadWeakReferences.Length;
            result.callbackPayloadCaptured = firstClone.callbackPayloadWeakReferences.Length > 0 &&
                firstClone.lifetimeProbeValuesValid &&
                firstClone.lifetimeProbeCacheCleared;
            var callbackPayloadWeakReferences = firstClone.callbackPayloadWeakReferences;
            sourcePayloadBeforeFirstClone = null;
            sourcePayloadAfterFirstClone = null;
            firstClonePayload = null;
            secondClonePayload = null;
            sourcePayloadBeforeSecondClone = null;
            sourcePayloadAfterSecondClone = null;
            UnityEngine.Object.Destroy(secondCloneObject);
            result.callbackPayloadCollected = CollectWeakPayloadReferences(callbackPayloadWeakReferences);
            UnityEngine.Object.Destroy(firstCloneObject);
            return result;
        }

        private static ManagedReferencePlayerProbeResult ProbeManagedReferencePlayer()
        {
            var result = new ManagedReferencePlayerProbeResult();
            var sourceObject = GameObject.Find("SpireManagedReferencePlayerHost");
            var source = sourceObject == null
                ? null
                : sourceObject.GetComponent<UnityManagedReferencePlayerHost>();
            if (source == null)
            {
                result.error = "SpireManagedReferencePlayerHost was unavailable.";
                return result;
            }

            result.metadataAvailable = source.managedReferenceMetadataAvailable;
            if (!result.metadataAvailable)
            {
                result.error = "Generated managed-reference payload metadata was unavailable; cyclic graph preparation was not attempted.";
                return result;
            }

            result.valuesValid = ManagedReferenceValuesMatch(source.data);
            result.nullValuesValid = ManagedReferenceNullValuesMatch(source.second);
            result.sharedIdentityValid = source.data.nodeArray.Length == 3 &&
                ReferenceEquals(source.data.nodeArray[0], source.data.nodeArray[2]) &&
                source.data.nodeArray[1] == null &&
                source.data.nodeList.Count == 3 &&
                ReferenceEquals(source.data.nodeList[0], source.data.nodeList[2]) &&
                source.data.nodeList[1] == null;
            result.separateHostIdentityValid = source.data.sharedNode != null &&
                source.second.sharedNode == null &&
                !ReferenceEquals(source.data.sharedNode, source.second.sharedNode);
            result.helperValid = source.data.helper.Node is UnityManagedReferenceNodeLeaf helperNode &&
                helperNode.id == "cycle-helper-node" &&
                source.data.helper.Opaque is UnityManagedReferenceNodeOther &&
                source.data.helper.child.kind == UnityManagedReferenceAdditiveUnion.Kind.Reference &&
                ReferenceEquals(source.data.helper.child.reference.value, source.data.helper.Node);
            result.cycleValid = source.managedReferenceMetadataAvailable &&
                source.data.cycleRoot != null &&
                ReferenceEquals(source.data.cycleRoot!.next!.next, source.data.cycleRoot) &&
                source.data.helper.Next != null &&
                ReferenceEquals(source.data.helper.Next!.Next, source.data.helper);
            result.cacheStateValid = ManagedReferenceCachesCleared(source.data);
            result.legacyAliasValid = ManagedReferenceLegacyAliasMatches(source.opaqueLegacyRenamed);

            var cloneObject = UnityEngine.Object.Instantiate(sourceObject);
            var clone = cloneObject.GetComponent<UnityManagedReferencePlayerHost>();
            if (clone == null)
            {
                UnityEngine.Object.Destroy(cloneObject);
                result.error = "The managed-reference clone did not contain UnityManagedReferencePlayerHost.";
                return result;
            }

            result.cloneValuesValid = ManagedReferenceValuesMatch(clone.data);
            result.cloneNullValuesValid = ManagedReferenceNullValuesMatch(clone.second);
            result.cloneSeparateHostIdentityValid = source.data.sharedNode != null &&
                clone.data.sharedNode != null &&
                !ReferenceEquals(source.data.sharedNode, clone.data.sharedNode) &&
                source.data.dynamicNode != null &&
                clone.data.dynamicNode != null &&
                !ReferenceEquals(source.data.dynamicNode, clone.data.dynamicNode);
            result.cloneCacheStateValid = ManagedReferenceCachesCleared(clone.data);
            var commonValid = result.metadataAvailable &&
                result.helperValid &&
                result.cacheStateValid &&
                result.legacyAliasValid &&
                result.cloneCacheStateValid;
            result.valid = source.probeMode switch
            {
                UnityManagedReferencePlayerHost.NullOnlyProbe => commonValid &&
                    result.nullValuesValid &&
                    result.cloneNullValuesValid,
                UnityManagedReferencePlayerHost.SharedOnlyProbe => commonValid &&
                    result.valuesValid &&
                    result.nullValuesValid &&
                    result.sharedIdentityValid &&
                    result.separateHostIdentityValid &&
                    result.cloneValuesValid &&
                    result.cloneNullValuesValid &&
                    result.cloneSeparateHostIdentityValid,
                UnityManagedReferencePlayerHost.CycleOnlyProbe => commonValid &&
                    result.valuesValid &&
                    result.nullValuesValid &&
                    result.sharedIdentityValid &&
                    result.separateHostIdentityValid &&
                    result.cycleValid &&
                    result.cloneValuesValid &&
                    result.cloneNullValuesValid &&
                    result.cloneSeparateHostIdentityValid,
                _ => commonValid &&
                    result.valuesValid &&
                    result.nullValuesValid &&
                    result.sharedIdentityValid &&
                    result.separateHostIdentityValid &&
                    result.cycleValid &&
                    result.cloneValuesValid &&
                    result.cloneNullValuesValid &&
                    result.cloneSeparateHostIdentityValid,
            };
            UnityEngine.Object.Destroy(cloneObject);
            return result;
        }

        private static bool ManagedReferenceValuesMatch(UnityManagedReferenceHostData data)
            => data.additive.kind == UnityManagedReferenceAdditiveUnion.Kind.Reference &&
                data.additive.reference.value is UnityManagedReferenceNodeLeaf additiveLeaf &&
                additiveLeaf.id == "player-shared" &&
                additiveLeaf.amount == 61 &&
                data.boxedFields.kind == UnityManagedReferenceBoxedFieldsUnion.Kind.Reference &&
                ReferenceEquals(data.boxedFields.reference.value, data.sharedNode) &&
                data.boxedTuple.kind == UnityManagedReferenceBoxedTupleUnion.Kind.Reference &&
                ReferenceEquals(data.boxedTuple.reference.value, data.sharedNode) &&
                data.unsafeOverlap.kind == UnityManagedReferenceUnsafeOverlapUnion.Kind.Reference &&
                ReferenceEquals(data.unsafeOverlap.reference.value, data.sharedNode) &&
                data.overlap.kind == UnityManagedReferenceOverlapUnion.Kind.Reference &&
                ReferenceEquals(data.overlap.reference.value, data.sharedNode) &&
                data.renamed.kind == UnityManagedReferenceRenamedUnion.Kind.Reference &&
                ReferenceEquals(data.renamed.reference.value, data.sharedNode) &&
                data.genericBase.kind == UnityManagedReferenceGeneric<UnityManagedReferenceNodeBase>.Kind.Value &&
                ReferenceEquals(data.genericBase.value.value, data.sharedNode) &&
                data.genericInterface.kind == UnityManagedReferenceGeneric<IUnityManagedReferenceNode>.Kind.Value &&
                ReferenceEquals(data.genericInterface.value.value, data.sharedNode) &&
                data.genericDerived.kind == UnityManagedReferenceGeneric<UnityManagedReferenceNodeLeaf>.Kind.Value &&
                ReferenceEquals(data.genericDerived.value.value, data.sharedNode) &&
                data.baseInterface.kind == UnityManagedReferenceBaseInterfaceUnion.Kind.Base &&
                ReferenceEquals(data.baseInterface.@base.baseValue, data.sharedNode) &&
                data.generatedStateNode != null &&
                data.generatedStateNode.state.kind == UnityManagedReferenceNodeStateUnion.Kind.Active &&
                data.generatedStateNode.state.active.marker == 303 &&
                data.generatedStateNode.ObjectHolder is UnityManagedReferenceNodeOther other &&
                other.id == "helper-opaque" &&
                ReferenceEquals(data.generatedStateNode.InterfaceHolder, data.sharedNode) &&
                data.dynamicNode is UnityManagedReferenceNodeGeneratedState dynamicNode &&
                ReferenceEquals(dynamicNode, data.generatedStateNode);

        private static bool ManagedReferenceNullValuesMatch(UnityManagedReferenceHostData data)
            => data.additive.kind == UnityManagedReferenceAdditiveUnion.Kind.Reference &&
                data.additive.reference.value == null &&
                data.boxedFields.kind == UnityManagedReferenceBoxedFieldsUnion.Kind.Reference &&
                data.boxedFields.reference.value == null &&
                data.boxedTuple.kind == UnityManagedReferenceBoxedTupleUnion.Kind.Reference &&
                data.boxedTuple.reference.value == null &&
                data.unsafeOverlap.kind == UnityManagedReferenceUnsafeOverlapUnion.Kind.Reference &&
                data.unsafeOverlap.reference.value == null &&
                data.overlap.kind == UnityManagedReferenceOverlapUnion.Kind.Reference &&
                data.overlap.reference.value == null;

        private static bool ManagedReferenceLegacyAliasMatches(object? payload)
        {
            var restored = UnityManagedReferenceRenamedUnion.__SpireFromSerialized(payload);
            return restored.kind == UnityManagedReferenceRenamedUnion.Kind.Reference &&
                restored.reference.value is UnityManagedReferenceNodeLeaf leaf &&
                leaf.id == "player-shared" &&
                leaf.amount == 61;
        }

        private static bool ManagedReferenceCachesCleared(UnityManagedReferenceHostData data)
            => UnionCacheCleared(data.additive) &&
                UnionCacheCleared(data.boxedFields) &&
                UnionCacheCleared(data.boxedTuple) &&
                UnionCacheCleared(data.unsafeOverlap) &&
                UnionCacheCleared(data.overlap) &&
                UnionCacheCleared(data.renamed) &&
                UnionCacheCleared(data.genericBase) &&
                UnionCacheCleared(data.genericInterface) &&
                UnionCacheCleared(data.genericDerived) &&
                UnionCacheCleared(data.baseInterface) &&
                data.generatedStateNode != null &&
                UnionCacheCleared(data.generatedStateNode.state) &&
                UnionCacheCleared(data.helper.child);

        private static bool CollectWeakPayloadReferences(WeakReference[] references)
        {
            if (references.Length == 0)
                return false;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return references.All(reference => !reference.IsAlive);
        }

        private static bool StabilityValuesMatch(StabilityPlayerHost host)
            => host.additive.kind == StabilityAdditiveUnion.Kind.First &&
                host.additive.first.amount == 11 &&
                host.additive.first.label == ExpectedStabilityLabel &&
                host.boxedFields.kind == StabilityBoxedFieldsUnion.Kind.First &&
                host.boxedFields.first.amount == 12 &&
                host.boxedFields.first.label == ExpectedStabilityLabel &&
                host.boxedTuple.kind == StabilityBoxedTupleUnion.Kind.First &&
                host.boxedTuple.first.amount == 13 &&
                host.boxedTuple.first.label == ExpectedStabilityLabel &&
                host.unsafeOverlap.kind == StabilityUnsafeOverlapUnion.Kind.First &&
                host.unsafeOverlap.first.amount == 14 &&
                host.unsafeOverlap.first.label == ExpectedStabilityLabel &&
                host.overlap.kind == StabilityOverlapUnion.Kind.First &&
                host.overlap.first.amount == 15 &&
                host.overlap.first.label == ExpectedStabilityLabel &&
                host.parent.kind == StabilityNestedParentUnion.Kind.Child &&
                host.parent.child.marker == ExpectedStabilityMarker &&
                host.parent.child.child.kind == StabilityNestedChildUnion.Kind.Text &&
                host.parent.child.child.text.count == 16 &&
                host.parent.child.child.text.label == ExpectedStabilityNestedLabel &&
                host.parentArray.Length == 2 &&
                host.parentArray[0].child.child.text.label == ExpectedStabilityArrayLabel &&
                host.parentArray[1].kind == StabilityNestedParentUnion.Kind.Empty &&
                host.parentList.Count == 2 &&
                host.parentList[0].child.child.text.label == ExpectedStabilityListLabel &&
                host.parentList[1].text.label == "stability-list-parent" &&
                host.genericInt.value.kind == StabilityGenericUnion<int>.Kind.First &&
                host.genericInt.value.first.value == ExpectedStabilityGenericInt &&
                host.genericString.value.kind == StabilityGenericUnion<string>.Kind.First &&
                host.genericString.value.first.value == ExpectedStabilityGenericString &&
                host.genericArray.Length == 2 &&
                host.genericArray[0].first.value == ExpectedStabilityGenericInt + 1 &&
                host.genericArray[1].kind == StabilityGenericUnion<int>.Kind.Empty &&
                host.callbackHelper.callbackMarker == 1 &&
                host.callbackHelper.child.kind == StabilityNestedChildUnion.Kind.Text &&
                host.callbackHelper.child.text.count == 19 &&
                host.callbackHelper.child.text.label == ExpectedStabilityNestedLabel;

        private static bool LegacyPayloadValuesMatch(StabilityPlayerHost host)
        {
            var formerPayloads = host.opaqueFormerPayloads;
            if (formerPayloads == null || formerPayloads.Length < 7)
                return false;

            var migration = StabilityMigrationUnion.__SpireFromSerialized(formerPayloads[0]);
            var generic = StabilityGenericUnion<int>.__SpireFromSerialized(formerPayloads[1]);
            var nested = StabilityNestedChildUnion.__SpireFromSerialized(formerPayloads[2]);
            var boxedFields = StabilityBoxedFieldsUnion.__SpireFromSerialized(formerPayloads[3]);
            var boxedTuple = StabilityBoxedTupleUnion.__SpireFromSerialized(formerPayloads[4]);
            var unsafeOverlap = StabilityUnsafeOverlapUnion.__SpireFromSerialized(formerPayloads[5]);
            var overlap = StabilityOverlapUnion.__SpireFromSerialized(formerPayloads[6]);
            return migration.kind == StabilityMigrationUnion.Kind.Renamed &&
                migration.renamed.value == 71 &&
                generic.kind == StabilityGenericUnion<int>.Kind.First &&
                generic.first.value == 72 &&
                nested.kind == StabilityNestedChildUnion.Kind.Text &&
                nested.text.count == 73 &&
                nested.text.label == "legacy-nested-player" &&
                boxedFields.kind == StabilityBoxedFieldsUnion.Kind.First &&
                boxedFields.first.amount == ExpectedLegacyBoxedFieldsValue &&
                boxedFields.first.label == ExpectedLegacyBoxedFieldsLabel &&
                boxedTuple.kind == StabilityBoxedTupleUnion.Kind.First &&
                boxedTuple.first.amount == ExpectedLegacyBoxedTupleValue &&
                boxedTuple.first.label == ExpectedLegacyBoxedTupleLabel &&
                unsafeOverlap.kind == StabilityUnsafeOverlapUnion.Kind.First &&
                unsafeOverlap.first.amount == ExpectedLegacyUnsafeOverlapValue &&
                unsafeOverlap.first.label == ExpectedLegacyUnsafeOverlapLabel &&
                overlap.kind == StabilityOverlapUnion.Kind.First &&
                overlap.first.amount == ExpectedLegacyOverlapValue &&
                overlap.first.label == ExpectedLegacyOverlapLabel;
        }

        private static bool StabilityCachesCleared(StabilityPlayerHost host)
            => UnionCacheCleared(host.additive) &&
                UnionCacheCleared(host.boxedFields) &&
                UnionCacheCleared(host.boxedTuple) &&
                UnionCacheCleared(host.unsafeOverlap) &&
                UnionCacheCleared(host.overlap) &&
                UnionCacheCleared(host.parent) &&
                UnionCacheCleared(host.callbackHelper.child) &&
                UnionCacheCleared(host.genericInt.value) &&
                UnionCacheCleared(host.genericString.value) &&
                host.genericArray.All(item => UnionCacheCleared(item)) &&
                host.parentArray.All(item => UnionCacheCleared(item)) &&
                host.parentList.TrueForAll(item => UnionCacheCleared(item));

        private static bool UnionCacheCleared<T>(T value)
            => HasCacheReflection(value) &&
                IsReconstructionMarker(value, GetMemberValue(value, "_serializedPayload"));

        private static bool IsReconstructionMarker<T>(T value, object? payload)
        {
            if (payload == null)
                return false;

            return !value!.GetType()
                .GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .Where(type => type.IsClass && !type.IsAbstract &&
                    type.IsDefined(typeof(SerializableAttribute), inherit: false))
                .Any(type => type.IsInstanceOfType(payload));
        }

        private static bool HasCacheReflection<T>(T value)
        {
            var type = value!.GetType();
            return FindField(type, "_serializedPayload") != null &&
                FindField(type, "__SpireSerializationOwner") == null &&
                FindField(type, "__SpireSerializationState") == null;
        }

        private static object? GetMemberValue<T>(T value, string name)
        {
            var field = FindField(value!.GetType(), name);
            return field?.GetValue(value);
        }

        private static FieldInfo? FindField(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(name, flags | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field;
            }

            return null;
        }

        private static int Identity(object? value)
            => value == null ? 0 : RuntimeHelpers.GetHashCode(value);

        private static string ResolveReportPath()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; index++)
            {
                if (!string.Equals(arguments[index], ReportArgument, StringComparison.Ordinal))
                    continue;

                if (index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index + 1]))
                    throw new InvalidOperationException(ReportArgument + " requires a report path.");

                return Path.GetFullPath(arguments[index + 1]);
            }

            return Path.Combine(Application.persistentDataPath, ReportFileName);
        }

        [Serializable]
        private sealed class PlayerReport
        {
            public string unityVersion = string.Empty;
            public string backend = string.Empty;
            public string runtimePlatform = string.Empty;
            public bool loaded;
            public string kind = string.Empty;
            public string label = string.Empty;
            public string boxedFieldsLabel = string.Empty;
            public int boxedTupleValue;
            public int boxedGenericValue;
            public string boxedGenericLabel = string.Empty;
            public uint unsafeRaw;
            public float unsafeAmount;
            public string unsafeLabel = string.Empty;
            public int unsafeGenericValue;
            public uint unsafeGenericCode;
            public float unsafeGenericWeight;
            public string unsafeGenericLabel = string.Empty;
            public float overlapWidth;
            public float overlapHeight;
            public string overlapLabel = string.Empty;
            public StabilityPlayerProbeResult stability = new StabilityPlayerProbeResult();
            public ManagedReferencePlayerProbeResult managedReferences = new ManagedReferencePlayerProbeResult();
            public string runtimeAssembly = string.Empty;
            public string fixtureProfile = string.Empty;
            public string expectedBackend = string.Empty;
            public bool profileKnown;
            public bool expectedMainFixture;
            public bool expectedStabilityFixture;
            public bool expectedManagedReferenceFixture;
            public bool mainFixturePresent;
            public bool stabilityFixturePresent;
            public bool managedReferenceFixturePresent;
            public bool actualBackendMatchesExpected;
            public bool mainPresenceValid;
            public bool stabilityPresenceValid;
            public bool managedReferencePresenceValid;
            public bool mainAssertionsValid;
            public bool stabilityAssertionsValid;
            public bool managedReferenceAssertionsValid;
            public bool profileValid;
            public bool requiredAssertionsValid;
        }

        [Serializable]
        private sealed class ManagedReferencePlayerProbeResult
        {
            public string error = string.Empty;
            public bool valid;
            public bool metadataAvailable;
            public bool valuesValid;
            public bool nullValuesValid;
            public bool sharedIdentityValid;
            public bool separateHostIdentityValid;
            public bool helperValid;
            public bool cycleValid;
            public bool cacheStateValid;
            public bool legacyAliasValid;
            public bool cloneValuesValid;
            public bool cloneNullValuesValid;
            public bool cloneSeparateHostIdentityValid;
            public bool cloneCacheStateValid;
        }

        [Serializable]
        private sealed class StabilityPlayerProbeResult
        {
            public string error = string.Empty;
            public int sourceAfterDeserializeCount;
            public int cloneAfterDeserializeCount;
            public int cloneBeforeSerializeCount;
            public int sourcePayloadBeforeCloneId;
            public int sourcePayloadAfterCloneId;
            public bool reflectionSurfaceAvailable;
            public bool valuesValid;
            public bool cloneValuesValid;
            public bool sourceCacheCleared;
            public bool cloneCacheCleared;
            public bool sourcePayloadCreatedForFirstClone;
            public bool sourcePayloadReusedAcrossClones;
            public bool cloneDoesNotRetainSourcePayload;
            public bool reconstructionMarkerShared;
            public int sourcePayloadBeforeSecondCloneId;
            public int sourcePayloadAfterSecondCloneId;
            public bool copyMutationIsolationVerified;
            public bool legacyPayloadValuesValid;
            public bool cloneLegacyPayloadValuesValid;
            public int lifetimeProbeAmount;
            public string lifetimeProbeLabel = string.Empty;
            public bool lifetimeProbeValuesValid;
            public bool lifetimeProbeCacheCleared;
            public int callbackPayloadCount;
            public bool callbackPayloadCaptured;
            public bool callbackPayloadCollected;
        }
    }

}
