namespace EnterpriseHR.Domain.Entities;

public static class ProjectStatus
{
    public const string Planning = "Planning";
    public const string Active = "Active";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = { Planning, Active, Completed, Cancelled };
}
