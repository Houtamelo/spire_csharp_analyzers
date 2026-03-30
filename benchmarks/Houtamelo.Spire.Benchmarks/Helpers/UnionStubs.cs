// Required for C# 15 `union` keyword in .NET 11 Preview 2.
// The runtime doesn't include these yet — delete once a later preview ships them.

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public sealed class UnionAttribute : Attribute;

    public interface IUnion
    {
        object? Value { get; }
    }
}
