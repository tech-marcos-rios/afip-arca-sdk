using System.Collections.Generic;
using System.Linq;

namespace Afip.Arca.Sdk.Common.Exceptions;

/// <summary>
/// Thrown when the SDK detects an invalid request before it reaches AFIP — saving a
/// roundtrip and producing a clearer error message than the cryptic codes AFIP returns.
/// </summary>
public sealed class AfipValidationException : AfipException
{
    /// <summary>List of individual validation failures.</summary>
    public IReadOnlyList<string> Failures { get; }

    /// <summary>Initializes a new instance of the <see cref="AfipValidationException"/> class.</summary>
    /// <param name="failures">A non-empty collection of failure descriptions.</param>
    public AfipValidationException(IEnumerable<string> failures)
        : base(BuildMessage(failures))
    {
        Failures = failures.ToList().AsReadOnly();
    }

    /// <summary>Initializes a new instance of the <see cref="AfipValidationException"/> class.</summary>
    /// <param name="failure">A single failure description.</param>
    public AfipValidationException(string failure) : this(new[] { failure })
    {
    }

    private static string BuildMessage(IEnumerable<string> failures)
    {
        var list = failures.ToList();
        return list.Count == 1
            ? "Validation failed: " + list[0]
            : "Validation failed with " + list.Count + " error(s): " + string.Join(" | ", list);
    }
}
