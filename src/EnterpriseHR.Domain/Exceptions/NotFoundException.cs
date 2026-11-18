namespace EnterpriseHR.Domain.Exceptions;

/// <summary>The requested entity does not exist. Maps to HTTP 404.</summary>
public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
