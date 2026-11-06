using Houtamelo.Spire;

[EnforceExhaustiveness]
enum State
{
    Idle,
    Moving,
    Attacking,
}

class Consumer
{
    void Tick(State state)
    {
        switch (state)
        {
            case State.Idle:
                throw new System.NotImplementedException();

            case State.Moving:
                throw new System.NotImplementedException();

            case State.Attacking:
                throw new System.NotImplementedException();
        }
    }
}
