using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace Spire.Benchmarks;

/// Reports sizeof() for every union type. The "benchmark" is a dummy; the value is in GlobalSetup output.
[Config(typeof(SpireConfig))]
public class SizeReportBenchmarks
{
    [GlobalSetup]
    public void Setup()
    {
        Console.WriteLine("── Event (mixed managed/unmanaged) ──");
        Console.WriteLine($"  EventAdditive        = {Unsafe.SizeOf<EventAdditive>()} bytes");
        Console.WriteLine($"  EventBoxedFields     = {Unsafe.SizeOf<EventBoxedFields>()} bytes");
        Console.WriteLine($"  EventBoxedTuple      = {Unsafe.SizeOf<EventBoxedTuple>()} bytes");
        Console.WriteLine($"  EventOverlap         = {Unsafe.SizeOf<EventOverlap>()} bytes");
        Console.WriteLine($"  EventUnsafeOverlap   = {Unsafe.SizeOf<EventUnsafeOverlap>()} bytes");
        Console.WriteLine($"  EventRecord (ref)    = {IntPtr.Size} bytes");
        Console.WriteLine($"  EventClass (ref)     = {IntPtr.Size} bytes");
        Console.WriteLine();
        Console.WriteLine("── Physics (all unmanaged) ──");
        Console.WriteLine($"  PhysicsAdditive      = {Unsafe.SizeOf<PhysicsAdditive>()} bytes");
        Console.WriteLine($"  PhysicsBoxedFields   = {Unsafe.SizeOf<PhysicsBoxedFields>()} bytes");
        Console.WriteLine($"  PhysicsBoxedTuple    = {Unsafe.SizeOf<PhysicsBoxedTuple>()} bytes");
        Console.WriteLine($"  PhysicsOverlap       = {Unsafe.SizeOf<PhysicsOverlap>()} bytes");
        Console.WriteLine($"  PhysicsUnsafeOverlap = {Unsafe.SizeOf<PhysicsUnsafeOverlap>()} bytes");
        Console.WriteLine($"  PhysicsRecord (ref)  = {IntPtr.Size} bytes");
        Console.WriteLine($"  PhysicsClass (ref)   = {IntPtr.Size} bytes");
    }

    [Benchmark]
    public int Dummy() => 42;
}
