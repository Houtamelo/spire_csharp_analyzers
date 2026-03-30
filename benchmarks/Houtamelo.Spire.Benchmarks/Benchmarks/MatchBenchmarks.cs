using BenchmarkDotNet.Attributes;
using Houtamelo.Spire.Benchmarks.Helpers;

namespace Houtamelo.Spire.Benchmarks.Benchmarks;

/// Batch match + accumulate across all strategies with distribution parameter.
[Config(typeof(SpireConfig))]
public class MatchBenchmarks
{
    [Params(BenchN.Default)]
    public int N { get; set; }

    [ParamsAllValues]
    public Distribution Dist { get; set; }

    Types.PhysicsAdditive[] _additive = null!;
    Types.PhysicsBoxedFields[] _boxedFields = null!;
    Types.PhysicsBoxedTuple[] _boxedTuple = null!;
    Types.PhysicsOverlap[] _overlap = null!;
    Types.PhysicsUnsafeOverlap[] _unsafeOverlap = null!;
    Types.PhysicsRecord[] _record = null!;
    Types.PhysicsNative[] _native = null!;

    [GlobalSetup]
    public void Setup()
    {
        _additive = new Types.PhysicsAdditive[N];
        _boxedFields = new Types.PhysicsBoxedFields[N];
        _boxedTuple = new Types.PhysicsBoxedTuple[N];
        _overlap = new Types.PhysicsOverlap[N];
        _unsafeOverlap = new Types.PhysicsUnsafeOverlap[N];
        _record = new Types.PhysicsRecord[N];
        _native = new Types.PhysicsNative[N];

        ArrayFiller.Fill(_additive, new Random(42), Dist);
        ArrayFiller.Fill(_boxedFields, new Random(42), Dist);
        ArrayFiller.Fill(_boxedTuple, new Random(42), Dist);
        ArrayFiller.Fill(_overlap, new Random(42), Dist);
        ArrayFiller.Fill(_unsafeOverlap, new Random(42), Dist);
        ArrayFiller.Fill(_record, new Random(42), Dist);
        ArrayFiller.Fill(_native, new Random(42), Dist);
    }

    // ════════════════════════════════════════════════════════════════
    // Deconstruct — generated Deconstruct overloads (public API)
    //
    // Physics Deconstruct overloads:
    //   (Kind, object?)                             — Impulse, Gravity (1-field)
    //   (Kind, object?, object?)                    — Position, Collision (2-field, mixed types)
    //   (Kind, float, float, float)                 — Force
    //   (Kind, float, float, float, float)          — Rotation
    //   (Kind, float, float, float, float, float)   — Spring
    // ════════════════════════════════════════════════════════════════

