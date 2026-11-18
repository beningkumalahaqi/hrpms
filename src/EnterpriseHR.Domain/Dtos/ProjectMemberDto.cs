namespace EnterpriseHR.Domain.Dtos;

public class ProjectMemberDto
{
    public int AssignmentId { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public string RoleInProject { get; set; } = string.Empty;
    public int AllocationPercentage { get; set; }
    public DateTime AssignedDate { get; set; }
    public bool IsEmployeeActive { get; set; }
}
