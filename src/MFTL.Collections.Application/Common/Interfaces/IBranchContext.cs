namespace MFTL.Collections.Application.Common.Interfaces;

public interface IBranchContext
{
    Guid? BranchId { get; }
    IReadOnlyList<Guid> BranchIds { get; }
    bool IsGlobalContext { get; }
    void UseBranch(Guid branchId);
    void UseBranches(IEnumerable<Guid> branchIds);
    void UseGlobalContext();
    void Clear();
}
