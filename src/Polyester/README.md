# Polyester

`Result<TValue>` holds either `Success<TValue>` or `Failure<Exception>`.
`Result<TValue, TError>` uses any non-null error type instead of an exception.

```csharp
using Polyester;

var result = Result<int>.Success(21);
var doubled = result.Convert(value => value * 2);
int value = doubled.OrThrow(); // 42

var failure = Result<int, string>.Failure("Not found");
int fallback = failure.Or(0);
Result<int> exceptionResult = failure.ToResult(error => new InvalidOperationException(error));
```

- `TrySuccess` (also named `TryGet`) and `TryFailure` extract cases without throwing.
  The union's `Value` property remains the wrapped case; `ValueOrDefault` exposes the success payload.
- `Convert` maps a success or chains a callback returning another result. Existing failures bypass
  the callback. `Result<TValue>` captures callback exceptions; `Result<TValue, TError>` propagates them.
- `Or`, `OrInvoke`, and `OrThrow` supply a fallback, evaluate a fallback lazily, or throw on failure.
  `OrInvoke` also accepts a callback receiving the error.
- `Box` preserves the case while boxing the success payload. `ToResult` converts a typed error to
  an exception and preserves errors that already are exceptions.
- `AsTask` returns a completed `ValueTask<TValue>` for exception results. A cancellation exception
  produces a canceled task only if its token is canceled; otherwise it produces a faulted task.
- The `|` operator selects a successful result or fallback value. `OrNull`, `Coalesce`, and
  `EnsureNotNull` provide nullable-value, result-selection, and non-null-success helpers.
- Conversion and fallback callbacks also have function-pointer overloads for unsafe callers.
  Null delegates and pointers are rejected, even when their callback would not run.

A successful null payload is valid. A default-valued non-null error, such as integer zero, is still
a failure. A default result (or a failure case containing null) is uninitialized: try methods return
false, simple fallbacks work, and operations that require a stored value or error throw
`InvalidOperationException`. Use the success/failure factories or explicit case wrappers to initialize results.

## Error values

`Polyester.Error.IError` is an optional contract for reporting typed errors. Its design follows
the small, composable contract of [Rust's `std::error::Error`](https://doc.rust-lang.org/std/error/trait.Error.html):
an error explains itself and may expose an immediate cause across an abstraction boundary.
`Result<TValue, TError>` still accepts any non-null error type.

```csharp
public interface IError
{
    string ToString();
    IError? Source => null;
}
```

`ToString()` provides a concise explanation of the current failure, and `Source` exposes its
immediate underlying cause, if any. Implementers should provide a public `ToString()` override
so that interface calls, interpolation, and result formatting use the same explanation.
Declaring `ToString()` documents this expectation but cannot force a custom override:
inherited and record-generated implementations can also satisfy it. Use concrete properties
and debugger views for additional diagnostic detail.

Each message describes one level of failure. If a wrapper exposes its cause through `Source`,
it should not also copy that cause's message into its own message. Source chains must terminate
and contain no cycles. A reporter can then walk the chain without repeating explanations:

```csharp
using Polyester;
using Polyester.Error;

var result = Result<int, ReadConfigError>.Failure(
    new ReadConfigError("server.json", new InvalidPortError(70000)));

if (result.TryFailure(out var error))
{
    for (IError? current = error; current is not null; current = current.Source)
    {
        Console.WriteLine(current.ToString());
    }

    // Pattern matching retains access to the cause's structured context.
    if (error.Source is InvalidPortError { Port: var port })
    {
        Console.WriteLine($"Rejected port: {port}");
    }
}

public readonly record struct InvalidPortError(int Port) : IError
{
    public override string ToString() => $"port {Port} is outside the range 1–65535";
}

public sealed record ReadConfigError(string Path, IError Source) : IError
{
    public override string ToString() => $"unable to read configuration at {Path}";
}
```

Leaf errors can override `ToString()` for display and use the default null `Source`.
The default `Source` member is available through an
`IError` reference or a generic type constrained to `IError`. Both classes and structs can
implement the contract; converting a struct to `IError` boxes it. Causes retain their concrete
types, so C# pattern matching supplies typed access without additional downcast helpers.
Error codes and other domain context belong on the concrete error type.

`Source` means a causal error, like `Exception.InnerException`; it does not mean the assembly
name in `Exception.Source`. Exceptions do not automatically implement `IError`.
Exception conversion remains an explicit boundary decision: `ToResult()` preserves an existing
exception, otherwise it uses the error's `ToString()` as a new exception's message. It does not
interpret `IError` or preserve its source chain. Use `ToResult(exceptionFactory)` or
`OrThrow(exceptionFactory)` when mapping domain errors and their causes to exceptions.
Likewise, `Result.ToString()` delegates to the contained value's `ToString()`, so a failed result
uses the same explanation as its error.
