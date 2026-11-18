namespace EnterpriseHR.Domain.Entities;

public class SalaryHistory
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public decimal OldSalary { get; set; }
    public decimal NewSalary { get; set; }
    public string ChangeReason { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}
