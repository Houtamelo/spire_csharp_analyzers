#nullable enable

using System;
using Houtamelo.Spire;

namespace Spire.UnityIntegration
{
    public partial struct UnityDrawerUnion
    {
        [Serializable]
        private sealed class InvalidDrawerPayload : UnitySerializationPayloadBase
        {
        }
    }
}
