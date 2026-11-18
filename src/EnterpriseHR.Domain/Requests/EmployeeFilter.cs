using System.ComponentModel.DataAnnotations;

namespace EnterpriseHR.Domain.Requests;

public class EmployeeFilter
{
    [StringLength(255)]
    public string? Search { get; set; }

    public int? DepartmentId { get; set; }

    public bool? IsActive { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;
}
