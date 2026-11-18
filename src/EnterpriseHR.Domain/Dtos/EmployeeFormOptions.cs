namespace EnterpriseHR.Domain.Dtos;

public class EmployeeFormOptions
{
    public IReadOnlyList<SelectOptionDto> Departments { get; set; } = Array.Empty<SelectOptionDto>();
    public IReadOnlyList<SelectOptionDto> Positions { get; set; } = Array.Empty<SelectOptionDto>();
    public IReadOnlyList<SelectOptionDto> Managers { get; set; } = Array.Empty<SelectOptionDto>();
}
