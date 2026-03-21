using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spire.Benchmarks;

// 8 unmanaged-only variants, 0-5 fields each:
//   Idle()                                            — 0 fields
//   Impulse(float magnitude)                          — 1 field
//   Position(float x, float y)                        — 2 fields
//   Force(float fx, float fy, float fz)               — 3 fields
//   Rotation(float x, float y, float z, float w)      — 4 fields
//   Spring(float k, float damping, float rest, float min, float max) — 5 fields
//   Gravity(double g)                                 — 1 field (double)
//   Collision(int entityA, int entityB)               — 2 fields (int)

public enum PhysicsKind : byte {
    Idle,
    Impulse,
    Position,
    Force,
    Rotation,
    Spring,
    Gravity,
    Collision,
}

// ── Layout A: Explicit (unmanaged, overlapping at sizeof(PhysicsKind)) ──

[StructLayout(LayoutKind.Explicit)]
public readonly struct PhysicsExplicit {
    [FieldOffset(0)] public readonly PhysicsKind tag;

    // All overlapping at offset 1 (sizeof(PhysicsKind) = 1 byte)
    // 0-field: Idle — no data field
    [FieldOffset(sizeof(PhysicsKind))] readonly float _impulse_magnitude;
    [FieldOffset(sizeof(PhysicsKind))] readonly (float x, float y) _position;
    [FieldOffset(sizeof(PhysicsKind))] readonly (float fx, float fy, float fz) _force;
    [FieldOffset(sizeof(PhysicsKind))] readonly (float x, float y, float z, float w) _rotation;
    [FieldOffset(sizeof(PhysicsKind))] readonly (float k, float damping, float rest, float min, float max) _spring;
    [FieldOffset(sizeof(PhysicsKind))] readonly double _gravity_g;
    [FieldOffset(sizeof(PhysicsKind))] readonly (int entityA, int entityB) _collision;

    PhysicsExplicit(PhysicsKind tag) : this() { this.tag = tag; }

    public static PhysicsExplicit NewIdle() => new(PhysicsKind.Idle);
    public static PhysicsExplicit NewImpulse(float magnitude) {
        var s = new PhysicsExplicit(PhysicsKind.Impulse);
        Unsafe.AsRef(in s._impulse_magnitude) = magnitude;
        return s;
    }
    public static PhysicsExplicit NewPosition(float x, float y) {
        var s = new PhysicsExplicit(PhysicsKind.Position);
        Unsafe.AsRef(in s._position) = (x, y);
        return s;
    }
    public static PhysicsExplicit NewForce(float fx, float fy, float fz) {
        var s = new PhysicsExplicit(PhysicsKind.Force);
        Unsafe.AsRef(in s._force) = (fx, fy, fz);
        return s;
    }
    public static PhysicsExplicit NewRotation(float x, float y, float z, float w) {
        var s = new PhysicsExplicit(PhysicsKind.Rotation);
        Unsafe.AsRef(in s._rotation) = (x, y, z, w);
        return s;
    }
    public static PhysicsExplicit NewSpring(float k, float damping, float rest, float min, float max) {
        var s = new PhysicsExplicit(PhysicsKind.Spring);
        Unsafe.AsRef(in s._spring) = (k, damping, rest, min, max);
        return s;
    }
    public static PhysicsExplicit NewGravity(double g) {
        var s = new PhysicsExplicit(PhysicsKind.Gravity);
        Unsafe.AsRef(in s._gravity_g) = g;
        return s;
    }
    public static PhysicsExplicit NewCollision(int entityA, int entityB) {
        var s = new PhysicsExplicit(PhysicsKind.Collision);
        Unsafe.AsRef(in s._collision) = (entityA, entityB);
        return s;
    }

    // Deconstruct — object? (boxes at match site)
    public void Deconstruct(out PhysicsKind kind, out object? payload) {
        kind = tag;
        payload = tag switch {
            PhysicsKind.Idle => null,
            PhysicsKind.Impulse => _impulse_magnitude,
            PhysicsKind.Position => _position,
            PhysicsKind.Force => _force,
            PhysicsKind.Rotation => _rotation,
            PhysicsKind.Spring => _spring,
            PhysicsKind.Gravity => _gravity_g,
            PhysicsKind.Collision => _collision,
            _ => null,
        };
    }

    // Typed accessors for property patterns
    public float ImpulseMagnitude => _impulse_magnitude;
    public (float x, float y) Pos => _position;
    public (float fx, float fy, float fz) Frc => _force;
    public (float x, float y, float z, float w) Rot => _rotation;
    public (float k, float damping, float rest, float min, float max) Spr => _spring;
    public double GravityG => _gravity_g;
    public (int entityA, int entityB) Col => _collision;
}

