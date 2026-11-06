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
    int Match(Color color) => color switch
    {
        Color.Red => color switch
        {
            Color.Red => 1,
            Color.Green => 2,
            Color.Blue => throw new System.NotImplementedException(),
        },
        Color.Green => 3,
        Color.Blue => 4,
    };
}
