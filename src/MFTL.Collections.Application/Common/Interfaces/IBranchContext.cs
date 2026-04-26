namespace MFTL.Collections.Application.Common.Interfaces;

public interface IBranchContext
{
    Guid? BranchId { get; }
    void UseBranch(Guid branchId);
    void Clear();
}
