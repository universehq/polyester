using System.Runtime.ExceptionServices;

namespace Polyester;

#if NET11_0_OR_GREATER
/// <summary>Provides factories and helpers for result values.</summary>
public static class Result
{
    /// <summary>Creates a successful result, including when the value is null.</summary>
    public static Result<TValue> FromValue<TValue>(TValue value) => Result<TValue>.Success(value);

    /// <summary>Creates a failed result containing the supplied exception.</summary>
    public static Result<TValue> FromException<TValue>(Exception exception) =>
        Result<TValue>.Failure(exception);

    /// <summary>Returns the successful value, or null on failure.</summary>
    public static TValue? OrNull<TValue>(this in Result<TValue> result)
        where TValue : struct => result.TrySuccess(out var value) ? value : null;

    /// <summary>Returns the successful value, or null on failure.</summary>
    public static TValue? OrNull<TValue, TError>(this in Result<TValue, TError> result)
        where TValue : struct
        where TError : notnull => result.TrySuccess(out var value) ? value : null;

    /// <summary>References the first result if successful; otherwise, the second result.</summary>
    public static ref readonly Result<TValue> Coalesce<TValue>(
        this in Result<TValue> first,
        in Result<TValue> second
    ) => ref (first.IsSuccessful ? ref first : ref second);

    /// <summary>References the first result if successful; otherwise, the second result.</summary>
    public static ref readonly Result<TValue, TError> Coalesce<TValue, TError>(
        this in Result<TValue, TError> first,
        in Result<TValue, TError> second
    )
        where TError : notnull => ref (first.IsSuccessful ? ref first : ref second);

    /// <summary>Checks whether a type is a constructed result type.</summary>
    public static bool IsResult(this Type resultType)
    {
        ArgumentNullException.ThrowIfNull(resultType);

        if (!resultType.IsConstructedGenericType)
        {
            return false;
        }

        var definition = resultType.GetGenericTypeDefinition();
        return definition == typeof(Result<>) || definition == typeof(Result<,>);
    }

    /// <summary>Returns a result's value type, or null for other types.</summary>
    public static Type? GetUnderlyingType(Type resultType) =>
        resultType.IsResult() ? resultType.GetGenericArguments()[0] : null;

    /// <summary>Replaces a successful null value with a null-reference failure.</summary>
    public static Result<TValue> EnsureNotNull<TValue>(this Result<TValue?> result)
        where TValue : class => EnsureNotNull<TValue, NullReferenceException>(result);

    /// <summary>Replaces a successful null value with a failure of the specified exception type.</summary>
    public static Result<TValue> EnsureNotNull<TValue, TException>(this Result<TValue?> result)
        where TValue : class
        where TException : Exception, new() =>
        result.TrySuccess(out var value) && value is null
            ? FromException<TValue>(ExceptionDispatchInfo.SetCurrentStackTrace(new TException()))
            : result!;
}
#endif
