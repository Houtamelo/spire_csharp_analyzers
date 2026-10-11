using System;

namespace Houtamelo.Spire;

internal static class UnityLinkerRoots
{
    // Unity 6000.5's native managed-reference loader calls this overload for
    // generic payloads, but High stripping does not retain that native dependency.
    [Preserve]
    private static Type? ResolveManagedReferenceType(string name) => Type.GetType(name);

    // Unity recognizes this name in precompiled libraries without a UnityEngine reference.
    [AttributeUsage(AttributeTargets.Method)]
    private sealed class PreserveAttribute : Attribute;
}
