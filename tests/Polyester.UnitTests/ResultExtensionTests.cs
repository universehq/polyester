namespace Polyester.UnitTests;

[TestFixture]
public class ResultExtensionTests
{
    [Test]
    public void FactoriesPreserveTheRequestedCase()
    {
        var exception = new InvalidOperationException("failure");
        var successfulException = Result.FromValue(exception);
        var successfulNull = Result.FromValue<string?>(null);
        var failure = Result.FromException<int>(exception);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(successfulException.TrySuccess(out var value), Is.True);
            Assert.That(value, Is.SameAs(exception));
            Assert.That(successfulNull.TrySuccess(out var nullValue), Is.True);
            Assert.That(nullValue, Is.Null);
            Assert.That(failure.TryFailure(out var error), Is.True);
            Assert.That(error, Is.SameAs(exception));
        }

        Assert.Throws<ArgumentNullException>(() => Result.FromException<int>(null!));
    }

    [Test]
    public void OrNullDistinguishesDefaultValuesFromFailures()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Result<int>.Success(0).OrNull(), Is.Zero);
            Assert.That(Result<int>.Success(42).OrNull(), Is.EqualTo(42));
            Assert.That(Result<int>.Failure(new Exception()).OrNull(), Is.Null);
            Assert.That(Result<int, string>.Success(0).OrNull(), Is.Zero);
            Assert.That(Result<int, string>.Success(42).OrNull(), Is.EqualTo(42));
            Assert.That(Result<int, string>.Failure("failure").OrNull(), Is.Null);
        }
    }

    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public void CoalesceReferencesTheSelectedResult(bool firstSuccessful, bool secondSuccessful)
    {
        var first = firstSuccessful
            ? Result<int>.Success(1)
            : Result<int>.Failure(new Exception("first"));
        var second = secondSuccessful
            ? Result<int>.Success(2)
            : Result<int>.Failure(new Exception("second"));
        ref readonly var chosen = ref first.Coalesce(in second);

        Assert.That(chosen.IsSuccessful, Is.EqualTo(firstSuccessful || secondSuccessful));
        first = Result<int>.Success(3);
        second = Result<int>.Success(4);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chosen.TrySuccess(out var value), Is.True);
            Assert.That(value, Is.EqualTo(firstSuccessful ? 3 : 4));
        }
    }

    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public void TypedCoalesceReferencesTheSelectedResult(
        bool firstSuccessful,
        bool secondSuccessful
    )
    {
        var first = firstSuccessful
            ? Result<int, string>.Success(1)
            : Result<int, string>.Failure("first");
        var second = secondSuccessful
            ? Result<int, string>.Success(2)
            : Result<int, string>.Failure("second");
        ref readonly var chosen = ref first.Coalesce(in second);

        Assert.That(chosen.IsSuccessful, Is.EqualTo(firstSuccessful || secondSuccessful));
        first = Result<int, string>.Success(3);
        second = Result<int, string>.Success(4);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chosen.TrySuccess(out var value), Is.True);
            Assert.That(value, Is.EqualTo(firstSuccessful ? 3 : 4));
        }
    }

    [Test]
    public void CoalesceKeepsSuccessfulNullValues()
    {
        var first = Result<string?>.Success(null);
        var second = Result<string?>.Success("fallback");
        var typedFirst = Result<string?, string>.Success(null);
        var typedSecond = Result<string?, string>.Success("fallback");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Coalesce(in second).TrySuccess(out var value), Is.True);
            Assert.That(value, Is.Null);
            Assert.That(
                typedFirst.Coalesce(in typedSecond).TrySuccess(out var typedValue),
                Is.True
            );
            Assert.That(typedValue, Is.Null);
        }
    }

    [TestCase(typeof(Result<int>), typeof(int))]
    [TestCase(typeof(Result<string, int>), typeof(string))]
    [TestCase(typeof(Result<int?>), typeof(int?))]
    public void ResultTypeHelpersRecognizeConstructedResults(Type type, Type underlyingType)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(type.IsResult(), Is.True);
            Assert.That(Result.GetUnderlyingType(type), Is.EqualTo(underlyingType));
        }
    }

    [TestCase(typeof(Result))]
    [TestCase(typeof(Result<>))]
    [TestCase(typeof(Result<,>))]
    [TestCase(typeof(Success<int>))]
    [TestCase(typeof(Failure<Exception>))]
    [TestCase(typeof(List<int>))]
    [TestCase(typeof(int))]
    public void ResultTypeHelpersRejectOtherTypes(Type type)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(type.IsResult(), Is.False);
            Assert.That(Result.GetUnderlyingType(type), Is.Null);
        }
    }

    [Test]
    public void ResultTypeHelpersRejectNull()
    {
        Assert.Throws<ArgumentNullException>(() => Result.IsResult(null!));
        Assert.Throws<ArgumentNullException>(() => Result.GetUnderlyingType(null!));
    }

    [Test]
    public void EnsureNotNullPreservesNonNullValuesAndExistingFailures()
    {
        var value = new object();
        var exception = new ArgumentException("original failure");
        var success = Result<object?>.Success(value).EnsureNotNull();
        var failure = Result<object?>.Failure(exception).EnsureNotNull();
        var customFailure = Result<object?>
            .Failure(exception)
            .EnsureNotNull<object, InvalidOperationException>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success.TrySuccess(out var actualValue), Is.True);
            Assert.That(actualValue, Is.SameAs(value));
            Assert.That(failure.TryFailure(out var actualException), Is.True);
            Assert.That(actualException, Is.SameAs(exception));
            Assert.That(customFailure.TryFailure(out var customException), Is.True);
            Assert.That(customException, Is.SameAs(exception));
        }
    }

    [Test]
    public void EnsureNotNullCapturesAStackTraceForNullFailures()
    {
        var result = Result<string?>.Success(null);
        var failure = result.EnsureNotNull();
        var customFailure = result.EnsureNotNull<string, InvalidOperationException>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(failure.TryFailure(out var error), Is.True);
            Assert.That(error, Is.TypeOf<NullReferenceException>());
            Assert.That(error!.StackTrace, Is.Not.Null.And.Not.Empty);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(customFailure.TryFailure(out var customError), Is.True);
            Assert.That(customError, Is.TypeOf<InvalidOperationException>());
            Assert.That(customError!.StackTrace, Is.Not.Null.And.Not.Empty);
        }
    }
}
