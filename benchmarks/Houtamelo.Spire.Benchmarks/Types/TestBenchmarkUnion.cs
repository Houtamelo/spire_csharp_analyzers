namespace Houtamelo.Spire.Benchmarks.Types;

[BenchmarkUnion(Name = "Phys")]
public partial struct PhysBench
{
    [Variant] public static partial PhysBench Idle();
    [Variant] public static partial PhysBench Impulse(float magnitude);
    [Variant] public static partial PhysBench Position(float x, float y);
    [Variant] public static partial PhysBench Force(float fx, float fy, float fz);
    [Variant] public static partial PhysBench Rotation(float x, float y, float z, float w);
    [Variant] public static partial PhysBench Spring(float k, float damping, float rest, float min, float max);
    [Variant] public static partial PhysBench Gravity(double g);
    [Variant] public static partial PhysBench Collision(int entityA, int entityB);
}
