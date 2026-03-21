using Spire;

namespace Spire.Benchmarks;

// 8 variants, all unmanaged fields:
//   Idle()                                                    — 0 fields
//   Impulse(float magnitude)                                  — 1 field
//   Position(float x, float y)                                — 2 fields
//   Force(float fx, float fy, float fz)                       — 3 fields
//   Rotation(float x, float y, float z, float w)              — 4 fields
//   Spring(float k, float damping, float rest, float min, float max) — 5 fields
//   Gravity(double g)                                         — 1 field
//   Collision(int entityA, int entityB)                        — 2 fields

[DiscriminatedUnion(Layout.Additive)]
public partial struct PhysicsAdditive
{
    [Variant] public static partial PhysicsAdditive Idle();
    [Variant] public static partial PhysicsAdditive Impulse(float magnitude);
    [Variant] public static partial PhysicsAdditive Position(float x, float y);
    [Variant] public static partial PhysicsAdditive Force(float fx, float fy, float fz);
    [Variant] public static partial PhysicsAdditive Rotation(float x, float y, float z, float w);
    [Variant] public static partial PhysicsAdditive Spring(float k, float damping, float rest, float min, float max);
    [Variant] public static partial PhysicsAdditive Gravity(double g);
    [Variant] public static partial PhysicsAdditive Collision(int entityA, int entityB);
}

[DiscriminatedUnion(Layout.BoxedFields)]
public partial struct PhysicsBoxedFields
{
    [Variant] public static partial PhysicsBoxedFields Idle();
    [Variant] public static partial PhysicsBoxedFields Impulse(float magnitude);
    [Variant] public static partial PhysicsBoxedFields Position(float x, float y);
    [Variant] public static partial PhysicsBoxedFields Force(float fx, float fy, float fz);
    [Variant] public static partial PhysicsBoxedFields Rotation(float x, float y, float z, float w);
    [Variant] public static partial PhysicsBoxedFields Spring(float k, float damping, float rest, float min, float max);
    [Variant] public static partial PhysicsBoxedFields Gravity(double g);
    [Variant] public static partial PhysicsBoxedFields Collision(int entityA, int entityB);
}

[DiscriminatedUnion(Layout.BoxedTuple)]
public partial struct PhysicsBoxedTuple
{
    [Variant] public static partial PhysicsBoxedTuple Idle();
    [Variant] public static partial PhysicsBoxedTuple Impulse(float magnitude);
    [Variant] public static partial PhysicsBoxedTuple Position(float x, float y);
    [Variant] public static partial PhysicsBoxedTuple Force(float fx, float fy, float fz);
    [Variant] public static partial PhysicsBoxedTuple Rotation(float x, float y, float z, float w);
    [Variant] public static partial PhysicsBoxedTuple Spring(float k, float damping, float rest, float min, float max);
    [Variant] public static partial PhysicsBoxedTuple Gravity(double g);
    [Variant] public static partial PhysicsBoxedTuple Collision(int entityA, int entityB);
}

[DiscriminatedUnion(Layout.Overlap)]
public partial struct PhysicsOverlap
{
    [Variant] public static partial PhysicsOverlap Idle();
    [Variant] public static partial PhysicsOverlap Impulse(float magnitude);
    [Variant] public static partial PhysicsOverlap Position(float x, float y);
    [Variant] public static partial PhysicsOverlap Force(float fx, float fy, float fz);
    [Variant] public static partial PhysicsOverlap Rotation(float x, float y, float z, float w);
    [Variant] public static partial PhysicsOverlap Spring(float k, float damping, float rest, float min, float max);
    [Variant] public static partial PhysicsOverlap Gravity(double g);
    [Variant] public static partial PhysicsOverlap Collision(int entityA, int entityB);
}

[DiscriminatedUnion(Layout.UnsafeOverlap)]
public partial struct PhysicsUnsafeOverlap
{
    [Variant] public static partial PhysicsUnsafeOverlap Idle();
    [Variant] public static partial PhysicsUnsafeOverlap Impulse(float magnitude);
    [Variant] public static partial PhysicsUnsafeOverlap Position(float x, float y);
    [Variant] public static partial PhysicsUnsafeOverlap Force(float fx, float fy, float fz);
    [Variant] public static partial PhysicsUnsafeOverlap Rotation(float x, float y, float z, float w);
    [Variant] public static partial PhysicsUnsafeOverlap Spring(float k, float damping, float rest, float min, float max);
    [Variant] public static partial PhysicsUnsafeOverlap Gravity(double g);
    [Variant] public static partial PhysicsUnsafeOverlap Collision(int entityA, int entityB);
}

[DiscriminatedUnion]
public abstract partial record PhysicsRecord
{
    public sealed partial record Idle() : PhysicsRecord;
    public sealed partial record Impulse(float Magnitude) : PhysicsRecord;
    public sealed partial record Position(float X, float Y) : PhysicsRecord;
    public sealed partial record Force(float FX, float FY, float FZ) : PhysicsRecord;
    public sealed partial record Rotation(float X, float Y, float Z, float W) : PhysicsRecord;
    public sealed partial record Spring(float K, float Damping, float Rest, float Min, float Max) : PhysicsRecord;
    public sealed partial record Gravity(double G) : PhysicsRecord;
    public sealed partial record Collision(int EntityA, int EntityB) : PhysicsRecord;
}

[DiscriminatedUnion]
public abstract partial class PhysicsClass
{
    public sealed partial class Idle : PhysicsClass;
    public sealed partial class Impulse(float magnitude) : PhysicsClass { public float Magnitude => magnitude; }
    public sealed partial class Position(float x, float y) : PhysicsClass { public float X => x; public float Y => y; }
    public sealed partial class Force(float fx, float fy, float fz) : PhysicsClass { public float FX => fx; public float FY => fy; public float FZ => fz; }
    public sealed partial class Rotation(float x, float y, float z, float w) : PhysicsClass { public float X => x; public float Y => y; public float Z => z; public float W => w; }
    public sealed partial class Spring(float k, float damping, float rest, float min, float max) : PhysicsClass { public float K => k; public float Damping => damping; public float Rest => rest; public float Min => min; public float Max => max; }
    public sealed partial class Gravity(double g) : PhysicsClass { public double G => g; }
    public sealed partial class Collision(int entityA, int entityB) : PhysicsClass { public int EntityA => entityA; public int EntityB => entityB; }
}
