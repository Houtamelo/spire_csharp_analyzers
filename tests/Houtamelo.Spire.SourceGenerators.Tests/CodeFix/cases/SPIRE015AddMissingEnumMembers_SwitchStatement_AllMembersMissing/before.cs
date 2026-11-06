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
        }
    }
}
