namespace EnterpriseHR.Domain.Dtos;

public class DepartmentSummaryDto
{
    public int DepartmentId { get; set; }
    public string DepartmentCode { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public int ActiveEmployeeCount { get; set; }
    public decimal TotalMonthlySalary { get; set; }
    public decimal RemainingBudget { get; set; }
    public int ActiveProjectCount { get; set; }

    public bool IsOverBudget => TotalMonthlySalary > Budget;
}
