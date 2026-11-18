using System.ComponentModel.DataAnnotations;

namespace EnterpriseHR.Domain.Requests;

public class UpdateEmployeeRequest
{
    [Required]
    [StringLength(255)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int DepartmentId { get; set; }

    public int? ManagerId { get; set; }

    public DateTime JoinDate { get; set; }
}
