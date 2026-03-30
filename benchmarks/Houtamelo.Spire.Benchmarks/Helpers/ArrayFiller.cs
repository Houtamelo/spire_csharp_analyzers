namespace Houtamelo.Spire.Benchmarks.Helpers;

/// Fills union arrays with consistent random data across all strategies.
static class ArrayFiller
{
    const int VariantCount = 8;
    static readonly string[] StringPool = ["hello", "world", "red", "blue", "Arial", "Mono", "error!", "warn"];

    static int PickVariant(Random rng, Distribution dist, int i)
    {
        return dist switch
        {
            Distribution.Skewed80 => rng.NextDouble() < 0.8 ? 0 : rng.Next(1, VariantCount),
            _ => i % VariantCount,
        };
    }

    // ── Event (mixed managed/unmanaged) ──

    public static void Fill(Types.EventAdditive[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
            arr[i] = MakeEvent<Types.EventAdditive>(PickVariant(rng, dist, i), rng,
                Types.EventAdditive.Point, Types.EventAdditive.Circle, Types.EventAdditive.Label,
                Types.EventAdditive.Rectangle, Types.EventAdditive.ColoredLine, Types.EventAdditive.Transform,
                Types.EventAdditive.RichText, Types.EventAdditive.Error);
    }

    public static void Fill(Types.EventBoxedFields[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
            arr[i] = MakeEvent<Types.EventBoxedFields>(PickVariant(rng, dist, i), rng,
                Types.EventBoxedFields.Point, Types.EventBoxedFields.Circle, Types.EventBoxedFields.Label,
                Types.EventBoxedFields.Rectangle, Types.EventBoxedFields.ColoredLine, Types.EventBoxedFields.Transform,
                Types.EventBoxedFields.RichText, Types.EventBoxedFields.Error);
    }

    public static void Fill(Types.EventBoxedTuple[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
            arr[i] = MakeEvent<Types.EventBoxedTuple>(PickVariant(rng, dist, i), rng,
                Types.EventBoxedTuple.Point, Types.EventBoxedTuple.Circle, Types.EventBoxedTuple.Label,
                Types.EventBoxedTuple.Rectangle, Types.EventBoxedTuple.ColoredLine, Types.EventBoxedTuple.Transform,
                Types.EventBoxedTuple.RichText, Types.EventBoxedTuple.Error);
    }

    public static void Fill(Types.EventOverlap[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
            arr[i] = MakeEvent<Types.EventOverlap>(PickVariant(rng, dist, i), rng,
                Types.EventOverlap.Point, Types.EventOverlap.Circle, Types.EventOverlap.Label,
                Types.EventOverlap.Rectangle, Types.EventOverlap.ColoredLine, Types.EventOverlap.Transform,
                Types.EventOverlap.RichText, Types.EventOverlap.Error);
    }

    public static void Fill(Types.EventUnsafeOverlap[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
            arr[i] = MakeEvent<Types.EventUnsafeOverlap>(PickVariant(rng, dist, i), rng,
                Types.EventUnsafeOverlap.Point, Types.EventUnsafeOverlap.Circle, Types.EventUnsafeOverlap.Label,
                Types.EventUnsafeOverlap.Rectangle, Types.EventUnsafeOverlap.ColoredLine, Types.EventUnsafeOverlap.Transform,
                Types.EventUnsafeOverlap.RichText, Types.EventUnsafeOverlap.Error);
    }

    public static void Fill(Types.EventRecord[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            int v = PickVariant(rng, dist, i);
            arr[i] = v switch
            {
                0 => new Types.EventRecord.Point(),
                1 => new Types.EventRecord.Circle(rng.NextDouble() * 200 - 100),
                2 => new Types.EventRecord.Label(StringPool[rng.Next(StringPool.Length)]),
                3 => new Types.EventRecord.Rectangle(rng.NextSingle() * 200 - 100, rng.NextSingle() * 200 - 100),
                4 => new Types.EventRecord.ColoredLine(rng.Next(-1000, 1000), rng.Next(-1000, 1000), StringPool[rng.Next(StringPool.Length)]),
                5 => new Types.EventRecord.Transform(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
                6 => new Types.EventRecord.RichText(StringPool[rng.Next(StringPool.Length)], rng.Next(8, 72), rng.Next(2) == 1, StringPool[rng.Next(StringPool.Length)], rng.NextDouble()),
                _ => new Types.EventRecord.Error(StringPool[rng.Next(StringPool.Length)]),
            };
        }
    }

    static T MakeEvent<T>(int v, Random rng,
        Func<T> point,
        Func<double, T> circle,
        Func<string, T> label,
        Func<float, float, T> rect,
        Func<int, int, string, T> coloredLine,
        Func<float, float, float, float, T> transform,
        Func<string, int, bool, string, double, T> richText,
        Func<string, T> error)
    {
        return v switch
        {
            0 => point(),
            1 => circle(rng.NextDouble() * 200 - 100),
            2 => label(StringPool[rng.Next(StringPool.Length)]),
            3 => rect(rng.NextSingle() * 200 - 100, rng.NextSingle() * 200 - 100),
            4 => coloredLine(rng.Next(-1000, 1000), rng.Next(-1000, 1000), StringPool[rng.Next(StringPool.Length)]),
            5 => transform(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
            6 => richText(StringPool[rng.Next(StringPool.Length)], rng.Next(8, 72), rng.Next(2) == 1, StringPool[rng.Next(StringPool.Length)], rng.NextDouble()),
            _ => error(StringPool[rng.Next(StringPool.Length)]),
        };
    }

    // ── Event (C# 15 native union) ──

    public static void Fill(Types.EventNative[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            int v = PickVariant(rng, dist, i);
            arr[i] = v switch
            {
                0 => new Types.EvtPoint(),
                1 => new Types.EvtCircle(rng.NextDouble() * 200 - 100),
                2 => new Types.EvtLabel(StringPool[rng.Next(StringPool.Length)]),
                3 => new Types.EvtRectangle(rng.NextSingle() * 200 - 100, rng.NextSingle() * 200 - 100),
                4 => new Types.EvtColoredLine(rng.Next(-1000, 1000), rng.Next(-1000, 1000), StringPool[rng.Next(StringPool.Length)]),
                5 => new Types.EvtTransform(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
                6 => new Types.EvtRichText(StringPool[rng.Next(StringPool.Length)], rng.Next(8, 72), rng.Next(2) == 1, StringPool[rng.Next(StringPool.Length)], rng.NextDouble()),
                _ => new Types.EvtError(StringPool[rng.Next(StringPool.Length)]),
            };
        }
    }

    // ── Physics (all unmanaged) ──

    public static void Fill(Types.PhysicsAdditive[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
            arr[i] = MakePhysics<Types.PhysicsAdditive>(PickVariant(rng, dist, i), rng,
                Types.PhysicsAdditive.Idle, Types.PhysicsAdditive.Impulse, Types.PhysicsAdditive.Position,
                Types.PhysicsAdditive.Force, Types.PhysicsAdditive.Rotation, Types.PhysicsAdditive.Spring,
                Types.PhysicsAdditive.Gravity, Types.PhysicsAdditive.Collision);
    }

    public static void Fill(Types.PhysicsBoxedFields[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
            arr[i] = MakePhysics<Types.PhysicsBoxedFields>(PickVariant(rng, dist, i), rng,
                Types.PhysicsBoxedFields.Idle, Types.PhysicsBoxedFields.Impulse, Types.PhysicsBoxedFields.Position,
                Types.PhysicsBoxedFields.Force, Types.PhysicsBoxedFields.Rotation, Types.PhysicsBoxedFields.Spring,
                Types.PhysicsBoxedFields.Gravity, Types.PhysicsBoxedFields.Collision);
    }

    public static void Fill(Types.PhysicsBoxedTuple[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
            arr[i] = MakePhysics<Types.PhysicsBoxedTuple>(PickVariant(rng, dist, i), rng,
                Types.PhysicsBoxedTuple.Idle, Types.PhysicsBoxedTuple.Impulse, Types.PhysicsBoxedTuple.Position,
                Types.PhysicsBoxedTuple.Force, Types.PhysicsBoxedTuple.Rotation, Types.PhysicsBoxedTuple.Spring,
                Types.PhysicsBoxedTuple.Gravity, Types.PhysicsBoxedTuple.Collision);
    }

    public static void Fill(Types.PhysicsOverlap[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
            arr[i] = MakePhysics<Types.PhysicsOverlap>(PickVariant(rng, dist, i), rng,
                Types.PhysicsOverlap.Idle, Types.PhysicsOverlap.Impulse, Types.PhysicsOverlap.Position,
                Types.PhysicsOverlap.Force, Types.PhysicsOverlap.Rotation, Types.PhysicsOverlap.Spring,
                Types.PhysicsOverlap.Gravity, Types.PhysicsOverlap.Collision);
    }

    public static void Fill(Types.PhysicsUnsafeOverlap[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
            arr[i] = MakePhysics<Types.PhysicsUnsafeOverlap>(PickVariant(rng, dist, i), rng,
                Types.PhysicsUnsafeOverlap.Idle, Types.PhysicsUnsafeOverlap.Impulse, Types.PhysicsUnsafeOverlap.Position,
                Types.PhysicsUnsafeOverlap.Force, Types.PhysicsUnsafeOverlap.Rotation, Types.PhysicsUnsafeOverlap.Spring,
                Types.PhysicsUnsafeOverlap.Gravity, Types.PhysicsUnsafeOverlap.Collision);
    }

    public static void Fill(Types.PhysicsRecord[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            int v = PickVariant(rng, dist, i);
            arr[i] = v switch
            {
                0 => new Types.PhysicsRecord.Idle(),
                1 => new Types.PhysicsRecord.Impulse(rng.NextSingle()),
                2 => new Types.PhysicsRecord.Position(rng.NextSingle(), rng.NextSingle()),
                3 => new Types.PhysicsRecord.Force(rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
                4 => new Types.PhysicsRecord.Rotation(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
                5 => new Types.PhysicsRecord.Spring(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
                6 => new Types.PhysicsRecord.Gravity(rng.NextDouble()),
                _ => new Types.PhysicsRecord.Collision(rng.Next(), rng.Next()),
            };
        }
    }

    // ── Physics (C# 15 native union) ──

    public static void Fill(Types.PhysicsNative[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            int v = PickVariant(rng, dist, i);
            arr[i] = v switch
            {
                0 => new Types.PhysIdle(),
                1 => new Types.PhysImpulse(rng.NextSingle()),
                2 => new Types.PhysPosition(rng.NextSingle(), rng.NextSingle()),
                3 => new Types.PhysForce(rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
                4 => new Types.PhysRotation(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
                5 => new Types.PhysSpring(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
                6 => new Types.PhysGravity(rng.NextDouble()),
                _ => new Types.PhysCollision(rng.Next(), rng.Next()),
            };
        }
    }

    static T MakePhysics<T>(int v, Random rng,
        Func<T> idle,
        Func<float, T> impulse,
        Func<float, float, T> position,
        Func<float, float, float, T> force,
        Func<float, float, float, float, T> rotation,
        Func<float, float, float, float, float, T> spring,
        Func<double, T> gravity,
        Func<int, int, T> collision)
    {
        return v switch
        {
            0 => idle(),
            1 => impulse(rng.NextSingle()),
            2 => position(rng.NextSingle(), rng.NextSingle()),
            3 => force(rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
            4 => rotation(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
            5 => spring(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
            6 => gravity(rng.NextDouble()),
            _ => collision(rng.Next(), rng.Next()),
        };
    }
}
