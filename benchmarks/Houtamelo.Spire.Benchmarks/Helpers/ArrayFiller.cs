using Houtamelo.Spire.Benchmarks.Types;

namespace Houtamelo.Spire.Benchmarks.Helpers;

/// Fills the current benchmark representations with deterministic data.
static class ArrayFiller
{
    private static readonly string[] Strings = ["hello", "world", "red", "blue", "Arial", "error"];

    public static void Fill(EventBench[] values, Random random, Distribution distribution)
        => FillEvents(values, random, distribution, EventBench.Point, EventBench.Circle, EventBench.Label,
            EventBench.Rectangle, EventBench.ColoredLine, EventBench.Transform, EventBench.RichText, EventBench.Error);

    public static void Fill(EventAuto[] values, Random random, Distribution distribution)
        => FillEvents(values, random, distribution, EventAuto.Point, EventAuto.Circle, EventAuto.Label,
            EventAuto.Rectangle, EventAuto.ColoredLine, EventAuto.Transform, EventAuto.RichText, EventAuto.Error);

    public static void Fill(EventTyped[] values, Random random, Distribution distribution)
        => FillEvents(values, random, distribution, EventTyped.Point, EventTyped.Circle, EventTyped.Label,
            EventTyped.Rectangle, EventTyped.ColoredLine, EventTyped.Transform, EventTyped.RichText, EventTyped.Error);

    public static void Fill(EventShared[] values, Random random, Distribution distribution)
        => FillEvents(values, random, distribution, EventShared.Point, EventShared.Circle, EventShared.Label,
            EventShared.Rectangle, EventShared.ColoredLine, EventShared.Transform, EventShared.RichText, EventShared.Error);

    public static void Fill(EventJsonBench[] values, Random random, Distribution distribution)
        => FillEvents(values, random, distribution, EventJsonBench.Point, EventJsonBench.Circle, EventJsonBench.Label,
            EventJsonBench.Rectangle, EventJsonBench.ColoredLine, EventJsonBench.Transform, EventJsonBench.RichText, EventJsonBench.Error);

    public static void Fill(EventJsonAuto[] values, Random random, Distribution distribution)
        => FillEvents(values, random, distribution, EventJsonAuto.Point, EventJsonAuto.Circle, EventJsonAuto.Label,
            EventJsonAuto.Rectangle, EventJsonAuto.ColoredLine, EventJsonAuto.Transform, EventJsonAuto.RichText, EventJsonAuto.Error);

    public static void Fill(EventJsonTyped[] values, Random random, Distribution distribution)
        => FillEvents(values, random, distribution, EventJsonTyped.Point, EventJsonTyped.Circle, EventJsonTyped.Label,
            EventJsonTyped.Rectangle, EventJsonTyped.ColoredLine, EventJsonTyped.Transform, EventJsonTyped.RichText, EventJsonTyped.Error);

    public static void Fill(EventJsonShared[] values, Random random, Distribution distribution)
        => FillEvents(values, random, distribution, EventJsonShared.Point, EventJsonShared.Circle, EventJsonShared.Label,
            EventJsonShared.Rectangle, EventJsonShared.ColoredLine, EventJsonShared.Transform, EventJsonShared.RichText, EventJsonShared.Error);

    public static void Fill(EventJsonRecord[] values, Random random, Distribution distribution)
    {
        for (var index = 0; index < values.Length; index++)
        {
            var variant = PickVariant(random, distribution, index, 8);
            values[index] = variant switch
            {
                0 => new EventJsonRecord.Point(),
                1 => new EventJsonRecord.Circle(random.NextDouble()),
                2 => new EventJsonRecord.Label(PickString(random)),
                3 => new EventJsonRecord.Rectangle(random.NextSingle(), random.NextSingle()),
                4 => new EventJsonRecord.ColoredLine(random.Next(), random.Next(), PickString(random)),
                5 => new EventJsonRecord.Transform(random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle()),
                6 => new EventJsonRecord.RichText(PickString(random), random.Next(8, 72), random.Next(2) == 1, PickString(random), random.NextDouble()),
                _ => new EventJsonRecord.Error(PickString(random)),
            };
        }
    }

    public static void Fill(EventRecord[] values, Random random, Distribution distribution)
    {
        for (var index = 0; index < values.Length; index++)
        {
            var variant = PickVariant(random, distribution, index, 8);
            values[index] = variant switch
            {
                0 => new EventRecord.Point(),
                1 => new EventRecord.Circle(random.NextDouble()),
                2 => new EventRecord.Label(PickString(random)),
                3 => new EventRecord.Rectangle(random.NextSingle(), random.NextSingle()),
                4 => new EventRecord.ColoredLine(random.Next(), random.Next(), PickString(random)),
                5 => new EventRecord.Transform(random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle()),
                6 => new EventRecord.RichText(PickString(random), random.Next(8, 72), random.Next(2) == 1, PickString(random), random.NextDouble()),
                _ => new EventRecord.Error(PickString(random)),
            };
        }
    }

    public static void Fill(EventNative[] values, Random random, Distribution distribution)
    {
        for (var index = 0; index < values.Length; index++)
        {
            var variant = PickVariant(random, distribution, index, 8);
            values[index] = variant switch
            {
                0 => new EvtPoint(),
                1 => new EvtCircle(random.NextDouble()),
                2 => new EvtLabel(PickString(random)),
                3 => new EvtRectangle(random.NextSingle(), random.NextSingle()),
                4 => new EvtColoredLine(random.Next(), random.Next(), PickString(random)),
                5 => new EvtTransform(random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle()),
                6 => new EvtRichText(PickString(random), random.Next(8, 72), random.Next(2) == 1, PickString(random), random.NextDouble()),
                _ => new EvtError(PickString(random)),
            };
        }
    }

