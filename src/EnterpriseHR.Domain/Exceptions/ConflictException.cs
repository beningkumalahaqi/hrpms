namespace EnterpriseHR.Domain.Exceptions;

/// <summary>
/// The request is well formed but collides with existing data or a guard: allocation would
/// exceed 100%, duplicate assignment, deleting an employee with assignments, duplicate
/// EmployeeNumber or Email. Maps to HTTP 409.
/// </summary>
public class ConflictException : AppException
{
    public ConflictException(string message) : base(message)
    {
    }

    public ConflictException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
