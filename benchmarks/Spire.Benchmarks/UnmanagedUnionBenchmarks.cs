using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace Spire.Benchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class UnmanagedUnionBenchmarks {
    const int N = 1000;

    PhysicsExplicit[] _explicit = null!;
    PhysicsMultiObj[] _multiObj = null!;
    PhysicsTupleObj[] _tupleObj = null!;
    PhysicsClass[] _class = null!;
    PhysicsAdditive[] _additive = null!;
    PhysicsUnsafeInline[] _unsafeInline = null!;
    PhysicsUnsafeLong[] _unsafeLong = null!;

    // Random construction inputs
    float[] _floats = null!;
    double[] _doubles = null!;
    int[] _ints = null!;

    [GlobalSetup]
    public void Setup() {
        Console.WriteLine($"sizeof(PhysicsExplicit)    = {Unsafe.SizeOf<PhysicsExplicit>()}");
        Console.WriteLine($"sizeof(PhysicsMultiObj)    = {Unsafe.SizeOf<PhysicsMultiObj>()}");
        Console.WriteLine($"sizeof(PhysicsTupleObj)    = {Unsafe.SizeOf<PhysicsTupleObj>()}");
        Console.WriteLine($"sizeof(PhysicsClass ref)   = {IntPtr.Size}");
        Console.WriteLine($"sizeof(PhysicsAdditive)    = {Unsafe.SizeOf<PhysicsAdditive>()}");
        Console.WriteLine($"sizeof(PhysicsUnsafeInline)= {Unsafe.SizeOf<PhysicsUnsafeInline>()}");
        Console.WriteLine($"sizeof(PhysicsUnsafeLong)  = {Unsafe.SizeOf<PhysicsUnsafeLong>()}");

        var rng = new Random(42);

        _floats = new float[N];
        _doubles = new double[N];
        _ints = new int[N];
        for (int i = 0; i < N; i++) {
            _floats[i] = rng.NextSingle() * 200 - 100;
            _doubles[i] = rng.NextDouble() * 200 - 100;
            _ints[i] = rng.Next(-1000, 1000);
        }

        _explicit = new PhysicsExplicit[N];
        _multiObj = new PhysicsMultiObj[N];
        _tupleObj = new PhysicsTupleObj[N];
        _class = new PhysicsClass[N];
        _additive = new PhysicsAdditive[N];
        _unsafeInline = new PhysicsUnsafeInline[N];
        _unsafeLong = new PhysicsUnsafeLong[N];

        PhysicsFactory.Fill(new Random(42), N, _explicit, _multiObj, _tupleObj, _class,
            _additive, _unsafeInline, _unsafeLong);
    }

    // ══════════════════════════════════════════
    // Construction — build N random variants
    // ══════════════════════════════════════════

    [BenchmarkCategory("Construct"), Benchmark(Baseline = true, Description = "baseline (array access only)")]
    public double ConstructBaseline() {
        double sum = 0;
        var f = _floats; var d = _doubles; var n = _ints;
        for (int i = 0; i < N; i++) {
            sum += (i % 8) switch {
                0 => 0,
                1 => f[i],
                2 => f[i] + f[(i+1)%N],
                3 => f[i] + f[(i+1)%N] + f[(i+2)%N],
                4 => f[i] + f[(i+1)%N] + f[(i+2)%N] + f[(i+3)%N],
                5 => f[i] + f[(i+1)%N] + f[(i+2)%N] + f[(i+3)%N] + f[(i+4)%N],
                6 => d[i],
                _ => n[i] + n[(i+1)%N],
            };
        }
        return sum;
    }

    [BenchmarkCategory("Construct"), Benchmark(Description = "explicit")]
    public PhysicsExplicit[] ConstructExplicit() {
        var result = new PhysicsExplicit[N];
        var f = _floats; var d = _doubles; var n = _ints;
        for (int i = 0; i < N; i++) {
            result[i] = (i % 8) switch {
                0 => PhysicsExplicit.NewIdle(),
                1 => PhysicsExplicit.NewImpulse(f[i]),
                2 => PhysicsExplicit.NewPosition(f[i], f[(i+1)%N]),
                3 => PhysicsExplicit.NewForce(f[i], f[(i+1)%N], f[(i+2)%N]),
                4 => PhysicsExplicit.NewRotation(f[i], f[(i+1)%N], f[(i+2)%N], f[(i+3)%N]),
                5 => PhysicsExplicit.NewSpring(f[i], f[(i+1)%N], f[(i+2)%N], f[(i+3)%N], f[(i+4)%N]),
                6 => PhysicsExplicit.NewGravity(d[i]),
                _ => PhysicsExplicit.NewCollision(n[i], n[(i+1)%N]),
            };
        }
        return result;
    }

    [BenchmarkCategory("Construct"), Benchmark(Description = "multiobj")]
    public PhysicsMultiObj[] ConstructMultiObj() {
        var result = new PhysicsMultiObj[N];
        var f = _floats; var d = _doubles; var n = _ints;
        for (int i = 0; i < N; i++) {
            result[i] = (i % 8) switch {
                0 => PhysicsMultiObj.NewIdle(),
                1 => PhysicsMultiObj.NewImpulse(f[i]),
                2 => PhysicsMultiObj.NewPosition(f[i], f[(i+1)%N]),
                3 => PhysicsMultiObj.NewForce(f[i], f[(i+1)%N], f[(i+2)%N]),
                4 => PhysicsMultiObj.NewRotation(f[i], f[(i+1)%N], f[(i+2)%N], f[(i+3)%N]),
                5 => PhysicsMultiObj.NewSpring(f[i], f[(i+1)%N], f[(i+2)%N], f[(i+3)%N], f[(i+4)%N]),
                6 => PhysicsMultiObj.NewGravity(d[i]),
                _ => PhysicsMultiObj.NewCollision(n[i], n[(i+1)%N]),
            };
        }
        return result;
    }

    [BenchmarkCategory("Construct"), Benchmark(Description = "tupleobj")]
    public PhysicsTupleObj[] ConstructTupleObj() {
        var result = new PhysicsTupleObj[N];
        var f = _floats; var d = _doubles; var n = _ints;
        for (int i = 0; i < N; i++) {
            result[i] = (i % 8) switch {
                0 => PhysicsTupleObj.NewIdle(),
                1 => PhysicsTupleObj.NewImpulse(f[i]),
                2 => PhysicsTupleObj.NewPosition(f[i], f[(i+1)%N]),
                3 => PhysicsTupleObj.NewForce(f[i], f[(i+1)%N], f[(i+2)%N]),
                4 => PhysicsTupleObj.NewRotation(f[i], f[(i+1)%N], f[(i+2)%N], f[(i+3)%N]),
                5 => PhysicsTupleObj.NewSpring(f[i], f[(i+1)%N], f[(i+2)%N], f[(i+3)%N], f[(i+4)%N]),
                6 => PhysicsTupleObj.NewGravity(d[i]),
                _ => PhysicsTupleObj.NewCollision(n[i], n[(i+1)%N]),
            };
        }
        return result;
    }

    [BenchmarkCategory("Construct"), Benchmark(Description = "abstract class")]
    public PhysicsClass[] ConstructClass() {
        var result = new PhysicsClass[N];
        var f = _floats; var d = _doubles; var n = _ints;
        for (int i = 0; i < N; i++) {
            result[i] = (i % 8) switch {
                0 => PhysicsClass.NewIdle(),
                1 => PhysicsClass.NewImpulse(f[i]),
                2 => PhysicsClass.NewPosition(f[i], f[(i+1)%N]),
                3 => PhysicsClass.NewForce(f[i], f[(i+1)%N], f[(i+2)%N]),
                4 => PhysicsClass.NewRotation(f[i], f[(i+1)%N], f[(i+2)%N], f[(i+3)%N]),
                5 => PhysicsClass.NewSpring(f[i], f[(i+1)%N], f[(i+2)%N], f[(i+3)%N], f[(i+4)%N]),
                6 => PhysicsClass.NewGravity(d[i]),
                _ => PhysicsClass.NewCollision(n[i], n[(i+1)%N]),
            };
        }
        return result;
    }

    [BenchmarkCategory("Construct"), Benchmark(Description = "additive")]
    public PhysicsAdditive[] ConstructAdditive() {
        var result = new PhysicsAdditive[N];
        var f = _floats; var d = _doubles; var n = _ints;
        for (int i = 0; i < N; i++) {
            result[i] = (i % 8) switch {
                0 => PhysicsAdditive.NewIdle(),
                1 => PhysicsAdditive.NewImpulse(f[i]),
                2 => PhysicsAdditive.NewPosition(f[i], f[(i+1)%N]),
                3 => PhysicsAdditive.NewForce(f[i], f[(i+1)%N], f[(i+2)%N]),
                4 => PhysicsAdditive.NewRotation(f[i], f[(i+1)%N], f[(i+2)%N], f[(i+3)%N]),
                5 => PhysicsAdditive.NewSpring(f[i], f[(i+1)%N], f[(i+2)%N], f[(i+3)%N], f[(i+4)%N]),
                6 => PhysicsAdditive.NewGravity(d[i]),
                _ => PhysicsAdditive.NewCollision(n[i], n[(i+1)%N]),
            };
        }
        return result;
    }

    [BenchmarkCategory("Construct"), Benchmark(Description = "unsafe inline")]
    public PhysicsUnsafeInline[] ConstructUnsafeInline() {
        var result = new PhysicsUnsafeInline[N];
        var f = _floats; var d = _doubles; var n = _ints;
        for (int i = 0; i < N; i++) {
            result[i] = (i % 8) switch {
                0 => PhysicsUnsafeInline.NewIdle(),
                1 => PhysicsUnsafeInline.NewImpulse(f[i]),
                2 => PhysicsUnsafeInline.NewPosition(f[i], f[(i+1)%N]),
                3 => PhysicsUnsafeInline.NewForce(f[i], f[(i+1)%N], f[(i+2)%N]),
                4 => PhysicsUnsafeInline.NewRotation(f[i], f[(i+1)%N], f[(i+2)%N], f[(i+3)%N]),
                5 => PhysicsUnsafeInline.NewSpring(f[i], f[(i+1)%N], f[(i+2)%N], f[(i+3)%N], f[(i+4)%N]),
                6 => PhysicsUnsafeInline.NewGravity(d[i]),
                _ => PhysicsUnsafeInline.NewCollision(n[i], n[(i+1)%N]),
            };
        }
        return result;
    }

    [BenchmarkCategory("Construct"), Benchmark(Description = "unsafe long")]
    public PhysicsUnsafeLong[] ConstructUnsafeLong() {
        var result = new PhysicsUnsafeLong[N];
        var f = _floats; var d = _doubles; var n = _ints;
        for (int i = 0; i < N; i++) {
            result[i] = (i % 8) switch {
                0 => PhysicsUnsafeLong.NewIdle(),
                1 => PhysicsUnsafeLong.NewImpulse(f[i]),
                2 => PhysicsUnsafeLong.NewPosition(f[i], f[(i+1)%N]),
                3 => PhysicsUnsafeLong.NewForce(f[i], f[(i+1)%N], f[(i+2)%N]),
                4 => PhysicsUnsafeLong.NewRotation(f[i], f[(i+1)%N], f[(i+2)%N], f[(i+3)%N]),
                5 => PhysicsUnsafeLong.NewSpring(f[i], f[(i+1)%N], f[(i+2)%N], f[(i+3)%N], f[(i+4)%N]),
                6 => PhysicsUnsafeLong.NewGravity(d[i]),
                _ => PhysicsUnsafeLong.NewCollision(n[i], n[(i+1)%N]),
            };
        }
        return result;
    }

    // ══════════════════════════════════════════
    // Match — Deconstruct (object? boxing)
    // ══════════════════════════════════════════

    [BenchmarkCategory("Match Deconstruct"), Benchmark(Description = "explicit")]
    public double MatchDeconstructExplicit() {
        double sum = 0;
        var arr = _explicit;
        for (int i = 0; i < arr.Length; i++) {
            var (kind, payload) = arr[i];
            sum += kind switch {
                PhysicsKind.Idle => 0,
                PhysicsKind.Impulse when payload is float m => m,
                PhysicsKind.Position when payload is (float x, float y) => x + y,
                PhysicsKind.Force when payload is (float fx, float fy, float fz) => fx + fy + fz,
                PhysicsKind.Rotation when payload is (float x, float y, float z, float w) => x + y + z + w,
                PhysicsKind.Spring when payload is (float k, float d, float r, float mn, float mx) => k + d + r + mn + mx,
                PhysicsKind.Gravity when payload is double g => g,
                PhysicsKind.Collision when payload is (int a, int b) => a + b,
                _ => 0,
            };
        }
        return sum;
    }

    [BenchmarkCategory("Match Deconstruct"), Benchmark(Description = "multiobj")]
    public double MatchDeconstructMultiObj() {
        double sum = 0;
        var arr = _multiObj;
        for (int i = 0; i < arr.Length; i++) {
            var (kind, f0) = arr[i];
            sum += kind switch {
                PhysicsKind.Idle => 0,
                PhysicsKind.Impulse when f0 is float m => m,
                PhysicsKind.Position when f0 is float x => x,
                PhysicsKind.Force when f0 is float fx => fx,
                PhysicsKind.Rotation when f0 is float x => x,
                PhysicsKind.Spring when f0 is float k => k,
                PhysicsKind.Gravity when f0 is double g => g,
                PhysicsKind.Collision when f0 is int a => a,
                _ => 0,
            };
        }
        return sum;
    }

    [BenchmarkCategory("Match Deconstruct"), Benchmark(Description = "tupleobj")]
    public double MatchDeconstructTupleObj() {
        double sum = 0;
        var arr = _tupleObj;
        for (int i = 0; i < arr.Length; i++) {
            var (kind, payload) = arr[i];
            sum += kind switch {
                PhysicsKind.Idle => 0,
                PhysicsKind.Impulse when payload is float m => m,
                PhysicsKind.Position when payload is (float x, float y) => x + y,
                PhysicsKind.Force when payload is (float fx, float fy, float fz) => fx + fy + fz,
                PhysicsKind.Rotation when payload is (float x, float y, float z, float w) => x + y + z + w,
                PhysicsKind.Spring when payload is (float k, float d, float r, float mn, float mx) => k + d + r + mn + mx,
                PhysicsKind.Gravity when payload is double g => g,
                PhysicsKind.Collision when payload is (int a, int b) => a + b,
                _ => 0,
            };
        }
        return sum;
    }

    [BenchmarkCategory("Match Deconstruct"), Benchmark(Description = "abstract class")]
    public double MatchDeconstructClass() {
        double sum = 0;
        var arr = _class;
        for (int i = 0; i < arr.Length; i++) {
            sum += arr[i] switch {
                PhysicsClass.IdleClass => 0,
                PhysicsClass.ImpulseClass imp => imp.Magnitude,
                PhysicsClass.PositionClass p => p.X + p.Y,
                PhysicsClass.ForceClass f => f.Fx + f.Fy + f.Fz,
                PhysicsClass.RotationClass r => r.X + r.Y + r.Z + r.W,
                PhysicsClass.SpringClass s => s.K + s.Damping + s.Rest + s.Min + s.Max,
                PhysicsClass.GravityClass g => g.G,
                PhysicsClass.CollisionClass c => c.EntityA + c.EntityB,
                _ => 0,
            };
        }
        return sum;
    }

    // ══════════════════════════════════════════
    // Match — Property pattern (no boxing)
    // ══════════════════════════════════════════

    [BenchmarkCategory("Match Property"), Benchmark(Description = "explicit")]
    public double MatchPropertyExplicit() {
        double sum = 0;
        var arr = _explicit;
        for (int i = 0; i < arr.Length; i++) {
            ref readonly var e = ref arr[i];
            sum += e.tag switch {
                PhysicsKind.Idle => 0,
                PhysicsKind.Impulse => e.ImpulseMagnitude,
                PhysicsKind.Position => e.Pos.x + e.Pos.y,
                PhysicsKind.Force => e.Frc.fx + e.Frc.fy + e.Frc.fz,
                PhysicsKind.Rotation => e.Rot.x + e.Rot.y + e.Rot.z + e.Rot.w,
                PhysicsKind.Spring => e.Spr.k + e.Spr.damping + e.Spr.rest + e.Spr.min + e.Spr.max,
                PhysicsKind.Gravity => e.GravityG,
                PhysicsKind.Collision => e.Col.entityA + e.Col.entityB,
                _ => 0,
            };
        }
        return sum;
    }

    [BenchmarkCategory("Match Property"), Benchmark(Description = "additive")]
    public double MatchPropertyAdditive() {
        double sum = 0;
        var arr = _additive;
        for (int i = 0; i < arr.Length; i++) {
            ref var e = ref arr[i];
            sum += e.tag switch {
                PhysicsKind.Idle => 0,
                PhysicsKind.Impulse => e.ImpulseMagnitude,
                PhysicsKind.Position => e.Pos.x + e.Pos.y,
                PhysicsKind.Force => e.Frc.fx + e.Frc.fy + e.Frc.fz,
                PhysicsKind.Rotation => e.Rot.x + e.Rot.y + e.Rot.z + e.Rot.w,
                PhysicsKind.Spring => e.Spr.k + e.Spr.damping + e.Spr.rest + e.Spr.min + e.Spr.max,
                PhysicsKind.Gravity => e.GravityG,
                PhysicsKind.Collision => e.Col.entityA + e.Col.entityB,
                _ => 0,
            };
        }
        return sum;
    }

    [BenchmarkCategory("Match Property"), Benchmark(Description = "unsafe inline")]
    public double MatchPropertyUnsafeInline() {
        double sum = 0;
        var arr = _unsafeInline;
        for (int i = 0; i < arr.Length; i++) {
            ref var e = ref arr[i];
            sum += e.tag switch {
                PhysicsKind.Idle => 0,
                PhysicsKind.Impulse => e.ImpulseMagnitude,
                PhysicsKind.Position => e.Pos.x + e.Pos.y,
                PhysicsKind.Force => e.Frc.fx + e.Frc.fy + e.Frc.fz,
                PhysicsKind.Rotation => e.Rot.x + e.Rot.y + e.Rot.z + e.Rot.w,
                PhysicsKind.Spring => e.Spr.k + e.Spr.damping + e.Spr.rest + e.Spr.min + e.Spr.max,
                PhysicsKind.Gravity => e.GravityG,
                PhysicsKind.Collision => e.Col.entityA + e.Col.entityB,
                _ => 0,
            };
        }
        return sum;
    }

    [BenchmarkCategory("Match Property"), Benchmark(Description = "unsafe long")]
    public double MatchPropertyUnsafeLong() {
        double sum = 0;
        var arr = _unsafeLong;
        for (int i = 0; i < arr.Length; i++) {
            ref var e = ref arr[i];
            sum += e.tag switch {
                PhysicsKind.Idle => 0,
                PhysicsKind.Impulse => e.ImpulseMagnitude,
                PhysicsKind.Position => e.Pos.x + e.Pos.y,
                PhysicsKind.Force => e.Frc.fx + e.Frc.fy + e.Frc.fz,
                PhysicsKind.Rotation => e.Rot.x + e.Rot.y + e.Rot.z + e.Rot.w,
                PhysicsKind.Spring => e.Spr.k + e.Spr.damping + e.Spr.rest + e.Spr.min + e.Spr.max,
                PhysicsKind.Gravity => e.GravityG,
                PhysicsKind.Collision => e.Col.entityA + e.Col.entityB,
                _ => 0,
            };
        }
        return sum;
    }

    // ══════════════════════════════════════════
    // Copy throughput
    // ══════════════════════════════════════════

    [BenchmarkCategory("Copy"), Benchmark(Description = "explicit")]
    public PhysicsExplicit[] CopyExplicit() {
        var dst = new PhysicsExplicit[N];
        Array.Copy(_explicit, dst, N);
        return dst;
    }

    [BenchmarkCategory("Copy"), Benchmark(Description = "multiobj")]
    public PhysicsMultiObj[] CopyMultiObj() {
        var dst = new PhysicsMultiObj[N];
        Array.Copy(_multiObj, dst, N);
        return dst;
    }

    [BenchmarkCategory("Copy"), Benchmark(Description = "tupleobj")]
    public PhysicsTupleObj[] CopyTupleObj() {
        var dst = new PhysicsTupleObj[N];
        Array.Copy(_tupleObj, dst, N);
        return dst;
    }

    [BenchmarkCategory("Copy"), Benchmark(Description = "abstract class")]
    public PhysicsClass[] CopyClass() {
        var dst = new PhysicsClass[N];
        Array.Copy(_class, dst, N);
        return dst;
    }

    [BenchmarkCategory("Copy"), Benchmark(Description = "additive")]
    public PhysicsAdditive[] CopyAdditive() {
        var dst = new PhysicsAdditive[N];
        Array.Copy(_additive, dst, N);
        return dst;
    }

    [BenchmarkCategory("Copy"), Benchmark(Description = "unsafe inline")]
    public PhysicsUnsafeInline[] CopyUnsafeInline() {
        var dst = new PhysicsUnsafeInline[N];
        Array.Copy(_unsafeInline, dst, N);
        return dst;
    }

    [BenchmarkCategory("Copy"), Benchmark(Description = "unsafe long")]
    public PhysicsUnsafeLong[] CopyUnsafeLong() {
        var dst = new PhysicsUnsafeLong[N];
        Array.Copy(_unsafeLong, dst, N);
        return dst;
    }
}
