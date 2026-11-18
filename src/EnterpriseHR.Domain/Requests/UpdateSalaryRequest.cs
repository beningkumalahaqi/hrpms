using System.ComponentModel.DataAnnotations;

namespace EnterpriseHR.Domain.Requests;

public class UpdateSalaryRequest
{
    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal NewSalary { get; set; }

    public int? NewPositionId { get; set; }

    [Required]
    [StringLength(255)]
    public string Reason { get; set; } = string.Empty;
}
