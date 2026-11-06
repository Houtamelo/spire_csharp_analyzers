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
        switch (state) {}
    }
}
