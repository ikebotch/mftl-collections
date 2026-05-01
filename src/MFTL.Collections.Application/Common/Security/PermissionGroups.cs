using System;
using System.Collections.Generic;

namespace MFTL.Collections.Application.Common.Security;

public static class PermissionGroups
{
    public const string Dashboard = "Dashboard";
    public const string Tenants = "Tenants";
    public const string Branches = "Branches";
    public const string Events = "Events";
    public const string RecipientFunds = "Recipient Funds";
    public const string Contributions = "Contributions";
    public const string Collectors = "Collectors";
    public const string Donors = "Donors";
    public const string Receipts = "Receipts";
    public const string Notifications = "Notifications";
    public const string Payments = "Payments";
    public const string Settlements = "Settlements";
    public const string Users = "Users";
    public const string Reports = "Reports";
    public const string Settings = "Settings";
    public const string Platform = "Platform";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Dashboard,
        Tenants,
        Branches,
        Events,
        RecipientFunds,
        Contributions,
        Collectors,
        Donors,
        Receipts,
        Notifications,
        Payments,
        Settlements,
        Users,
        Reports,
        Settings,
        Platform
    };

    public static string Guard(string group)
    {
        if (All.Contains(group))
        {
            return group;
        }

        throw new InvalidOperationException(
            $"Invalid permission group '{group}'. Use PermissionGroups constants only.");
    }
}
