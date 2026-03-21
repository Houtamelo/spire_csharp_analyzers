using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace Spire.Benchmarks;

/// Batch match + accumulate across all strategies with distribution parameter.
[Config(typeof(SpireConfig))]
public class MatchBenchmarks
{
    [Params(BenchN.Default)]
    public int N { get; set; }

    [ParamsAllValues]
    public Distribution Dist { get; set; }

    PhysicsAdditive[] _additive = null!;
    PhysicsBoxedFields[] _boxedFields = null!;
    PhysicsBoxedTuple[] _boxedTuple = null!;
    PhysicsOverlap[] _overlap = null!;
    PhysicsUnsafeOverlap[] _unsafeOverlap = null!;
    PhysicsRecord[] _record = null!;
    PhysicsClass[] _class = null!;

    [GlobalSetup]
    public void Setup()
    {
        _additive = new PhysicsAdditive[N];
        _boxedFields = new PhysicsBoxedFields[N];
        _boxedTuple = new PhysicsBoxedTuple[N];
        _overlap = new PhysicsOverlap[N];
        _unsafeOverlap = new PhysicsUnsafeOverlap[N];
        _record = new PhysicsRecord[N];
        _class = new PhysicsClass[N];

        ArrayFiller.Fill(_additive, new Random(42), Dist);
        ArrayFiller.Fill(_boxedFields, new Random(42), Dist);
        ArrayFiller.Fill(_boxedTuple, new Random(42), Dist);
        ArrayFiller.Fill(_overlap, new Random(42), Dist);
        ArrayFiller.Fill(_unsafeOverlap, new Random(42), Dist);
        ArrayFiller.Fill(_record, new Random(42), Dist);
        ArrayFiller.Fill(_class, new Random(42), Dist);
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
            switch (e.tag)
            {
                case PhysicsAdditive.Kind.Impulse:
                    e.Deconstruct(out _, out object? imp);
                    sum += (float)imp!;
                    break;
                case PhysicsAdditive.Kind.Position:
                    e.Deconstruct(out _, out object? px, out object? py);
                    sum += (float)px! + (float)py!;
                    break;
                case PhysicsAdditive.Kind.Force:
                    e.Deconstruct(out _, out float fx, out float fy, out float fz);
                    sum += fx * fy + fz;
                    break;
                case PhysicsAdditive.Kind.Rotation:
                    e.Deconstruct(out _, out float rx, out float ry, out float rz, out float rw);
                    sum += rx * ry + rz * rw;
                    break;
                case PhysicsAdditive.Kind.Spring:
                    e.Deconstruct(out _, out float sk, out float sd, out float sr, out float smn, out float smx);
                    sum += sk * sd + sr * smn + smx;
                    break;
                case PhysicsAdditive.Kind.Gravity:
                    e.Deconstruct(out _, out object? gf);
                    sum += (double)gf!;
                    break;
                case PhysicsAdditive.Kind.Collision:
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
            switch (e.tag)
            {
                case PhysicsBoxedFields.Kind.Impulse:
                    e.Deconstruct(out _, out object? imp);
                    sum += (float)imp!;
                    break;
                case PhysicsBoxedFields.Kind.Position:
                    e.Deconstruct(out _, out object? px, out object? py);
                    sum += (float)px! + (float)py!;
                    break;
                case PhysicsBoxedFields.Kind.Force:
                    e.Deconstruct(out _, out float fx, out float fy, out float fz);
                    sum += fx * fy + fz;
                    break;
                case PhysicsBoxedFields.Kind.Rotation:
                    e.Deconstruct(out _, out float rx, out float ry, out float rz, out float rw);
                    sum += rx * ry + rz * rw;
                    break;
                case PhysicsBoxedFields.Kind.Spring:
                    e.Deconstruct(out _, out float sk, out float sd, out float sr, out float smn, out float smx);
                    sum += sk * sd + sr * smn + smx;
                    break;
                case PhysicsBoxedFields.Kind.Gravity:
                    e.Deconstruct(out _, out object? gf);
                    sum += (double)gf!;
                    break;
                case PhysicsBoxedFields.Kind.Collision:
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
                case PhysicsBoxedTuple.Kind.Impulse:
                    sum += (float)pay!;
                    break;
                case PhysicsBoxedTuple.Kind.Position:
                    var pos = ((float, float))pay!;
                    sum += pos.Item1 + pos.Item2;
                    break;
                case PhysicsBoxedTuple.Kind.Force:
                    var frc = ((float, float, float))pay!;
                    sum += frc.Item1 * frc.Item2 + frc.Item3;
                    break;
                case PhysicsBoxedTuple.Kind.Rotation:
                    var rot = ((float, float, float, float))pay!;
                    sum += rot.Item1 * rot.Item2 + rot.Item3 * rot.Item4;
                    break;
                case PhysicsBoxedTuple.Kind.Spring:
                    var spr = ((float, float, float, float, float))pay!;
                    sum += spr.Item1 * spr.Item2 + spr.Item3 * spr.Item4 + spr.Item5;
                    break;
                case PhysicsBoxedTuple.Kind.Gravity:
                    sum += (double)pay!;
                    break;
                case PhysicsBoxedTuple.Kind.Collision:
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
            switch (e.tag)
            {
                case PhysicsOverlap.Kind.Impulse:
                    e.Deconstruct(out _, out object? imp);
                    sum += (float)imp!;
                    break;
                case PhysicsOverlap.Kind.Position:
                    e.Deconstruct(out _, out object? px, out object? py);
                    sum += (float)px! + (float)py!;
                    break;
                case PhysicsOverlap.Kind.Force:
                    e.Deconstruct(out _, out float fx, out float fy, out float fz);
                    sum += fx * fy + fz;
                    break;
                case PhysicsOverlap.Kind.Rotation:
                    e.Deconstruct(out _, out float rx, out float ry, out float rz, out float rw);
                    sum += rx * ry + rz * rw;
                    break;
                case PhysicsOverlap.Kind.Spring:
                    e.Deconstruct(out _, out float sk, out float sd, out float sr, out float smn, out float smx);
                    sum += sk * sd + sr * smn + smx;
                    break;
                case PhysicsOverlap.Kind.Gravity:
                    e.Deconstruct(out _, out object? gf);
                    sum += (double)gf!;
                    break;
                case PhysicsOverlap.Kind.Collision:
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
            switch (e.tag)
            {
                case PhysicsUnsafeOverlap.Kind.Impulse:
                    e.Deconstruct(out _, out object? imp);
                    sum += (float)imp!;
                    break;
                case PhysicsUnsafeOverlap.Kind.Position:
                    e.Deconstruct(out _, out object? px, out object? py);
                    sum += (float)px! + (float)py!;
                    break;
                case PhysicsUnsafeOverlap.Kind.Force:
                    e.Deconstruct(out _, out float fx, out float fy, out float fz);
                    sum += fx * fy + fz;
                    break;
                case PhysicsUnsafeOverlap.Kind.Rotation:
                    e.Deconstruct(out _, out float rx, out float ry, out float rz, out float rw);
                    sum += rx * ry + rz * rw;
                    break;
                case PhysicsUnsafeOverlap.Kind.Spring:
                    e.Deconstruct(out _, out float sk, out float sd, out float sr, out float smn, out float smx);
                    sum += sk * sd + sr * smn + smx;
                    break;
                case PhysicsUnsafeOverlap.Kind.Gravity:
                    e.Deconstruct(out _, out object? gf);
                    sum += (double)gf!;
                    break;
                case PhysicsUnsafeOverlap.Kind.Collision:
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
                PhysicsRecord.Impulse(var m) => (double)m,
                PhysicsRecord.Position(var x, var y) => x + y,
                PhysicsRecord.Force(var fx, var fy, var fz) => fx * fy + fz,
                PhysicsRecord.Rotation(var x, var y, var z, var w) => x * y + z * w,
                PhysicsRecord.Spring(var k, var d, var r, var mn, var mx) => k * d + r * mn + mx,
                PhysicsRecord.Gravity(var g) => g,
                PhysicsRecord.Collision(var a, var b) => a + b,
                _ => 0,
            };
        }
        return sum;
    }

    [BenchmarkCategory("Deconstruct"), Benchmark(Description = "class")]
    public double DeconstructClass()
    {
        double sum = 0;
        var arr = _class;
        for (int i = 0; i < arr.Length; i++)
        {
            sum += arr[i] switch
            {
                PhysicsClass.Impulse p => (double)p.Magnitude,
                PhysicsClass.Position p => p.X + p.Y,
                PhysicsClass.Force p => p.FX * p.FY + p.FZ,
                PhysicsClass.Rotation p => p.X * p.Y + p.Z * p.W,
                PhysicsClass.Spring p => p.K * p.Damping + p.Rest * p.Min + p.Max,
                PhysicsClass.Gravity p => p.G,
                PhysicsClass.Collision p => p.EntityA + p.EntityB,
                _ => 0,
            };
        }
        return sum;
    }

    // ════════════════════════════════════════════════════════════════
    // Property — C# property pattern syntax: case { tag: Kind.X, field: var x }
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
                case { tag: PhysicsAdditive.Kind.Impulse, magnitude: var m }: sum += m; break;
                case { tag: PhysicsAdditive.Kind.Position, x: var x, y: var y }: sum += x + y; break;
                case { tag: PhysicsAdditive.Kind.Force, fx: var fx, fy: var fy, fz: var fz }: sum += fx * fy + fz; break;
                case { tag: PhysicsAdditive.Kind.Rotation, x: var x, y: var y, z: var z, w: var w }: sum += x * y + z * w; break;
                case { tag: PhysicsAdditive.Kind.Spring, k: var k, damping: var d, rest: var r, min: var mn, max: var mx }: sum += k * d + r * mn + mx; break;
                case { tag: PhysicsAdditive.Kind.Gravity, g: var g }: sum += g; break;
                case { tag: PhysicsAdditive.Kind.Collision, entityA: var a, entityB: var b }: sum += a + b; break;
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
                case { tag: PhysicsBoxedFields.Kind.Impulse, magnitude: var m }: sum += m; break;
                case { tag: PhysicsBoxedFields.Kind.Position, x: var x, y: var y }: sum += x + y; break;
                case { tag: PhysicsBoxedFields.Kind.Force, fx: var fx, fy: var fy, fz: var fz }: sum += fx * fy + fz; break;
                case { tag: PhysicsBoxedFields.Kind.Rotation, x: var x, y: var y, z: var z, w: var w }: sum += x * y + z * w; break;
                case { tag: PhysicsBoxedFields.Kind.Spring, k: var k, damping: var d, rest: var r, min: var mn, max: var mx }: sum += k * d + r * mn + mx; break;
                case { tag: PhysicsBoxedFields.Kind.Gravity, g: var g }: sum += g; break;
                case { tag: PhysicsBoxedFields.Kind.Collision, entityA: var a, entityB: var b }: sum += a + b; break;
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
                case { tag: PhysicsBoxedTuple.Kind.Impulse, magnitude: var m }: sum += m; break;
                case { tag: PhysicsBoxedTuple.Kind.Position, x: var x, y: var y }: sum += x + y; break;
                case { tag: PhysicsBoxedTuple.Kind.Force, fx: var fx, fy: var fy, fz: var fz }: sum += fx * fy + fz; break;
                case { tag: PhysicsBoxedTuple.Kind.Rotation, x: var x, y: var y, z: var z, w: var w }: sum += x * y + z * w; break;
                case { tag: PhysicsBoxedTuple.Kind.Spring, k: var k, damping: var d, rest: var r, min: var mn, max: var mx }: sum += k * d + r * mn + mx; break;
                case { tag: PhysicsBoxedTuple.Kind.Gravity, g: var g }: sum += g; break;
                case { tag: PhysicsBoxedTuple.Kind.Collision, entityA: var a, entityB: var b }: sum += a + b; break;
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
                case { tag: PhysicsOverlap.Kind.Impulse, magnitude: var m }: sum += m; break;
                case { tag: PhysicsOverlap.Kind.Position, x: var x, y: var y }: sum += x + y; break;
                case { tag: PhysicsOverlap.Kind.Force, fx: var fx, fy: var fy, fz: var fz }: sum += fx * fy + fz; break;
                case { tag: PhysicsOverlap.Kind.Rotation, x: var x, y: var y, z: var z, w: var w }: sum += x * y + z * w; break;
                case { tag: PhysicsOverlap.Kind.Spring, k: var k, damping: var d, rest: var r, min: var mn, max: var mx }: sum += k * d + r * mn + mx; break;
                case { tag: PhysicsOverlap.Kind.Gravity, g: var g }: sum += g; break;
                case { tag: PhysicsOverlap.Kind.Collision, entityA: var a, entityB: var b }: sum += a + b; break;
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
                case { tag: PhysicsUnsafeOverlap.Kind.Impulse, magnitude: var m }: sum += m; break;
                case { tag: PhysicsUnsafeOverlap.Kind.Position, x: var x, y: var y }: sum += x + y; break;
                case { tag: PhysicsUnsafeOverlap.Kind.Force, fx: var fx, fy: var fy, fz: var fz }: sum += fx * fy + fz; break;
                case { tag: PhysicsUnsafeOverlap.Kind.Rotation, x: var x, y: var y, z: var z, w: var w }: sum += x * y + z * w; break;
                case { tag: PhysicsUnsafeOverlap.Kind.Spring, k: var k, damping: var d, rest: var r, min: var mn, max: var mx }: sum += k * d + r * mn + mx; break;
                case { tag: PhysicsUnsafeOverlap.Kind.Gravity, g: var g }: sum += g; break;
                case { tag: PhysicsUnsafeOverlap.Kind.Collision, entityA: var a, entityB: var b }: sum += a + b; break;
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
                case PhysicsRecord.Impulse { Magnitude: var m }: sum += m; break;
                case PhysicsRecord.Position { X: var x, Y: var y }: sum += x + y; break;
                case PhysicsRecord.Force { FX: var fx, FY: var fy, FZ: var fz }: sum += fx * fy + fz; break;
                case PhysicsRecord.Rotation { X: var x, Y: var y, Z: var z, W: var w }: sum += x * y + z * w; break;
                case PhysicsRecord.Spring { K: var k, Damping: var d, Rest: var r, Min: var mn, Max: var mx }: sum += k * d + r * mn + mx; break;
                case PhysicsRecord.Gravity { G: var g }: sum += g; break;
                case PhysicsRecord.Collision { EntityA: var a, EntityB: var b }: sum += a + b; break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Property"), Benchmark(Description = "class")]
    public double PropertyClass()
    {
        double sum = 0;
        var arr = _class;
        for (int i = 0; i < arr.Length; i++)
        {
            switch (arr[i])
            {
                case PhysicsClass.Impulse { Magnitude: var m }: sum += m; break;
                case PhysicsClass.Position { X: var x, Y: var y }: sum += x + y; break;
                case PhysicsClass.Force { FX: var fx, FY: var fy, FZ: var fz }: sum += fx * fy + fz; break;
                case PhysicsClass.Rotation { X: var x, Y: var y, Z: var z, W: var w }: sum += x * y + z * w; break;
                case PhysicsClass.Spring { K: var k, Damping: var d, Rest: var r, Min: var mn, Max: var mx }: sum += k * d + r * mn + mx; break;
                case PhysicsClass.Gravity { G: var g }: sum += g; break;
                case PhysicsClass.Collision { EntityA: var a, EntityB: var b }: sum += a + b; break;
            }
        }
        return sum;
    }
}
