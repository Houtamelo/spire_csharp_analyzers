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
      // The idle state does no work.
      case State.Idle:
        break;

      // Moving has a block body to keep the section shape realistic.
      case State.Moving:
      {
        state = State.Idle;
        break;
      }

      case State.Attacking:
        throw new System.NotImplementedException();
    }
  }
}
