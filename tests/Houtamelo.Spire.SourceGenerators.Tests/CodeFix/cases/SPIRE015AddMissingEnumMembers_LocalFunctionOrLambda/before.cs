using Houtamelo.Spire;

[EnforceExhaustiveness]
enum Color
{
    Red,
    Green,
    Blue,
}

class Consumer
{
    int Match(Color color)
    {
        int Local(Color value) => value switch
        {
            Color.Red => 1,
            Color.Green => 2,
        };

        return Local(color);
    }
}
