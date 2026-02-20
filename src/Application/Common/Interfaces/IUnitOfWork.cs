namespace Application.Common.Interfaces;

/// <summary>
/// Unit of Work wrapping DbContext.SaveChangesAsync + domain event dispatch.
/// Replaces scattered repository.UpdateAsync() calls that each trigger SaveChanges.
/// 
/// Usage in handlers:
///   // ... mutate aggregates ...
///   await _unitOfWork.SaveChangesAsync(ct);
///   // Domain events dispatched automatically after save
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
