namespace SwarmFish.Core.Domain.Exceptions;

/// <summary>
/// Exception thrown when a domain invariant is violated.
/// </summary>
public class DomainException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="DomainException"/> with the specified message.
    /// </summary>
    /// <param name="message">The message describing the invariant violation.</param>
    public DomainException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DomainException"/> with a message and inner exception.
    /// </summary>
    /// <param name="message">The message describing the invariant violation.</param>
    /// <param name="innerException">The inner exception that caused this violation.</param>
    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
