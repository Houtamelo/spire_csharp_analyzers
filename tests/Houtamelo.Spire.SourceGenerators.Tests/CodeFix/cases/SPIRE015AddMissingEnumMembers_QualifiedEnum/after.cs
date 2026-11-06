using Houtamelo.Spire;

namespace Game.Model
{
    [EnforceExhaustiveness]
    public enum State
    {
        Idle,
        Moving,
        Attacking,
    }
}

namespace Game.Consumer
{
    class Reader
    {
        int GetValue(Game.Model.State state) => state switch
        {
            Game.Model.State.Idle => 1,
            Game.Model.State.Moving => 2,
            Model.State.Attacking => throw new System.NotImplementedException(),
        };
    }
}
