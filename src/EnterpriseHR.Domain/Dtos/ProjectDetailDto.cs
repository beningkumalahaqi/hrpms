namespace EnterpriseHR.Domain.Dtos;

public class ProjectDetailDto
{
    public int Id { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<ProjectMemberDto> Members { get; set; } = new();

    public int TotalAllocationOfMembers => Members.Sum(m => m.AllocationPercentage);
}
