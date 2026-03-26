using Houtamelo.Spire;

namespace Traffic
{
    [DiscriminatedUnion]
    public partial record Light
    {
        public partial record Red() : Light;
        public partial record Yellow() : Light;
        public partial record Green() : Light;
    }
}
