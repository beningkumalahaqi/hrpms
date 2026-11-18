/* ------------------------------------------------------------------ */
/* dbo.sp_GetDepartmentSummary                                         */
/*                                                                     */
/* One row per department with active headcount, active payroll,        */
/* remaining budget and active project count. Employees and projects    */
/* are aggregated in separate CTEs first: joining both detail tables    */
/* directly would multiply each department's rows and inflate COUNT /   */
/* SUM. Column aliases match DepartmentSummaryDto exactly.              */
/*                                                                     */
/* CREATE OR ALTER keeps the script idempotent for `db-init` re-runs    */
/* and is the first statement in its batch (required by SQL Server).    */
/* ------------------------------------------------------------------ */
USE EnterpriseHRDB;
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetDepartmentSummary
AS
BEGIN
    SET NOCOUNT ON;

    WITH emp AS (
        SELECT DepartmentId,
               COUNT(*)         AS ActiveEmployeeCount,
               SUM(Salary)      AS TotalMonthlySalary
        FROM dbo.Employees
        WHERE IsActive = 1
        GROUP BY DepartmentId
    ),
    prj AS (
        SELECT DepartmentId,
               COUNT(*)         AS ActiveProjectCount
        FROM dbo.Projects
        WHERE Status = 'Active'
        GROUP BY DepartmentId
    )
    SELECT d.Id                             AS DepartmentId,
           d.DepartmentCode                 AS DepartmentCode,
           d.DepartmentName                 AS DepartmentName,
           d.Budget                         AS Budget,
           ISNULL(e.ActiveEmployeeCount, 0) AS ActiveEmployeeCount,
           ISNULL(e.TotalMonthlySalary, 0)  AS TotalMonthlySalary,
           d.Budget - ISNULL(e.TotalMonthlySalary, 0) AS RemainingBudget,
           ISNULL(p.ActiveProjectCount, 0)  AS ActiveProjectCount
    FROM dbo.Departments d
    LEFT JOIN emp e ON e.DepartmentId = d.Id
    LEFT JOIN prj p ON p.DepartmentId = d.Id
    ORDER BY d.DepartmentName;
END;
GO
