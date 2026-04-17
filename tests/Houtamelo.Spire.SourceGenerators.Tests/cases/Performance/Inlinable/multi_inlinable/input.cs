using System;
using Houtamelo.Spire;

namespace TestNs;

public static partial class Pipeline
{
    public static R Run<T, R>(T seed, [Inlinable] Func<T, T> pre, [Inlinable] Func<T, R> post)
        => post(pre(seed));
}
