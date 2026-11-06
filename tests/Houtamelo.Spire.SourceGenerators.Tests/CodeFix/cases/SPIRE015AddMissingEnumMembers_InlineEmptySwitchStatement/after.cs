using Houtamelo.Spire;

[EnforceExhaustiveness]
enum State
{
    Idle,
    Moving,
    Speaking,
}

class Consumer
{
    static void Tick(State state)
    {
        switch (state) {
            case State.Idle:
                throw new System.NotImplementedException();
            case State.Moving:
                throw new System.NotImplementedException();
            case State.Speaking:
                throw new System.NotImplementedException();
        }
    }
}