// ── Layout B: Multi object? fields (max 5) ──

public readonly struct PhysicsMultiObj {
    public readonly PhysicsKind tag;
    readonly object? _f0, _f1, _f2, _f3, _f4;

    PhysicsMultiObj(PhysicsKind tag, object? f0, object? f1, object? f2, object? f3, object? f4) {
        this.tag = tag; _f0 = f0; _f1 = f1; _f2 = f2; _f3 = f3; _f4 = f4;
    }

    public static PhysicsMultiObj NewIdle() => new(PhysicsKind.Idle, null, null, null, null, null);
    public static PhysicsMultiObj NewImpulse(float mag) => new(PhysicsKind.Impulse, mag, null, null, null, null);
    public static PhysicsMultiObj NewPosition(float x, float y) => new(PhysicsKind.Position, x, y, null, null, null);
    public static PhysicsMultiObj NewForce(float fx, float fy, float fz) => new(PhysicsKind.Force, fx, fy, fz, null, null);
    public static PhysicsMultiObj NewRotation(float x, float y, float z, float w) => new(PhysicsKind.Rotation, x, y, z, w, null);
    public static PhysicsMultiObj NewSpring(float k, float d, float r, float mn, float mx) => new(PhysicsKind.Spring, k, d, r, mn, mx);
    public static PhysicsMultiObj NewGravity(double g) => new(PhysicsKind.Gravity, g, null, null, null, null);
    public static PhysicsMultiObj NewCollision(int a, int b) => new(PhysicsKind.Collision, a, b, null, null, null);

    public void Deconstruct(out PhysicsKind kind, out object? f0) {
        kind = tag; f0 = _f0;
    }
    public void Deconstruct(out PhysicsKind kind, out object? f0, out object? f1) {
        kind = tag; f0 = _f0; f1 = _f1;
    }
}

// ── Layout C: Single object? payload (tuple) ──

public readonly struct PhysicsTupleObj {
    public readonly PhysicsKind tag;
    readonly object? _payload;

    PhysicsTupleObj(PhysicsKind tag, object? payload) {
        this.tag = tag; _payload = payload;
    }

    public static PhysicsTupleObj NewIdle() => new(PhysicsKind.Idle, null);
    public static PhysicsTupleObj NewImpulse(float mag) => new(PhysicsKind.Impulse, mag);
    public static PhysicsTupleObj NewPosition(float x, float y) => new(PhysicsKind.Position, (x, y));
    public static PhysicsTupleObj NewForce(float fx, float fy, float fz) => new(PhysicsKind.Force, (fx, fy, fz));
    public static PhysicsTupleObj NewRotation(float x, float y, float z, float w) => new(PhysicsKind.Rotation, (x, y, z, w));
    public static PhysicsTupleObj NewSpring(float k, float d, float r, float mn, float mx) => new(PhysicsKind.Spring, (k, d, r, mn, mx));
    public static PhysicsTupleObj NewGravity(double g) => new(PhysicsKind.Gravity, g);
    public static PhysicsTupleObj NewCollision(int a, int b) => new(PhysicsKind.Collision, (a, b));

    public void Deconstruct(out PhysicsKind kind, out object? payload) {
        kind = tag; payload = _payload;
    }
}

// ── Layout D: Abstract class hierarchy ──

