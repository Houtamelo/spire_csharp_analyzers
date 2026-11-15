//@ codefix: Spire.Union.RemoveRecord
using System;
using Houtamelo.Spire;

[Serializable]
[DiscriminatedUnion]
public partial record struct GenericState<T> : IDisposable
    where T : unmanaged
{
    [Variant] public static partial GenericState<T> Value(T amount);

    public void Dispose()
    {
    }
}
