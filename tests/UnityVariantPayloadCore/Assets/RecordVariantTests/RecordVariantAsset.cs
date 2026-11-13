#nullable enable

using System;
using UnityEngine;

namespace Spire.UnityRecordVariantTests
{
    [Serializable]
    public sealed class RecordVariantAsset : ScriptableObject
    {
        public RecordVariantUnion partialRecord;
        public RecordVariantUnion fullyAnnotated;
    public RecordVariantUnion explicitRecord;
        public RecordVariantUnion methodControl;
        public RecordVariantUnion fieldlessRecord;
        public RecordVariantUnion managedRecord;
        public RecordVariantUnion managedNullRecord;
        public RecordVariantUnion managedSharedRecord;
    }
}
