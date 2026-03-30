using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace Houtamelo.Spire.Benchmarks.Benchmarks;

/// Reports sizeof() for every union type. The "benchmark" is a dummy; the value is in GlobalSetup output.
[Config(typeof(SpireConfig))]
public class SizeReportBenchmarks
{
    [GlobalSetup]
    public void Setup()
    {
        Console.WriteLine("── Event (mixed managed/unmanaged) ──");
        Console.WriteLine($"  EventAdditive        = {Unsafe.SizeOf<Types.EventAdditive>()} bytes");
        Console.WriteLine($"  EventBoxedFields     = {Unsafe.SizeOf<Types.EventBoxedFields>()} bytes");
        Console.WriteLine($"  EventBoxedTuple      = {Unsafe.SizeOf<Types.EventBoxedTuple>()} bytes");
        Console.WriteLine($"  EventOverlap         = {Unsafe.SizeOf<Types.EventOverlap>()} bytes");
        Console.WriteLine($"  EventUnsafeOverlap   = {Unsafe.SizeOf<Types.EventUnsafeOverlap>()} bytes");
        Console.WriteLine($"  EventRecord (ref)    = {IntPtr.Size} bytes");
        Console.WriteLine($"  EventNative          = {Unsafe.SizeOf<Types.EventNative>()} bytes");
        Console.WriteLine();
        Console.WriteLine("── Physics (all unmanaged) ──");
        Console.WriteLine($"  PhysicsAdditive      = {Unsafe.SizeOf<Types.PhysicsAdditive>()} bytes");
        Console.WriteLine($"  PhysicsBoxedFields   = {Unsafe.SizeOf<Types.PhysicsBoxedFields>()} bytes");
        Console.WriteLine($"  PhysicsBoxedTuple    = {Unsafe.SizeOf<Types.PhysicsBoxedTuple>()} bytes");
        Console.WriteLine($"  PhysicsOverlap       = {Unsafe.SizeOf<Types.PhysicsOverlap>()} bytes");
        Console.WriteLine($"  PhysicsUnsafeOverlap = {Unsafe.SizeOf<Types.PhysicsUnsafeOverlap>()} bytes");
        Console.WriteLine($"  PhysicsRecord (ref)  = {IntPtr.Size} bytes");
        Console.WriteLine($"  PhysicsNative        = {Unsafe.SizeOf<Types.PhysicsNative>()} bytes");
    }

    [Benchmark]
    public int Dummy() => 42;
}
