using System.ComponentModel.DataAnnotations;

namespace EnterpriseHR.Domain.Requests;

public class UpdateSalaryRequest
{
    // ParseLimitsInInvariantCulture: the limits are written with "." as the decimal separator,
    // so they must not be parsed with the request's culture. On an en-ID/IDR-style locale
    // (separator ",") the default parsing turns "0.01" into 1 and throws on the max, which
    // surfaces as a 500 during validation instead of a 400 message.
    [Range(typeof(decimal), "0.01", "9999999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal NewSalary { get; set; }

    public int? NewPositionId { get; set; }

    [Required]
    [StringLength(255)]
    public string Reason { get; set; } = string.Empty;
}