public abstract class PhysicsClass {
    public sealed class IdleClass : PhysicsClass;
    public sealed class ImpulseClass(float Magnitude) : PhysicsClass { public float Magnitude { get; } = Magnitude; }
    public sealed class PositionClass(float X, float Y) : PhysicsClass { public float X { get; } = X; public float Y { get; } = Y; }
    public sealed class ForceClass(float Fx, float Fy, float Fz) : PhysicsClass { public float Fx { get; } = Fx; public float Fy { get; } = Fy; public float Fz { get; } = Fz; }
    public sealed class RotationClass(float X, float Y, float Z, float W) : PhysicsClass { public float X { get; } = X; public float Y { get; } = Y; public float Z { get; } = Z; public float W { get; } = W; }
    public sealed class SpringClass(float K, float Damping, float Rest, float Min, float Max) : PhysicsClass { public float K { get; } = K; public float Damping { get; } = Damping; public float Rest { get; } = Rest; public float Min { get; } = Min; public float Max { get; } = Max; }
    public sealed class GravityClass(double G) : PhysicsClass { public double G { get; } = G; }
    public sealed class CollisionClass(int EntityA, int EntityB) : PhysicsClass { public int EntityA { get; } = EntityA; public int EntityB { get; } = EntityB; }

    public static PhysicsClass NewIdle() => new IdleClass();
    public static PhysicsClass NewImpulse(float mag) => new ImpulseClass(mag);
    public static PhysicsClass NewPosition(float x, float y) => new PositionClass(x, y);
    public static PhysicsClass NewForce(float fx, float fy, float fz) => new ForceClass(fx, fy, fz);
    public static PhysicsClass NewRotation(float x, float y, float z, float w) => new RotationClass(x, y, z, w);
    public static PhysicsClass NewSpring(float k, float d, float r, float mn, float mx) => new SpringClass(k, d, r, mn, mx);
    public static PhysicsClass NewGravity(double g) => new GravityClass(g);
    public static PhysicsClass NewCollision(int a, int b) => new CollisionClass(a, b);
}

// ── Layout E: Additive (typed field dedup, no boxing, no StructLayout.Explicit) ──
//
// float  slots: max(Impulse=1, Position=2, Force=3, Rotation=4, Spring=5) = 5
// double slots: max(Gravity=1) = 1
// int    slots: max(Collision=2) = 2

public struct PhysicsAdditive {
    public readonly PhysicsKind tag;
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal float _s0, _s1, _s2, _s3, _s4;
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal double _s5;
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal int _s6, _s7;

    PhysicsAdditive(PhysicsKind tag, float s0, float s1, float s2, float s3, float s4,
                    double s5, int s6, int s7) {
        this.tag = tag;
        _s0 = s0; _s1 = s1; _s2 = s2; _s3 = s3; _s4 = s4;
        _s5 = s5; _s6 = s6; _s7 = s7;
    }

    public static PhysicsAdditive NewIdle()
        => new(PhysicsKind.Idle, default, default, default, default, default, default, default, default);
    public static PhysicsAdditive NewImpulse(float magnitude)
        => new(PhysicsKind.Impulse, magnitude, default, default, default, default, default, default, default);
    public static PhysicsAdditive NewPosition(float x, float y)
        => new(PhysicsKind.Position, x, y, default, default, default, default, default, default);
    public static PhysicsAdditive NewForce(float fx, float fy, float fz)
        => new(PhysicsKind.Force, fx, fy, fz, default, default, default, default, default);
    public static PhysicsAdditive NewRotation(float x, float y, float z, float w)
        => new(PhysicsKind.Rotation, x, y, z, w, default, default, default, default);
    public static PhysicsAdditive NewSpring(float k, float damping, float rest, float min, float max)
        => new(PhysicsKind.Spring, k, damping, rest, min, max, default, default, default);
    public static PhysicsAdditive NewGravity(double g)
        => new(PhysicsKind.Gravity, default, default, default, default, default, g, default, default);
    public static PhysicsAdditive NewCollision(int entityA, int entityB)
        => new(PhysicsKind.Collision, default, default, default, default, default, default, entityA, entityB);

