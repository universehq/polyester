using System.Runtime.CompilerServices;

namespace Polyester;

public interface IResult<TValue, TError, TSelf>
#if NET11_0_OR_GREATER
    where TSelf : IResult<TValue, TError, TSelf>, IUnion
#else
    where TSelf : IResult<TValue, TError, TSelf>
#endif
    where TError : notnull
{
    bool IsSuccessful { get; }
    bool IsFailed => !IsSuccessful;
    static abstract TSelf Success(TValue value);
    static abstract TSelf Failure(TError error);
}
