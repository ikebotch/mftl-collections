using System.Reflection;
using FluentAssertions;
using MFTL.Collections.Application.Common.Security;
using Xunit;

namespace MFTL.Collections.Tests.Security;

public class PermissionAuditTests
{
    [Fact]
    public void AllDefinedPermissions_ShouldFollowNamingConvention()
    {
        var permissionKeys = GetDefinedPermissionKeys();

        foreach (var key in permissionKeys)
        {
            key.Should().NotBeNullOrEmpty();
            // Basic convention: resource.action or module.* or *
            if (key != "*")
            {
                key.Should().Contain(".");
            }
        }
    }

    [Fact]
    public void PermissionsClass_ShouldNotContainDuplicateValues()
    {
        var permissionKeys = GetDefinedPermissionKeys();
        var duplicates = permissionKeys.GroupBy(x => x)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        duplicates.Should().BeEmpty($"Duplicate permission values found: {string.Join(", ", duplicates)}");
    }

    private List<string> GetDefinedPermissionKeys()
    {
        var keys = new List<string>();
        var permissionsType = typeof(Permissions);
        
        // Root constants
        keys.AddRange(permissionsType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => f.GetRawConstantValue()?.ToString() ?? ""));

        // Nested classes
        var nestedTypes = permissionsType.GetNestedTypes(BindingFlags.Public | BindingFlags.Static);
        foreach (var type in nestedTypes)
        {
            keys.AddRange(type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
                .Select(f => f.GetRawConstantValue()?.ToString() ?? ""));
        }

        return keys.Where(k => !string.IsNullOrEmpty(k)).ToList();
    }
}
