using Spire;

namespace My.Deep.Namespace
{
    [DiscriminatedUnion]
    public partial record Result
    {
        public partial record Ok() : Result;
        public partial record Error() : Result;
    }
}
