namespace Polyester.Error;

/// <summary>Describes an error value and its optional lower-level cause.</summary>
/// <remarks>
/// <para>
/// This is an opt-in reporting contract. Error values need not derive from
/// <see cref="Exception"/>, and <c>Result&lt;TValue, TError&gt;</c> does not require
/// its error type to implement this interface.
/// </para>
/// <para>
/// By convention, implementers should override <see cref="object.ToString"/> to
/// provide a concise, human-readable explanation of the failure at this level.
/// Do not repeat the explanation of an error also exposed through
/// <see cref="Source"/>. Reporters should render each error in the chain once.
/// Use C# pattern matching to inspect concrete error types and their context.
/// </para>
/// </remarks>
public interface IError
{
    /// <summary>Returns a concise explanation of this error.</summary>
    string ToString();

    /// <summary>
    /// Gets the immediate lower-level error that caused this error, or null.
    /// </summary>
    /// <remarks>
    /// Leaf errors use the default null implementation. Wrappers can expose their
    /// cause while adding their own context in <see cref="object.ToString"/>. The source
    /// chain must terminate: do not return this error or introduce a cycle.
    /// This is a causal relationship, not a collection of related failures or
    /// the assembly name represented by <see cref="Exception.Source"/>.
    /// </remarks>
    IError? Source => null;
}
