using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using Polyester.CompilerServices;

namespace Polyester;

#if NET11_0_OR_GREATER
/// <summary>Stores either a success value or an error.</summary>
/// <remarks>Default instances and failure cases containing null are uninitialized results.</remarks>
[NeverInstantiate]
public readonly union Result<TValue, TError>(Success<TValue>, Failure<TError>) :
    IResult<TValue, TError, Result<TValue, TError>> where TError : notnull
{
    /// <summary>Indicates whether this result contains a success value.</summary>
    public bool IsSuccessful => this is Success<TValue>;

    /// <summary>Indicates whether this result is unsuccessful.</summary>
    public bool IsFailed => !IsSuccessful;

    /// <summary>Gets the success value, or its default when no success is present.</summary>
    public TValue? ValueOrDefault => TrySuccess(out var value) ? value : default;

    /// <summary>Gets the failure error, or its default when no failure is present.</summary>
    public TError? Error => TryFailure(out var error) ? error : default;

    public static Result<TValue, TError> Success(TValue value) => new Success<TValue>(value);

    public static Result<TValue, TError> Failure(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Failure<TError>(error);
    }

    public static Result<TValue, TError> FromError(TError error) => Failure(error);

    /// <summary>Maps a success value and forwards an existing failure.</summary>
    /// <remarks>Exceptions thrown by the converter propagate to the caller.</remarks>
    public Result<TResult, TError> Convert<TResult>(Converter<TValue, TResult> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        return TrySuccess(out var value)
            ? Result<TResult, TError>.Success(converter(value))
            : Result<TResult, TError>.Failure(GetError());
    }

    /// <summary>Chains a success to another result and forwards an existing failure.</summary>
    /// <remarks>Exceptions thrown by the converter propagate to the caller.</remarks>
    public Result<TResult, TError> Convert<TResult>(Converter<TValue, Result<TResult, TError>> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        return TrySuccess(out var value)
            ? converter(value)
            : Result<TResult, TError>.Failure(GetError());
    }

    /// <summary>Maps a success through a function pointer; converter exceptions propagate.</summary>
    public unsafe Result<TResult, TError> Convert<TResult>(delegate*<TValue, TResult> converter)
    {
        return converter == null
            ? throw new ArgumentNullException(nameof(converter))
            : TrySuccess(out var value)
            ? Result<TResult, TError>.Success(converter(value))
            : Result<TResult, TError>.Failure(GetError());
    }

    /// <summary>Chains a success through a function pointer; converter exceptions propagate.</summary>
    public unsafe Result<TResult, TError> Convert<TResult>(delegate*<TValue, Result<TResult, TError>> converter)
    {
        return converter == null
            ? throw new ArgumentNullException(nameof(converter))
            : TrySuccess(out var value)
            ? converter(value)
            : Result<TResult, TError>.Failure(GetError());
    }

    /// <summary>Extracts a success value, returning false when no success is present.</summary>
    public bool TrySuccess([MaybeNullWhen(false)] out TValue value)
    {
        if (Value is Success<TValue> success)
        {
            value = success.Value;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Extracts a failure error, returning false when no failure is present.</summary>
    public bool TryFailure([MaybeNullWhen(false)] out TError value)
    {
        if (Value is Failure<TError> { Error: not null } failure)
        {
            value = failure.Error;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Attempts to extract the success value.</summary>
    public bool TryGet([MaybeNullWhen(false)] out TValue value) => TrySuccess(out value);

    /// <summary>Returns the success value or the supplied fallback.</summary>
    public TValue? Or(TValue? defaultValue) => TrySuccess(out var value) ? value : defaultValue;

    /// <summary>Invokes the fallback only when no success is present.</summary>
    public TValue OrInvoke(Func<TValue> defaultFunc)
    {
        ArgumentNullException.ThrowIfNull(defaultFunc);
        return TrySuccess(out var value) ? value : defaultFunc();
    }

    /// <summary>Passes a failure's error to the fallback, or returns the success value.</summary>
    public TValue OrInvoke(Func<TError, TValue> defaultFunc)
    {
        ArgumentNullException.ThrowIfNull(defaultFunc);
        return TrySuccess(out var value) ? value : defaultFunc(GetError());
    }

    /// <summary>Invokes the fallback pointer only when no success is present.</summary>
    public unsafe TValue OrInvoke(delegate*<TValue> defaultFunc)
    {
        if (defaultFunc == null)
        {
            throw new ArgumentNullException(nameof(defaultFunc));
        }

        return TrySuccess(out var value) ? value : defaultFunc();
    }

    /// <summary>Passes a failure's error to the fallback pointer.</summary>
    public unsafe TValue OrInvoke(delegate*<TError, TValue> defaultFunc)
    {
        if (defaultFunc == null)
        {
            throw new ArgumentNullException(nameof(defaultFunc));
        }

        return TrySuccess(out var value) ? value : defaultFunc(GetError());
    }

    /// <summary>Returns the success value or throws the error converted to an exception.</summary>
    public TValue OrThrow() => TrySuccess(out var value) ? value : ToResult().OrThrow();

    /// <summary>Returns the success value or throws an exception created from the error.</summary>
    public TValue OrThrow(Func<TError, Exception> exceptionFactory)
    {
        ArgumentNullException.ThrowIfNull(exceptionFactory);
        if (TrySuccess(out var value))
        {
            return value;
        }

        ExceptionDispatchInfo.Throw(exceptionFactory(GetError()));
        return default!;
    }

    /// <summary>Returns the success value or throws an exception created by the pointer.</summary>
    public unsafe TValue OrThrow(delegate*<TError, Exception> exceptionFactory)
    {
        if (exceptionFactory == null)
        {
            throw new ArgumentNullException(nameof(exceptionFactory));
        }

        if (TrySuccess(out var value))
        {
            return value;
        }

        ExceptionDispatchInfo.Throw(exceptionFactory(GetError()));
        return default!;
    }

    /// <summary>Converts to an exception result, retaining errors that already are exceptions.</summary>
    public Result<TValue> ToResult()
    {
        if (TrySuccess(out var value))
        {
            return Result<TValue>.Success(value);
        }

        var error = GetError();
        return Result<TValue>.Failure(error is Exception exception ? exception : new Exception(error.ToString()));
    }

    /// <summary>Converts a failure using the factory; successes bypass the factory.</summary>
    public Result<TValue> ToResult(Func<TError, Exception> exceptionFactory)
    {
        ArgumentNullException.ThrowIfNull(exceptionFactory);
        return TrySuccess(out var value)
            ? Result<TValue>.Success(value)
            : Result<TValue>.Failure(exceptionFactory(GetError()));
    }

    /// <summary>Boxes the success value while retaining any failure error.</summary>
    public Result<object?, TError> Box() => TrySuccess(out var value)
        ? Result<object?, TError>.Success(value)
        : Result<object?, TError>.Failure(GetError());

    public static TValue? operator |(in Result<TValue, TError> result, TValue? defaultValue) => result.Or(defaultValue);

    public static Result<TValue, TError> operator |(in Result<TValue, TError> first, in Result<TValue, TError> second)
        => first.IsSuccessful ? first : second;

    public static bool operator &(in Result<TValue, TError> first, in Result<TValue, TError> second)
        => first.IsSuccessful && second.IsSuccessful;

    public static bool operator !(in Result<TValue, TError> result) => result.IsFailed;
    public static bool operator true(in Result<TValue, TError> result) => result.IsSuccessful;
    public static bool operator false(in Result<TValue, TError> result) => result.IsFailed;

    public static implicit operator Result<TValue, TError>(TValue value) => Success(value);
    public static implicit operator Result<TValue>(in Result<TValue, TError> result) => result.ToResult();
    public static explicit operator TValue(in Result<TValue, TError> result) => result.OrThrow();

    public override string ToString() => TrySuccess(out var value)
        ? value?.ToString() ?? string.Empty
        : Error?.ToString() ?? string.Empty;

    private TError GetError() => TryFailure(out var error)
        ? error
        : throw new InvalidOperationException("The result has not been initialized with a valid success or failure.");
}

/// <summary>Stores either a success value or an exception.</summary>
/// <remarks>Default instances and failure cases containing null are uninitialized results.</remarks>
[NeverInstantiate]
public readonly union Result<TValue>(Success<TValue>, Failure<Exception>) :
    IResult<TValue, Exception, Result<TValue>>
{
    /// <summary>Indicates whether this result contains a success value.</summary>
    public bool IsSuccessful => this is Success<TValue>;

    /// <summary>Indicates whether this result is unsuccessful.</summary>
    public bool IsFailed => !IsSuccessful;

    /// <summary>Gets the success value, or its default when no success is present.</summary>
    public TValue? ValueOrDefault => TrySuccess(out var value) ? value : default;

    /// <summary>Gets the stored exception, or null when no failure is present.</summary>
    public Exception? Error => TryFailure(out var error) ? error : null;

    public static Result<TValue> Success(TValue value) => new Success<TValue>(value);

    public static Result<TValue> Failure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new Failure<Exception>(exception);
    }

    public static Result<TValue> FromError(Exception exception) => Failure(exception);

    /// <summary>Maps a success, storing any converter exception as a failure.</summary>
    public Result<TResult> Convert<TResult>(Converter<TValue, TResult> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        if (!TrySuccess(out var value))
        {
            return Result<TResult>.Failure(GetError());
        }

        try
        {
            return Result<TResult>.Success(converter(value));
        }
        catch (Exception exception)
        {
            return Result<TResult>.Failure(exception);
        }
    }

    /// <summary>Chains a success to another result, storing converter exceptions as failures.</summary>
    public Result<TResult> Convert<TResult>(Converter<TValue, Result<TResult>> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        if (!TrySuccess(out var value))
        {
            return Result<TResult>.Failure(GetError());
        }

        try
        {
            return converter(value);
        }
        catch (Exception exception)
        {
            return Result<TResult>.Failure(exception);
        }
    }

    /// <summary>Maps a success through a pointer, storing converter exceptions as failures.</summary>
    public unsafe Result<TResult> Convert<TResult>(delegate*<TValue, TResult> converter)
    {
        if (converter == null)
        {
            throw new ArgumentNullException(nameof(converter));
        }

        if (!TrySuccess(out var value))
        {
            return Result<TResult>.Failure(GetError());
        }

        try
        {
            return Result<TResult>.Success(converter(value));
        }
        catch (Exception exception)
        {
            return Result<TResult>.Failure(exception);
        }
    }

    /// <summary>Chains a success through a pointer, storing converter exceptions as failures.</summary>
    public unsafe Result<TResult> Convert<TResult>(delegate*<TValue, Result<TResult>> converter)
    {
        if (converter == null)
        {
            throw new ArgumentNullException(nameof(converter));
        }

        if (!TrySuccess(out var value))
        {
            return Result<TResult>.Failure(GetError());
        }

        try
        {
            return converter(value);
        }
        catch (Exception exception)
        {
            return Result<TResult>.Failure(exception);
        }
    }

    /// <summary>Extracts a success value, returning false when no success is present.</summary>
    public bool TrySuccess([MaybeNullWhen(false)] out TValue value)
    {
        if (Value is Success<TValue> success)
        {
            value = success.Value;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Extracts a failure exception, returning false when no failure is present.</summary>
    public bool TryFailure([MaybeNullWhen(false)] out Exception value)
    {
        if (Value is Failure<Exception> { Error: not null } failure)
        {
            value = failure.Error;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Attempts to extract the success value.</summary>
    public bool TryGet([MaybeNullWhen(false)] out TValue value) => TrySuccess(out value);

    /// <summary>Returns the success value or the supplied fallback.</summary>
    public TValue? Or(TValue? defaultValue) => TrySuccess(out var value) ? value : defaultValue;

    /// <summary>Invokes the fallback only when no success is present.</summary>
    public TValue OrInvoke(Func<TValue> defaultFunc)
    {
        ArgumentNullException.ThrowIfNull(defaultFunc);
        return TrySuccess(out var value) ? value : defaultFunc();
    }

    /// <summary>Passes a failure's exception to the fallback, or returns the success value.</summary>
    public TValue OrInvoke(Func<Exception, TValue> defaultFunc)
    {
        ArgumentNullException.ThrowIfNull(defaultFunc);
        return TrySuccess(out var value) ? value : defaultFunc(GetError());
    }

    /// <summary>Invokes the fallback pointer only when no success is present.</summary>
    public unsafe TValue OrInvoke(delegate*<TValue> defaultFunc)
    {
        if (defaultFunc == null)
        {
            throw new ArgumentNullException(nameof(defaultFunc));
        }

        return TrySuccess(out var value) ? value : defaultFunc();
    }

    /// <summary>Passes a failure's exception to the fallback pointer.</summary>
    public unsafe TValue OrInvoke(delegate*<Exception, TValue> defaultFunc)
    {
        return defaultFunc == null
            ? throw new ArgumentNullException(nameof(defaultFunc))
            : TrySuccess(out var value) ? value : defaultFunc(GetError());
    }

    /// <summary>Returns the success value or rethrows the stored exception with its original stack.</summary>
    public TValue OrThrow()
    {
        if (TrySuccess(out var value))
        {
            return value;
        }

        ExceptionDispatchInfo.Throw(GetError());
        return default!;
    }

    /// <summary>Boxes the success value while retaining any failure exception.</summary>
    public Result<object?> Box() => TrySuccess(out var value)
        ? Result<object?>.Success(value)
        : Result<object?>.Failure(GetError());

    /// <summary>Creates a completed value task representing this success, failure, or cancellation.</summary>
    /// <remarks>A cancellation exception with an uncanceled token produces a faulted task.</remarks>
    public ValueTask<TValue> AsTask()
    {
        if (TrySuccess(out var value))
        {
            return new ValueTask<TValue>(value);
        }

        var error = GetError();
        return error is OperationCanceledException canceled && canceled.CancellationToken.IsCancellationRequested
            ? ValueTask.FromCanceled<TValue>(canceled.CancellationToken)
            : ValueTask.FromException<TValue>(error);
    }

    public static TValue? operator |(in Result<TValue> result, TValue? defaultValue) => result.Or(defaultValue);

    public static Result<TValue> operator |(in Result<TValue> first, in Result<TValue> second)
        => first.IsSuccessful ? first : second;

    public static bool operator &(in Result<TValue> first, in Result<TValue> second)
        => first.IsSuccessful && second.IsSuccessful;

    public static bool operator !(in Result<TValue> result) => result.IsFailed;
    public static bool operator true(in Result<TValue> result) => result.IsSuccessful;
    public static bool operator false(in Result<TValue> result) => result.IsFailed;

    public static implicit operator Result<TValue>(TValue value) => Success(value);
    public static explicit operator TValue(in Result<TValue> result) => result.OrThrow();
    public static explicit operator ValueTask<TValue>(in Result<TValue> result) => result.AsTask();

    public override string ToString() => TrySuccess(out var value)
        ? value?.ToString() ?? string.Empty
        : Error?.ToString() ?? string.Empty;

    private Exception GetError() => TryFailure(out var error)
        ? error
        : throw new InvalidOperationException("The result has not been initialized with a valid success or failure.");
}

public readonly record struct Success<T>(T Value)
{
    public void Deconstruct(out T value)
    {
        value = Value;
    }
}

public readonly record struct Failure<E>(E Error)
{
    public void Deconstruct(out E error)
    {
        error = Error;
    }
}
#endif
