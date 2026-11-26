/* ------------------------------------------------------------------ */
/* Seed data (T04)                                                     */
/*                                                                     */
/* Test-matrix reference data for the dashboard and the API:           */
/*   4 departments, 7 positions, 10 employees,                         */
/*   16 projects, 7 project assignments.                               */
/* SalaryHistories is left empty on purpose.                           */
/*                                                                     */
/* Idempotent: the whole seed is skipped when Departments already has   */
/* a row, so re-running `db-init` never duplicates data. The inserts    */
/* run in a single transaction with XACT_ABORT ON, so a failed seed     */
/* rolls back instead of leaving a partial data set that the guard      */
/* would then skip forever.                                            */
/*                                                                     */
/* Foreign keys are resolved with subselects on the natural keys        */
/* (DepartmentCode, Title, EmployeeNumber, ProjectCode); no identity    */
/* value is hard-coded. Employees are inserted managers-first because   */
/* ManagerId is a self-referencing FK.                                  */
/* ------------------------------------------------------------------ */
USE EnterpriseHRDB;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Departments)
BEGIN
    BEGIN TRANSACTION;

    /* -------------------------------------------------------------- */
    /* Departments — OPS has no employees on purpose, so the          */
    /* department summary exercises its LEFT JOIN                      */
    /* -------------------------------------------------------------- */
    INSERT INTO dbo.Departments (DepartmentCode, DepartmentName, Budget)
    SELECT v.DepartmentCode, v.DepartmentName, v.Budget
    FROM (VALUES
        ('IT',  N'Information Technology', CAST(70000000.00 AS DECIMAL(18, 2))),
        ('HR',  N'Human Resources',        CAST(60000000.00 AS DECIMAL(18, 2))),
        ('FIN', N'Finance',                CAST(50000000.00 AS DECIMAL(18, 2))),
        ('OPS', N'Operations',             CAST(20000000.00 AS DECIMAL(18, 2)))
    ) AS v (DepartmentCode, DepartmentName, Budget);

    /* -------------------------------------------------------------- */
    /* Positions — monthly IDR salary ranges                          */
    /* -------------------------------------------------------------- */
    INSERT INTO dbo.Positions (Title, MinSalary, MaxSalary)
    SELECT v.Title, v.MinSalary, v.MaxSalary
    FROM (VALUES
        (N'Junior Developer',    CAST( 6000000.00 AS DECIMAL(18, 2)), CAST(12000000.00 AS DECIMAL(18, 2))),
        (N'Senior Developer',    CAST(12000000.00 AS DECIMAL(18, 2)), CAST(25000000.00 AS DECIMAL(18, 2))),
        (N'Engineering Manager', CAST(25000000.00 AS DECIMAL(18, 2)), CAST(40000000.00 AS DECIMAL(18, 2))),
        (N'HR Specialist',       CAST( 7000000.00 AS DECIMAL(18, 2)), CAST(15000000.00 AS DECIMAL(18, 2))),
        (N'HR Manager',          CAST(15000000.00 AS DECIMAL(18, 2)), CAST(30000000.00 AS DECIMAL(18, 2))),
        (N'Accountant',          CAST( 8000000.00 AS DECIMAL(18, 2)), CAST(18000000.00 AS DECIMAL(18, 2))),
        (N'Finance Manager',     CAST(18000000.00 AS DECIMAL(18, 2)), CAST(35000000.00 AS DECIMAL(18, 2)))
    ) AS v (Title, MinSalary, MaxSalary);

    /* -------------------------------------------------------------- */
    /* Employees, pass 1 — the three managers (ManagerId is NULL)      */
    /* -------------------------------------------------------------- */
    INSERT INTO dbo.Employees
        (EmployeeNumber, FullName, Email, DepartmentId, PositionId,
         ManagerId, Salary, JoinDate, IsActive)
    SELECT v.EmployeeNumber,
           v.FullName,
           v.Email,
           (SELECT Id FROM dbo.Departments WHERE DepartmentCode = v.DepartmentCode),
           (SELECT Id FROM dbo.Positions   WHERE Title          = v.PositionTitle),
           NULL,
           v.Salary,
           v.JoinDate,
           v.IsActive
    FROM (VALUES
        ('E001', N'Budi Santoso', N'budi.santoso@enterprisehr.local', 'IT',  N'Engineering Manager',
         CAST(35000000.00 AS DECIMAL(18, 2)), CAST('2020-03-02' AS DATETIME), CAST(1 AS BIT)),
        ('E006', N'Maya Putri',   N'maya.putri@enterprisehr.local',   'HR',  N'HR Manager',
         CAST(28000000.00 AS DECIMAL(18, 2)), CAST('2020-07-13' AS DATETIME), CAST(1 AS BIT)),
        ('E008', N'Rina Kusuma',  N'rina.kusuma@enterprisehr.local',  'FIN', N'Finance Manager',
         CAST(30000000.00 AS DECIMAL(18, 2)), CAST('2020-11-09' AS DATETIME), CAST(1 AS BIT))
    ) AS v (EmployeeNumber, FullName, Email, DepartmentCode, PositionTitle,
            Salary, JoinDate, IsActive);

    /* -------------------------------------------------------------- */
    /* Employees, pass 2 — reports, managers already exist above       */
    /* -------------------------------------------------------------- */
    INSERT INTO dbo.Employees
        (EmployeeNumber, FullName, Email, DepartmentId, PositionId,
         ManagerId, Salary, JoinDate, IsActive)
    SELECT v.EmployeeNumber,
           v.FullName,
           v.Email,
           (SELECT Id FROM dbo.Departments WHERE DepartmentCode = v.DepartmentCode),
           (SELECT Id FROM dbo.Positions   WHERE Title          = v.PositionTitle),
           (SELECT Id FROM dbo.Employees   WHERE EmployeeNumber = v.ManagerNumber),
           v.Salary,
           v.JoinDate,
           v.IsActive
    FROM (VALUES
        ('E002', N'Siti Rahma',    N'siti.rahma@enterprisehr.local',   'IT',  N'Senior Developer',
         'E001', CAST(22000000.00 AS DECIMAL(18, 2)), CAST('2021-02-15' AS DATETIME), CAST(1 AS BIT)),
        ('E003', N'Andi Wijaya',   N'andi.wijaya@enterprisehr.local',  'IT',  N'Junior Developer',
         'E001', CAST( 8000000.00 AS DECIMAL(18, 2)), CAST('2022-06-06' AS DATETIME), CAST(1 AS BIT)),
        ('E004', N'Dewi Lestari',  N'dewi.lestari@enterprisehr.local', 'IT',  N'Senior Developer',
         'E001', CAST(12000000.00 AS DECIMAL(18, 2)), CAST('2021-09-20' AS DATETIME), CAST(1 AS BIT)),
        ('E005', N'Rudi Hartono',  N'rudi.hartono@enterprisehr.local', 'IT',  N'Junior Developer',
         'E001', CAST( 7000000.00 AS DECIMAL(18, 2)), CAST('2023-01-16' AS DATETIME), CAST(0 AS BIT)),
        ('E007', N'Joko Prasetyo', N'joko.prasetyo@enterprisehr.local', 'HR', N'HR Specialist',
         'E006', CAST(10000000.00 AS DECIMAL(18, 2)), CAST('2022-04-04' AS DATETIME), CAST(1 AS BIT)),
        ('E009', N'Hadi Gunawan',  N'hadi.gunawan@enterprisehr.local', 'FIN', N'Accountant',
         'E008', CAST( 9000000.00 AS DECIMAL(18, 2)), CAST('2023-08-14' AS DATETIME), CAST(1 AS BIT)),
        ('E010', N'Lina Marlina',  N'lina.marlina@enterprisehr.local', 'FIN', N'Accountant',
         'E008', CAST( 9000000.00 AS DECIMAL(18, 2)), CAST('2024-02-05' AS DATETIME), CAST(1 AS BIT))
    ) AS v (EmployeeNumber, FullName, Email, DepartmentCode, PositionTitle,
            ManagerNumber, Salary, JoinDate, IsActive);

    /* -------------------------------------------------------------- */
    /* Projects                                                        */
    /* -------------------------------------------------------------- */
    INSERT INTO dbo.Projects
        (ProjectCode, ProjectName, DepartmentId, StartDate, EndDate, Status)
    SELECT v.ProjectCode,
           v.ProjectName,
           (SELECT Id FROM dbo.Departments WHERE DepartmentCode = v.DepartmentCode),
           v.StartDate,
           v.EndDate,
           v.Status
    FROM (VALUES
        ('PRJ-001', N'ERP Revamp',
         'IT',  CAST('2026-01-05' AS DATE), CAST('2026-12-31' AS DATE), 'Active'),
        ('PRJ-002', N'Mobile App',
         'IT',  CAST('2026-02-01' AS DATE), CAST(NULL AS DATE),          'Active'),
        ('PRJ-003', N'HR Portal',
         'HR',  CAST('2026-12-01' AS DATE), CAST(NULL AS DATE),          'Planning'),
        ('PRJ-004', N'Legacy Migration',
         'IT',  CAST('2025-01-01' AS DATE), CAST('2025-12-31' AS DATE), 'Completed'),
        ('PRJ-005', N'Vendor Audit',
         'FIN', CAST('2026-03-01' AS DATE), CAST('2026-04-01' AS DATE), 'Cancelled'),
        ('PRJ-006', N'Budget Tool',
         'FIN', CAST('2026-05-01' AS DATE), CAST(NULL AS DATE),          'Active')
    ) AS v (ProjectCode, ProjectName, DepartmentCode, StartDate, EndDate, Status);

    /* -------------------------------------------------------------- */
    /* Project assignments — AssignedDate mirrors the project start    */
    /* date so the data is deterministic (the column default is        */
    /* GETDATE(), which would make every run different)                */
    /* -------------------------------------------------------------- */
    INSERT INTO dbo.ProjectAssignments
        (ProjectId, EmployeeId, RoleInProject, AllocationPercentage, AssignedDate)
    SELECT (SELECT Id FROM dbo.Projects  WHERE ProjectCode   = v.ProjectCode),
           (SELECT Id FROM dbo.Employees WHERE EmployeeNumber = v.EmployeeNumber),
           v.RoleInProject,
           v.AllocationPercentage,
           (SELECT StartDate FROM dbo.Projects WHERE ProjectCode = v.ProjectCode)
    FROM (VALUES
        ('PRJ-001', 'E002', N'Tech Lead',      60),
        ('PRJ-002', 'E002', N'Developer',      40),
        ('PRJ-001', 'E003', N'Developer',      30),
        ('PRJ-004', 'E004', N'Developer',     100),
        ('PRJ-003', 'E007', N'Analyst',        50),
        ('PRJ-005', 'E009', N'Auditor',        20),
        ('PRJ-006', 'E010', N'Analyst',        40)
    ) AS v (ProjectCode, EmployeeNumber, RoleInProject, AllocationPercentage);

    COMMIT TRANSACTION;
