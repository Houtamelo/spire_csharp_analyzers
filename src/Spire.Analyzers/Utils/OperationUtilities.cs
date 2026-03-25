using Microsoft.CodeAnalysis;

namespace Spire.Analyzers.Utils;

public static class OperationUtilities
{
    /// <summary>
    /// Returns true if the operation is known at compile time to evaluate to zero.
    /// Currently uses Roslyn's ConstantValue API, which handles literals, const variables,
    /// and constant expressions. Can be extended with more sophisticated analysis later.
    /// </summary>
    public static bool IsKnownToBeZero(IOperation operation)
    {
        if (!operation.ConstantValue.HasValue)
            return false;

        return operation.ConstantValue.Value switch
        {
            int v => v == 0,
            uint v => v == 0,
            long v => v == 0,
            ulong v => v == 0,
            short v => v == 0,
            ushort v => v == 0,
            byte v => v == 0,
            sbyte v => v == 0,
            _ => false,
        };
    }
}
