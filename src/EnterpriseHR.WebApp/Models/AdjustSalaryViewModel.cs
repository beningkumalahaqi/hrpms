using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EnterpriseHR.WebApp.Models;

/// <summary>
/// The salary adjustment form: the <see cref="Domain.Requests.UpdateSalaryRequest"/> fields plus
/// the read-only record summary the page displays and the position options.
/// </summary>
/// <remarks>
/// Salary and position are not editable on the edit form (A3), so this screen is the only place
/// they change, and every change writes a <c>SalaryHistories</c> row. As on the other forms the
/// model composes the request instead of inheriting it: an unfilled select posts an empty string,
/// which binds to the contract's non-nullable <c>int</c> as "The value '' is invalid.", while a
/// nullable field with a named requirement reports "Select a position."
///
/// <see cref="PositionId"/> is the position the record has <em>now</em>, filled from the stored
/// row. It is never read from the request body: it is the baseline the posted
/// <see cref="NewPositionId"/> is compared against to decide whether this is a promotion.
/// </remarks>
public class AdjustSalaryViewModel
{
    public int Id { get; set; }

    /// <summary>Read-only: the number identifies the record.</summary>
    public string EmployeeNumber { get; set; } = string.Empty;

    /// <summary>Read-only: shown in the page header.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Read-only: part of the confirmation summary.</summary>
    public string DepartmentName { get; set; } = string.Empty;

    /// <summary>Read-only: the stored position, and the baseline for a promotion.</summary>
    public int PositionId { get; set; }

    /// <summary>Read-only: the stored position's title.</summary>
    public string PositionTitle { get; set; } = string.Empty;

    /// <summary>Read-only: the salary before this adjustment.</summary>
    public decimal CurrentSalary { get; set; }

    public bool IsActive { get; set; }

    // ParseLimitsInInvariantCulture for the same reason as the request contracts: the limits are
    // written with "." and must not be parsed with the machine's en-ID decimal separator.
    [Display(Name = "New monthly salary")]
    [Required(ErrorMessage = "Enter the new monthly salary.")]
    [Range(typeof(decimal), "0.01", "9999999999999999.99",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Enter a valid monthly salary.")]
    public decimal? NewSalary { get; set; }

    /// <summary>Preselected to <see cref="PositionId"/>; a different value is a promotion.</summary>
    [Display(Name = "Position")]
    [Required(ErrorMessage = "Select a position.")]
    [Range(1, int.MaxValue, ErrorMessage = "Select a position.")]
    public int? NewPositionId { get; set; }

    [Display(Name = "Reason")]
    [Required(ErrorMessage = "Enter a reason for the change.")]
    [StringLength(255, ErrorMessage = "Use 255 characters or fewer.")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>Display data, never posted back and rebuilt from the database on every render.</summary>
    public IEnumerable<SelectListItem> Positions { get; set; } = Array.Empty<SelectListItem>();
}
