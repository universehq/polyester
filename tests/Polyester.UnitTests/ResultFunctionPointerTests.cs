namespace Polyester.UnitTests;

public unsafe class ResultFunctionPointerTests
{
    [Test]
    public void GenericPointerConversionsMapAndFlattenSuccesses()
    {
        var success = Result<int, string>.Success(42);
        delegate* <int, string> map = &Format;
        delegate* <int, Result<string, string>> bind = &GenericSuccess;
        delegate* <int, Result<string, string>> fail = &GenericFailure;

        Assert.Multiple(() =>
        {
            Assert.That((string)success.Convert<string>(map), Is.EqualTo("42"));
            Assert.That((string)success.Convert<string>(bind), Is.EqualTo("42"));
            Assert.That(success.Convert<string>(fail).TryFailure(out var error), Is.True);
            Assert.That(error, Is.EqualTo("42"));
        });
    }

    [Test]
    public void ExceptionPointerConversionsMapAndFlattenSuccesses()
    {
        var success = Result<int>.Success(42);
        delegate* <int, string> map = &Format;
        delegate* <int, Result<string>> bind = &ExceptionSuccess;
        delegate* <int, Result<string>> fail = &ExceptionFailure;

        Assert.Multiple(() =>
        {
            Assert.That((string)success.Convert<string>(map), Is.EqualTo("42"));
            Assert.That((string)success.Convert<string>(bind), Is.EqualTo("42"));
            Assert.That(success.Convert<string>(fail).TryFailure(out var error), Is.True);
            Assert.That(error!.Message, Is.EqualTo("42"));
        });
    }

    [Test]
    public void GenericPointerConversionsPreserveFailuresAndPropagateMapperExceptions()
    {
        var failure = Result<int, string>.Failure("original");
        var success = Result<int, string>.Success(42);
        delegate* <int, string> map = &ThrowMap;
        delegate* <int, Result<string, string>> bind = &ThrowGenericBind;

        Assert.Multiple(() =>
        {
            Assert.That(failure.Convert<string>(map).TryFailure(out var mapError), Is.True);
            Assert.That(mapError, Is.EqualTo("original"));
            Assert.That(failure.Convert<string>(bind).TryFailure(out var bindError), Is.True);
            Assert.That(bindError, Is.EqualTo("original"));
        });
        Assert.Throws<InvalidOperationException>(() => success.Convert<string>(map));
        Assert.Throws<InvalidOperationException>(() => success.Convert<string>(bind));
    }

    [Test]
    public void ExceptionPointerConversionsPreserveFailuresAndCaptureMapperExceptions()
    {
        var error = new InvalidOperationException("original");
        var failure = Result<int>.Failure(error);
        var success = Result<int>.Success(42);
        delegate* <int, string> map = &ThrowMap;
        delegate* <int, Result<string>> bind = &ThrowExceptionBind;

        Assert.Multiple(() =>
        {
            Assert.That(failure.Convert<string>(map).TryFailure(out var mapError), Is.True);
            Assert.That(mapError, Is.SameAs(error));
            Assert.That(failure.Convert<string>(bind).TryFailure(out var bindError), Is.True);
            Assert.That(bindError, Is.SameAs(error));
            Assert.That(success.Convert<string>(map).TryFailure(out var thrownMapError), Is.True);
            Assert.That(thrownMapError, Is.TypeOf<InvalidOperationException>());
            Assert.That(thrownMapError!.Message, Is.EqualTo("mapper failed"));
            Assert.That(success.Convert<string>(bind).TryFailure(out var thrownBindError), Is.True);
            Assert.That(thrownBindError, Is.TypeOf<InvalidOperationException>());
            Assert.That(thrownBindError!.Message, Is.EqualTo("mapper failed"));
        });
    }

    [Test]
    public void GenericPointerFallbacksRunOnlyOnFailure()
    {
        var success = Result<int, string>.Success(42);
        var failure = Result<int, string>.Failure("failure");
        delegate* <int> fallback = &Fallback;
        delegate* <string, int> errorFallback = &StringLength;
        delegate* <int> throwingFallback = &ThrowFallback;
        delegate* <string, int> throwingErrorFallback = &ThrowStringFallback;
        delegate* <string, Exception> exceptionFactory = &CreateException;
        delegate* <string, Exception> throwingFactory = &ThrowExceptionFactory;

        Assert.Multiple(() =>
        {
            Assert.That(failure.OrInvoke(fallback), Is.EqualTo(23));
            Assert.That(failure.OrInvoke(errorFallback), Is.EqualTo(7));
            Assert.That(success.OrInvoke(throwingFallback), Is.EqualTo(42));
            Assert.That(success.OrInvoke(throwingErrorFallback), Is.EqualTo(42));
            Assert.That(success.OrThrow(throwingFactory), Is.EqualTo(42));
        });
        var error = Assert.Throws<InvalidOperationException>(() =>
            failure.OrThrow(exceptionFactory)
        );
        Assert.That(error!.Message, Is.EqualTo("failure"));
        Assert.Throws<InvalidOperationException>(() => failure.OrInvoke(throwingFallback));
        Assert.Throws<InvalidOperationException>(() => failure.OrInvoke(throwingErrorFallback));
    }

