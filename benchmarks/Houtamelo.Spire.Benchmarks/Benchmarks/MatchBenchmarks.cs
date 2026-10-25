using BenchmarkDotNet.Attributes;
using Houtamelo.Spire.Benchmarks.Helpers;

namespace Houtamelo.Spire.Benchmarks.Benchmarks;

[Config(typeof(SpireConfig))]
public class MatchBenchmarks
{
    [Params(BenchN.Default)]
    public int N { get; set; }

    [ParamsAllValues]
    public Distribution Dist { get; set; }

    private Types.PhysicsBench[] _auto = null!;
    private Types.PhysicsAuto[] _explicitAuto = null!;
    private Types.PhysicsTyped[] _typed = null!;
    private Types.PhysicsShared[] _shared = null!;
    private Types.PhysicsRecord[] _record = null!;
    private Types.PhysicsNative[] _native = null!;

    [GlobalSetup]
    public void Setup()
    {
        _auto = new Types.PhysicsBench[N];
        _explicitAuto = new Types.PhysicsAuto[N];
        _typed = new Types.PhysicsTyped[N];
        _shared = new Types.PhysicsShared[N];
        _record = new Types.PhysicsRecord[N];
        _native = new Types.PhysicsNative[N];
        ArrayFiller.Fill(_auto, new Random(42), Dist);
        ArrayFiller.Fill(_explicitAuto, new Random(42), Dist);
        ArrayFiller.Fill(_typed, new Random(42), Dist);
        ArrayFiller.Fill(_shared, new Random(42), Dist);
        ArrayFiller.Fill(_record, new Random(42), Dist);
        ArrayFiller.Fill(_native, new Random(42), Dist);
    }

    [BenchmarkCategory("Property"), Benchmark(Baseline = true)]
    public double Auto() => Sum(_auto);

    [BenchmarkCategory("Property"), Benchmark]
    public double ExplicitAuto() => Sum(_explicitAuto);

    [BenchmarkCategory("Property"), Benchmark]
    public double Typed() => Sum(_typed);

    [BenchmarkCategory("Property"), Benchmark]
    public double Shared() => Sum(_shared);

    [BenchmarkCategory("Property"), Benchmark]
    public double Record() => Sum(_record);

    [BenchmarkCategory("Property"), Benchmark]
    public double Native() => Sum(_native);

    [BenchmarkCategory("Payload Deconstruct"), Benchmark]
    public double PayloadDeconstruct()
    {
        double sum = 0;
        foreach (var value in _auto)
        {
            switch (value.kind)
            {
                case Types.PhysicsBench.Kind.Impulse:
                    value.impulse.Deconstruct(out var magnitude);
                    sum += magnitude;
                    break;
                case Types.PhysicsBench.Kind.Position:
                    value.position.Deconstruct(out var x, out var y);
                    sum += x + y;
                    break;
                case Types.PhysicsBench.Kind.Force:
                    value.force.Deconstruct(out var fx, out var fy, out var fz);
                    sum += fx * fy + fz;
                    break;
                case Types.PhysicsBench.Kind.Rotation:
                    value.rotation.Deconstruct(out var rx, out var ry, out var rz, out var rw);
                    sum += rx * ry + rz * rw;
                    break;
                case Types.PhysicsBench.Kind.Spring:
                    value.spring.Deconstruct(out var k, out var d, out var r, out var mn, out var mx);
                    sum += k * d + r * mn + mx;
                    break;
                case Types.PhysicsBench.Kind.Gravity:
                    value.gravity.Deconstruct(out var g);
                    sum += g;
                    break;
                case Types.PhysicsBench.Kind.Collision:
                    value.collision.Deconstruct(out var a, out var b);
                    sum += a + b;
                    break;
            }
        }

        return sum;
    }

    private static double Sum(Types.PhysicsBench[] values)
    {
        double sum = 0;
        foreach (var value in values)
        {
            switch (value.kind)
            {
                case Types.PhysicsBench.Kind.Impulse: sum += value.impulse.magnitude; break;
                case Types.PhysicsBench.Kind.Position: sum += value.position.x + value.position.y; break;
                case Types.PhysicsBench.Kind.Force: sum += value.force.fx * value.force.fy + value.force.fz; break;
                case Types.PhysicsBench.Kind.Rotation: sum += value.rotation.x * value.rotation.y + value.rotation.z * value.rotation.w; break;
                case Types.PhysicsBench.Kind.Spring: sum += value.spring.k * value.spring.damping + value.spring.rest * value.spring.min + value.spring.max; break;
                case Types.PhysicsBench.Kind.Gravity: sum += value.gravity.g; break;
                case Types.PhysicsBench.Kind.Collision: sum += value.collision.entityA + value.collision.entityB; break;
            }
        }
        return sum;
    }

