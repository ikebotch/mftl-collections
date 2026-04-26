namespace MFTL.Collections.Application.Common.Interfaces;

public interface IBranchContext
{
    Guid? BranchId { get; }
    IReadOnlyList<Guid> BranchIds { get; }
    void UseBranch(Guid branchId);
    void UseBranches(IEnumerable<Guid> branchIds);
    void Clear();
}
