namespace EnterpriseHR.Domain.Exceptions;

/// <summary>
/// Base type for every expected, business-level failure. Hosts translate these into
/// responses (404 / 400 / 409); the Domain layer carries no HTTP types of its own.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message)
    {
    }

    protected AppException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
