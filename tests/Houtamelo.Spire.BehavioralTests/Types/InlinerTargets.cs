using System;
using System.Collections.Generic;
using Houtamelo.Spire;

namespace Spire.BehavioralTests.Types;

public static partial class InlinerTargets
{
    [InlinerStruct]
    public static int Double(int x) => x * 2;

    [InlinerStruct]
    public static void Accumulate(List<int> bucket, int x) => bucket.Add(x * 10);

    [InlinerStruct]
    public static void LogNamed(string name, int value)
    {
        // empty body; test just checks the forwarder compiles and calls through
    }
}

public static partial class InlinerHosts
{
    public static T Apply<T, TF>(T seed, TF f) where TF : struct, IFuncInliner<T, T>
        => f.Invoke(seed);

    public static void ForEach<T>(
        List<T> list,
        [Inlinable] Action<T> action)
    {
        foreach (var item in list)
            action(item);
    }

    public static int Reduce<T>(
        List<T> list,
        int seed,
        [Inlinable] Func<int, T, int> fold)
    {
        var acc = seed;
        foreach (var item in list)
            acc = fold(acc, item);
        return acc;
    }
}
