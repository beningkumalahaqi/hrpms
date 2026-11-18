namespace EnterpriseHR.Domain.Exceptions;

/// <summary>
/// Input or state violates a business rule: salary outside the position range, inactive
/// employee, project not Planning/Active, salary unchanged, unknown department/position/
/// manager. Maps to HTTP 400.
/// </summary>
public class BusinessRuleException : AppException
{
    public BusinessRuleException(string message) : base(message)
    {
    }

    public BusinessRuleException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
