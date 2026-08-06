using System.Collections.Generic;
using System.Linq;

namespace Afip.Arca.Sdk.Common.Exceptions;

/// <summary>
/// Thrown when a business operation must be aborted because AFIP returned an
/// unrecoverable error (e.g. attempting to query a non-existent invoice).
/// Most "AFIP errors" are returned inside the result object instead; this exception
/// is reserved for paths where there is no result to inspect.
/// </summary>
public sealed class AfipBusinessException : AfipException
{
    /// <summary>Collection of <c>(code, message)</c> pairs as reported by AFIP.</summary>
    public IReadOnlyList<(int Code, string Message)> Errors { get; }

    /// <summary>Initializes a new instance of the <see cref="AfipBusinessException"/> class.</summary>
    /// <param name="errors">AFIP-reported errors.</param>
    public AfipBusinessException(IEnumerable<(int Code, string Message)> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors.ToList().AsReadOnly();
    }

    private static string BuildMessage(IEnumerable<(int Code, string Message)> errors)
    {
        return "AFIP business error: " +
               string.Join(" | ", errors.Select(e => "[" + e.Code + "] " + e.Message));
    }
}