    [BenchmarkCategory("Deconstruct"), Benchmark(Baseline = true, Description = "additive")]
    public double DeconstructAdditive()
    {
        double sum = 0;
        var arr = _additive;
        for (int i = 0; i < arr.Length; i++)
        {
            var e = arr[i];
            switch (e.kind)
            {
                case Types.PhysicsAdditive.Kind.Impulse:
                    e.Deconstruct(out _, out object? imp);
                    sum += (float)imp!;
                    break;
                case Types.PhysicsAdditive.Kind.Position:
                    e.Deconstruct(out _, out object? px, out object? py);
                    sum += (float)px! + (float)py!;
                    break;
                case Types.PhysicsAdditive.Kind.Force:
                    e.Deconstruct(out _, out float fx, out float fy, out float fz);
                    sum += fx * fy + fz;
                    break;
                case Types.PhysicsAdditive.Kind.Rotation:
                    e.Deconstruct(out _, out float rx, out float ry, out float rz, out float rw);
                    sum += rx * ry + rz * rw;
                    break;
                case Types.PhysicsAdditive.Kind.Spring:
                    e.Deconstruct(out _, out float sk, out float sd, out float sr, out float smn, out float smx);
                    sum += sk * sd + sr * smn + smx;
                    break;
                case Types.PhysicsAdditive.Kind.Gravity:
                    e.Deconstruct(out _, out object? gf);
                    sum += (double)gf!;
                    break;
                case Types.PhysicsAdditive.Kind.Collision:
                    e.Deconstruct(out _, out object? ca, out object? cb);
                    sum += (int)ca! + (int)cb!;
                    break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Deconstruct"), Benchmark(Description = "boxedFields")]
    public double DeconstructBoxedFields()
    {
        double sum = 0;
        var arr = _boxedFields;
        for (int i = 0; i < arr.Length; i++)
        {
            var e = arr[i];
            switch (e.kind)
            {
                case Types.PhysicsBoxedFields.Kind.Impulse:
                    e.Deconstruct(out _, out object? imp);
                    sum += (float)imp!;
                    break;
                case Types.PhysicsBoxedFields.Kind.Position:
                    e.Deconstruct(out _, out object? px, out object? py);
                    sum += (float)px! + (float)py!;
                    break;
                case Types.PhysicsBoxedFields.Kind.Force:
                    e.Deconstruct(out _, out float fx, out float fy, out float fz);
                    sum += fx * fy + fz;
                    break;
                case Types.PhysicsBoxedFields.Kind.Rotation:
                    e.Deconstruct(out _, out float rx, out float ry, out float rz, out float rw);
                    sum += rx * ry + rz * rw;
                    break;
                case Types.PhysicsBoxedFields.Kind.Spring:
                    e.Deconstruct(out _, out float sk, out float sd, out float sr, out float smn, out float smx);
                    sum += sk * sd + sr * smn + smx;
                    break;
                case Types.PhysicsBoxedFields.Kind.Gravity:
                    e.Deconstruct(out _, out object? gf);
                    sum += (double)gf!;
                    break;
                case Types.PhysicsBoxedFields.Kind.Collision:
                    e.Deconstruct(out _, out object? ca, out object? cb);
                    sum += (int)ca! + (int)cb!;
                    break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Deconstruct"), Benchmark(Description = "boxedTuple")]
    public double DeconstructBoxedTuple()
    {
        double sum = 0;
        var arr = _boxedTuple;
        for (int i = 0; i < arr.Length; i++)
        {
            var e = arr[i];
            // BoxedTuple only has (Kind, object?) Deconstruct — must cast payload
            e.Deconstruct(out var kind, out object? pay);
            switch (kind)
            {
                case Types.PhysicsBoxedTuple.Kind.Impulse:
                    sum += (float)pay!;
                    break;
                case Types.PhysicsBoxedTuple.Kind.Position:
                    var pos = ((float, float))pay!;
                    sum += pos.Item1 + pos.Item2;
                    break;
                case Types.PhysicsBoxedTuple.Kind.Force:
                    var frc = ((float, float, float))pay!;
                    sum += frc.Item1 * frc.Item2 + frc.Item3;
                    break;
                case Types.PhysicsBoxedTuple.Kind.Rotation:
                    var rot = ((float, float, float, float))pay!;
                    sum += rot.Item1 * rot.Item2 + rot.Item3 * rot.Item4;
                    break;
                case Types.PhysicsBoxedTuple.Kind.Spring:
                    var spr = ((float, float, float, float, float))pay!;
                    sum += spr.Item1 * spr.Item2 + spr.Item3 * spr.Item4 + spr.Item5;
                    break;
                case Types.PhysicsBoxedTuple.Kind.Gravity:
                    sum += (double)pay!;
                    break;
                case Types.PhysicsBoxedTuple.Kind.Collision:
                    var col = ((int, int))pay!;
                    sum += col.Item1 + col.Item2;
                    break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Deconstruct"), Benchmark(Description = "overlap")]
    public double DeconstructOverlap()
    {
        double sum = 0;
        var arr = _overlap;
        for (int i = 0; i < arr.Length; i++)
        {
            var e = arr[i];
            switch (e.kind)
            {
                case Types.PhysicsOverlap.Kind.Impulse:
                    e.Deconstruct(out _, out object? imp);
                    sum += (float)imp!;
                    break;
                case Types.PhysicsOverlap.Kind.Position:
                    e.Deconstruct(out _, out object? px, out object? py);
                    sum += (float)px! + (float)py!;
                    break;
                case Types.PhysicsOverlap.Kind.Force:
                    e.Deconstruct(out _, out float fx, out float fy, out float fz);
                    sum += fx * fy + fz;
                    break;
                case Types.PhysicsOverlap.Kind.Rotation:
                    e.Deconstruct(out _, out float rx, out float ry, out float rz, out float rw);
                    sum += rx * ry + rz * rw;
                    break;
                case Types.PhysicsOverlap.Kind.Spring:
                    e.Deconstruct(out _, out float sk, out float sd, out float sr, out float smn, out float smx);
                    sum += sk * sd + sr * smn + smx;
                    break;
                case Types.PhysicsOverlap.Kind.Gravity:
                    e.Deconstruct(out _, out object? gf);
                    sum += (double)gf!;
                    break;
                case Types.PhysicsOverlap.Kind.Collision:
                    e.Deconstruct(out _, out object? ca, out object? cb);
                    sum += (int)ca! + (int)cb!;
                    break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Deconstruct"), Benchmark(Description = "unsafeOverlap")]
    public double DeconstructUnsafeOverlap()
    {
        double sum = 0;
        var arr = _unsafeOverlap;
        for (int i = 0; i < arr.Length; i++)
        {
            var e = arr[i];
            switch (e.kind)
            {
                case Types.PhysicsUnsafeOverlap.Kind.Impulse:
                    e.Deconstruct(out _, out object? imp);
                    sum += (float)imp!;
                    break;
                case Types.PhysicsUnsafeOverlap.Kind.Position:
                    e.Deconstruct(out _, out object? px, out object? py);
                    sum += (float)px! + (float)py!;
                    break;
                case Types.PhysicsUnsafeOverlap.Kind.Force:
                    e.Deconstruct(out _, out float fx, out float fy, out float fz);
                    sum += fx * fy + fz;
                    break;
                case Types.PhysicsUnsafeOverlap.Kind.Rotation:
                    e.Deconstruct(out _, out float rx, out float ry, out float rz, out float rw);
                    sum += rx * ry + rz * rw;
                    break;
                case Types.PhysicsUnsafeOverlap.Kind.Spring:
                    e.Deconstruct(out _, out float sk, out float sd, out float sr, out float smn, out float smx);
                    sum += sk * sd + sr * smn + smx;
                    break;
                case Types.PhysicsUnsafeOverlap.Kind.Gravity:
                    e.Deconstruct(out _, out object? gf);
                    sum += (double)gf!;
                    break;
                case Types.PhysicsUnsafeOverlap.Kind.Collision:
                    e.Deconstruct(out _, out object? ca, out object? cb);
                    sum += (int)ca! + (int)cb!;
                    break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Deconstruct"), Benchmark(Description = "record")]
    public double DeconstructRecord()
    {
        double sum = 0;
        var arr = _record;
        for (int i = 0; i < arr.Length; i++)
        {
            sum += arr[i] switch
            {
                Types.PhysicsRecord.Impulse(var m) => (double)m,
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

    [BenchmarkCategory("Deconstruct"), Benchmark(Description = "native")]
    public double DeconstructNative()
    {
        double sum = 0;
        var arr = _native;
        for (int i = 0; i < arr.Length; i++)
        {
            sum += arr[i] switch
            {
                Types.PhysImpulse(var m) => (double)m,
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

    // ════════════════════════════════════════════════════════════════
    // Property — C# property pattern syntax: case { kind: Kind.X, field: var x }
    // ════════════════════════════════════════════════════════════════

    [BenchmarkCategory("Property"), Benchmark(Baseline = true, Description = "additive")]
    public double PropertyAdditive()
    {
        double sum = 0;
        var arr = _additive;
        for (int i = 0; i < arr.Length; i++)
        {
            switch (arr[i])
            {
                case { kind: Types.PhysicsAdditive.Kind.Impulse, magnitude: var m }: sum += m; break;
                case { kind: Types.PhysicsAdditive.Kind.Position, x: var x, y: var y }: sum += x + y; break;
                case { kind: Types.PhysicsAdditive.Kind.Force, fx: var fx, fy: var fy, fz: var fz }: sum += fx * fy + fz; break;
                case { kind: Types.PhysicsAdditive.Kind.Rotation, x: var x, y: var y, z: var z, w: var w }: sum += x * y + z * w; break;
                case { kind: Types.PhysicsAdditive.Kind.Spring, k: var k, damping: var d, rest: var r, min: var mn, max: var mx }: sum += k * d + r * mn + mx; break;
                case { kind: Types.PhysicsAdditive.Kind.Gravity, g: var g }: sum += g; break;
                case { kind: Types.PhysicsAdditive.Kind.Collision, entityA: var a, entityB: var b }: sum += a + b; break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Property"), Benchmark(Description = "boxedFields")]
    public double PropertyBoxedFields()
    {
        double sum = 0;
        var arr = _boxedFields;
        for (int i = 0; i < arr.Length; i++)
        {
            switch (arr[i])
            {
                case { kind: Types.PhysicsBoxedFields.Kind.Impulse, magnitude: var m }: sum += m; break;
                case { kind: Types.PhysicsBoxedFields.Kind.Position, x: var x, y: var y }: sum += x + y; break;
                case { kind: Types.PhysicsBoxedFields.Kind.Force, fx: var fx, fy: var fy, fz: var fz }: sum += fx * fy + fz; break;
                case { kind: Types.PhysicsBoxedFields.Kind.Rotation, x: var x, y: var y, z: var z, w: var w }: sum += x * y + z * w; break;
                case { kind: Types.PhysicsBoxedFields.Kind.Spring, k: var k, damping: var d, rest: var r, min: var mn, max: var mx }: sum += k * d + r * mn + mx; break;
                case { kind: Types.PhysicsBoxedFields.Kind.Gravity, g: var g }: sum += g; break;
                case { kind: Types.PhysicsBoxedFields.Kind.Collision, entityA: var a, entityB: var b }: sum += a + b; break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Property"), Benchmark(Description = "boxedTuple")]
    public double PropertyBoxedTuple()
    {
        double sum = 0;
        var arr = _boxedTuple;
        for (int i = 0; i < arr.Length; i++)
        {
            switch (arr[i])
            {
                case { kind: Types.PhysicsBoxedTuple.Kind.Impulse, magnitude: var m }: sum += m; break;
                case { kind: Types.PhysicsBoxedTuple.Kind.Position, x: var x, y: var y }: sum += x + y; break;
                case { kind: Types.PhysicsBoxedTuple.Kind.Force, fx: var fx, fy: var fy, fz: var fz }: sum += fx * fy + fz; break;
                case { kind: Types.PhysicsBoxedTuple.Kind.Rotation, x: var x, y: var y, z: var z, w: var w }: sum += x * y + z * w; break;
                case { kind: Types.PhysicsBoxedTuple.Kind.Spring, k: var k, damping: var d, rest: var r, min: var mn, max: var mx }: sum += k * d + r * mn + mx; break;
                case { kind: Types.PhysicsBoxedTuple.Kind.Gravity, g: var g }: sum += g; break;
                case { kind: Types.PhysicsBoxedTuple.Kind.Collision, entityA: var a, entityB: var b }: sum += a + b; break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Property"), Benchmark(Description = "overlap")]
    public double PropertyOverlap()
    {
        double sum = 0;
        var arr = _overlap;
        for (int i = 0; i < arr.Length; i++)
        {
            switch (arr[i])
            {
                case { kind: Types.PhysicsOverlap.Kind.Impulse, magnitude: var m }: sum += m; break;
                case { kind: Types.PhysicsOverlap.Kind.Position, x: var x, y: var y }: sum += x + y; break;
                case { kind: Types.PhysicsOverlap.Kind.Force, fx: var fx, fy: var fy, fz: var fz }: sum += fx * fy + fz; break;
                case { kind: Types.PhysicsOverlap.Kind.Rotation, x: var x, y: var y, z: var z, w: var w }: sum += x * y + z * w; break;
                case { kind: Types.PhysicsOverlap.Kind.Spring, k: var k, damping: var d, rest: var r, min: var mn, max: var mx }: sum += k * d + r * mn + mx; break;
                case { kind: Types.PhysicsOverlap.Kind.Gravity, g: var g }: sum += g; break;
                case { kind: Types.PhysicsOverlap.Kind.Collision, entityA: var a, entityB: var b }: sum += a + b; break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Property"), Benchmark(Description = "unsafeOverlap")]
    public double PropertyUnsafeOverlap()
    {
        double sum = 0;
        var arr = _unsafeOverlap;
        for (int i = 0; i < arr.Length; i++)
        {
            switch (arr[i])
            {
                case { kind: Types.PhysicsUnsafeOverlap.Kind.Impulse, magnitude: var m }: sum += m; break;
                case { kind: Types.PhysicsUnsafeOverlap.Kind.Position, x: var x, y: var y }: sum += x + y; break;
                case { kind: Types.PhysicsUnsafeOverlap.Kind.Force, fx: var fx, fy: var fy, fz: var fz }: sum += fx * fy + fz; break;
                case { kind: Types.PhysicsUnsafeOverlap.Kind.Rotation, x: var x, y: var y, z: var z, w: var w }: sum += x * y + z * w; break;
                case { kind: Types.PhysicsUnsafeOverlap.Kind.Spring, k: var k, damping: var d, rest: var r, min: var mn, max: var mx }: sum += k * d + r * mn + mx; break;
                case { kind: Types.PhysicsUnsafeOverlap.Kind.Gravity, g: var g }: sum += g; break;
                case { kind: Types.PhysicsUnsafeOverlap.Kind.Collision, entityA: var a, entityB: var b }: sum += a + b; break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Property"), Benchmark(Description = "record")]
    public double PropertyRecord()
    {
        double sum = 0;
        var arr = _record;
        for (int i = 0; i < arr.Length; i++)
        {
            switch (arr[i])
            {
                case Types.PhysicsRecord.Impulse { Magnitude: var m }: sum += m; break;
                case Types.PhysicsRecord.Position { X: var x, Y: var y }: sum += x + y; break;
                case Types.PhysicsRecord.Force { FX: var fx, FY: var fy, FZ: var fz }: sum += fx * fy + fz; break;
                case Types.PhysicsRecord.Rotation { X: var x, Y: var y, Z: var z, W: var w }: sum += x * y + z * w; break;
                case Types.PhysicsRecord.Spring { K: var k, Damping: var d, Rest: var r, Min: var mn, Max: var mx }: sum += k * d + r * mn + mx; break;
                case Types.PhysicsRecord.Gravity { G: var g }: sum += g; break;
                case Types.PhysicsRecord.Collision { EntityA: var a, EntityB: var b }: sum += a + b; break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Property"), Benchmark(Description = "native")]
    public double PropertyNative()
    {
        double sum = 0;
        var arr = _native;
        for (int i = 0; i < arr.Length; i++)
        {
            switch (arr[i])
            {
                case Types.PhysImpulse { Magnitude: var m }: sum += m; break;
                case Types.PhysPosition { X: var x, Y: var y }: sum += x + y; break;
                case Types.PhysForce { FX: var fx, FY: var fy, FZ: var fz }: sum += fx * fy + fz; break;
                case Types.PhysRotation { X: var x, Y: var y, Z: var z, W: var w }: sum += x * y + z * w; break;
                case Types.PhysSpring { K: var k, Damping: var d, Rest: var r, Min: var mn, Max: var mx }: sum += k * d + r * mn + mx; break;
                case Types.PhysGravity { G: var g }: sum += g; break;
                case Types.PhysCollision { EntityA: var a, EntityB: var b }: sum += a + b; break;
            }
        }
        return sum;
    }
}