    // Typed accessors
    public float ImpulseMagnitude => _s0;
    public (float x, float y) Pos => (_s0, _s1);
    public (float fx, float fy, float fz) Frc => (_s0, _s1, _s2);
    public (float x, float y, float z, float w) Rot => (_s0, _s1, _s2, _s3);
    public (float k, float damping, float rest, float min, float max) Spr => (_s0, _s1, _s2, _s3, _s4);
    public double GravityG => _s5;
    public (int entityA, int entityB) Col => (_s6, _s7);
}

// ── Layout F: UnsafeOverlap (InlineArray) — byte buffer for unmanaged overlap ──
//
// Max unmanaged bytes per variant:
//   Idle=0, Impulse=4, Position=8, Force=12, Rotation=16, Spring=20, Gravity=8, Collision=8
//   Max = 20 bytes

public struct PhysicsUnsafeInline {
    public readonly PhysicsKind tag;

    [InlineArray(20)]
    internal struct _Buffer { internal byte _element; }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal _Buffer _data;

    PhysicsUnsafeInline(PhysicsKind tag) { this.tag = tag; _data = default; }

    public static PhysicsUnsafeInline NewIdle() => new(PhysicsKind.Idle);
    public static PhysicsUnsafeInline NewImpulse(float magnitude) {
        var s = new PhysicsUnsafeInline(PhysicsKind.Impulse);
        Unsafe.WriteUnaligned(ref s._data[0], magnitude);
        return s;
    }
    public static PhysicsUnsafeInline NewPosition(float x, float y) {
        var s = new PhysicsUnsafeInline(PhysicsKind.Position);
        Unsafe.WriteUnaligned(ref s._data[0], x);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref s._data[0], 4), y);
        return s;
    }
    public static PhysicsUnsafeInline NewForce(float fx, float fy, float fz) {
        var s = new PhysicsUnsafeInline(PhysicsKind.Force);
        Unsafe.WriteUnaligned(ref s._data[0], fx);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref s._data[0], 4), fy);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref s._data[0], 8), fz);
        return s;
    }
    public static PhysicsUnsafeInline NewRotation(float x, float y, float z, float w) {
        var s = new PhysicsUnsafeInline(PhysicsKind.Rotation);
        Unsafe.WriteUnaligned(ref s._data[0], x);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref s._data[0], 4), y);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref s._data[0], 8), z);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref s._data[0], 12), w);
        return s;
    }
    public static PhysicsUnsafeInline NewSpring(float k, float damping, float rest, float min, float max) {
        var s = new PhysicsUnsafeInline(PhysicsKind.Spring);
        Unsafe.WriteUnaligned(ref s._data[0], k);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref s._data[0], 4), damping);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref s._data[0], 8), rest);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref s._data[0], 12), min);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref s._data[0], 16), max);
        return s;
    }
    public static PhysicsUnsafeInline NewGravity(double g) {
        var s = new PhysicsUnsafeInline(PhysicsKind.Gravity);
        Unsafe.WriteUnaligned(ref s._data[0], g);
        return s;
    }
    public static PhysicsUnsafeInline NewCollision(int entityA, int entityB) {
        var s = new PhysicsUnsafeInline(PhysicsKind.Collision);
        Unsafe.WriteUnaligned(ref s._data[0], entityA);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref s._data[0], 4), entityB);
        return s;
    }

    // Typed accessors
    public float ImpulseMagnitude => Unsafe.ReadUnaligned<float>(ref _data[0]);
    public (float x, float y) Pos => (
        Unsafe.ReadUnaligned<float>(ref _data[0]),
        Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref _data[0], 4)));
    public (float fx, float fy, float fz) Frc => (
        Unsafe.ReadUnaligned<float>(ref _data[0]),
        Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref _data[0], 4)),
        Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref _data[0], 8)));
    public (float x, float y, float z, float w) Rot => (
        Unsafe.ReadUnaligned<float>(ref _data[0]),
        Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref _data[0], 4)),
        Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref _data[0], 8)),
        Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref _data[0], 12)));
    public (float k, float damping, float rest, float min, float max) Spr => (
        Unsafe.ReadUnaligned<float>(ref _data[0]),
        Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref _data[0], 4)),
        Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref _data[0], 8)),
        Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref _data[0], 12)),
        Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref _data[0], 16)));
    public double GravityG => Unsafe.ReadUnaligned<double>(ref _data[0]);
    public (int entityA, int entityB) Col => (
        Unsafe.ReadUnaligned<int>(ref _data[0]),
        Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref _data[0], 4)));
}

