using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EnterpriseHR.WebApp.Models;

/// <summary>
/// The create-employee form: the <see cref="Domain.Requests.CreateEmployeeRequest"/> fields a
/// new record needs, plus the three option lists the selects display.
/// </summary>
/// <remarks>
/// It composes the request instead of inheriting it because two controls need a message that
/// the contract cannot carry: an unfilled department or position select posts an empty string,
/// and binding that to the contract's non-nullable <c>int</c> yields "The value '' is invalid."
/// A nullable field with a named requirement turns the same submit into "Select a department."
/// The remaining constraints mirror the request's attributes one-for-one, and the data layer
/// stays the authority for everything a form cannot know: unique employee number and email,
/// the position's salary range, and that the manager is an active employee.
///
/// The option lists are display data. They are never posted back, and the controller rebuilds
/// them from the database on every render, including after a rejected submit.
/// </remarks>
public class EmployeeCreateViewModel
{
    [Display(Name = "Employee number")]
    [Required(ErrorMessage = "Enter an employee number.")]
    [StringLength(20, ErrorMessage = "Use 20 characters or fewer.")]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Display(Name = "Full name")]
    [Required(ErrorMessage = "Enter the employee's full name.")]
    [StringLength(255, ErrorMessage = "Use 255 characters or fewer.")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Email")]
    [Required(ErrorMessage = "Enter an email address.")]
    [StringLength(150, ErrorMessage = "Use 150 characters or fewer.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Department")]
    [Required(ErrorMessage = "Select a department.")]
    [Range(1, int.MaxValue, ErrorMessage = "Select a department.")]
    public int? DepartmentId { get; set; }

    [Display(Name = "Position")]
    [Required(ErrorMessage = "Select a position.")]
    [Range(1, int.MaxValue, ErrorMessage = "Select a position.")]
    public int? PositionId { get; set; }

    /// <summary>Optional, so the "-- No manager --" option posts an empty value.</summary>
    [Display(Name = "Manager")]
    public int? ManagerId { get; set; }

    // "." limits must parse invariantly; see UpdateSalaryRequest.NewSalary for the failure.
    [Display(Name = "Monthly salary")]
    [Required(ErrorMessage = "Enter a monthly salary.")]
    [Range(typeof(decimal), "0", "9999999999999999.99",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Enter a valid monthly salary.")]
    public decimal? Salary { get; set; }

    [Display(Name = "Join date")]
    [Required(ErrorMessage = "Enter a join date.")]
    [DataType(DataType.Date)]
    public DateTime? JoinDate { get; set; }

    public IEnumerable<SelectListItem> Departments { get; set; } = Array.Empty<SelectListItem>();

    public IEnumerable<SelectListItem> Positions { get; set; } = Array.Empty<SelectListItem>();

    /// <summary>Active employees only.</summary>
    public IEnumerable<SelectListItem> Managers { get; set; } = Array.Empty<SelectListItem>();
}
