namespace Spire.Benchmarks;

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

    public static void Fill(EventAdditive[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
            arr[i] = MakeEvent<EventAdditive>(PickVariant(rng, dist, i), rng,
                EventAdditive.Point, EventAdditive.Circle, EventAdditive.Label,
                EventAdditive.Rectangle, EventAdditive.ColoredLine, EventAdditive.Transform,
                EventAdditive.RichText, EventAdditive.Error);
    }

    public static void Fill(EventBoxedFields[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
            arr[i] = MakeEvent<EventBoxedFields>(PickVariant(rng, dist, i), rng,
                EventBoxedFields.Point, EventBoxedFields.Circle, EventBoxedFields.Label,
                EventBoxedFields.Rectangle, EventBoxedFields.ColoredLine, EventBoxedFields.Transform,
                EventBoxedFields.RichText, EventBoxedFields.Error);
    }

    public static void Fill(EventBoxedTuple[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
            arr[i] = MakeEvent<EventBoxedTuple>(PickVariant(rng, dist, i), rng,
                EventBoxedTuple.Point, EventBoxedTuple.Circle, EventBoxedTuple.Label,
                EventBoxedTuple.Rectangle, EventBoxedTuple.ColoredLine, EventBoxedTuple.Transform,
                EventBoxedTuple.RichText, EventBoxedTuple.Error);
    }

    public static void Fill(EventOverlap[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
            arr[i] = MakeEvent<EventOverlap>(PickVariant(rng, dist, i), rng,
                EventOverlap.Point, EventOverlap.Circle, EventOverlap.Label,
                EventOverlap.Rectangle, EventOverlap.ColoredLine, EventOverlap.Transform,
                EventOverlap.RichText, EventOverlap.Error);
    }

    public static void Fill(EventUnsafeOverlap[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
            arr[i] = MakeEvent<EventUnsafeOverlap>(PickVariant(rng, dist, i), rng,
                EventUnsafeOverlap.Point, EventUnsafeOverlap.Circle, EventUnsafeOverlap.Label,
                EventUnsafeOverlap.Rectangle, EventUnsafeOverlap.ColoredLine, EventUnsafeOverlap.Transform,
                EventUnsafeOverlap.RichText, EventUnsafeOverlap.Error);
    }

    public static void Fill(EventRecord[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            int v = PickVariant(rng, dist, i);
            arr[i] = v switch
            {
                0 => new EventRecord.Point(),
                1 => new EventRecord.Circle(rng.NextDouble() * 200 - 100),
                2 => new EventRecord.Label(StringPool[rng.Next(StringPool.Length)]),
                3 => new EventRecord.Rectangle(rng.NextSingle() * 200 - 100, rng.NextSingle() * 200 - 100),
                4 => new EventRecord.ColoredLine(rng.Next(-1000, 1000), rng.Next(-1000, 1000), StringPool[rng.Next(StringPool.Length)]),
                5 => new EventRecord.Transform(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
                6 => new EventRecord.RichText(StringPool[rng.Next(StringPool.Length)], rng.Next(8, 72), rng.Next(2) == 1, StringPool[rng.Next(StringPool.Length)], rng.NextDouble()),
                _ => new EventRecord.Error(StringPool[rng.Next(StringPool.Length)]),
            };
        }
    }

    public static void Fill(EventClass[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            int v = PickVariant(rng, dist, i);
            arr[i] = v switch
            {
                0 => new EventClass.Point(),
                1 => new EventClass.Circle(rng.NextDouble() * 200 - 100),
                2 => new EventClass.Label(StringPool[rng.Next(StringPool.Length)]),
                3 => new EventClass.Rectangle(rng.NextSingle() * 200 - 100, rng.NextSingle() * 200 - 100),
                4 => new EventClass.ColoredLine(rng.Next(-1000, 1000), rng.Next(-1000, 1000), StringPool[rng.Next(StringPool.Length)]),
                5 => new EventClass.Transform(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
                6 => new EventClass.RichText(StringPool[rng.Next(StringPool.Length)], rng.Next(8, 72), rng.Next(2) == 1, StringPool[rng.Next(StringPool.Length)], rng.NextDouble()),
                _ => new EventClass.Error(StringPool[rng.Next(StringPool.Length)]),
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

    // ── Physics (all unmanaged) ──

    public static void Fill(PhysicsAdditive[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
            arr[i] = MakePhysics<PhysicsAdditive>(PickVariant(rng, dist, i), rng,
                PhysicsAdditive.Idle, PhysicsAdditive.Impulse, PhysicsAdditive.Position,
                PhysicsAdditive.Force, PhysicsAdditive.Rotation, PhysicsAdditive.Spring,
                PhysicsAdditive.Gravity, PhysicsAdditive.Collision);
    }

    public static void Fill(PhysicsBoxedFields[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
            arr[i] = MakePhysics<PhysicsBoxedFields>(PickVariant(rng, dist, i), rng,
                PhysicsBoxedFields.Idle, PhysicsBoxedFields.Impulse, PhysicsBoxedFields.Position,
                PhysicsBoxedFields.Force, PhysicsBoxedFields.Rotation, PhysicsBoxedFields.Spring,
                PhysicsBoxedFields.Gravity, PhysicsBoxedFields.Collision);
    }

    public static void Fill(PhysicsBoxedTuple[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
            arr[i] = MakePhysics<PhysicsBoxedTuple>(PickVariant(rng, dist, i), rng,
                PhysicsBoxedTuple.Idle, PhysicsBoxedTuple.Impulse, PhysicsBoxedTuple.Position,
                PhysicsBoxedTuple.Force, PhysicsBoxedTuple.Rotation, PhysicsBoxedTuple.Spring,
                PhysicsBoxedTuple.Gravity, PhysicsBoxedTuple.Collision);
    }

    public static void Fill(PhysicsOverlap[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
            arr[i] = MakePhysics<PhysicsOverlap>(PickVariant(rng, dist, i), rng,
                PhysicsOverlap.Idle, PhysicsOverlap.Impulse, PhysicsOverlap.Position,
                PhysicsOverlap.Force, PhysicsOverlap.Rotation, PhysicsOverlap.Spring,
                PhysicsOverlap.Gravity, PhysicsOverlap.Collision);
    }

    public static void Fill(PhysicsUnsafeOverlap[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
            arr[i] = MakePhysics<PhysicsUnsafeOverlap>(PickVariant(rng, dist, i), rng,
                PhysicsUnsafeOverlap.Idle, PhysicsUnsafeOverlap.Impulse, PhysicsUnsafeOverlap.Position,
                PhysicsUnsafeOverlap.Force, PhysicsUnsafeOverlap.Rotation, PhysicsUnsafeOverlap.Spring,
                PhysicsUnsafeOverlap.Gravity, PhysicsUnsafeOverlap.Collision);
    }

    public static void Fill(PhysicsRecord[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            int v = PickVariant(rng, dist, i);
            arr[i] = v switch
            {
                0 => new PhysicsRecord.Idle(),
                1 => new PhysicsRecord.Impulse(rng.NextSingle()),
                2 => new PhysicsRecord.Position(rng.NextSingle(), rng.NextSingle()),
                3 => new PhysicsRecord.Force(rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
                4 => new PhysicsRecord.Rotation(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
                5 => new PhysicsRecord.Spring(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
                6 => new PhysicsRecord.Gravity(rng.NextDouble()),
                _ => new PhysicsRecord.Collision(rng.Next(), rng.Next()),
            };
        }
    }

    public static void Fill(PhysicsClass[] arr, Random rng, Distribution dist)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            int v = PickVariant(rng, dist, i);
            arr[i] = v switch
            {
                0 => new PhysicsClass.Idle(),
                1 => new PhysicsClass.Impulse(rng.NextSingle()),
                2 => new PhysicsClass.Position(rng.NextSingle(), rng.NextSingle()),
                3 => new PhysicsClass.Force(rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
                4 => new PhysicsClass.Rotation(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
                5 => new PhysicsClass.Spring(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
                6 => new PhysicsClass.Gravity(rng.NextDouble()),
                _ => new PhysicsClass.Collision(rng.Next(), rng.Next()),
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
