using Houtamelo.Spire;

class Container<T>
{
    [EnforceExhaustiveness]
    public enum State
    {
        Idle,
        Moving,
        Attacking,
    }
}

class Consumer
{
    int GetValue<T>(Container<T>.State state) => state switch
    {
        Container<T>.State.Idle => 1,
        Container<T>.State.Moving => 2,
    };
}
