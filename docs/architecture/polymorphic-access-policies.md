# Polymorphic Access Policies

## Overview

Polymorphic Access Policies move authorization logic away from hardcoded `if/else` statements in application handlers and into dedicated, role-specific classes.

## The IAccessPolicy Interface

All policies implement `IAccessPolicy`, which defines:
1. **Scoped Checks**: Boolean methods like `CanAccessBranch(Guid id)`.
2. **Query Filters**: Methods that take an `IQueryable<T>` and return a filtered version.

## Role Behaviors

### Platform Admin
- **Policy**: `PlatformAdminAccessPolicy`
- **Behavior**: Returns unfiltered queries. All scoped checks return `true`.

### Organisation Admin
- **Policy**: `OrganisationAdminAccessPolicy`
- **Behavior**: Filters all data to the user's assigned `TenantIds`. Can see all branches, events, and funds within those tenants.

### Branch Admin
- **Policy**: `BranchAdminAccessPolicy`
- **Behavior**: Filters all data to the user's assigned `BranchIds`. Can see all events and funds within those branches.

### Collector
- **Policy**: `CollectorAccessPolicy`
- **Behavior**: 
  - Can only see assigned events.
  - Can only see collection records they recorded themselves (`CollectorId` match).
  - Can only see their own profile.

### Viewer
- **Policy**: `ViewerAccessPolicy`
- **Behavior**: 
  - Read-only access.
  - Restricted to explicitly assigned scopes.

## Implementation Example

In an Application Handler:

```csharp
public async Task<IEnumerable<Dto>> Handle(Query request, CancellationToken ct)
{
    var policy = await _policyResolver.ResolvePolicyAsync();
    
    // Automatic row-level filtering
    var query = policy.FilterEvents(_dbContext.Events);
    
    return await query.ToListAsync(ct);
}
```