// ── Layout G: UnsafeOverlap (MultiLong) — long fields as byte buffer, pre-.NET 8 fallback ──
//
// Buffer = 20 bytes, rounded up to 24 (3 longs)

public struct PhysicsUnsafeLong {
    public readonly PhysicsKind tag;

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal long _data0, _data1, _data2;

    PhysicsUnsafeLong(PhysicsKind tag) { this.tag = tag; _data0 = 0; _data1 = 0; _data2 = 0; }

    public static PhysicsUnsafeLong NewIdle() => new(PhysicsKind.Idle);
    public static PhysicsUnsafeLong NewImpulse(float magnitude) {
        var s = new PhysicsUnsafeLong(PhysicsKind.Impulse);
        ref byte start = ref Unsafe.As<long, byte>(ref s._data0);
        Unsafe.WriteUnaligned(ref start, magnitude);
        return s;
    }
    public static PhysicsUnsafeLong NewPosition(float x, float y) {
        var s = new PhysicsUnsafeLong(PhysicsKind.Position);
        ref byte start = ref Unsafe.As<long, byte>(ref s._data0);
        Unsafe.WriteUnaligned(ref start, x);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, 4), y);
        return s;
    }
    public static PhysicsUnsafeLong NewForce(float fx, float fy, float fz) {
        var s = new PhysicsUnsafeLong(PhysicsKind.Force);
        ref byte start = ref Unsafe.As<long, byte>(ref s._data0);
        Unsafe.WriteUnaligned(ref start, fx);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, 4), fy);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, 8), fz);
        return s;
    }
    public static PhysicsUnsafeLong NewRotation(float x, float y, float z, float w) {
        var s = new PhysicsUnsafeLong(PhysicsKind.Rotation);
        ref byte start = ref Unsafe.As<long, byte>(ref s._data0);
        Unsafe.WriteUnaligned(ref start, x);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, 4), y);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, 8), z);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, 12), w);
        return s;
    }
    public static PhysicsUnsafeLong NewSpring(float k, float damping, float rest, float min, float max) {
        var s = new PhysicsUnsafeLong(PhysicsKind.Spring);
        ref byte start = ref Unsafe.As<long, byte>(ref s._data0);
        Unsafe.WriteUnaligned(ref start, k);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, 4), damping);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, 8), rest);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, 12), min);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, 16), max);
        return s;
    }
    public static PhysicsUnsafeLong NewGravity(double g) {
        var s = new PhysicsUnsafeLong(PhysicsKind.Gravity);
        ref byte start = ref Unsafe.As<long, byte>(ref s._data0);
        Unsafe.WriteUnaligned(ref start, g);
        return s;
    }
    public static PhysicsUnsafeLong NewCollision(int entityA, int entityB) {
        var s = new PhysicsUnsafeLong(PhysicsKind.Collision);
        ref byte start = ref Unsafe.As<long, byte>(ref s._data0);
        Unsafe.WriteUnaligned(ref start, entityA);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, 4), entityB);
        return s;
    }

    // Typed accessors
    public float ImpulseMagnitude {
        get { ref byte start = ref Unsafe.As<long, byte>(ref _data0); return Unsafe.ReadUnaligned<float>(ref start); }
    }
    public (float x, float y) Pos {
        get {
            ref byte start = ref Unsafe.As<long, byte>(ref _data0);
            return (Unsafe.ReadUnaligned<float>(ref start),
                    Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref start, 4)));
        }
    }
    public (float fx, float fy, float fz) Frc {
        get {
            ref byte start = ref Unsafe.As<long, byte>(ref _data0);
            return (Unsafe.ReadUnaligned<float>(ref start),
                    Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref start, 4)),
                    Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref start, 8)));
        }
    }
    public (float x, float y, float z, float w) Rot {
        get {
            ref byte start = ref Unsafe.As<long, byte>(ref _data0);
            return (Unsafe.ReadUnaligned<float>(ref start),
                    Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref start, 4)),
                    Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref start, 8)),
                    Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref start, 12)));
        }
    }
    public (float k, float damping, float rest, float min, float max) Spr {
        get {
            ref byte start = ref Unsafe.As<long, byte>(ref _data0);
            return (Unsafe.ReadUnaligned<float>(ref start),
                    Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref start, 4)),
                    Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref start, 8)),
                    Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref start, 12)),
                    Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref start, 16)));
        }
    }
    public double GravityG {
        get { ref byte start = ref Unsafe.As<long, byte>(ref _data0); return Unsafe.ReadUnaligned<double>(ref start); }
    }
    public (int entityA, int entityB) Col {
        get {
            ref byte start = ref Unsafe.As<long, byte>(ref _data0);
            return (Unsafe.ReadUnaligned<int>(ref start),
                    Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref start, 4)));
        }
    }
}

