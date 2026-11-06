using Houtamelo.Spire;

[EnforceExhaustiveness]
enum State
{
    Idle = 0,
    Moving = 1,
    Attacking = 2,
}

class Consumer
{
    private State _state;

    void Tick()
    {
        switch (_state)
        {
            case State.Idle: break;

            case State.Moving:
            {
                _state = State.Idle;
            }
            break;

            case State.Attacking:
                throw new System.NotImplementedException();
        }
    }
}
