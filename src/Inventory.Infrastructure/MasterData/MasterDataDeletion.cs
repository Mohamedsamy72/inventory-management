using Inventory.Application.Common;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Inventory.Infrastructure.MasterData;

/// <summary>Hard delete for master data that was NEVER used (docs/04 §4, plan 5.12: an entity referenced by
/// any transaction can be deactivated but never deleted). The referential integrity of the schema is the
/// authority: every table that points at master data uses a RESTRICT foreign key (stock ledger, balances,
/// receiving/supply/stock-count lines, discrepancies, consumption, scopes, ...), so a referenced record
/// makes the delete fail with a foreign-key violation and NOTHING is removed - including the audit row
/// queued in the same unit of work. Only pure configuration children (item conversions, a restaurant's
/// allowed-warehouse mapping) cascade. Audit rows carry the entity id without a foreign key, so history
/// of a deleted record stays intact. The tenant query filter makes another company's id a plain 404.</summary>
internal static class MasterDataDeletion
{
    public static async Task<MasterDataResult<bool>> DeleteAsync<TEntity>(
        InventoryDbContext context, IAuditLogger auditLogger, TEntity? entity, Guid id,
        string action, string entityType, string description, CancellationToken cancellationToken)
        where TEntity : class
    {
        if (entity is null)
        {
            return MasterDataResult.Failure<bool>(MasterDataError.NotFound);
        }

        context.Remove(entity);
        auditLogger.Record(new AuditEntry(
            action, entityType, id, description,
            OldValues: null, NewValues: null, Domain.Enums.AuditResult.Success));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return MasterDataResult.Success(true);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            context.ChangeTracker.Clear();
            return MasterDataResult.Failure<bool>(MasterDataError.InUse);
        }
    }
}
