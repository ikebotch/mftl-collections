namespace MFTL.Collections.Application.Common.Interfaces;

public interface IPermissionEvaluator
{
    /// <summary>
    /// Evaluates if the current user has the specified permission for a target scope.
    /// </summary>
    /// <param name="permission">The permission string (e.g. "events.create")</param>
    /// <param name="scopeId">The optional target ID (e.g. BranchId, EventId)</param>
    /// <returns>True if access is granted</returns>
    Task<bool> HasPermissionAsync(string permission, Guid? scopeId = null);
}
