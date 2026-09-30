namespace Polyester.UnitTests;

public class ResultOperatorTests
{
    [Test]
    public void GenericOperatorsChooseSuccessfulValues()
    {
        Result<int, string> success = 42;
        var failure = Result<int, string>.Failure("failed");
        var secondFailure = Result<int, string>.Failure("second");

        using (Assert.EnterMultipleScope())
        {
            Assert.That((int)success, Is.EqualTo(42));
            Assert.That(success | 10, Is.EqualTo(42));
            Assert.That(failure | 10, Is.EqualTo(10));
            Assert.That((int)(success | failure), Is.EqualTo(42));
            Assert.That((int)(failure | success), Is.EqualTo(42));
            Assert.That((failure | secondFailure).TryFailure(out var error), Is.True);
            Assert.That(error, Is.EqualTo("second"));
            Assert.That(success & success, Is.True);
            Assert.That(success & failure, Is.False);
            Assert.That(failure & success, Is.False);
            Assert.That(failure & failure, Is.False);
            Assert.That(!success, Is.False);
            Assert.That(!failure, Is.True);
            Assert.That(success ? true : false, Is.True);
            Assert.That(failure ? true : false, Is.False);
        }
    }

    [Test]
    public void ExceptionOperatorsChooseSuccessfulValues()
    {
        Result<int> success = 42;
        var error = new InvalidOperationException("failed");
        var secondError = new InvalidOperationException("second");
        var failure = Result<int>.Failure(error);
        var secondFailure = Result<int>.Failure(secondError);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((int)success, Is.EqualTo(42));
            Assert.That(success | 10, Is.EqualTo(42));
            Assert.That(failure | 10, Is.EqualTo(10));
            Assert.That((int)(success | failure), Is.EqualTo(42));
            Assert.That((int)(failure | success), Is.EqualTo(42));
            Assert.That((failure | secondFailure).TryFailure(out var actual), Is.True);
            Assert.That(actual, Is.SameAs(secondError));
            Assert.That(success & success, Is.True);
            Assert.That(success & failure, Is.False);
            Assert.That(failure & success, Is.False);
            Assert.That(failure & failure, Is.False);
            Assert.That(!success, Is.False);
            Assert.That(!failure, Is.True);
            Assert.That(success ? true : false, Is.True);
            Assert.That(failure ? true : false, Is.False);
        }
    }

    [Test]
    public void ExplicitValueConversionsRethrowTheOriginalException()
    {
        var error = new InvalidOperationException("failed");
        var generic = Result<int, Exception>.Failure(error);
        var result = Result<int>.Failure(error);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                Assert.Throws<InvalidOperationException>(() => _ = (int)generic),
                Is.SameAs(error)
            );
            Assert.That(
                Assert.Throws<InvalidOperationException>(() => _ = (int)result),
                Is.SameAs(error)
            );
        }
    }

    [Test]
    public void GenericResultConvertsToExceptionResult()
    {
        Result<int> success = Result<int, string>.Success(42);
        Result<int> failure = Result<int, string>.Failure("failed");
        var error = new InvalidOperationException("original");
        Result<int> exceptionFailure = Result<int, Exception>.Failure(error);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((int)success, Is.EqualTo(42));
            Assert.That(failure.TryFailure(out var convertedError), Is.True);
            Assert.That(convertedError!.Message, Is.EqualTo("failed"));
            Assert.That(exceptionFailure.TryFailure(out var preservedError), Is.True);
            Assert.That(preservedError, Is.SameAs(error));
        }
    }

    [Test]
    public async Task ExceptionResultConvertsToValueTask()
    {
        var success = Result<int>.Success(42);
        var error = new InvalidOperationException("failed");
        var failure = Result<int>.Failure(error);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await (ValueTask<int>)success, Is.EqualTo(42));
            Assert.That(
                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    _ = await (ValueTask<int>)failure
                ),
                Is.SameAs(error)
            );
        }
    }

    [Test]
    public void ToStringDisplaysTheContainedValueOrError()
    {
        var error = new InvalidOperationException("failed");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Result<int, string>.Success(42).ToString(), Is.EqualTo("42"));
            Assert.That(Result<int, string>.Failure("failed").ToString(), Is.EqualTo("failed"));
            Assert.That(Result<int>.Success(42).ToString(), Is.EqualTo("42"));
            Assert.That(Result<int>.Failure(error).ToString(), Is.EqualTo(error.ToString()));
            Assert.That(Result<string?, string>.Success(null).ToString(), Is.Empty);
            Assert.That(Result<string?>.Success(null).ToString(), Is.Empty);
            Assert.That(Result<NullText, string>.Success(new NullText()).ToString(), Is.Empty);
            Assert.That(Result<int, NullText>.Failure(new NullText()).ToString(), Is.Empty);
            Assert.That(Result<NullText>.Success(new NullText()).ToString(), Is.Empty);
            Assert.That(default(Result<int, string>).ToString(), Is.Empty);
            Assert.That(default(Result<int>).ToString(), Is.Empty);
        }
    }

    private sealed class NullText
    {
        public override string? ToString() => null;
    }
}
