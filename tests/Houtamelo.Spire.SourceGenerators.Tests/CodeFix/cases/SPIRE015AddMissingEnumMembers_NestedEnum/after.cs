using Houtamelo.Spire;

class Outer
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
    int GetValue(Outer.State state) => state switch
    {
        Outer.State.Idle => 1,
        Outer.State.Moving => 2,
        Outer.State.Attacking => throw new System.NotImplementedException(),
    };
}