    public static void Fill(PhysicsBench[] values, Random random, Distribution distribution)
        => FillPhysics(values, random, distribution, PhysicsBench.Idle, PhysicsBench.Impulse, PhysicsBench.Position,
            PhysicsBench.Force, PhysicsBench.Rotation, PhysicsBench.Spring, PhysicsBench.Gravity, PhysicsBench.Collision);

    public static void Fill(PhysicsAuto[] values, Random random, Distribution distribution)
        => FillPhysics(values, random, distribution, PhysicsAuto.Idle, PhysicsAuto.Impulse, PhysicsAuto.Position,
            PhysicsAuto.Force, PhysicsAuto.Rotation, PhysicsAuto.Spring, PhysicsAuto.Gravity, PhysicsAuto.Collision);

    public static void Fill(PhysicsTyped[] values, Random random, Distribution distribution)
        => FillPhysics(values, random, distribution, PhysicsTyped.Idle, PhysicsTyped.Impulse, PhysicsTyped.Position,
            PhysicsTyped.Force, PhysicsTyped.Rotation, PhysicsTyped.Spring, PhysicsTyped.Gravity, PhysicsTyped.Collision);

    public static void Fill(PhysicsShared[] values, Random random, Distribution distribution)
        => FillPhysics(values, random, distribution, PhysicsShared.Idle, PhysicsShared.Impulse, PhysicsShared.Position,
            PhysicsShared.Force, PhysicsShared.Rotation, PhysicsShared.Spring, PhysicsShared.Gravity, PhysicsShared.Collision);

    public static void Fill(PhysicsRecord[] values, Random random, Distribution distribution)
    {
        for (var index = 0; index < values.Length; index++)
        {
            var variant = PickVariant(random, distribution, index, 8);
            values[index] = variant switch
            {
                0 => new PhysicsRecord.Idle(),
                1 => new PhysicsRecord.Impulse(random.NextSingle()),
                2 => new PhysicsRecord.Position(random.NextSingle(), random.NextSingle()),
                3 => new PhysicsRecord.Force(random.NextSingle(), random.NextSingle(), random.NextSingle()),
                4 => new PhysicsRecord.Rotation(random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle()),
                5 => new PhysicsRecord.Spring(random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle()),
                6 => new PhysicsRecord.Gravity(random.NextDouble()),
                _ => new PhysicsRecord.Collision(random.Next(), random.Next()),
            };
        }
    }

    public static void Fill(PhysicsNative[] values, Random random, Distribution distribution)
    {
        for (var index = 0; index < values.Length; index++)
        {
            var variant = PickVariant(random, distribution, index, 8);
            values[index] = variant switch
            {
                0 => new PhysIdle(),
                1 => new PhysImpulse(random.NextSingle()),
                2 => new PhysPosition(random.NextSingle(), random.NextSingle()),
                3 => new PhysForce(random.NextSingle(), random.NextSingle(), random.NextSingle()),
                4 => new PhysRotation(random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle()),
                5 => new PhysSpring(random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle()),
                6 => new PhysGravity(random.NextDouble()),
                _ => new PhysCollision(random.Next(), random.Next()),
            };
        }
    }

    private static void FillEvents<T>(
        T[] values,
        Random random,
        Distribution distribution,
        Func<T> point,
        Func<double, T> circle,
        Func<string, T> label,
        Func<float, float, T> rectangle,
        Func<int, int, string, T> coloredLine,
        Func<float, float, float, float, T> transform,
        Func<string, int, bool, string, double, T> richText,
        Func<string, T> error)
    {
        for (var index = 0; index < values.Length; index++)
        {
            var variant = PickVariant(random, distribution, index, 8);
            values[index] = variant switch
            {
                0 => point(),
                1 => circle(random.NextDouble()),
                2 => label(PickString(random)),
                3 => rectangle(random.NextSingle(), random.NextSingle()),
                4 => coloredLine(random.Next(), random.Next(), PickString(random)),
                5 => transform(random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle()),
                6 => richText(PickString(random), random.Next(8, 72), random.Next(2) == 1, PickString(random), random.NextDouble()),
                _ => error(PickString(random)),
            };
        }
    }

    private static void FillPhysics<T>(
        T[] values,
        Random random,
        Distribution distribution,
        Func<T> idle,
        Func<float, T> impulse,
        Func<float, float, T> position,
        Func<float, float, float, T> force,
        Func<float, float, float, float, T> rotation,
        Func<float, float, float, float, float, T> spring,
        Func<double, T> gravity,
        Func<int, int, T> collision)
    {
        for (var index = 0; index < values.Length; index++)
        {
            var variant = PickVariant(random, distribution, index, 8);
            values[index] = variant switch
            {
                0 => idle(),
                1 => impulse(random.NextSingle()),
                2 => position(random.NextSingle(), random.NextSingle()),
                3 => force(random.NextSingle(), random.NextSingle(), random.NextSingle()),
                4 => rotation(random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle()),
                5 => spring(random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle()),
                6 => gravity(random.NextDouble()),
                _ => collision(random.Next(), random.Next()),
            };
        }
    }

    private static int PickVariant(Random random, Distribution distribution, int index, int count)
        => distribution == Distribution.Skewed80 && random.NextDouble() >= 0.8
            ? random.Next(1, count)
            : index % count;

    private static string PickString(Random random) => Strings[random.Next(Strings.Length)];
}
