namespace EnterpriseHR.Domain.Entities;

public class Department
{
    public int Id { get; set; }
    public string DepartmentCode { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public decimal Budget { get; set; }
}
