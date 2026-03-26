using System;

namespace Houtamelo.Spire.Core;

public interface IDiscriminatedUnion<TEnum> where TEnum : Enum
{
    TEnum kind { get; }
}
