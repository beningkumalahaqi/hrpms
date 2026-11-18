IF DB_ID('EnterpriseHRDB') IS NULL
    CREATE DATABASE EnterpriseHRDB;
GO

USE EnterpriseHRDB;
GO

/* ------------------------------------------------------------------ */
/* Departments                                                         */
/* ------------------------------------------------------------------ */
IF OBJECT_ID('dbo.Departments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Departments
    (
        Id             INT            IDENTITY (1, 1) NOT NULL
            CONSTRAINT PK_Departments PRIMARY KEY,
        DepartmentCode VARCHAR(10)    NOT NULL
            CONSTRAINT UQ_Departments_Code UNIQUE,
        DepartmentName NVARCHAR(100)  NOT NULL,
        Budget         DECIMAL(18, 2) NOT NULL
            CONSTRAINT CK_Departments_Budget CHECK (Budget >= 0)
    );
END;
GO

/* ------------------------------------------------------------------ */
/* Positions                                                           */
/* ------------------------------------------------------------------ */
IF OBJECT_ID('dbo.Positions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Positions
    (
        Id        INT            IDENTITY (1, 1) NOT NULL
            CONSTRAINT PK_Positions PRIMARY KEY,
        Title     NVARCHAR(100)  NOT NULL,
        MinSalary DECIMAL(18, 2) NOT NULL,
        MaxSalary DECIMAL(18, 2) NOT NULL,
        CONSTRAINT CK_Positions_SalaryRange
            CHECK (MinSalary >= 0 AND MaxSalary >= MinSalary)
    );
END;
GO

/* ------------------------------------------------------------------ */
/* Employees                                                           */
/* ------------------------------------------------------------------ */
IF OBJECT_ID('dbo.Employees', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Employees
    (
        Id             INT            IDENTITY (1, 1) NOT NULL
            CONSTRAINT PK_Employees PRIMARY KEY,
        EmployeeNumber VARCHAR(20)    NOT NULL
            CONSTRAINT UQ_Employees_EmployeeNumber UNIQUE,
        FullName       NVARCHAR(255)  NOT NULL,
        Email          NVARCHAR(150)  NOT NULL
            CONSTRAINT UQ_Employees_Email UNIQUE,
        DepartmentId   INT            NOT NULL
            CONSTRAINT FK_Employees_Departments REFERENCES dbo.Departments (Id),
        PositionId     INT            NOT NULL
            CONSTRAINT FK_Employees_Positions REFERENCES dbo.Positions (Id),
        ManagerId      INT            NULL
            CONSTRAINT FK_Employees_Manager REFERENCES dbo.Employees (Id),
        Salary         DECIMAL(18, 2) NOT NULL
            CONSTRAINT CK_Employees_Salary CHECK (Salary >= 0),
        JoinDate       DATETIME       NOT NULL,
        IsActive       BIT            NOT NULL
            CONSTRAINT DF_Employees_IsActive DEFAULT 1,
        CONSTRAINT CK_Employees_Manager_NotSelf
            CHECK (ManagerId IS NULL OR ManagerId <> Id)
    );
END;
GO

/* ------------------------------------------------------------------ */
/* Projects                                                            */
/* ------------------------------------------------------------------ */
IF OBJECT_ID('dbo.Projects', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Projects
    (
        Id           INT           IDENTITY (1, 1) NOT NULL
            CONSTRAINT PK_Projects PRIMARY KEY,
        ProjectCode  VARCHAR(20)   NOT NULL
            CONSTRAINT UQ_Projects_ProjectCode UNIQUE,
        ProjectName  NVARCHAR(200) NOT NULL,
        DepartmentId INT           NOT NULL
            CONSTRAINT FK_Projects_Departments REFERENCES dbo.Departments (Id),
        StartDate    DATE          NOT NULL,
        EndDate      DATE          NULL,
        Status       VARCHAR(20)   NOT NULL
            CONSTRAINT CK_Projects_Status
            CHECK (Status IN ('Planning', 'Active', 'Completed', 'Cancelled')),
        CONSTRAINT CK_Projects_DateRange
            CHECK (EndDate IS NULL OR EndDate >= StartDate)
    );
END;
GO

/* ------------------------------------------------------------------ */
/* ProjectAssignments                                                  */
/* ------------------------------------------------------------------ */
IF OBJECT_ID('dbo.ProjectAssignments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProjectAssignments
    (
        Id                   INT           IDENTITY (1, 1) NOT NULL
            CONSTRAINT PK_ProjectAssignments PRIMARY KEY,
        ProjectId            INT           NOT NULL
            CONSTRAINT FK_ProjectAssignments_Projects REFERENCES dbo.Projects (Id),
        EmployeeId           INT           NOT NULL
            CONSTRAINT FK_ProjectAssignments_Employees REFERENCES dbo.Employees (Id),
        RoleInProject        NVARCHAR(100) NOT NULL,
        AllocationPercentage INT           NOT NULL
            CONSTRAINT CK_ProjectAssignments_Allocation
            CHECK (AllocationPercentage BETWEEN 1 AND 100),
        AssignedDate         DATETIME      NOT NULL
            CONSTRAINT DF_ProjectAssignments_AssignedDate DEFAULT GETDATE(),
        CONSTRAINT UQ_ProjectAssignments_Project_Employee UNIQUE (ProjectId, EmployeeId)
    );
END;
GO

/* ------------------------------------------------------------------ */
/* SalaryHistories                                                     */
/* ------------------------------------------------------------------ */
IF OBJECT_ID('dbo.SalaryHistories', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalaryHistories
    (
        Id           INT            IDENTITY (1, 1) NOT NULL
            CONSTRAINT PK_SalaryHistories PRIMARY KEY,
        EmployeeId   INT            NOT NULL
            CONSTRAINT FK_SalaryHistories_Employees REFERENCES dbo.Employees (Id),
        OldSalary    DECIMAL(18, 2) NOT NULL,
        NewSalary    DECIMAL(18, 2) NOT NULL,
        ChangeReason NVARCHAR(255)  NOT NULL,
        ChangedAt    DATETIME       NOT NULL
            CONSTRAINT DF_SalaryHistories_ChangedAt DEFAULT GETDATE()
    );
END;
GO

/* ------------------------------------------------------------------ */
/* Supporting indexes                                                  */
/* ------------------------------------------------------------------ */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Employees_DepartmentId'
                 AND object_id = OBJECT_ID('dbo.Employees'))
    CREATE INDEX IX_Employees_DepartmentId ON dbo.Employees (DepartmentId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Employees_PositionId'
                 AND object_id = OBJECT_ID('dbo.Employees'))
    CREATE INDEX IX_Employees_PositionId ON dbo.Employees (PositionId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Employees_ManagerId'
                 AND object_id = OBJECT_ID('dbo.Employees'))
    CREATE INDEX IX_Employees_ManagerId ON dbo.Employees (ManagerId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Projects_DepartmentId'
                 AND object_id = OBJECT_ID('dbo.Projects'))
    CREATE INDEX IX_Projects_DepartmentId ON dbo.Projects (DepartmentId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_ProjectAssignments_EmployeeId'
                 AND object_id = OBJECT_ID('dbo.ProjectAssignments'))
    CREATE INDEX IX_ProjectAssignments_EmployeeId ON dbo.ProjectAssignments (EmployeeId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_SalaryHistories_EmployeeId'
                 AND object_id = OBJECT_ID('dbo.SalaryHistories'))
    CREATE INDEX IX_SalaryHistories_EmployeeId ON dbo.SalaryHistories (EmployeeId);
GO
