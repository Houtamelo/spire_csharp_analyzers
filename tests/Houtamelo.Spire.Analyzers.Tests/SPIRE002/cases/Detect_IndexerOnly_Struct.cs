//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is applied to a struct containing only an indexer and no fields.
[EnforceInitialization] //~ ERROR
public struct IndexerOnlyStruct
{
    public int this[int index] { get => index * 2; }
}
