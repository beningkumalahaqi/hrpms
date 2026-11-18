using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EnterpriseHR.WebApp.Models;

/// <summary>
/// The edit-employee form: the <see cref="Domain.Requests.UpdateEmployeeRequest"/> fields, the
/// identity and read-only values the page displays, and the option lists.
/// </summary>
/// <remarks>
/// There are no position or salary controls here (assumption A3): <c>UpdateEmployeeSql</c> does
/// not touch those columns, and a change goes through the salary adjustment instead. The page
/// therefore shows them as read-only text next to a link to that page, and this model carries no
/// <c>Positions</c> list because nothing would render it.
///
/// As with the create form the model composes the request rather than inheriting it, so an
/// unfilled department select reports "Select a department." instead of a binding failure. The
/// read-only properties are filled from the stored row, never from the request body.
/// </remarks>
public class EmployeeEditViewModel
{
    public int Id { get; set; }

    /// <summary>Read-only: the number identifies the record and cannot be changed.</summary>
    public string EmployeeNumber { get; set; } = string.Empty;

    /// <summary>Read-only: A3, changed only by a salary adjustment.</summary>
    public string PositionTitle { get; set; } = string.Empty;

    /// <summary>Read-only: A3, changed only by a salary adjustment.</summary>
    public decimal Salary { get; set; }

    public bool IsActive { get; set; }

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

    /// <summary>Optional, so the "-- No manager --" option posts an empty value.</summary>
    [Display(Name = "Manager")]
    public int? ManagerId { get; set; }

    [Display(Name = "Join date")]
    [Required(ErrorMessage = "Enter a join date.")]
    [DataType(DataType.Date)]
    public DateTime? JoinDate { get; set; }

    public IEnumerable<SelectListItem> Departments { get; set; } = Array.Empty<SelectListItem>();

    /// <summary>Active employees only, never the employee being edited.</summary>
    public IEnumerable<SelectListItem> Managers { get; set; } = Array.Empty<SelectListItem>();
}
