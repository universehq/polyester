using System.Runtime.CompilerServices;

namespace Polyester.UnitTests;

public class ResultTests
{
    [Test]
    public void SuccessfulResultExposesItsValue()
    {
        var result = Result<int>.Success(42);

        Assert.That(result.IsSuccessful, Is.True);
        Assert.That(result.IsFailed, Is.False);
        Assert.That(result.ValueOrDefault, Is.EqualTo(42));
        Assert.That(result.Error, Is.Null);
        Assert.That(result.TrySuccess(out var value), Is.True);
        Assert.That(value, Is.EqualTo(42));
        Assert.That(result.TryGet(out var retrieved), Is.True);
        Assert.That(retrieved, Is.EqualTo(42));
        Assert.That(result.TryFailure(out var error), Is.False);
        Assert.That(error, Is.Null);
    }

    [Test]
    public void FailedResultExposesItsOriginalException()
    {
        var exception = new InvalidOperationException("failure");
        var result = Result<int>.FromError(exception);

        Assert.That(result.IsSuccessful, Is.False);
        Assert.That(result.IsFailed, Is.True);
        Assert.That(result.ValueOrDefault, Is.Zero);
        Assert.That(result.Error, Is.SameAs(exception));
        Assert.That(result.TrySuccess(out var value), Is.False);
        Assert.That(value, Is.Zero);
        Assert.That(result.TryGet(out var retrieved), Is.False);
        Assert.That(retrieved, Is.Zero);
        Assert.That(result.TryFailure(out var error), Is.True);
        Assert.That(error, Is.SameAs(exception));
    }

    [Test]
    public void SuccessfulGenericResultExposesItsValue()
    {
        var result = Result<int, string>.Success(42);

        Assert.That(result.IsSuccessful, Is.True);
        Assert.That(result.IsFailed, Is.False);
        Assert.That(result.ValueOrDefault, Is.EqualTo(42));
        Assert.That(result.Error, Is.Null);
        Assert.That(result.TrySuccess(out var value), Is.True);
        Assert.That(value, Is.EqualTo(42));
        Assert.That(result.TryGet(out var retrieved), Is.True);
        Assert.That(retrieved, Is.EqualTo(42));
        Assert.That(result.TryFailure(out var error), Is.False);
        Assert.That(error, Is.Null);
    }

    [Test]
    public void DefaultValuedGenericErrorRemainsAFailure()
    {
        var result = Result<string, int>.FromError(0);

        Assert.That(result.IsSuccessful, Is.False);
        Assert.That(result.IsFailed, Is.True);
        Assert.That(result.ValueOrDefault, Is.Null);
        Assert.That(result.Error, Is.Zero);
        Assert.That(result.TrySuccess(out var value), Is.False);
        Assert.That(value, Is.Null);
        Assert.That(result.TryGet(out var retrieved), Is.False);
        Assert.That(retrieved, Is.Null);
        Assert.That(result.TryFailure(out var error), Is.True);
        Assert.That(error, Is.Zero);
    }

    [Test]
    public void NullIsAValidSuccessfulValue()
    {
        var result = Result<string?>.Success(null);
        var generic = Result<string?, int>.Success(null);

        Assert.That(result.IsSuccessful, Is.True);
        Assert.That(result.TrySuccess(out var value), Is.True);
        Assert.That(value, Is.Null);
        Assert.That(result.Or("fallback"), Is.Null);
        Assert.That(generic.IsSuccessful, Is.True);
        Assert.That(generic.TryGet(out var genericValue), Is.True);
        Assert.That(genericValue, Is.Null);
        Assert.That(generic.Or("fallback"), Is.Null);
    }

    [Test]
    public void FailureFactoriesRejectNullErrors()
    {
        Assert.Throws<ArgumentNullException>(() => Result<int>.Failure(null!));
        Assert.Throws<ArgumentNullException>(() => Result<int>.FromError(null!));
        Assert.Throws<ArgumentNullException>(() => Result<int, string>.Failure(null!));
        Assert.Throws<ArgumentNullException>(() => Result<int, string>.FromError(null!));
    }

