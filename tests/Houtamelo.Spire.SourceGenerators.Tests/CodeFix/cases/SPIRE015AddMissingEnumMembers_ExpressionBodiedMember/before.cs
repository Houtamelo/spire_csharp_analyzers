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
    private Color _color;

    int Score => _color switch
    {
        Color.Red => 1,
        Color.Green => 2,
    };
}