// ── Helper ──

public static class PhysicsFactory {
    public static void Fill(Random rng, int n,
        PhysicsExplicit[] expl, PhysicsMultiObj[] multi,
        PhysicsTupleObj[] tuple, PhysicsClass[] cls,
        PhysicsAdditive[]? additive = null,
        PhysicsUnsafeInline[]? unsafeInline = null,
        PhysicsUnsafeLong[]? unsafeLong = null) {

        for (int i = 0; i < n; i++) {
            int v = rng.Next(8);
            switch (v) {
                case 0:
                    expl[i] = PhysicsExplicit.NewIdle();
                    multi[i] = PhysicsMultiObj.NewIdle();
                    tuple[i] = PhysicsTupleObj.NewIdle();
                    cls[i] = PhysicsClass.NewIdle();
                    if (additive != null) additive[i] = PhysicsAdditive.NewIdle();
                    if (unsafeInline != null) unsafeInline[i] = PhysicsUnsafeInline.NewIdle();
                    if (unsafeLong != null) unsafeLong[i] = PhysicsUnsafeLong.NewIdle();
                    break;
                case 1:
                    float mag = rng.NextSingle() * 100;
                    expl[i] = PhysicsExplicit.NewImpulse(mag);
                    multi[i] = PhysicsMultiObj.NewImpulse(mag);
                    tuple[i] = PhysicsTupleObj.NewImpulse(mag);
                    cls[i] = PhysicsClass.NewImpulse(mag);
                    if (additive != null) additive[i] = PhysicsAdditive.NewImpulse(mag);
                    if (unsafeInline != null) unsafeInline[i] = PhysicsUnsafeInline.NewImpulse(mag);
                    if (unsafeLong != null) unsafeLong[i] = PhysicsUnsafeLong.NewImpulse(mag);
                    break;
                case 2:
                    float px = rng.NextSingle() * 100, py = rng.NextSingle() * 100;
                    expl[i] = PhysicsExplicit.NewPosition(px, py);
                    multi[i] = PhysicsMultiObj.NewPosition(px, py);
                    tuple[i] = PhysicsTupleObj.NewPosition(px, py);
                    cls[i] = PhysicsClass.NewPosition(px, py);
                    if (additive != null) additive[i] = PhysicsAdditive.NewPosition(px, py);
                    if (unsafeInline != null) unsafeInline[i] = PhysicsUnsafeInline.NewPosition(px, py);
                    if (unsafeLong != null) unsafeLong[i] = PhysicsUnsafeLong.NewPosition(px, py);
                    break;
                case 3:
                    float fx = rng.NextSingle(), fy = rng.NextSingle(), fz = rng.NextSingle();
                    expl[i] = PhysicsExplicit.NewForce(fx, fy, fz);
                    multi[i] = PhysicsMultiObj.NewForce(fx, fy, fz);
                    tuple[i] = PhysicsTupleObj.NewForce(fx, fy, fz);
                    cls[i] = PhysicsClass.NewForce(fx, fy, fz);
                    if (additive != null) additive[i] = PhysicsAdditive.NewForce(fx, fy, fz);
                    if (unsafeInline != null) unsafeInline[i] = PhysicsUnsafeInline.NewForce(fx, fy, fz);
                    if (unsafeLong != null) unsafeLong[i] = PhysicsUnsafeLong.NewForce(fx, fy, fz);
                    break;
                case 4:
                    float rx = rng.NextSingle(), ry = rng.NextSingle();
                    float rz = rng.NextSingle(), rw = rng.NextSingle();
                    expl[i] = PhysicsExplicit.NewRotation(rx, ry, rz, rw);
                    multi[i] = PhysicsMultiObj.NewRotation(rx, ry, rz, rw);
                    tuple[i] = PhysicsTupleObj.NewRotation(rx, ry, rz, rw);
                    cls[i] = PhysicsClass.NewRotation(rx, ry, rz, rw);
                    if (additive != null) additive[i] = PhysicsAdditive.NewRotation(rx, ry, rz, rw);
                    if (unsafeInline != null) unsafeInline[i] = PhysicsUnsafeInline.NewRotation(rx, ry, rz, rw);
                    if (unsafeLong != null) unsafeLong[i] = PhysicsUnsafeLong.NewRotation(rx, ry, rz, rw);
                    break;
                case 5:
                    float k = rng.NextSingle(), d = rng.NextSingle(), r = rng.NextSingle();
                    float mn = rng.NextSingle(), mx = rng.NextSingle();
                    expl[i] = PhysicsExplicit.NewSpring(k, d, r, mn, mx);
                    multi[i] = PhysicsMultiObj.NewSpring(k, d, r, mn, mx);
                    tuple[i] = PhysicsTupleObj.NewSpring(k, d, r, mn, mx);
                    cls[i] = PhysicsClass.NewSpring(k, d, r, mn, mx);
                    if (additive != null) additive[i] = PhysicsAdditive.NewSpring(k, d, r, mn, mx);
                    if (unsafeInline != null) unsafeInline[i] = PhysicsUnsafeInline.NewSpring(k, d, r, mn, mx);
                    if (unsafeLong != null) unsafeLong[i] = PhysicsUnsafeLong.NewSpring(k, d, r, mn, mx);
                    break;
                case 6:
                    double g = rng.NextDouble() * 20;
                    expl[i] = PhysicsExplicit.NewGravity(g);
                    multi[i] = PhysicsMultiObj.NewGravity(g);
                    tuple[i] = PhysicsTupleObj.NewGravity(g);
                    cls[i] = PhysicsClass.NewGravity(g);
                    if (additive != null) additive[i] = PhysicsAdditive.NewGravity(g);
                    if (unsafeInline != null) unsafeInline[i] = PhysicsUnsafeInline.NewGravity(g);
                    if (unsafeLong != null) unsafeLong[i] = PhysicsUnsafeLong.NewGravity(g);
                    break;
                case 7:
                    int a = rng.Next(1000), b = rng.Next(1000);
                    expl[i] = PhysicsExplicit.NewCollision(a, b);
                    multi[i] = PhysicsMultiObj.NewCollision(a, b);
                    tuple[i] = PhysicsTupleObj.NewCollision(a, b);
                    cls[i] = PhysicsClass.NewCollision(a, b);
                    if (additive != null) additive[i] = PhysicsAdditive.NewCollision(a, b);
                    if (unsafeInline != null) unsafeInline[i] = PhysicsUnsafeInline.NewCollision(a, b);
                    if (unsafeLong != null) unsafeLong[i] = PhysicsUnsafeLong.NewCollision(a, b);
                    break;
            }
        }
    }
}
