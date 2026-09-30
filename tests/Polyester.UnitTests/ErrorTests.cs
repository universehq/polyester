using Polyester.CompilerServices;

namespace Polyester.UnitTests;

public class Tests
{
    [Test]
    public void GeneratedResultFactoriesCreateTheExpectedCases()
    {
        var success = TestResult<int, string>.Success(42);
        var failure = TestResult<int, string>.Failure("nope");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success.IsSuccessful, Is.True);
            Assert.That(failure.IsSuccessful, Is.False);
            Assert.That(success.Value, Is.EqualTo(new Success<int>(42)));
            Assert.That(failure.Value, Is.EqualTo(new Failure<string>("nope")));
        }
    }

    [Test]
    public void GeneratedFailureRejectsNullErrors()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TestResult<int, string>.Failure(null!)
        );
    }

    [Test]
    public void GeneratedResultImplementsIResult()
    {
        IResult<int, TestError, TestResult<int>> result =
            TestResult<int>.Failure(new NotFound());

        Assert.That(result.IsFailed, Is.True);
    }

    [Test]
    public void GeneratedUnitResultSupportsFixedSuccessAndErrorTypes()
    {
        Assert.That(TestResult.Success(Unit.Instance).IsSuccessful, Is.True);
    }
}

[ResultImpl]
public readonly partial union TestResult<T, E>(Success<T>, Failure<E>);

[ResultImpl]
public readonly partial union TestResult<T>(Success<T>, Failure<TestError>);

[ResultImpl]
public readonly partial union TestResult(Success<Unit>, Failure<TestError>);

public readonly union TestError(NotFound, BadRequest);

public readonly record struct NotFound();
public readonly record struct BadRequest();