END;
GO

/* ------------------------------------------------------------------ */
/* Additional projects (10)                                            */
/*                                                                     */
/* Additive and idempotent by ProjectCode, so an existing database can  */
/* receive them without deleting the volume and reseeding from scratch. */
/* Statuses are mixed on purpose: 3 Active, 3 Planning, 3 Completed,    */
/* 1 Cancelled. None has assignments, so the workload fixture and the   */
/* project member counts are unchanged.                                 */
/* ------------------------------------------------------------------ */
BEGIN TRANSACTION;

INSERT INTO dbo.Projects
    (ProjectCode, ProjectName, DepartmentId, StartDate, EndDate, Status)
SELECT v.ProjectCode,
       v.ProjectName,
       (SELECT Id FROM dbo.Departments WHERE DepartmentCode = v.DepartmentCode),
       v.StartDate,
       v.EndDate,
       v.Status
FROM (VALUES
    ('PRJ-007', N'Data Warehouse',       'IT',  CAST('2026-03-02' AS DATE), CAST('2026-12-31' AS DATE), 'Active'),
    ('PRJ-008', N'Talent Review',        'HR',  CAST('2026-04-06' AS DATE), CAST(NULL AS DATE),          'Active'),
    ('PRJ-009', N'Forecast Model',       'FIN', CAST('2027-01-11' AS DATE), CAST('2027-06-30' AS DATE), 'Planning'),
    ('PRJ-010', N'Facilities Tracker',   'OPS', CAST('2026-02-09' AS DATE), CAST(NULL AS DATE),          'Active'),
    ('PRJ-011', N'Security Hardening',   'IT',  CAST('2027-02-01' AS DATE), CAST(NULL AS DATE),          'Planning'),
    ('PRJ-012', N'Onboarding Refresh',   'HR',  CAST('2025-03-03' AS DATE), CAST('2025-08-29' AS DATE), 'Completed'),
    ('PRJ-013', N'Tax Automation',       'FIN', CAST('2025-05-05' AS DATE), CAST('2025-11-28' AS DATE), 'Completed'),
    ('PRJ-014', N'Vendor Consolidation', 'OPS', CAST('2027-03-01' AS DATE), CAST('2027-09-30' AS DATE), 'Planning'),
    ('PRJ-015', N'Legacy CRM',           'IT',  CAST('2025-06-02' AS DATE), CAST('2025-12-19' AS DATE), 'Cancelled'),
    ('PRJ-016', N'Asset Inventory',      'OPS', CAST('2025-09-01' AS DATE), CAST('2026-01-30' AS DATE), 'Completed')
) AS v (ProjectCode, ProjectName, DepartmentCode, StartDate, EndDate, Status)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Projects p WHERE p.ProjectCode = v.ProjectCode
);

COMMIT TRANSACTION;
GO
