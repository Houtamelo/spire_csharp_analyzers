using System;

namespace Spire;

public interface IDiscriminatedUnion<TEnum> where TEnum : Enum
{
    TEnum kind { get; }
}