    private static double Sum(Types.PhysicsAuto[] values)
    {
        double sum = 0;
        foreach (var value in values)
        {
            switch (value.kind)
            {
                case Types.PhysicsAuto.Kind.Impulse: sum += value.impulse.magnitude; break;
                case Types.PhysicsAuto.Kind.Position: sum += value.position.x + value.position.y; break;
                case Types.PhysicsAuto.Kind.Force: sum += value.force.fx * value.force.fy + value.force.fz; break;
                case Types.PhysicsAuto.Kind.Rotation: sum += value.rotation.x * value.rotation.y + value.rotation.z * value.rotation.w; break;
                case Types.PhysicsAuto.Kind.Spring: sum += value.spring.k * value.spring.damping + value.spring.rest * value.spring.min + value.spring.max; break;
                case Types.PhysicsAuto.Kind.Gravity: sum += value.gravity.g; break;
                case Types.PhysicsAuto.Kind.Collision: sum += value.collision.entityA + value.collision.entityB; break;
            }
        }
        return sum;
    }

    private static double Sum(Types.PhysicsTyped[] values)
    {
        double sum = 0;
        foreach (var value in values)
        {
            switch (value.kind)
            {
                case Types.PhysicsTyped.Kind.Impulse: sum += value.impulse.magnitude; break;
                case Types.PhysicsTyped.Kind.Position: sum += value.position.x + value.position.y; break;
                case Types.PhysicsTyped.Kind.Force: sum += value.force.fx * value.force.fy + value.force.fz; break;
                case Types.PhysicsTyped.Kind.Rotation: sum += value.rotation.x * value.rotation.y + value.rotation.z * value.rotation.w; break;
                case Types.PhysicsTyped.Kind.Spring: sum += value.spring.k * value.spring.damping + value.spring.rest * value.spring.min + value.spring.max; break;
                case Types.PhysicsTyped.Kind.Gravity: sum += value.gravity.g; break;
                case Types.PhysicsTyped.Kind.Collision: sum += value.collision.entityA + value.collision.entityB; break;
            }
        }
        return sum;
    }

    private static double Sum(Types.PhysicsShared[] values)
    {
        double sum = 0;
        foreach (var value in values)
        {
            switch (value.kind)
            {
                case Types.PhysicsShared.Kind.Impulse: sum += value.impulse.magnitude; break;
                case Types.PhysicsShared.Kind.Position: sum += value.position.x + value.position.y; break;
                case Types.PhysicsShared.Kind.Force: sum += value.force.fx * value.force.fy + value.force.fz; break;
                case Types.PhysicsShared.Kind.Rotation: sum += value.rotation.x * value.rotation.y + value.rotation.z * value.rotation.w; break;
                case Types.PhysicsShared.Kind.Spring: sum += value.spring.k * value.spring.damping + value.spring.rest * value.spring.min + value.spring.max; break;
                case Types.PhysicsShared.Kind.Gravity: sum += value.gravity.g; break;
                case Types.PhysicsShared.Kind.Collision: sum += value.collision.entityA + value.collision.entityB; break;
            }
        }
        return sum;
    }

    private static double Sum(Types.PhysicsRecord[] values)
    {
        double sum = 0;
        foreach (var value in values)
        {
            sum += value switch
            {
                Types.PhysicsRecord.Impulse(var magnitude) => magnitude,
                Types.PhysicsRecord.Position(var x, var y) => x + y,
                Types.PhysicsRecord.Force(var fx, var fy, var fz) => fx * fy + fz,
                Types.PhysicsRecord.Rotation(var x, var y, var z, var w) => x * y + z * w,
                Types.PhysicsRecord.Spring(var k, var d, var r, var mn, var mx) => k * d + r * mn + mx,
                Types.PhysicsRecord.Gravity(var g) => g,
                Types.PhysicsRecord.Collision(var a, var b) => a + b,
                _ => 0,
            };
        }
        return sum;
    }

    private static double Sum(Types.PhysicsNative[] values)
    {
        double sum = 0;
        foreach (var value in values)
        {
            sum += value switch
            {
                Types.PhysImpulse(var magnitude) => magnitude,
                Types.PhysPosition(var x, var y) => x + y,
                Types.PhysForce(var fx, var fy, var fz) => fx * fy + fz,
                Types.PhysRotation(var x, var y, var z, var w) => x * y + z * w,
                Types.PhysSpring(var k, var d, var r, var mn, var mx) => k * d + r * mn + mx,
                Types.PhysGravity(var g) => g,
                Types.PhysCollision(var a, var b) => a + b,
                _ => 0,
            };
        }
        return sum;
    }
}
