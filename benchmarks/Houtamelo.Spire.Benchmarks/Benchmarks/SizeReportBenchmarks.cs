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
        Console.WriteLine($"  EventBench           = {Unsafe.SizeOf<Types.EventBench>()} bytes");
        Console.WriteLine($"  EventAuto            = {Unsafe.SizeOf<Types.EventAuto>()} bytes");
        Console.WriteLine($"  EventTyped           = {Unsafe.SizeOf<Types.EventTyped>()} bytes");
        Console.WriteLine($"  EventShared          = {Unsafe.SizeOf<Types.EventShared>()} bytes");
        Console.WriteLine($"  EventRecord (ref)    = {IntPtr.Size} bytes");
        Console.WriteLine($"  EventNative          = {Unsafe.SizeOf<Types.EventNative>()} bytes");
        Console.WriteLine();
        Console.WriteLine("── Physics (all unmanaged) ──");
        Console.WriteLine($"  PhysicsBench         = {Unsafe.SizeOf<Types.PhysicsBench>()} bytes");
        Console.WriteLine($"  PhysicsAuto          = {Unsafe.SizeOf<Types.PhysicsAuto>()} bytes");
        Console.WriteLine($"  PhysicsTyped         = {Unsafe.SizeOf<Types.PhysicsTyped>()} bytes");
        Console.WriteLine($"  PhysicsShared        = {Unsafe.SizeOf<Types.PhysicsShared>()} bytes");
        Console.WriteLine($"  PhysicsRecord (ref)  = {IntPtr.Size} bytes");
        Console.WriteLine($"  PhysicsNative        = {Unsafe.SizeOf<Types.PhysicsNative>()} bytes");
    }

    [Benchmark]
    public int Dummy() => 42;
}
