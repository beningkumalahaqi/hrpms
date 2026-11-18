using System.ComponentModel.DataAnnotations;

namespace EnterpriseHR.Domain.Requests;

public class AssignEmployeeRequest
{
    [Range(1, int.MaxValue)]
    public int EmployeeId { get; set; }

    [Required]
    [StringLength(100)]
    public string RoleInProject { get; set; } = string.Empty;

    [Range(1, 100)]
    public int AllocationPercentage { get; set; }
}
