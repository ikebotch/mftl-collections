using System.Collections.Generic;

namespace MFTL.Collections.Application.Common.Security;

public static class AppRoles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string OrganisationAdmin = "OrganisationAdmin";
    public const string FinanceAdmin = "FinanceAdmin";
    public const string BranchAdmin = "BranchAdmin";
    public const string EventManager = "EventManager";
    public const string NotificationManager = "NotificationManager";
    public const string Collector = "Collector";
    public const string Viewer = "Viewer";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        PlatformAdmin,
        OrganisationAdmin,
        FinanceAdmin,
        BranchAdmin,
        EventManager,
        NotificationManager,
        Collector,
        Viewer
    };

    public static string GetDisplayName(string roleKey)
    {
        return roleKey switch
        {
            PlatformAdmin => "Platform Admin",
            OrganisationAdmin => "Organisation Admin",
            FinanceAdmin => "Finance Admin",
            BranchAdmin => "Branch Admin",
            EventManager => "Event Manager",
            NotificationManager => "Notification Manager",
            Collector => "Collector",
            Viewer => "Viewer",
            _ => roleKey
        };
    }

    public static bool IsValid(string roleKey) => All.Contains(roleKey);
    
    public static string Guard(string roleKey)
    {
        if (!IsValid(roleKey))
        {
            throw new System.InvalidOperationException($"Invalid role key '{roleKey}'. Use canonical AppRoles values only.");
        }
        return roleKey;
    }
}
