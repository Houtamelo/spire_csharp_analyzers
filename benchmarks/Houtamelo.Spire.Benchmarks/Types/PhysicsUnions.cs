using Houtamelo.Spire;

namespace Houtamelo.Spire.Benchmarks.Types;

// A single declaration is expanded into PhysicsBench (Auto), PhysicsAuto,
// PhysicsTyped, PhysicsShared, and PhysicsRecord.
[BenchmarkUnion(Name = "Physics")]
public partial struct PhysicsBench
{
    [Variant] public static partial PhysicsBench Idle();
    [Variant] public static partial PhysicsBench Impulse(float magnitude);
    [Variant] public static partial PhysicsBench Position(float x, float y);
    [Variant] public static partial PhysicsBench Force(float fx, float fy, float fz);
    [Variant] public static partial PhysicsBench Rotation(float x, float y, float z, float w);
    [Variant] public static partial PhysicsBench Spring(float k, float damping, float rest, float min, float max);
    [Variant] public static partial PhysicsBench Gravity(double g);
    [Variant] public static partial PhysicsBench Collision(int entityA, int entityB);
}
