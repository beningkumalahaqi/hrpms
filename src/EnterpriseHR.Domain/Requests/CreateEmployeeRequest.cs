using System.ComponentModel.DataAnnotations;

namespace EnterpriseHR.Domain.Requests;

public class CreateEmployeeRequest
{
    [Required]
    [StringLength(20)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int DepartmentId { get; set; }

    [Range(1, int.MaxValue)]
    public int PositionId { get; set; }

    public int? ManagerId { get; set; }

    // ParseLimitsInInvariantCulture: see UpdateSalaryRequest.NewSalary. Without it the
    // decimal limits are parsed with the request's culture and validation itself throws.
    [Range(typeof(decimal), "0", "9999999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Salary { get; set; }

    public DateTime JoinDate { get; set; }
}
