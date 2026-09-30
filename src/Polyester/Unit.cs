using System.Diagnostics;

namespace Polyester;

/// <summary>
/// Represents the <c>Unit</c> type, a type with exactly one meaningful value.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Unit"/> is useful when an API requires a value or generic type argument,
/// but no meaningful data needs to be returned or stored. It is conceptually similar
/// to <see langword="void"/>, except that <see cref="Unit"/> is an ordinary value type
/// and can therefore be used as a generic type argument.
/// </para>
/// <para>
/// See <see href="https://en.wikipedia.org/wiki/Unit_type">Unit type</see>
/// for more information.
/// </para>
/// </remarks>
[DebuggerDisplay("()")]
public readonly record struct Unit
{
    /// <summary>
    /// Gets the singleton-equivalent value of the <see cref="Unit"/> type.
    /// </summary>
    public static Unit Instance => default;

    /// <inheritdoc/>
    public override readonly string ToString() => "()";
}