    [Test]
    public void ExceptionPointerFallbacksRunOnlyOnFailure()
    {
        var success = Result<int>.Success(42);
        var failure = Result<int>.Failure(new InvalidOperationException("failure"));
        delegate* <int> fallback = &Fallback;
        delegate* <Exception, int> errorFallback = &ExceptionLength;
        delegate* <int> throwingFallback = &ThrowFallback;
        delegate* <Exception, int> throwingErrorFallback = &ThrowExceptionFallback;

        Assert.Multiple(() =>
        {
            Assert.That(failure.OrInvoke(fallback), Is.EqualTo(23));
            Assert.That(failure.OrInvoke(errorFallback), Is.EqualTo(7));
            Assert.That(success.OrInvoke(throwingFallback), Is.EqualTo(42));
            Assert.That(success.OrInvoke(throwingErrorFallback), Is.EqualTo(42));
        });
        Assert.Throws<InvalidOperationException>(() => failure.OrInvoke(throwingFallback));
        Assert.Throws<InvalidOperationException>(() => failure.OrInvoke(throwingErrorFallback));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void GenericNullPointersAreRejectedRegardlessOfResultState(bool successful)
    {
        var result = successful
            ? Result<int, string>.Success(42)
            : Result<int, string>.Failure("failure");

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() =>
                result.Convert<string>((delegate* <int, string>)null)
            );
            Assert.Throws<ArgumentNullException>(() =>
                result.Convert<string>((delegate* <int, Result<string, string>>)null)
            );
            Assert.Throws<ArgumentNullException>(() => result.OrInvoke((delegate* <int>)null));
            Assert.Throws<ArgumentNullException>(() =>
                result.OrInvoke((delegate* <string, int>)null)
            );
            Assert.Throws<ArgumentNullException>(() =>
                result.OrThrow((delegate* <string, Exception>)null)
            );
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ExceptionNullPointersAreRejectedRegardlessOfResultState(bool successful)
    {
        var result = successful
            ? Result<int>.Success(42)
            : Result<int>.Failure(new Exception("failure"));

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() =>
                result.Convert<string>((delegate* <int, string>)null)
            );
            Assert.Throws<ArgumentNullException>(() =>
                result.Convert<string>((delegate* <int, Result<string>>)null)
            );
            Assert.Throws<ArgumentNullException>(() => result.OrInvoke((delegate* <int>)null));
            Assert.Throws<ArgumentNullException>(() =>
                result.OrInvoke((delegate* <Exception, int>)null)
            );
        });
    }

    private static string Format(int value) => value.ToString();

    private static Result<string, string> GenericSuccess(int value) =>
        Result<string, string>.Success(value.ToString());

    private static Result<string, string> GenericFailure(int value) =>
        Result<string, string>.Failure(value.ToString());

    private static Result<string> ExceptionSuccess(int value) =>
        Result<string>.Success(value.ToString());

    private static Result<string> ExceptionFailure(int value) =>
        Result<string>.Failure(new InvalidOperationException(value.ToString()));

    private static string ThrowMap(int value) =>
        throw new InvalidOperationException("mapper failed");

    private static Result<string, string> ThrowGenericBind(int value) =>
        throw new InvalidOperationException("mapper failed");

    private static Result<string> ThrowExceptionBind(int value) =>
        throw new InvalidOperationException("mapper failed");

    private static int Fallback() => 23;

    private static int StringLength(string error) => error.Length;

    private static int ExceptionLength(Exception error) => error.Message.Length;

    private static int ThrowFallback() => throw new InvalidOperationException("fallback failed");

    private static int ThrowStringFallback(string error) =>
        throw new InvalidOperationException("fallback failed");

    private static int ThrowExceptionFallback(Exception error) =>
        throw new InvalidOperationException("fallback failed");

    private static Exception CreateException(string error) => new InvalidOperationException(error);

    private static Exception ThrowExceptionFactory(string error) =>
        throw new InvalidOperationException("factory failed");
}