    [Test]
    public void ImplicitVariantConversionsRetainTheUnionValue()
    {
        var exception = new InvalidOperationException("failure");
        Result<int> success = new Success<int>(42);
        Result<int> failure = new Failure<Exception>(exception);
        Result<int, string> genericSuccess = new Success<int>(42);
        Result<int, string> genericFailure = new Failure<string>("failure");

        Assert.That(success.Value is Success<int>(42), Is.True);
        Assert.That(
            failure.Value is Failure<Exception>(var error) && ReferenceEquals(error, exception),
            Is.True
        );
        Assert.That(genericSuccess.Value is Success<int>(42), Is.True);
        Assert.That(genericFailure.Value is Failure<string>("failure"), Is.True);
        Assert.That(success.OrThrow(), Is.EqualTo(42));
        Assert.That(failure.Error, Is.SameAs(exception));
        Assert.That(genericSuccess.OrThrow(), Is.EqualTo(42));
        Assert.That(genericFailure.Error, Is.EqualTo("failure"));
    }

    [Test]
    public void ConvertMapsSuccessfulValuesToAnotherType()
    {
        var result = Result<int>.Success(42).Convert(value => value.ToString());
        var generic = Result<int, string>.Success(42).Convert(value => value.ToString());

        Assert.That(result.OrThrow(), Is.EqualTo("42"));
        Assert.That(generic.OrThrow(), Is.EqualTo("42"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ConvertFlattensTheReturnedResult(bool returnFailure)
    {
        var exception = new InvalidOperationException("converted failure");
        Converter<int, Result<string>> converter = value =>
            returnFailure
                ? Result<string>.Failure(exception)
                : Result<string>.Success(value.ToString());
        Converter<int, Result<string, int>> genericConverter = value =>
            returnFailure
                ? Result<string, int>.Failure(7)
                : Result<string, int>.Success(value.ToString());

        Result<string> result = Result<int>.Success(42).Convert(converter);
        Result<string, int> generic = Result<int, int>.Success(42).Convert(genericConverter);

        Assert.That(result.IsFailed, Is.EqualTo(returnFailure));
        Assert.That(generic.IsFailed, Is.EqualTo(returnFailure));
        if (returnFailure)
        {
            Assert.That(result.Error, Is.SameAs(exception));
            Assert.That(generic.Error, Is.EqualTo(7));
        }
        else
        {
            Assert.That(result.OrThrow(), Is.EqualTo("42"));
            Assert.That(generic.OrThrow(), Is.EqualTo("42"));
        }
    }

    [Test]
    public void ConvertPreservesFailuresWithoutCallingEitherConverter()
    {
        var exception = new InvalidOperationException("original failure");
        var result = Result<int>.Failure(exception);
        var generic = Result<int, string>.Failure("original failure");
        var calls = 0;
        Converter<int, string> converter = value =>
        {
            calls++;
            return value.ToString();
        };
        Converter<int, Result<string>> binder = value =>
        {
            calls++;
            return Result<string>.Success(value.ToString());
        };
        Converter<int, Result<string, string>> genericBinder = value =>
        {
            calls++;
            return Result<string, string>.Success(value.ToString());
        };

        Assert.That(result.Convert(converter).Error, Is.SameAs(exception));
        Assert.That(result.Convert(binder).Error, Is.SameAs(exception));
        Assert.That(generic.Convert(converter).Error, Is.EqualTo("original failure"));
        Assert.That(generic.Convert(genericBinder).Error, Is.EqualTo("original failure"));
        Assert.That(calls, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExceptionResultCapturesConverterExceptions(bool flatten)
    {
        var exception = new InvalidOperationException("converter failure");
        Converter<int, string> converter = _ => throw exception;
        Converter<int, Result<string>> binder = _ => throw exception;

        var result = flatten
            ? Result<int>.Success(42).Convert(binder)
            : Result<int>.Success(42).Convert(converter);

        Assert.That(result.IsFailed, Is.True);
        Assert.That(result.Error, Is.SameAs(exception));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GenericResultPropagatesConverterExceptions(bool flatten)
    {
        var exception = new InvalidOperationException("converter failure");
        Converter<int, string> converter = _ => throw exception;
        Converter<int, Result<string, int>> binder = _ => throw exception;

        var thrown = Assert.Throws<InvalidOperationException>(() =>
        {
            if (flatten)
                Result<int, int>.Success(42).Convert(binder);
            else
                Result<int, int>.Success(42).Convert(converter);
        });

        Assert.That(thrown, Is.SameAs(exception));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ConvertRejectsNullConvertersInEitherState(bool successful)
    {
        var result = successful ? Result<int>.Success(42) : Result<int>.Failure(new Exception());
        var generic = successful
            ? Result<int, string>.Success(42)
            : Result<int, string>.Failure("failure");

        Assert.Throws<ArgumentNullException>(() => result.Convert((Converter<int, string>)null!));
        Assert.Throws<ArgumentNullException>(() =>
            result.Convert((Converter<int, Result<string>>)null!)
        );
        Assert.Throws<ArgumentNullException>(() => generic.Convert((Converter<int, string>)null!));
        Assert.Throws<ArgumentNullException>(() =>
            generic.Convert((Converter<int, Result<string, string>>)null!)
        );
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OrUsesTheFallbackOnlyForFailures(bool successful)
    {
        var result = successful ? Result<int>.Success(42) : Result<int>.Failure(new Exception());
        var generic = successful ? Result<int, int>.Success(42) : Result<int, int>.Failure(0);

        Assert.That(result.Or(7), Is.EqualTo(successful ? 42 : 7));
        Assert.That(generic.Or(7), Is.EqualTo(successful ? 42 : 7));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OrInvokeEvaluatesFallbacksOnlyForFailures(bool successful)
    {
        var exception = new InvalidOperationException("failure");
        var result = successful ? Result<int>.Success(42) : Result<int>.Failure(exception);
        var generic = successful ? Result<int, int>.Success(42) : Result<int, int>.Failure(7);
        var calls = 0;

        Assert.That(
            result.OrInvoke(() =>
            {
                calls++;
                return 7;
            }),
            Is.EqualTo(successful ? 42 : 7)
        );
        Assert.That(
            result.OrInvoke(error =>
            {
                calls++;
                Assert.That(error, Is.SameAs(exception));
                return 7;
            }),
            Is.EqualTo(successful ? 42 : 7)
        );
        Assert.That(
            generic.OrInvoke(() =>
            {
                calls++;
                return 7;
            }),
            Is.EqualTo(successful ? 42 : 7)
        );
        Assert.That(
            generic.OrInvoke(error =>
            {
                calls++;
                return error;
            }),
            Is.EqualTo(successful ? 42 : 7)
        );
        Assert.That(calls, Is.EqualTo(successful ? 0 : 4));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OrInvokeRejectsNullFallbacksInEitherState(bool successful)
    {
        var result = successful ? Result<int>.Success(42) : Result<int>.Failure(new Exception());
        var generic = successful
            ? Result<int, string>.Success(42)
            : Result<int, string>.Failure("failure");

        Assert.Throws<ArgumentNullException>(() => result.OrInvoke((Func<int>)null!));
        Assert.Throws<ArgumentNullException>(() => result.OrInvoke((Func<Exception, int>)null!));
        Assert.Throws<ArgumentNullException>(() => generic.OrInvoke((Func<int>)null!));
        Assert.Throws<ArgumentNullException>(() => generic.OrInvoke((Func<string, int>)null!));
    }

    [Test]
    public void OrThrowReturnsSuccessfulValuesIncludingNull()
    {
        Assert.That(Result<int>.Success(42).OrThrow(), Is.EqualTo(42));
        Assert.That(Result<string?>.Success(null).OrThrow(), Is.Null);
        Assert.That(Result<int, string>.Success(42).OrThrow(), Is.EqualTo(42));
        Assert.That(Result<string?, string>.Success(null).OrThrow(), Is.Null);
    }

    [Test]
    public void OrThrowPreservesTheOriginalExceptionAndThrowSite()
    {
        var exception = CaptureException();
        var thrown = Assert.Throws<InvalidOperationException>(() =>
            Result<int>.Failure(exception).OrThrow()
        );
        var genericThrown = Assert.Throws<InvalidOperationException>(() =>
            Result<int, Exception>.Failure(exception).OrThrow()
        );

        Assert.That(thrown, Is.SameAs(exception));
        Assert.That(genericThrown, Is.SameAs(exception));
        Assert.That(thrown!.StackTrace, Does.Contain(nameof(ThrowOriginal)));
        Assert.That(genericThrown!.StackTrace, Does.Contain(nameof(ThrowOriginal)));
    }

    [Test]
    public void GenericOrThrowMapsOnlyFailures()
    {
        var exception = new InvalidOperationException("mapped failure");
        var calls = 0;
        Exception MapError(string error)
        {
            calls++;
            Assert.That(error, Is.EqualTo("failure"));
            return exception;
        }

        Assert.That(Result<int, string>.Success(42).OrThrow(MapError), Is.EqualTo(42));
        Assert.That(calls, Is.Zero);
        var thrown = Assert.Throws<InvalidOperationException>(() =>
            Result<int, string>.Failure("failure").OrThrow(MapError)
        );
        Assert.That(thrown, Is.SameAs(exception));
        Assert.That(calls, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ErrorMappingRejectsNullCallbacksInEitherState(bool successful)
    {
        var result = successful
            ? Result<int, string>.Success(42)
            : Result<int, string>.Failure("failure");

        Assert.Throws<ArgumentNullException>(() => result.OrThrow((Func<string, Exception>)null!));
        Assert.Throws<ArgumentNullException>(() => result.ToResult(null!));
    }

    [Test]
    public void BoxPreservesSuccessesNullsAndErrors()
    {
        var exception = new InvalidOperationException("failure");

        Assert.That(Result<int>.Success(42).Box().OrThrow(), Is.EqualTo(42));
        Assert.That(Result<string?>.Success(null).Box().IsSuccessful, Is.True);
        Assert.That(Result<string?>.Success(null).Box().OrThrow(), Is.Null);
        Assert.That(Result<int>.Failure(exception).Box().Error, Is.SameAs(exception));
        Assert.That(Result<int, int>.Success(42).Box().OrThrow(), Is.EqualTo(42));
        Assert.That(Result<string?, int>.Success(null).Box().IsSuccessful, Is.True);
        Assert.That(Result<string?, int>.Success(null).Box().OrThrow(), Is.Null);
        Assert.That(Result<int, int>.Failure(0).Box().IsFailed, Is.True);
        Assert.That(Result<int, int>.Failure(0).Box().Error, Is.Zero);
    }

    [Test]
    public void ToResultPreservesSuccessesAndExceptionErrors()
    {
        var exception = new InvalidOperationException("failure");

        Assert.That(Result<int, string>.Success(42).ToResult().OrThrow(), Is.EqualTo(42));
        Assert.That(Result<string?, string>.Success(null).ToResult().IsSuccessful, Is.True);
        Assert.That(Result<string?, string>.Success(null).ToResult().OrThrow(), Is.Null);
        Assert.That(
            Result<int, Exception>.Failure(exception).ToResult().Error,
            Is.SameAs(exception)
        );
        Assert.That(Result<int, int>.Failure(7).ToResult().Error!.Message, Is.EqualTo("7"));
    }

    [Test]
    public void ToResultMapsOnlyFailuresAndPropagatesMapperExceptions()
    {
        var exception = new InvalidOperationException("mapped failure");
        var calls = 0;
        Exception MapError(string error)
        {
            calls++;
            Assert.That(error, Is.EqualTo("failure"));
            return exception;
        }

        Assert.That(Result<int, string>.Success(42).ToResult(MapError).OrThrow(), Is.EqualTo(42));
        Assert.That(calls, Is.Zero);
        Assert.That(
            Result<int, string>.Failure("failure").ToResult(MapError).Error,
            Is.SameAs(exception)
        );
        Assert.That(calls, Is.EqualTo(1));
        var thrown = Assert.Throws<InvalidOperationException>(() =>
            Result<int, string>.Failure("failure").ToResult(_ => throw exception)
        );
        Assert.That(thrown, Is.SameAs(exception));
    }

    [Test]
    public async Task AsTaskCompletesWithSuccessfulValuesIncludingNull()
    {
        var task = Result<int>.Success(42).AsTask();
        var nullTask = Result<string?>.Success(null).AsTask();

        Assert.That(task.IsCompletedSuccessfully, Is.True);
        Assert.That(await task, Is.EqualTo(42));
        Assert.That(nullTask.IsCompletedSuccessfully, Is.True);
        Assert.That(await nullTask, Is.Null);
    }

    [Test]
    public void AsTaskFaultsWithTheOriginalException()
    {
        var exception = new InvalidOperationException("failure");
        var task = Result<int>.Failure(exception).AsTask();

        Assert.That(task.IsFaulted, Is.True);
        Assert.That(
            Assert.ThrowsAsync<InvalidOperationException>(async () => await task),
            Is.SameAs(exception)
        );
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AsTaskCancelsOnlyWhenTheExceptionTokenIsCanceled(bool canceled)
    {
        var token = new CancellationToken(canceled);
        var exception = new OperationCanceledException(token);
        var task = Result<int>.Failure(exception).AsTask();

        Assert.That(task.IsCanceled, Is.EqualTo(canceled));
        Assert.That(task.IsFaulted, Is.EqualTo(!canceled));
        var thrown = Assert.CatchAsync<OperationCanceledException>(async () => await task);
        Assert.That(thrown!.CancellationToken, Is.EqualTo(token));
        if (!canceled)
            Assert.That(thrown, Is.SameAs(exception));
    }

    [Test]
    public void DefaultUnionHasNoValueOrErrorAndRejectsOperationsRequiringAnError()
    {
        Result<int> result = default;
        Result<int, string> generic = default;

        Assert.That(result.IsSuccessful, Is.False);
        Assert.That(result.IsFailed, Is.True);
        Assert.That(result.ValueOrDefault, Is.Zero);
        Assert.That(result.Error, Is.Null);
        Assert.That(result.TryGet(out _), Is.False);
        Assert.That(result.TryFailure(out _), Is.False);
        Assert.That(result.Or(7), Is.EqualTo(7));
        Assert.That(result.OrInvoke(() => 7), Is.EqualTo(7));
        Assert.Throws<InvalidOperationException>(() => result.OrThrow());
        Assert.Throws<InvalidOperationException>(() => result.AsTask());
        Assert.That(generic.IsSuccessful, Is.False);
        Assert.That(generic.IsFailed, Is.True);
        Assert.That(generic.ValueOrDefault, Is.Zero);
        Assert.That(generic.Error, Is.Null);
        Assert.That(generic.TrySuccess(out _), Is.False);
        Assert.That(generic.TryFailure(out _), Is.False);
        Assert.That(generic.Or(7), Is.EqualTo(7));
        Assert.Throws<InvalidOperationException>(() => generic.ToResult());
        Assert.Throws<InvalidOperationException>(() => generic.OrInvoke(_ => 7));
    }

    [Test]
    public void DefaultFailureCasesDoNotExposeNullAsAValidError()
    {
        Result<int> result = default(Failure<Exception>);
        Result<int, string> generic = default(Failure<string>);
        var calls = 0;

        Assert.That(result.IsSuccessful, Is.False);
        Assert.That(result.TryFailure(out var exception), Is.False);
        Assert.That(exception, Is.Null);
        Assert.That(generic.IsSuccessful, Is.False);
        Assert.That(generic.TryFailure(out var error), Is.False);
        Assert.That(error, Is.Null);
        Assert.Throws<InvalidOperationException>(() =>
            result.OrInvoke(_ =>
            {
                calls++;
                return 7;
            })
        );
        Assert.Throws<InvalidOperationException>(() =>
            generic.OrInvoke(_ =>
            {
                calls++;
                return 7;
            })
        );
        Assert.Throws<InvalidOperationException>(() => result.AsTask());
        Assert.Throws<InvalidOperationException>(() => generic.ToResult());
        Assert.That(calls, Is.Zero);
    }

    private static InvalidOperationException CaptureException()
    {
        try
        {
            ThrowOriginal();
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }

        throw new AssertionException("Expected the original throw site to throw.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowOriginal() => throw new InvalidOperationException("original failure");
}
